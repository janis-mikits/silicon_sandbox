using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitTopologyTests
    {
        private static readonly GridCell Cell = new GridCell(5, 1, 5);

        [Test]
        public void CrossingIsSeparateUntilExplicitCenterJoin()
        {
            var west = Node(0, new QuarterPoint(0, 1, 1));
            var horizontalCenter = Node(0, new QuarterPoint(2, 2, 2));
            var east = Node(0, new QuarterPoint(4, 1, 1));
            var south = Node(1, new QuarterPoint(1, 1, 0));
            var verticalCenter = Node(1, new QuarterPoint(2, 2, 2));
            var north = Node(1, new QuarterPoint(1, 1, 4));
            var horizontal = Route(west, horizontalCenter, east);
            var vertical = Route(south, verticalCenter, north);
            var hWest = JoinMember.ConnectorNode(horizontal.Id, west.Id);
            var hEast = JoinMember.ConnectorNode(horizontal.Id, east.Id);
            var vSouth = JoinMember.ConnectorNode(vertical.Id, south.Id);
            var vNorth = JoinMember.ConnectorNode(vertical.Id, north.Id);

            var separated = OneBitTopologyGraphBuilder.Build(Design(horizontal, vertical));
            Assert.That(separated.Connected(hWest, hEast), Is.True);
            Assert.That(separated.Connected(vSouth, vNorth), Is.True);
            Assert.That(separated.Connected(hWest, vSouth), Is.False,
                "Shared cell and crossing coordinate do not imply an electrical join.");
            Assert.That(separated.Nets.Count, Is.EqualTo(2));

            var junction = new ElectricalJoin(Guid.NewGuid(), new[]
            {
                JoinMember.ConnectorNode(horizontal.Id, horizontalCenter.Id),
                JoinMember.ConnectorNode(vertical.Id, verticalCenter.Id)
            });
            var joined = OneBitTopologyGraphBuilder.Build(Design(
                new[] { horizontal, vertical }, new[] { junction }));
            Assert.That(joined.Connected(hWest, vNorth), Is.True);
            Assert.That(joined.Nets.Count, Is.EqualTo(1));
        }

        [Test]
        public void PinNeedsExplicitJoinAndCannotAcceptTwoConnectors()
        {
            var objectId = Guid.NewGuid();
            var pinId = Guid.NewGuid();
            var pin = new AuthoredPin(objectId, pinId, new GridCell(4, 1, 5),
                new QuarterPoint(4, 1, 1));
            var firstNode = Node(0, new QuarterPoint(0, 1, 1));
            var secondNode = Node(1, new QuarterPoint(0, 1, 1));
            var first = Route(firstNode);
            var second = Route(secondNode);
            var pinRef = JoinMember.ComponentPin(objectId, pinId);
            var firstRef = JoinMember.ConnectorNode(first.Id, firstNode.Id);
            var secondRef = JoinMember.ConnectorNode(second.Id, secondNode.Id);

            var noJoin = OneBitTopologyGraphBuilder.Build(new OneBitAuthoredTopology(
                new[] { pin }, new[] { first, second }, Array.Empty<ElectricalJoin>()));
            Assert.That(noJoin.Connected(pinRef, firstRef), Is.False,
                "Matching face points alone do not attach a pin.");

            var attached = Join(pinRef, firstRef);
            var single = OneBitTopologyGraphBuilder.Build(new OneBitAuthoredTopology(
                new[] { pin }, new[] { first, second }, new[] { attached }));
            Assert.That(single.Connected(pinRef, firstRef), Is.True);
            Assert.That(single.Connected(pinRef, secondRef), Is.False);

            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(
                new OneBitAuthoredTopology(new[] { pin }, new[] { first, second },
                    new[] { attached, Join(pinRef, secondRef) })));
        }

        [Test]
        public void ChannelIdentityIsIndependentOfNetIdentity()
        {
            var firstNode = Node(0, new QuarterPoint(0, 1, 1));
            var secondNode = Node(0, new QuarterPoint(4, 1, 1));
            var first = Route(firstNode);
            var second = Route(secondNode);
            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(Design(first, second)),
                "Two unjoined connector paths cannot occupy the same channel in one cell.");

            var invalidChannel = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { Node(4, new QuarterPoint(0, 1, 1)) }, Array.Empty<RouteSpan>());
            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(Design(invalidChannel)));
        }

        [Test]
        public void SpanCannotJumpCellsOrInventConnectivity()
        {
            var first = Node(0, new QuarterPoint(4, 1, 1));
            var jumped = new RouteNode(Guid.NewGuid(), new GridCell(7, 1, 5), 0,
                new QuarterPoint(0, 1, 1));
            var bad = Route(first, jumped);
            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(Design(bad)));

            var next = new RouteNode(Guid.NewGuid(), new GridCell(6, 1, 5), 0,
                new QuarterPoint(0, 1, 1));
            var valid = Route(first, next);
            var graph = OneBitTopologyGraphBuilder.Build(Design(valid));
            Assert.That(graph.Connected(JoinMember.ConnectorNode(valid.Id, first.Id),
                JoinMember.ConnectorNode(valid.Id, next.Id)), Is.True);
        }

        [Test]
        public void JoinedConflictingTagsAreRejectedInsteadOfSilentlyChosen()
        {
            var firstNode = Node(0, new QuarterPoint(2, 2, 2));
            var secondNode = Node(1, new QuarterPoint(2, 2, 2));
            var first = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { firstNode }, Array.Empty<RouteSpan>(), "DATA");
            var second = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { secondNode }, Array.Empty<RouteSpan>(), "OTHER");
            var join = Join(JoinMember.ConnectorNode(first.Id, firstNode.Id),
                JoinMember.ConnectorNode(second.Id, secondNode.Id));
            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(
                Design(new[] { first, second }, new[] { join })));

            var untagged = new ConnectorRoute(second.Id, "wire", 1,
                new[] { secondNode }, Array.Empty<RouteSpan>());
            Assert.Throws<ArgumentException>(() => OneBitTopologyGraphBuilder.Build(
                Design(new[] { first, untagged }, new[] { join })),
                "A loaded joined net must have one authored tag on every piece.");
        }

        [Test]
        public void DerivedNetOrderDoesNotDependOnAuthoredArrayOrder()
        {
            var first = Route(Node(0, new QuarterPoint(0, 1, 1)));
            var second = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { new RouteNode(Guid.NewGuid(), new GridCell(8, 1, 5), 0,
                    new QuarterPoint(0, 1, 1)) }, Array.Empty<RouteSpan>());
            var forward = OneBitTopologyGraphBuilder.Build(Design(first, second));
            var reversed = OneBitTopologyGraphBuilder.Build(Design(second, first));
            Assert.That(forward.Nets.Count, Is.EqualTo(2));
            Assert.That(reversed.Nets.Count, Is.EqualTo(2));
            for (var i = 0; i < 2; i++)
                Assert.That(reversed.Nets[i].Members, Is.EqualTo(forward.Nets[i].Members));
        }

        private static RouteNode Node(int channel, QuarterPoint point) =>
            new RouteNode(Guid.NewGuid(), Cell, channel, point);

        private static ConnectorRoute Route(params RouteNode[] nodes)
        {
            var spans = new List<RouteSpan>();
            for (var i = 1; i < nodes.Length; i++)
                spans.Add(new RouteSpan(Guid.NewGuid(), nodes[i - 1].Id, nodes[i].Id));
            return new ConnectorRoute(Guid.NewGuid(), "wire", 1, nodes, spans);
        }

        private static ElectricalJoin Join(params JoinMember[] members) =>
            new ElectricalJoin(Guid.NewGuid(), members);

        private static OneBitAuthoredTopology Design(params ConnectorRoute[] routes) =>
            Design(routes, Array.Empty<ElectricalJoin>());

        private static OneBitAuthoredTopology Design(ConnectorRoute[] routes, ElectricalJoin[] joins) =>
            new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(), routes, joins);
    }
}
