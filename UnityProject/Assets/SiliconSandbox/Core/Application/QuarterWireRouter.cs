using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Application
{
    // Integer quarter-cell search. The objective is lexicographic: turns,
    // total length, then descending straight-run lengths from the input.
    internal static class QuarterWireRouter
    {
        internal static readonly GridCell[] Steps = {
            new GridCell(1,0,0), new GridCell(-1,0,0),
            new GridCell(0,1,0), new GridCell(0,-1,0),
            new GridCell(0,0,1), new GridCell(0,0,-1) };
        internal static GridCell Add(GridCell a, GridCell b) =>
            new GridCell(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        internal static GridCell Point(GridCell cell, QuarterPoint p) =>
            new GridCell(cell.X*4+p.X,cell.Y*4+p.Y,cell.Z*4+p.Z);
        internal static int Normal(QuarterPoint p) => p.X==4 ? 0 : p.X==0 ? 1 :
            p.Y==4 ? 2 : p.Y==0 ? 3 : p.Z==4 ? 4 : 5;
        internal static GridCell SegmentCell(GridCell a, GridCell b) =>
            new GridCell((a.X+b.X)/8,(a.Y+b.Y)/8,(a.Z+b.Z)/8);
        internal static QuarterPoint Local(GridCell p, GridCell c) =>
            new QuarterPoint(p.X-c.X*4,p.Y-c.Y*4,p.Z-c.Z*4);
        internal static bool ValidPoint(GridCell p)
        {
            if (p.X<0 || p.Y<0 || p.Z<0) return false;
            var q = new QuarterPoint(p.X%4,p.Y%4,p.Z%4);
            return q.IsInteriorQuarterPoint || q.IsFacePoint;
        }
        internal sealed class Path
        {
            internal GridCell Position;
            internal int Direction, Turns, Length, EstimateTurns, EstimateLength;
            internal Path Parent;
            internal int[] Runs;
            internal long Serial;
        }
        internal static int Compare(Path a, Path b)
        {
            var c=a.Turns.CompareTo(b.Turns); if(c!=0)return c;
            c=a.Length.CompareTo(b.Length); if(c!=0)return c;
            for(var i=0;i<Math.Min(a.Runs.Length,b.Runs.Length);i++)
            { c=b.Runs[i].CompareTo(a.Runs[i]); if(c!=0)return c; }
            return 0;
        }
        internal static List<GridCell> Points(Path path)
        {
            var result=new List<GridCell>();
            for(var p=path;p!=null;p=p.Parent)result.Add(p.Position);
            result.Reverse();return result;
        }
        // Shared by every channel attempt in one preview/placement request.
        // A limit is a safe rejection, never permission to publish a partial
        // path or a candidate whose routing priority has not been established.
        internal sealed class SearchBudget
        {
            private readonly System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            private int expanded, recorded;
            internal void Check()
            {
                if (timer.Elapsed.TotalMilliseconds >= 50 || expanded > 8192 || recorded > 32768)
                    throw new ArgumentException("Wire routing search limit reached. Clear nearby obstructions and retry.");
            }
            internal void Expand() { expanded++; Check(); }
            internal void Record() { recorded++; Check(); }
        }

        internal static Path Find(GridCell start, int startDirection,
            GridCell goal, int finalDirection, Func<GridCell,GridCell,bool> free, SearchBudget budget)
        {
            var open=new Heap();
            var best=new Dictionary<(GridCell,int),Path>();
            long serial=0;
            var origin=new Path { Position=start,Direction=startDirection, Runs=Array.Empty<int>() };
            origin.EstimateTurns=LowerTurns(start,startDirection,goal,finalDirection);
            origin.EstimateLength=Distance(start,goal);
            open.Push(origin);best[(start,startDirection)]=origin;
            Path answer=null;
            while(open.Count>0)
            {
                budget.Expand();
                var current=open.Pop();
                if(best[(current.Position,current.Direction)]!=current)continue;
                if(answer!=null && (current.EstimateTurns>answer.Turns ||
                    current.EstimateTurns==answer.Turns && current.EstimateLength>answer.Length))break;
                if(current.Position.Equals(goal) && current.Length>0 &&
                    (finalDirection<0 || current.Direction==finalDirection))
                { if(answer==null || Compare(current,answer)<0)answer=current;continue; }
                for(var d=0;d<6;d++)
                {
                    if(current.Length==0 && startDirection>=0 && d!=startDirection ||
                        current.Length>0 && d==(current.Direction^1))continue;
                    var next=Add(current.Position,Steps[d]);
                    var stepLength=1;
                    if (!ValidPoint(next))
                    {
                        // A half-cell run may connect two quadrants along one
                        // face. Its midpoint need not become an authored node.
                        var far=Add(next,Steps[d]);
                        if (!ValidPoint(far) || !SegmentCell(current.Position,next).Equals(SegmentCell(next,far)) ||
                            !free(current.Position,next) || !free(next,far)) continue;
                        next=far;stepLength=2;
                    }
                    else if(!free(current.Position,next))continue;
                    if(next.Equals(goal) && finalDirection>=0 && d!=finalDirection)continue;
                    var turn=current.Length>0 && d!=current.Direction;
                    var runs=new int[current.Runs.Length+(current.Length==0 || turn ? 1 : 0)];
                    Array.Copy(current.Runs,runs,current.Runs.Length);
                    runs[runs.Length-1]+=stepLength;
                    var candidate=new Path {Position=next,Direction=d,Parent=current,
                        Length=current.Length+stepLength,Turns=current.Turns+(turn?1:0),Runs=runs,Serial=++serial};
                    var key=(next,d);
                    if(best.TryGetValue(key,out var prior) && Compare(candidate,prior)>=0)continue;
                    candidate.EstimateTurns=candidate.Turns+LowerTurns(next,d,goal,finalDirection);
                    candidate.EstimateLength=candidate.Length+Distance(next,goal);
                    budget.Record();
                    best[key]=candidate;open.Push(candidate);
                }
            }
            return answer;
        }
        private static int Distance(GridCell a,GridCell b) =>
            Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)+Math.Abs(a.Z-b.Z);
        // Every displacement axis must be traversed in its signed direction.
        // Permuting these mandatory headings gives an admissible turn bound;
        // obstacles or required overshoots can only add turns.
        private static int LowerTurns(GridCell p,int heading,GridCell goal,int final)
        {
            var mask=0;
            if(p.X!=goal.X)mask|=1<<(goal.X>p.X?0:1);
            if(p.Y!=goal.Y)mask|=1<<(goal.Y>p.Y?2:3);
            if(p.Z!=goal.Z)mask|=1<<(goal.Z>p.Z?4:5);
            return Permute(mask,heading,final);
        }
        private static int Permute(int mask,int heading,int final)
        {
            if(mask==0)return final<0 || heading<0 || heading==final ? 0 : 1;
            var result=10;
            for(var d=0;d<6;d++)if((mask&(1<<d))!=0)
                result=Math.Min(result,(heading<0 || heading==d ? 0 : 1)+Permute(mask^(1<<d),d,final));
            return result;
        }
        private sealed class Heap
        {
            private readonly List<Path> items=new List<Path>();
            internal int Count=>items.Count;
            private static int Order(Path a,Path b)
            {
                var c=a.EstimateTurns.CompareTo(b.EstimateTurns);if(c!=0)return c;
                c=a.EstimateLength.CompareTo(b.EstimateLength);if(c!=0)return c;
                c=Compare(a,b);return c!=0?c:a.Serial.CompareTo(b.Serial);
            }
            internal void Push(Path p)
            {
                items.Add(p);var i=items.Count-1;
                while(i>0){var parent=(i-1)/2;if(Order(items[parent],p)<=0)break;
                    items[i]=items[parent];i=parent;}items[i]=p;
            }
            internal Path Pop()
            {
                var result=items[0];var last=items[items.Count-1];items.RemoveAt(items.Count-1);
                if(items.Count==0)return result;var i=0;
                while(i*2+1<items.Count){var c=i*2+1;
                    if(c+1<items.Count && Order(items[c+1],items[c])<0)c++;
                    if(Order(last,items[c])<=0)break;items[i]=items[c];i=c;}
                items[i]=last;return result;
            }
        }
    }
}
