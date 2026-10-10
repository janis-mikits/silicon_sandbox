using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SiliconSandbox.Persistence;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitPinRoutePlannerTests
    {
        [Test]
        public void OffsetInputHasTwoTurnsAndLongestInputRunRegardlessOfClickOrder()
        {
            var world=OneBitWorldDesign.Empty(new WorldBounds(10,8,5));
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(6,1,2),GridOrientation.Default);
            var output=JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]);
            var input=JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["B"]);
            var a=OneBitPinRoutePlanner.Plan(world,output,input).Route;
            var b=OneBitPinRoutePlanner.Plan(world,input,output).Route;
            var expected=new[]{new GridCell(24,7,9),new GridCell(13,7,9),new GridCell(13,5,9),new GridCell(12,5,9)};
            Assert.That(Bends(a),Is.EqualTo(expected));
            Assert.That(Bends(b),Is.EqualTo(expected));
            Assert.That(a.GeometryVersion,Is.EqualTo(2));
        }

        [Test]
        public void PinApproachesAndQuarterMinimumHoldInAll24Orientations()
        {
            var count=0;
            foreach(GridDirection forward in Enum.GetValues(typeof(GridDirection)))
            foreach(GridDirection up in Enum.GetValues(typeof(GridDirection)))
            {
                GridOrientation rotation;
                try{rotation=new GridOrientation(forward,up);}catch(ArgumentException){continue;}
                var delta=rotation.TransformCellOffset(new GridCell(4,0,0));
                var world=OneBitWorldDesign.Empty(new WorldBounds(24,24,24));
                world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.Source,new GridCell(10,10,10),rotation);
                world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,
                    new GridCell(10+delta.X,10+delta.Y,10+delta.Z),rotation);
                var route=OneBitPinRoutePlanner.Plan(world,
                    JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]),
                    JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["B"])).Route;
                var bends=Bends(route);
                Assert.That(bends.Count,Is.EqualTo(4));
                var inputOut=rotation.TransformCellOffset(new GridCell(-1,0,0));
                var outputIn=rotation.TransformCellOffset(new GridCell(-1,0,0));
                Assert.That(Unit(bends[0],bends[1]),Is.EqualTo(inputOut));
                Assert.That(Unit(bends[2],bends[3]),Is.EqualTo(outputIn));
                for(var i=1;i<bends.Count;i++)
                    Assert.That(Length(bends[i-1],bends[i]),Is.GreaterThanOrEqualTo(1));
                Assert.That(Length(bends[0],bends[1]),Is.EqualTo(11));
                count++;
            }
            Assert.That(count,Is.EqualTo(24));
        }

        [Test]
        public void ObstacleDetourUsesFourTurnsAndNeverEntersBlockedCell()
        {
            var world=OneBitWorldDesign.Empty(new WorldBounds(9,7,4));
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.Source,new GridCell(1,1,2),GridOrientation.Default);
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(6,1,2),GridOrientation.Default);
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(3,1,2),GridOrientation.Default);
            var route=OneBitPinRoutePlanner.Plan(world,
                JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["A"])).Route;
            Assert.That(Bends(route).Count-2,Is.EqualTo(4));
            Assert.That(route.Nodes.Any(n=>n.Cell.Equals(new GridCell(3,1,2))),Is.False);
            Assert.That(Bends(route).Zip(Bends(route).Skip(1),Length).Sum(),Is.EqualTo(20));
        }

        [Test]
        public void FewerTurnsWinEvenWhenAStaggeredZigzagWouldBeShorter()
        {
            var world=OneBitWorldDesign.Empty(new WorldBounds(12,12,2));
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.Source,new GridCell(1,1,5),GridOrientation.Default);
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(8,1,5),GridOrientation.Default);
            for(var z=5;z<=9;z++)world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(3,1,z),GridOrientation.Default);
            for(var z=1;z<=5;z++)world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(5,1,z),GridOrientation.Default);
            var route=OneBitPinRoutePlanner.Plan(world,
                JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["A"])).Route;
            var bends=Bends(route);
            Assert.That(bends.Count-2,Is.EqualTo(4));
            // Go below both walls: six cells horizontally and nine vertically
            // on the X/Z diagram. The short gap zigzag would require six turns.
            Assert.That(bends.Zip(bends.Skip(1),Length).Sum(),Is.EqualTo(60));
        }

        [Test]
        public void TwoWiresUseSeparateQuarterTracksWithoutChannelDisplayOffsets()
        {
            var session=new OneBitWorldSession(OneBitWorldDesign.Empty(new WorldBounds(10,8,5)));
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,3),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(6,1,2),GridOrientation.Default);
            var a=session.Design.Components[0];var b=session.Design.Components[1];var gate=session.Design.Components[2];
            var inputA=JoinMember.ComponentPin(gate.Id,gate.PinIds["A"]);
            var inputB=JoinMember.ComponentPin(gate.Id,gate.PinIds["B"]);
            session.ConnectPins(JoinMember.ComponentPin(a.Id,a.PinIds["OUT"]),inputA);
            session.ConnectPins(JoinMember.ComponentPin(b.Id,b.PinIds["OUT"]),inputB);
            var first=session.Design.Topology.Connectors[0];var second=session.Design.Topology.Connectors[1];
            Assert.That(second.Nodes[0].Channel,Is.EqualTo(1));
            Assert.That(session.Built.Graph.Connected(inputA,inputB),Is.False);
            var occupied=new HashSet<GridCell>(Samples(first));
            foreach(var point in Samples(second))Assert.That(occupied.Contains(point),Is.False);
        }

        [Test]
        public void ExactBendsRoundTripThroughVersionTwoArchiveAndSurviveBreaking()
        {
            var session=new OneBitWorldSession(OneBitWorldDesign.Empty(new WorldBounds(10,8,5)));
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(6,1,2),GridOrientation.Default);
            session.ConnectPins(JoinMember.ComponentPin(session.Design.Components[0].Id,session.Design.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(session.Design.Components[1].Id,session.Design.Components[1].PinIds["B"]));
            var route=session.Design.Topology.Connectors[0];
            var snapshot=new WorldSaveSnapshot(Guid.NewGuid(),"Routing","builtin.floor.default","builtin.wall.default","1",
                new SavedPlayerPose(1,2,1,0,0,1),new SavedInventoryItem[36],0,session.Design,
                new Dictionary<Guid,OneBitModuleVersion>());
            using(var stream=new MemoryStream(WorldV1ArchiveCodec.Write(snapshot)))
            {
                var loaded=WorldV1ArchiveCodec.Read(stream);
                Assert.That(loaded.Manifest.FormatVersion,Is.EqualTo(2));
                var copy=loaded.Snapshot.Design.Topology.Connectors[0];
                Assert.That(copy.GeometryVersion,Is.EqualTo(2));
                Assert.That(Bends(copy),Is.EqualTo(Bends(route)));
                Assert.That(copy.Nodes.Select(n=>n.Id),Is.EqualTo(route.Nodes.Select(n=>n.Id)));
                var reopened=new OneBitWorldSession(loaded.Snapshot.Design);
                reopened.BreakSpan(copy.Id,copy.Spans[copy.Spans.Count/2].Id);
                foreach(var piece in reopened.Design.Topology.Connectors)Assert.That(piece.GeometryVersion,Is.EqualTo(2));
            }
        }

        [Test]
        public void TieBreakMaximizesEachSuccessiveInputSideRun()
        {
            var world=OneBitWorldDesign.Empty(new WorldBounds(10,8,5));
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(6,2,4),GridOrientation.Default);
            var route=OneBitPinRoutePlanner.Plan(world,
                JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["B"])).Route;
            Assert.That(Bends(route),Is.EqualTo(new[]{new GridCell(24,11,17),new GridCell(13,11,17),
                new GridCell(13,11,9),new GridCell(13,5,9),new GridCell(12,5,9)}));
            world=OneBitWorldEdits.PlaceConnector(world,route,new[]{
                new ElectricalJoin(Guid.NewGuid(),new[]{JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]),
                    JoinMember.ConnectorNode(route.Id,route.Nodes[route.Nodes.Count-1].Id)}),
                new ElectricalJoin(Guid.NewGuid(),new[]{JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["B"]),
                    JoinMember.ConnectorNode(route.Id,route.Nodes[0].Id)})});
            var snapshot=OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(1,1,1),new GridCell(7,3,5)));
            Assert.That(snapshot.Topology.Connectors[0].GeometryVersion,Is.EqualTo(2));
        }

        [Test]
        public void LargeWorldLocalRouteUsesTheSameMinimumWithoutScanningTheWorld()
        {
            var world=OneBitWorldDesign.Empty(new WorldBounds(1024,1024,256));
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.Source,new GridCell(500,100,500),GridOrientation.Default);
            world=OneBitWorldEdits.PlaceComponent(world,BuiltInPinCatalog.And,new GridCell(510,105,509),GridOrientation.Default);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var route=OneBitPinRoutePlanner.Plan(world,
                JoinMember.ComponentPin(world.Components[0].Id,world.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(world.Components[1].Id,world.Components[1].PinIds["B"])).Route;
            timer.Stop();
            TestContext.WriteLine("Large-world quarter-route planning: "+timer.Elapsed.TotalMilliseconds.ToString("F3")+" ms");
            var bends=Bends(route);
            Assert.That(bends.Count-2,Is.EqualTo(3));
            Assert.That(bends.Zip(bends.Skip(1),Length).Sum(),Is.EqualTo(94));
        }

        [Test]
        public void PinCanBranchIntoAnExistingWireAtItsFaceBoundaryNode()
        {
            var session=new OneBitWorldSession(OneBitWorldDesign.Empty(new WorldBounds(10,8,4)));
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(2,1,2),GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,new GridCell(7,1,2),GridOrientation.Default);
            session.ConnectPins(JoinMember.ComponentPin(session.Design.Components[0].Id,session.Design.Components[0].PinIds["OUT"]),
                JoinMember.ComponentPin(session.Design.Components[1].Id,session.Design.Components[1].PinIds["A"]));
            var trunk=session.Design.Topology.Connectors[0];
            var target=trunk.Nodes.First(n=>n.Cell.X*4+n.PointQ.X==16);
            session.PlaceComponent(BuiltInPinCatalog.Source,new GridCell(4,1,0),
                GridOrientation.Default.CounterclockwiseYaw());
            var source=session.Design.Components[2];
            var pin=JoinMember.ComponentPin(source.Id,source.PinIds["OUT"]);
            var junction=JoinMember.ConnectorNode(trunk.Id,target.Id);
            session.ConnectPinToNode(pin,junction);
            Assert.That(session.Built.Graph.Connected(pin,junction),Is.True);
            var branch=session.Design.Topology.Connectors[1];
            Assert.That(branch.GeometryVersion,Is.EqualTo(2));
            var bends=Bends(branch);
            Assert.That(Unit(bends[0],bends[1]),Is.EqualTo(new GridCell(0,0,1)));
            for(var i=1;i<bends.Count;i++)Assert.That(Length(bends[i-1],bends[i]),Is.GreaterThanOrEqualTo(1));
        }

        private static IEnumerable<GridCell> Samples(ConnectorRoute route)
        {
            var bends=Bends(route);
            for(var i=1;i<bends.Count;i++)
            {
                var step=Unit(bends[i-1],bends[i]);
                for(var p=bends[i-1];!p.Equals(bends[i]);p=new GridCell(p.X+step.X,p.Y+step.Y,p.Z+step.Z))yield return p;
            }
            yield return bends[bends.Count-1];
        }

        private static int Length(GridCell a,GridCell b)=>Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)+Math.Abs(a.Z-b.Z);
        private static GridCell Unit(GridCell a,GridCell b)=>new GridCell(Math.Sign(b.X-a.X),Math.Sign(b.Y-a.Y),Math.Sign(b.Z-a.Z));
        private static List<GridCell> Bends(ConnectorRoute route)
        {
            var points=new List<GridCell>();
            foreach(var n in route.Nodes)
            {
                var p=new GridCell(n.Cell.X*4+n.PointQ.X,n.Cell.Y*4+n.PointQ.Y,n.Cell.Z*4+n.PointQ.Z);
                if(points.Count==0 || !points[points.Count-1].Equals(p))points.Add(p);
            }
            for(var i=points.Count-2;i>0;i--)
                if(Unit(points[i-1],points[i]).Equals(Unit(points[i],points[i+1])))points.RemoveAt(i);
            return points;
        }

        [Test]
        public void GateOutputCanEndAtVisibleOpenWireAndRemainInspectable()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)));
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(3, 1, 3), GridOrientation.Default);
            var gate = session.Design.Components[0];
            var output = JoinMember.ComponentPin(gate.Id, gate.PinIds["Y"]);

            session.PlaceWireStub(output, new GridCell(5, 1, 3));

            var route = session.Design.Topology.Connectors[0];
            Assert.That(session.Design.Topology.Joins.Count, Is.EqualTo(1));
            Assert.That(route.Spans.Count, Is.GreaterThan(0));
            Assert.That(route.Nodes[route.Nodes.Count - 1].Cell,
                Is.EqualTo(new GridCell(5, 1, 3)));
            Assert.That(route.Nodes[route.Nodes.Count - 1].PointQ,
                Is.EqualTo(new QuarterPoint(2, 2, 2)));
            Assert.That(session.Built.Graph.Connected(output,
                JoinMember.ConnectorNode(route.Id,
                    route.Nodes[route.Nodes.Count - 1].Id)), Is.True);
            Assert.That(session.Inspector.InspectConnector(route.Id).Value,
                Is.EqualTo(LogicBit.X),
                "Two released AND inputs make an uncertain, inspectable Y net.");
        }

        [Test]
        public void OpenWireTargetInsideOccupiedCellRejectsAtomically()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(4, 1, 2), GridOrientation.Default);
            var source = session.Design.Components[0];
            var before = session.Design;
            var revision = session.Revision;
            Assert.Throws<ArgumentException>(() => session.PlaceWireStub(
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                new GridCell(4, 1, 2)));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void TwoTargetWirePublishesExplicitJoinsAndSettledAndInput()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)));
            session.PlaceComponent(BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And, new GridCell(4, 1, 2),
                GridOrientation.Default);
            var source = session.Design.Components[0];
            var gate = session.Design.Components[1];
            var output = JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);
            var input = JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]);
            var beforeRevision = session.Revision;
            session.ConnectPins(output, input);

            Assert.That(session.Revision, Is.EqualTo(beforeRevision + 1));
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(1));
            Assert.That(session.Design.Topology.Joins.Count, Is.EqualTo(2));
            Assert.That(session.Design.Topology.Connectors[0].Nodes.Count, Is.EqualTo(2));
            Assert.That(session.Design.Topology.Connectors[0].Spans.Count, Is.EqualTo(1));
            Assert.That(session.Built.Graph.Connected(output, input), Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(input)).Value,
                Is.EqualTo(LogicBit.Zero));
            var inspected = session.Inspector.InspectConnector(
                session.Design.Topology.Connectors[0].Id);
            Assert.That(inspected.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(inspected.ConnectedPins.Count, Is.EqualTo(2));
            var foundOutput = false;
            var foundInput = false;
            foreach (var pin in inspected.ConnectedPins)
            {
                if (pin.Equals(output)) foundOutput = true;
                if (pin.Equals(input)) foundInput = true;
            }
            Assert.That(foundOutput && foundInput, Is.True);
        }

        [Test]
        public void RouteChangesQuadrantWithinWireCellToReachSrResetPin()
        {
            var world = OneBitWorldDesign.Empty(new WorldBounds(8, 8, 4));
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.SrFlipFlop,
                new GridCell(4, 1, 2), GridOrientation.Default);
            var source = world.Components[0];
            var sr = world.Components[1];
            var proposal = OneBitPinRoutePlanner.Plan(world,
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                JoinMember.ComponentPin(sr.Id, sr.PinIds["R"]));
            Assert.That(proposal.Route.Nodes[0].PointQ,
                Is.EqualTo(new QuarterPoint(4, 3, 1)));
            Assert.That(proposal.Route.Nodes.Select(n=>n.PointQ),Is.EqualTo(new[]{
                new QuarterPoint(4,3,1),new QuarterPoint(1,3,1),
                new QuarterPoint(1,1,1),new QuarterPoint(0,1,1)}));
            Assert.That(proposal.Route.Spans.Count, Is.EqualTo(3));
            var connected = OneBitWorldEdits.PlaceConnector(world,
                proposal.Route, proposal.Joins);
            Assert.That(connected.Topology.Connectors.Count, Is.EqualTo(1));
        }

        [Test]
        public void AdjacentMatchingPinsNeedAVisibleExplicitFaceBridge()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(5, 3, 3)));
            session.PlaceComponent(BuiltInPinCatalog.Source, new GridCell(1, 1, 1),
                GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And, new GridCell(2, 1, 1),
                GridOrientation.Default);
            var source = session.Design.Components[0];
            var gate = session.Design.Components[1];
            var output = JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);
            var input = JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]);
            Assert.That(session.Built.Graph.Connected(output, input), Is.False,
                "Touching component pins never connect by proximity.");

            session.ConnectPins(output, input);
            var bridge = session.Design.Topology.Connectors[0];
            Assert.That(bridge.Nodes.Count, Is.EqualTo(2));
            Assert.That(bridge.Spans.Count, Is.EqualTo(1));
            Assert.That(bridge.Nodes[0].Cell, Is.EqualTo(source.AnchorCell));
            Assert.That(bridge.Nodes[1].Cell, Is.EqualTo(gate.AnchorCell));
            Assert.That(session.Built.Graph.Connected(output, input), Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(input)).Value,
                Is.EqualTo(LogicBit.Zero));

            session.BreakSpan(bridge.Id, bridge.Spans[0].Id);
            Assert.That(session.Built.Graph.Connected(output, input), Is.False);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(input)).Value,
                Is.EqualTo(LogicBit.Z));
        }

        [Test]
        public void UnjoinedFaceEndInsideComponentCellRemainsElectricallyOpen()
        {
            var world = OneBitWorldEdits.PlaceComponent(
                OneBitWorldDesign.Empty(new WorldBounds(5, 3, 3)),
                BuiltInPinCatalog.Source, new GridCell(1, 1, 1),
                GridOrientation.Default);
            var unjoinedNode = new RouteNode(Guid.NewGuid(),
                world.Components[0].AnchorCell, 0, new QuarterPoint(4, 1, 1));
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { unjoinedNode }, Array.Empty<RouteSpan>());
            var withOpenEnd = OneBitWorldEdits.PlaceConnector(world, route,
                Array.Empty<ElectricalJoin>());
            var graph = OneBitTopologyGraphBuilder.Build(withOpenEnd.Topology);
            Assert.That(graph.Connected(
                JoinMember.ComponentPin(world.Components[0].Id,
                    world.Components[0].PinIds["OUT"]),
                JoinMember.ConnectorNode(route.Id, unjoinedNode.Id)), Is.False);
            var interiorNode = new RouteNode(Guid.NewGuid(),
                world.Components[0].AnchorCell, 1,
                new QuarterPoint(2, 2, 2));
            var interiorRoute = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { interiorNode }, Array.Empty<RouteSpan>());
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceConnector(
                world, interiorRoute, Array.Empty<ElectricalJoin>()));
            Assert.That(world.Topology.Connectors.Count, Is.EqualTo(0));
        }

        [Test]
        public void TargetedExistingNodeMakesJunctionOnlyAfterExplicitEdit()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var source = session.Design.Components[0];
            var center = new RouteNode(Guid.NewGuid(), new GridCell(4, 1, 2),
                0, new QuarterPoint(2, 2, 2));
            var existing = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { center }, Array.Empty<RouteSpan>());
            session.PlaceConnector(existing, Array.Empty<ElectricalJoin>());
            var output = JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);
            var target = JoinMember.ConnectorNode(existing.Id, center.Id);
            Assert.That(session.Built.Graph.Connected(output, target), Is.False,
                "A visible nearby route does not create an implicit join.");
            var before = session.Revision;

            session.ConnectPinToNode(output, target);
            Assert.That(session.Revision, Is.EqualTo(before + 1));
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(2));
            Assert.That(session.Design.Topology.Joins.Count, Is.EqualTo(2));
            Assert.That(session.Built.Graph.Connected(output, target), Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(target)).Value,
                Is.EqualTo(LogicBit.Zero));
            var branch = session.Design.Topology.Connectors[1];
            Assert.That(branch.Nodes[branch.Nodes.Count - 1].PointQ,
                Is.EqualTo(new QuarterPoint(2, 2, 2)));
        }

        [Test]
        public void AllFourChannelsBlockedRejectsWithoutPublishing()
        {
            var world = OneBitWorldDesign.Empty(new WorldBounds(3, 1, 2));
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.Source,
                new GridCell(0, 1, 0), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.And,
                new GridCell(2, 1, 0), GridOrientation.Default);
            for (var channel = 0; channel < 4; channel++)
            {
                var node = new RouteNode(Guid.NewGuid(), new GridCell(1, 1, 0),
                    channel, new QuarterPoint(2, 2, 2));
                var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                    new[] { node }, Array.Empty<RouteSpan>(), geometryVersion: 2);
                world = OneBitWorldEdits.PlaceConnector(world, route,
                    Array.Empty<ElectricalJoin>());
            }
            var session = new OneBitWorldSession(world);
            var before = session.Design;
            var source = world.Components[0];
            var gate = world.Components[1];
            Assert.Throws<ArgumentException>(() => session.ConnectPins(
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                JoinMember.ComponentPin(gate.Id, gate.PinIds["A"])));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(0UL));
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(4));
        }
    }
}
