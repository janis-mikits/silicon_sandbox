using System;
using System.Linq;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WireRouteFailureTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void BrokenPinLegRejectsImmediatelyThenCanReconnectAfterObstructionIsRemoved(bool reverseClicks)
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(new WorldBounds(1024,1024,256)));
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,new GridCell(4,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(6,1,4),GridOrientation.Default);
            var source = session.Design.Components[0]; var sr = session.Design.Components[1];
            var gate = session.Design.Components[2];
            var output = JoinMember.ComponentPin(source.Id,source.PinIds["OUT"]);
            var input = JoinMember.ComponentPin(gate.Id,gate.PinIds["A"]);
            session.ConnectPins(output,JoinMember.ComponentPin(sr.Id,sr.PinIds["R"]));
            var route = session.Design.Topology.Connectors[0];
            // Input-prioritized route ends with a literal quarter-cell source leg.
            var last = route.Spans[route.Spans.Count-1];
            var predecessor = route.Nodes.Single(n=>n.Id==last.FromNodeId);
            Assert.That(predecessor.Cell.X*4+predecessor.PointQ.X,Is.EqualTo(13));
            session.BreakSpan(route.Id,last.Id);
            Assert.That(session.Design.Topology.Joins.Any(j=>j.Members.Contains(output)),Is.False);
            var before = session.Design; var revision = session.Revision;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var error = Assert.Throws<ArgumentException>(()=>session.ConnectPins(
                reverseClicks?input:output,reverseClicks?output:input));
            timer.Stop();
            Assert.That(error.Message,Does.Not.Contain("search limit"),"Blocked entry should reject before search.");
            Assert.That(timer.Elapsed.TotalSeconds,Is.LessThan(1));
            Assert.That(session.Design,Is.SameAs(before));
            Assert.That(session.Revision,Is.EqualTo(revision));
            TestContext.WriteLine("Blocked pin rejection: "+timer.Elapsed.TotalMilliseconds.ToString("F3")+" ms");
            foreach(var remaining in session.Design.Topology.Connectors.ToArray())session.BreakConnector(remaining.Id);
            session.ConnectPins(output,input);
            Assert.That(session.Built.Graph.Connected(output,input),Is.True);
        }

        [Test]
        public void UnreachablePocketHasBoundedSearchAndNeverPublishesAPartialRoute()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(new WorldBounds(1024,1024,256)));
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(12,1,2),GridOrientation.Default);
            // The source's adjacent cell is open, but completely enclosed by
            // the floor and five component bodies. One-step preflight passes.
            foreach(var cell in new[]{new GridCell(4,1,2),new GridCell(3,2,2),
                new GridCell(3,1,1),new GridCell(3,1,3)})
                session.PlaceComponent(BuiltInPinCatalog.And,cell,GridOrientation.Default);
            var before = session.Design; var revision = session.Revision;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var error = Assert.Throws<ArgumentException>(()=>session.ConnectPins(
                JoinMember.ComponentPin(session.Design.Components[0].Id,session.Design.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(session.Design.Components[1].Id,session.Design.Components[1].PinIds["A"])));
            timer.Stop();
            Assert.That(error.Message,Does.Contain("search limit"));
            Assert.That(timer.Elapsed.TotalSeconds,Is.LessThan(1));
            Assert.That(session.Design,Is.SameAs(before));
            Assert.That(session.Revision,Is.EqualTo(revision));
            Assert.That(session.Design.Topology.Connectors,Is.Empty);
            TestContext.WriteLine("Unreachable pocket rejection: "+timer.Elapsed.TotalMilliseconds.ToString("F3")+" ms");
        }
    }
}
