using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldClockNetLinkTopologyTests
    {
        [Test]
        public void ReservedWorldClockStubsJoinByIdentityWithoutGeometricContact()
        {
            var first = ClockRoute(new GridCell(5, 1, 5), 0);
            var second = ClockRoute(new GridCell(9, 1, 9), 0);
            var ordinary = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { new RouteNode(Guid.NewGuid(), new GridCell(10, 1, 10),
                    0, new QuarterPoint(2, 2, 2)) }, Array.Empty<RouteSpan>());
            var topology = new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                new[] { first, second, ordinary }, Array.Empty<ElectricalJoin>());
            var graph = OneBitTopologyGraphBuilder.Build(topology);
            Assert.That(graph.Connected(Node(first), Node(second)), Is.True);
            Assert.That(graph.Connected(Node(first), Node(ordinary)), Is.False);
            Assert.That(graph.Nets.Count, Is.EqualTo(2));

            var built = OneBitCircuitPlanBuilder.Build(topology, Array.Empty<OneBitComponent>());
            Assert.That(built.Plan.WorldClock.HasValue, Is.True);
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            Assert.That(circuit.Net(built.NetIndex(Node(second))).Value, Is.EqualTo(LogicBit.Zero));
            circuit.SetWorldClockLevel(LogicBit.One);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Net(built.NetIndex(Node(first))).Value, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Net(built.NetIndex(Node(second))).Value, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Net(built.NetIndex(Node(ordinary))).Value, Is.EqualTo(LogicBit.Z));
        }

        [Test]
        public void BreakingClockStubKeepsLinkOnlyOnItsFirstNodeSide()
        {
            var anchor = new RouteNode(Guid.NewGuid(), new GridCell(5, 1, 5), 0,
                new QuarterPoint(0, 1, 1));
            var freeEnd = new RouteNode(Guid.NewGuid(), new GridCell(5, 1, 5), 0,
                new QuarterPoint(4, 1, 1));
            var span = new RouteSpan(Guid.NewGuid(), anchor.Id, freeEnd.Id);
            var first = new ConnectorRoute(Guid.NewGuid(), "netLink", 1,
                new[] { anchor, freeEnd }, new[] { span }, "", null,
                "@world-clock", "world", "worldClock");
            var second = ClockRoute(new GridCell(9, 1, 9), 0);
            var original = new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                new[] { first, second }, Array.Empty<ElectricalJoin>());
            Assert.That(OneBitTopologyGraphBuilder.Build(original)
                .Connected(Node(first, anchor), Node(first, freeEnd)), Is.True);

            var edit = OneBitTopologyEdits.BreakSpan(original, first.Id, span.Id);
            ConnectorRoute anchored = null, detached = null;
            foreach (var route in edit.Design.Connectors)
            {
                if (route.Nodes[0].Id == anchor.Id) anchored = route;
                if (route.Nodes[0].Id == freeEnd.Id) detached = route;
            }
            Assert.That(anchored, Is.Not.Null);
            Assert.That(detached, Is.Not.Null);
            Assert.That(anchored.Kind, Is.EqualTo("netLink"));
            Assert.That(detached.Kind, Is.EqualTo("wire"));
            Assert.That(detached.LinkName, Is.Null);
            Assert.That(edit.Graph.Connected(Node(anchored), Node(second)), Is.True);
            Assert.That(edit.Graph.Connected(Node(detached), Node(second)), Is.False);
            Assert.That(edit.Graph.Nets.Count, Is.EqualTo(2));

            var built = OneBitCircuitPlanBuilder.Build(edit.Design,
                Array.Empty<OneBitComponent>());
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            Assert.That(circuit.Net(built.NetIndex(Node(anchored))).Value,
                Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.Net(built.NetIndex(Node(detached))).Value,
                Is.EqualTo(LogicBit.Z));
        }

        [Test]
        public void InvalidReservedLinkFieldsAreRejected()
        {
            var node = new RouteNode(Guid.NewGuid(), new GridCell(5, 1, 5), 0,
                new QuarterPoint(2, 2, 2));
            var impostor = new ConnectorRoute(Guid.NewGuid(), "netLink", 1,
                new[] { node }, Array.Empty<RouteSpan>(), "", null,
                "@world-clock", "world", "player");
            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(
                new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                    new[] { impostor }, Array.Empty<ElectricalJoin>())));
        }

        private static ConnectorRoute ClockRoute(GridCell cell, int channel) =>
            new ConnectorRoute(Guid.NewGuid(), "netLink", 1,
                new[] { new RouteNode(Guid.NewGuid(), cell, channel,
                    new QuarterPoint(2, 2, 2)) }, Array.Empty<RouteSpan>(),
                "", null, "@world-clock", "world", "worldClock");

        private static JoinMember Node(ConnectorRoute route) =>
            Node(route, route.Nodes[0]);

        private static JoinMember Node(ConnectorRoute route, RouteNode node) =>
            JoinMember.ConnectorNode(route.Id, node.Id);
    }
}
