using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitWorldConnectorTests
    {
        [Test]
        public void TouchingPinsNeedsTwoExplicitJoinsAndBreakingRecalculates()
        {
            var world = SourceAndGate();
            var source = world.Components[0];
            var gate = world.Components[1];
            var west = Node(new QuarterPoint(0, 1, 1), 0);
            var east = Node(new QuarterPoint(4, 1, 1), 0);
            var span = new RouteSpan(Guid.NewGuid(), west.Id, east.Id);
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { west, east }, new[] { span });
            var touched = OneBitWorldEdits.PlaceConnector(world, route,
                Array.Empty<ElectricalJoin>());
            var untouchedSession = new OneBitWorldSession(touched);
            var gateA = JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]);
            Assert.That(untouchedSession.Circuit.Net(untouchedSession.Built.NetIndex(gateA)).Value,
                Is.EqualTo(LogicBit.Z), "Matching geometry alone does not attach a pin.");

            var joins = new[]
            {
                Join(JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                    JoinMember.ConnectorNode(route.Id, west.Id)),
                Join(JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]),
                    JoinMember.ConnectorNode(route.Id, east.Id))
            };
            var connected = OneBitWorldEdits.PlaceConnector(world, route, joins);
            var session = new OneBitWorldSession(connected);
            var y = JoinMember.ComponentPin(gate.Id, gate.PinIds["Y"]);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(gateA)).Value,
                Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(y)).Value,
                Is.EqualTo(LogicBit.Zero), "0 AND undriven B is zero.");

            session.BreakSpan(route.Id, span.Id);
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(2));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(gateA)).Value,
                Is.EqualTo(LogicBit.Z));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(y)).Value,
                Is.EqualTo(LogicBit.X), "Z AND undriven B is X.");
            foreach (var piece in session.Design.Topology.Connectors)
                Assert.That(piece.Id, Is.Not.EqualTo(route.Id));
        }

        [Test]
        public void SecondConnectorOnOccupiedPinRejectsWholeRevision()
        {
            var world = SourceAndGate();
            var source = world.Components[0];
            var firstNode = Node(new QuarterPoint(0, 1, 1), 0);
            var first = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { firstNode }, Array.Empty<RouteSpan>());
            var session = new OneBitWorldSession(OneBitWorldEdits.PlaceConnector(world,
                first, new[] { Join(JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                    JoinMember.ConnectorNode(first.Id, firstNode.Id)) }));
            var before = session.Design;
            var secondNode = Node(new QuarterPoint(0, 1, 1), 1);
            var second = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { secondNode }, Array.Empty<RouteSpan>());
            Assert.Throws<ArgumentException>(() => session.PlaceConnector(second,
                new[] { Join(JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                    JoinMember.ConnectorNode(second.Id, secondNode.Id)) }));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(0UL));
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(1));
            Assert.That(session.Scheduler.IsPaused, Is.True);
        }

        [Test]
        public void CrossingStaysSeparateUntilTargetedJunctionIsPublished()
        {
            var empty = OneBitWorldDesign.Empty(new WorldBounds(20, 20, 10));
            var west = Node(new QuarterPoint(0, 1, 1), 0);
            var hCenter = Node(new QuarterPoint(2, 2, 2), 0);
            var east = Node(new QuarterPoint(4, 1, 1), 0);
            var south = Node(new QuarterPoint(1, 1, 0), 1);
            var vCenter = Node(new QuarterPoint(2, 2, 2), 1);
            var north = Node(new QuarterPoint(1, 1, 4), 1);
            var horizontal = Route(0, west, hCenter, east);
            var vertical = Route(1, south, vCenter, north);
            var session = new OneBitWorldSession(empty);
            session.PlaceConnector(horizontal, Array.Empty<ElectricalJoin>());
            session.PlaceConnector(vertical, Array.Empty<ElectricalJoin>());
            var westRef = JoinMember.ConnectorNode(horizontal.Id, west.Id);
            var northRef = JoinMember.ConnectorNode(vertical.Id, north.Id);
            Assert.That(session.Built.Graph.Connected(westRef, northRef), Is.False);
            Assert.That(OneBitVisualTopology.Junctions(session.Design.Topology).Count,
                Is.EqualTo(0), "An unjoined crossing has no junction marker.");
            session.AddJoin(Join(JoinMember.ConnectorNode(horizontal.Id, hCenter.Id),
                JoinMember.ConnectorNode(vertical.Id, vCenter.Id)));
            Assert.That(session.Built.Graph.Connected(westRef, northRef), Is.True);
            var junctions = OneBitVisualTopology.Junctions(session.Design.Topology);
            Assert.That(junctions.Count, Is.EqualTo(1));
            Assert.That(junctions[0].DirectionCount, Is.EqualTo(4));
            Assert.That(junctions[0].PointQ, Is.EqualTo(new QuarterPoint(2, 2, 2)));
        }

        [Test]
        public void TargetedThreeWayBranchGetsOneThreeDirectionMarker()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(20, 20, 10)));
            var west = Node(new QuarterPoint(0, 1, 1), 0);
            var horizontalCenter = Node(new QuarterPoint(2, 2, 2), 0);
            var east = Node(new QuarterPoint(4, 1, 1), 0);
            var south = Node(new QuarterPoint(1, 1, 0), 1);
            var branchCenter = Node(new QuarterPoint(2, 2, 2), 1);
            var horizontal = Route(0, west, horizontalCenter, east);
            var branch = Route(1, south, branchCenter);
            session.PlaceConnector(horizontal, Array.Empty<ElectricalJoin>());
            session.PlaceConnector(branch, Array.Empty<ElectricalJoin>());
            Assert.That(OneBitVisualTopology.Junctions(session.Design.Topology).Count,
                Is.EqualTo(0));

            session.AddJoin(Join(
                JoinMember.ConnectorNode(horizontal.Id, horizontalCenter.Id),
                JoinMember.ConnectorNode(branch.Id, branchCenter.Id)));
            var markers = OneBitVisualTopology.Junctions(session.Design.Topology);
            Assert.That(markers.Count, Is.EqualTo(1));
            Assert.That(markers[0].DirectionCount, Is.EqualTo(3));
        }

        private static OneBitWorldDesign SourceAndGate()
        {
            var world = OneBitWorldDesign.Empty(new WorldBounds(20, 20, 10));
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            return OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.And,
                new GridCell(4, 1, 2), GridOrientation.Default);
        }

        private static RouteNode Node(QuarterPoint point, int channel) =>
            new RouteNode(Guid.NewGuid(), new GridCell(3, 1, 2), channel, point);

        private static ElectricalJoin Join(params JoinMember[] members) =>
            new ElectricalJoin(Guid.NewGuid(), members);

        private static ConnectorRoute Route(int channel, params RouteNode[] nodes)
        {
            var spans = new RouteSpan[nodes.Length - 1];
            for (var i = 1; i < nodes.Length; i++)
                spans[i - 1] = new RouteSpan(Guid.NewGuid(), nodes[i - 1].Id, nodes[i].Id);
            return new ConnectorRoute(Guid.NewGuid(), "wire", 1, nodes, spans);
        }
    }
}
