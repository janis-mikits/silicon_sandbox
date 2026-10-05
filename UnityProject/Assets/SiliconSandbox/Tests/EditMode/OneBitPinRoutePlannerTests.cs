using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitPinRoutePlannerTests
    {
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
