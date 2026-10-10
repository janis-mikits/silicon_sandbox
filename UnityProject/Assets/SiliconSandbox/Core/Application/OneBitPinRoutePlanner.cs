using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Application
{
    public sealed class OneBitPinRouteProposal
    {
        public ConnectorRoute Route { get; }
        public IReadOnlyList<ElectricalJoin> Joins { get; }

        internal OneBitPinRouteProposal(ConnectorRoute route,
            params ElectricalJoin[] joins)
        {
            Route = route;
            Joins = Array.AsReadOnly(joins);
        }
    }

    // Two-target first-playable wire tool. It records exact quarter-grid
    // routes and explicit joins, with straight approaches at both pins.
    // This is an authoring convenience; the topology, not this pathfinder,
    // remains authoritative for electrical connectivity.
    public static class OneBitPinRoutePlanner
    {
        public static OneBitPinRouteProposal Plan(OneBitWorldDesign design,
            JoinMember first, JoinMember second)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (!IsPhysicalPin(first) || !IsPhysicalPin(second) ||
                first.Equals(second))
                throw new ArgumentException("Choose two distinct physical pins.");
            var firstPin = FindPin(design, first);
            var secondPin = FindPin(design, second);
            foreach (var join in design.Topology.Joins)
                foreach (var member in join.Members)
                    if (member.Equals(first) || member.Equals(second))
                        throw new ArgumentException("A selected pin already has a connector.");

            if (SameWorldPoint(firstPin, secondPin))
            {
                var proposal = DirectFaceBridge(design, first, firstPin,
                    second, secondPin);
                OneBitWorldEdits.PlaceConnector(design, proposal.Route, proposal.Joins);
                return proposal;
            }

            // Search from the input so the run-length tie-break is independent
            // of which pin the player clicked first.
            if (IsInput(design, first) && !IsInput(design, second))
                return Route(design, first, firstPin, second, secondPin, null);
            return Route(design, second, secondPin, first, firstPin, null);
        }

        public static OneBitPinRouteProposal PlanToConnectorNode(
            OneBitWorldDesign design, JoinMember pin, JoinMember targetNode)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (!IsPhysicalPin(pin) || targetNode.Kind != JoinTargetKind.ConnectorNode)
                throw new ArgumentException("Choose a free physical pin and a connector node.");
            foreach (var route in design.Topology.Connectors)
                if (route.Id == targetNode.OwnerId)
                    foreach (var node in route.Nodes)
                        if (node.Id == targetNode.PartId)
                            return Route(design, pin, FindPin(design,pin), targetNode, null, node);
            throw new ArgumentException("Unknown connector node.");
        }

        public static OneBitPinRouteProposal PlanToOpenCell(
            OneBitWorldDesign design, JoinMember pin, GridCell targetCell)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (!IsPhysicalPin(pin)) throw new ArgumentException("Choose a free physical pin.");
            return Route(design,pin,FindPin(design,pin),null,null,
                new RouteNode(Guid.Empty,targetCell,0,new QuarterPoint(2,2,2)));
        }

        private static bool IsInput(OneBitWorldDesign design,JoinMember member)
        {
            foreach(var component in design.Components)
                if(component.Id==member.OwnerId)
                    foreach(var pin in BuiltInPinCatalog.Pins(component.TypeId,component.TypeVersion))
                        if(component.PinIds[pin.Key]==member.PartId)return pin.Direction==PinDirection.Input;
            foreach(var module in design.Modules)
                if(module.Id==member.OwnerId)
                    foreach(var port in module.InterfacePorts)
                        if(port.Id==member.PartId)return port.Direction==OneBitPortDirection.Input;
            return false;
        }

        private static OneBitPinRouteProposal Route(OneBitWorldDesign design,
            JoinMember first, AuthoredPin firstPin, JoinMember? second,
            AuthoredPin secondPin, RouteNode target)
        {
            var budget = new QuarterWireRouter.SearchBudget();
            foreach(var join in design.Topology.Joins)
                foreach(var member in join.Members)
                    if(member.Equals(first) || secondPin!=null && member.Equals(second.Value))
                        throw new ArgumentException("A selected pin already has a connector.");
            var start=QuarterWireRouter.Point(firstPin.Cell,firstPin.PointQ);
            var finish=secondPin!=null ? QuarterWireRouter.Point(secondPin.Cell,secondPin.PointQ) :
                QuarterWireRouter.Point(target.Cell,target.PointQ);
            var startDirection=QuarterWireRouter.Normal(firstPin.PointQ);
            var finishDirection=secondPin==null ? -1 : QuarterWireRouter.Normal(secondPin.PointQ)^1;
            var reverse=secondPin==null && !IsInput(design,first);
            if(reverse)
            {
                var swap=start;start=finish;finish=swap;
                finishDirection=startDirection^1;startDirection=-1;
            }
            var allowedPins = new List<JoinMember> { first };
            if (secondPin != null) allowedPins.Add(second.Value);
            var corridors = new PinConnectionCorridors(design.Topology, allowedPins);
            var blocked=BlockedCells(design);
            var occupiedPoints=new HashSet<GridCell>();
            foreach(var existing in design.Topology.Connectors)
            {
                var map=new Dictionary<Guid,RouteNode>();
                foreach(var node in existing.Nodes)
                {
                    budget.Check();
                    map[node.Id]=node;
                    occupiedPoints.Add(QuarterWireRouter.Point(node.Cell,node.PointQ));
                    // Old presentation paths have sub-quarter lane offsets.
                    // Keep their cells clear except the explicitly targeted cell.
                    if(existing.GeometryVersion==1 && (target==null || !node.Cell.Equals(target.Cell)))
                        blocked.Add(node.Cell);
                }
                if(existing.GeometryVersion!=2)continue;
                foreach(var span in existing.Spans)
                {
                    var a=QuarterWireRouter.Point(map[span.FromNodeId].Cell,map[span.FromNodeId].PointQ);
                    var b=QuarterWireRouter.Point(map[span.ToNodeId].Cell,map[span.ToNodeId].PointQ);
                    var step=new GridCell(Math.Sign(b.X-a.X),Math.Sign(b.Y-a.Y),Math.Sign(b.Z-a.Z));
                    for(var q=a;!q.Equals(b);q=QuarterWireRouter.Add(q,step))
                    { budget.Check(); occupiedPoints.Add(q); }
                }
            }
            QuarterWireRouter.Path best=null;var bestChannel=-1;
            var searchedOccupancy = new List<HashSet<GridCell>>();
            for(var channel=0;channel<4;channel++)
            {
                var occupied=new HashSet<GridCell>(blocked);
                foreach(var existing in design.Topology.Connectors)
                    foreach(var node in existing.Nodes)
                        if(node.Channel==channel && !(second.HasValue &&
                            second.Value.Kind==JoinTargetKind.ConnectorNode &&
                            target.Cell.Equals(node.Cell) && existing.Id==second.Value.OwnerId))
                            occupied.Add(node.Cell);
                var alreadySearched = false;
                foreach (var prior in searchedOccupancy)
                    if (prior.SetEquals(occupied)) { alreadySearched = true; break; }
                if (alreadySearched) continue;
                searchedOccupancy.Add(occupied);
                bool PointFree(GridCell p) => !occupiedPoints.Contains(p) ||
                    target != null && p.Equals(QuarterWireRouter.Point(target.Cell,target.PointQ));
                bool Free(GridCell a, GridCell b)
                {
                    var cell=QuarterWireRouter.SegmentCell(a,b);
                    return design.Bounds.ContainsPlaceable(cell) && !occupied.Contains(cell) &&
                        corridors.AllowsSegment(a,b) &&
                        PointFree(a) && PointFree(b);
                }
                var canLeave = false; var canEnter = false;
                for (var d = 0; d < 6; d++)
                {
                    var next = QuarterWireRouter.Add(start, QuarterWireRouter.Steps[d]);
                    var prior = QuarterWireRouter.Add(finish, QuarterWireRouter.Steps[d ^ 1]);
                    var farNext = QuarterWireRouter.Add(next, QuarterWireRouter.Steps[d]);
                    var farPrior = QuarterWireRouter.Add(prior, QuarterWireRouter.Steps[d ^ 1]);
                    if ((startDirection < 0 || d == startDirection) && Free(start,next) &&
                        (QuarterWireRouter.ValidPoint(next) || QuarterWireRouter.ValidPoint(farNext) &&
                         QuarterWireRouter.SegmentCell(start,next).Equals(QuarterWireRouter.SegmentCell(next,farNext)) &&
                         Free(next,farNext))) canLeave = true;
                    if ((finishDirection < 0 || d == finishDirection) && Free(prior,finish) &&
                        (QuarterWireRouter.ValidPoint(prior) || QuarterWireRouter.ValidPoint(farPrior) &&
                         QuarterWireRouter.SegmentCell(farPrior,prior).Equals(QuarterWireRouter.SegmentCell(prior,finish)) &&
                         Free(farPrior,prior))) canEnter = true;
                }
                if (!canLeave || !canEnter) continue;
                var path=QuarterWireRouter.Find(start,startDirection,finish,finishDirection,Free,budget);
                if(path!=null && (best==null || QuarterWireRouter.Compare(path,best)<0))
                {best=path;bestChannel=channel;}
            }
            if(best==null)throw new ArgumentException("No valid one-bit wire route or channel.");
            var points=QuarterWireRouter.Points(best);
            // Keep the established first-pin -> target authored order.
            if(reverse)points.Reverse();
            var proposal=BuildExact(points,bestChannel,first,second);
            OneBitWorldEdits.PlaceConnector(design,proposal.Route,proposal.Joins);
            return proposal;
        }

        private static OneBitPinRouteProposal BuildExact(List<GridCell> points,
            int channel,JoinMember first,JoinMember? second)
        {
            var nodes=new List<RouteNode>();var spans=new List<RouteSpan>();
            RouteNode prior=null;
            for(var i=0;i<points.Count-1;i++)
            {
                var cell=QuarterWireRouter.SegmentCell(points[i],points[i+1]);
                if(prior==null || !prior.Cell.Equals(cell))
                {
                    var node=new RouteNode(Guid.NewGuid(),cell,channel,QuarterWireRouter.Local(points[i],cell));
                    nodes.Add(node);
                    if(prior!=null)spans.Add(new RouteSpan(Guid.NewGuid(),prior.Id,node.Id));
                    prior=node;
                }
                // Only bends, face boundaries, and the endpoint need nodes.
                if(i+2<points.Count && QuarterWireRouter.SegmentCell(points[i+1],points[i+2]).Equals(cell) &&
                    points[i+1].X-points[i].X==points[i+2].X-points[i+1].X &&
                    points[i+1].Y-points[i].Y==points[i+2].Y-points[i+1].Y &&
                    points[i+1].Z-points[i].Z==points[i+2].Z-points[i+1].Z)continue;
                var end=new RouteNode(Guid.NewGuid(),cell,channel,QuarterWireRouter.Local(points[i+1],cell));
                nodes.Add(end);spans.Add(new RouteSpan(Guid.NewGuid(),prior.Id,end.Id));prior=end;
            }
            var route=new ConnectorRoute(Guid.NewGuid(),"wire",1,nodes,spans,geometryVersion:2);
            var firstJoin=new ElectricalJoin(Guid.NewGuid(),new[]{first,JoinMember.ConnectorNode(route.Id,nodes[0].Id)});
            return second.HasValue ? new OneBitPinRouteProposal(route,firstJoin,
                new ElectricalJoin(Guid.NewGuid(),new[]{second.Value,JoinMember.ConnectorNode(route.Id,prior.Id)})) :
                new OneBitPinRouteProposal(route,firstJoin);
        }

        private static OneBitPinRouteProposal DirectFaceBridge(
            OneBitWorldDesign design, JoinMember first, AuthoredPin firstPin,
            JoinMember second, AuthoredPin secondPin)
        {
            if (firstPin.Cell.Equals(secondPin.Cell))
                throw new ArgumentException("Distinct pins in one component cannot share a face point.");
            for (var channel = 0; channel < 4; channel++)
            {
                var available = true;
                foreach (var existingRoute in design.Topology.Connectors)
                    foreach (var node in existingRoute.Nodes)
                        if (node.Channel == channel &&
                            (node.Cell.Equals(firstPin.Cell) ||
                             node.Cell.Equals(secondPin.Cell)))
                            available = false;
                if (!available) continue;
                var firstNode = new RouteNode(Guid.NewGuid(), firstPin.Cell,
                    channel, firstPin.PointQ);
                var secondNode = new RouteNode(Guid.NewGuid(), secondPin.Cell,
                    channel, secondPin.PointQ);
                var routeId = Guid.NewGuid();
                var route = new ConnectorRoute(routeId, "wire", 1,
                    new[] { firstNode, secondNode },
                    new[] { new RouteSpan(Guid.NewGuid(), firstNode.Id, secondNode.Id) });
                return new OneBitPinRouteProposal(route,
                    new ElectricalJoin(Guid.NewGuid(), new[]
                    {
                        first, JoinMember.ConnectorNode(routeId, firstNode.Id)
                    }),
                    new ElectricalJoin(Guid.NewGuid(), new[]
                    {
                        second, JoinMember.ConnectorNode(routeId, secondNode.Id)
                    }));
            }
            throw new ArgumentException("No free channel on the shared component face.");
        }

        private static bool SameWorldPoint(AuthoredPin first, AuthoredPin second) =>
            first.Cell.X * 4L + first.PointQ.X == second.Cell.X * 4L + second.PointQ.X &&
            first.Cell.Y * 4L + first.PointQ.Y == second.Cell.Y * 4L + second.PointQ.Y &&
            first.Cell.Z * 4L + first.PointQ.Z == second.Cell.Z * 4L + second.PointQ.Z;

        private static AuthoredPin FindPin(OneBitWorldDesign design, JoinMember member)
        {
            foreach (var pin in design.Topology.Pins)
                if (member.Equals(JoinMember.ComponentPin(pin.ObjectId, pin.PinId)))
                    return pin;
            foreach (var port in design.Topology.ModulePorts)
                if (member.Equals(JoinMember.ModulePortBit(port.ObjectId,
                    port.PortId, port.BitIndex)))
                    return new AuthoredPin(port.ObjectId, port.PortId,
                        port.Cell, port.PointQ);
            throw new ArgumentException("Unknown physical pin.");
        }

        private static bool IsPhysicalPin(JoinMember member) =>
            member.Kind == JoinTargetKind.ComponentPin ||
            member.Kind == JoinTargetKind.ModulePortBit && member.BitIndex == 0;

        private static HashSet<GridCell> BlockedCells(OneBitWorldDesign design)
        {
            var blocked = new HashSet<GridCell>();
            foreach (var component in design.Components)
                blocked.Add(component.AnchorCell);
            foreach (var module in design.Modules)
                foreach (var cell in module.OccupiedCells()) blocked.Add(cell);
            return blocked;
        }

        private static GridDirection Face(QuarterPoint point)
        {
            if (point.X == 0) return GridDirection.West;
            if (point.X == 4) return GridDirection.East;
            if (point.Y == 0) return GridDirection.Down;
            if (point.Y == 4) return GridDirection.Up;
            if (point.Z == 0) return GridDirection.South;
            if (point.Z == 4) return GridDirection.North;
            throw new ArgumentException("Pin is not on a cell face.");
        }

    }
}
