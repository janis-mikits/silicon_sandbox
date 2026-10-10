using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class PinConnectionCorridorTests
    {
        private static OneBitWorldDesign Empty() => OneBitWorldDesign.Empty(new WorldBounds(12,12,12));
        private static JoinMember Pin(PlacedOneBitComponent block, string key) =>
            JoinMember.ComponentPin(block.Id,block.PinIds[key]);

        [Test]
        public void PassingWirePreservesNeighboringSrOutputAndBothAndInputs()
        {
            var session = new OneBitWorldSession(Empty());
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(4,1,2),GridOrientation.Default);
            var sr = session.Design.Components[0]; var gate = session.Design.Components[1];
            session.ConnectPins(Pin(sr,"Q"),Pin(gate,"B"));
            var first = session.Design.Topology.Connectors[0];
            // The old bend at x=3.25 crossed Q_bar's required entry. Literal
            // world coordinates, independent of the corridor implementation.
            foreach (var node in first.Nodes)
                if (node.Cell.Y*4+node.PointQ.Y==7 && node.Cell.Z*4+node.PointQ.Z==9)
                    Assert.That(node.Cell.X*4+node.PointQ.X,Is.GreaterThanOrEqualTo(14));
            session.ConnectPins(Pin(sr,"Q_bar"),Pin(gate,"A"));
            Assert.That(session.Built.Graph.Connected(Pin(sr,"Q"),Pin(gate,"B")),Is.True);
            Assert.That(session.Built.Graph.Connected(Pin(sr,"Q_bar"),Pin(gate,"A")),Is.True);
            Assert.That(session.Built.Graph.Connected(Pin(gate,"A"),Pin(gate,"B")),Is.False);
        }

        [Test]
        public void ComponentPlacementRejectsBlockedFrontButAllowsHalfCellOffset()
        {
            foreach (var qx in new[] {1,2})
            {
                var session = new OneBitWorldSession(Empty());
                session.PlaceConnector(Wire(new GridCell(3,1,2),new QuarterPoint(qx,1,1),
                    new QuarterPoint(qx,3,1)),Array.Empty<ElectricalJoin>());
                var before=session.Design;var revision=session.Revision;
                if(qx==1)
                {
                    Assert.Throws<ArgumentException>(()=>session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                        new GridCell(2,1,2),GridOrientation.Default));
                    Assert.That(session.Design,Is.SameAs(before));
                    Assert.That(session.Revision,Is.EqualTo(revision));
                }
                else
                    Assert.DoesNotThrow(()=>session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                        new GridCell(2,1,2),GridOrientation.Default));
            }
        }

        [Test]
        public void RotatedPinCorridorsRejectBlockPlacementInAll24Orientations()
        {
            var count=0;
            foreach(GridDirection forward in Enum.GetValues(typeof(GridDirection)))
            foreach(GridDirection up in Enum.GetValues(typeof(GridDirection)))
            {
                GridOrientation rotation;
                try { rotation=new GridOrientation(forward,up); } catch(ArgumentException) { continue; }
                var offset=rotation.TransformCellOffset(new GridCell(1,0,0));
                var wire=Wire(new GridCell(4+offset.X,4+offset.Y,4+offset.Z),
                    rotation.TransformPoint(new QuarterPoint(1,1,1)),
                    rotation.TransformPoint(new QuarterPoint(1,3,1)));
                var world=OneBitWorldEdits.PlaceConnector(Empty(),wire,Array.Empty<ElectricalJoin>());
                Assert.Throws<ArgumentException>(()=>OneBitWorldEdits.PlaceComponent(world,
                    BuiltInPinCatalog.SrFlipFlop,new GridCell(4,4,4),rotation));
                count++;
            }
            Assert.That(count,Is.EqualTo(24));
        }

        [Test]
        public void ModulePortsReserveTheSameApproachSpaceAsBuiltInPins()
        {
            var internalNode=new RouteNode(Guid.NewGuid(),new GridCell(0,0,0),0,new QuarterPoint(2,2,2));
            var internalRoute=new ConnectorRoute(Guid.NewGuid(),"wire",1,new[]{internalNode},Array.Empty<RouteSpan>());
            var port=new OneBitModulePort(Guid.NewGuid(),"OUT",OneBitPortDirection.Output,new GridCell(0,0,0),
                new QuarterPoint(4,1,1),JoinMember.ConnectorNode(internalRoute.Id,internalNode.Id));
            var version=new OneBitModuleVersion(Guid.NewGuid(),Guid.NewGuid(),"Port",new GridCell(1,1,1),
                Array.Empty<PlacedOneBitComponent>(),new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                    new[]{internalRoute},Array.Empty<ElectricalJoin>()),new[]{port});
            var wire=Wire(new GridCell(3,1,2),new QuarterPoint(1,1,1),new QuarterPoint(1,3,1));
            var blocked=OneBitWorldEdits.PlaceConnector(Empty(),wire,Array.Empty<ElectricalJoin>());
            Assert.Throws<ArgumentException>(()=>OneBitWorldEdits.PlaceModule(blocked,version,"P",
                new GridCell(2,1,2),GridOrientation.Default));
            var world=OneBitWorldEdits.PlaceModule(Empty(),version,"P",new GridCell(2,1,2),GridOrientation.Default);
            Assert.Throws<ArgumentException>(()=>OneBitWorldEdits.PlaceConnector(world,wire,Array.Empty<ElectricalJoin>()));
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(4,1,2),GridOrientation.Default);
            var proposal=OneBitPinRoutePlanner.Plan(world,
                JoinMember.ModulePortBit(world.Modules[0].Id,port.Id,0),Pin(world.Components[0],"A"));
            Assert.DoesNotThrow(()=>OneBitWorldEdits.PlaceConnector(world,proposal.Route,proposal.Joins));
        }

        [Test]
        public void TouchingPinsStillAcceptTheirExplicitBridge()
        {
            var session=new OneBitWorldSession(Empty());
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(3,1,2),GridOrientation.Default);
            var output=Pin(session.Design.Components[0],"OUT");var input=Pin(session.Design.Components[1],"A");
            session.ConnectPins(output,input);
            Assert.That(session.Built.Graph.Connected(output,input),Is.True);
            Assert.That(session.Design.Topology.Connectors.Single().Spans.Count,Is.EqualTo(1));
        }

        [TestCase(0, false)] // 0.21 offset on both transverse axes: 0.297 clearance.
        [TestCase(1, true)]  // 0.07 offset on both transverse axes: 0.099 clearance.
        public void PlacementClearanceUsesTheVisiblePositionOfOlderWireLanes(int channel, bool blocked)
        {
            var a=new RouteNode(Guid.NewGuid(),new GridCell(3,1,2),channel,new QuarterPoint(0,1,1));
            var b=new RouteNode(Guid.NewGuid(),a.Cell,channel,new QuarterPoint(4,1,1));
            var route=new ConnectorRoute(Guid.NewGuid(),"wire",1,new[]{a,b},
                new[]{new RouteSpan(Guid.NewGuid(),a.Id,b.Id)});
            var world=OneBitWorldEdits.PlaceConnector(Empty(),route,Array.Empty<ElectricalJoin>());
            TestDelegate place=()=>OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2,1,2),GridOrientation.Default);
            if(blocked)Assert.Throws<ArgumentException>(place);
            else Assert.DoesNotThrow(place);
        }

        private static ConnectorRoute Wire(GridCell cell,QuarterPoint a,QuarterPoint b)
        {
            var first=new RouteNode(Guid.NewGuid(),cell,0,a);var second=new RouteNode(Guid.NewGuid(),cell,0,b);
            return new ConnectorRoute(Guid.NewGuid(),"wire",1,new[]{first,second},
                new[]{new RouteSpan(Guid.NewGuid(),first.Id,second.Id)},geometryVersion:2);
        }
    }
}
