using System;
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
                Is.EqualTo(new QuarterPoint(0, 1, 1)));
            Assert.That(proposal.Route.Nodes[1].PointQ,
                Is.EqualTo(new QuarterPoint(4, 3, 1)));
            Assert.That(proposal.Route.Spans.Count, Is.EqualTo(1));
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
                    new[] { node }, Array.Empty<RouteSpan>());
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
