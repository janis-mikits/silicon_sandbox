using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitTopologyEditsTests
    {
        [Test]
        public void BreakingBoundarySpanRetiresRouteAndPreservesSurvivingPartsAndUnrelatedNet()
        {
            var west = new RouteNode(Guid.NewGuid(), new GridCell(1, 1, 1), 0,
                new QuarterPoint(0, 1, 1));
            var westEnd = new RouteNode(Guid.NewGuid(), new GridCell(1, 1, 1), 0,
                new QuarterPoint(4, 1, 1));
            var eastStart = new RouteNode(Guid.NewGuid(), new GridCell(2, 1, 1), 0,
                new QuarterPoint(0, 1, 1));
            var east = new RouteNode(Guid.NewGuid(), new GridCell(2, 1, 1), 0,
                new QuarterPoint(4, 1, 1));
            var firstSpan = new RouteSpan(Guid.NewGuid(), west.Id, westEnd.Id);
            var brokenSpan = new RouteSpan(Guid.NewGuid(), westEnd.Id, eastStart.Id);
            var lastSpan = new RouteSpan(Guid.NewGuid(), eastStart.Id, east.Id);
            var originalRoute = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { west, westEnd, eastStart, east },
                new[] { firstSpan, brokenSpan, lastSpan }, "DATA");
            var unrelated = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { new RouteNode(Guid.NewGuid(), new GridCell(8, 1, 1), 0,
                    new QuarterPoint(0, 1, 1)) }, Array.Empty<RouteSpan>());
            var sourcePin = new AuthoredPin(Guid.NewGuid(), Guid.NewGuid(),
                new GridCell(0, 1, 1), new QuarterPoint(4, 1, 1));
            var pinRef = JoinMember.ComponentPin(sourcePin.ObjectId, sourcePin.PinId);
            var attachment = new ElectricalJoin(Guid.NewGuid(), new[]
            {
                pinRef, JoinMember.ConnectorNode(originalRoute.Id, west.Id)
            });
            var original = new OneBitAuthoredTopology(new[] { sourcePin },
                new[] { originalRoute, unrelated }, new[] { attachment });

            var result = OneBitTopologyEdits.BreakSpan(original, originalRoute.Id, brokenSpan.Id);
            Assert.That(result.RetiredConnectorId, Is.EqualTo(originalRoute.Id));
            Assert.That(result.ReplacementConnectorIds.Count, Is.EqualTo(2));
            Assert.That(result.ReplacementConnectorIds[0], Is.Not.EqualTo(originalRoute.Id));
            Assert.That(result.ReplacementConnectorIds[1], Is.Not.EqualTo(originalRoute.Id));
            Assert.That(result.Design.Connectors.Count, Is.EqualTo(3));
            Assert.That(result.Design.Connectors[0].Nodes[0].Id, Is.EqualTo(west.Id));
            Assert.That(result.Design.Connectors[0].Spans[0].Id, Is.EqualTo(firstSpan.Id));
            Assert.That(result.Design.Connectors[1].Nodes[0].Id, Is.EqualTo(eastStart.Id));
            Assert.That(result.Design.Connectors[1].Spans[0].Id, Is.EqualTo(lastSpan.Id));
            Assert.That(result.Design.Connectors[2].Id, Is.EqualTo(unrelated.Id));
            Assert.That(result.Design.Joins[0].Id, Is.EqualTo(attachment.Id));
            Assert.That(result.Graph.Connected(pinRef, JoinMember.ConnectorNode(
                result.ReplacementConnectorIds[0], west.Id)), Is.True);
            Assert.That(result.Graph.Connected(pinRef, JoinMember.ConnectorNode(
                result.ReplacementConnectorIds[1], east.Id)), Is.False);
            Assert.That(original.Connectors[0].Id, Is.EqualTo(originalRoute.Id),
                "The rejected/old revision remains available for undo.");
            Assert.That(original.Connectors[0].Spans.Count, Is.EqualTo(3));
        }

        [Test]
        public void ConflictingTagJoinNeedsChoiceAndPublishesOneCoherentRevision()
        {
            var cell = new GridCell(5, 1, 5);
            var point = new QuarterPoint(2, 2, 2);
            var aNode = new RouteNode(Guid.NewGuid(), cell, 0, point);
            var bNode = new RouteNode(Guid.NewGuid(), cell, 1, point);
            var a = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { aNode }, Array.Empty<RouteSpan>(), "A");
            var b = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { bNode }, Array.Empty<RouteSpan>(), "B");
            var original = new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                new[] { a, b }, Array.Empty<ElectricalJoin>());
            var join = new ElectricalJoin(Guid.NewGuid(), new[]
            {
                JoinMember.ConnectorNode(a.Id, aNode.Id),
                JoinMember.ConnectorNode(b.Id, bNode.Id)
            });

            Assert.Throws<ArgumentException>(() => OneBitTopologyEdits.AddJoin(original, join));
            Assert.That(original.Joins.Count, Is.Zero);
            Assert.That(original.Connectors[0].Tag, Is.EqualTo("A"));
            Assert.That(original.Connectors[1].Tag, Is.EqualTo("B"));

            var result = OneBitTopologyEdits.AddJoin(original, join, "A");
            Assert.That(result.Design.Joins.Count, Is.EqualTo(1));
            Assert.That(result.Design.Connectors[0].Tag, Is.EqualTo("A"));
            Assert.That(result.Design.Connectors[1].Tag, Is.EqualTo("A"));
            Assert.That(result.Graph.Nets.Count, Is.EqualTo(1));
        }

        [Test]
        public void InCellBreakRechannelsSurvivingPieceWithoutChangingNodeIdentity()
        {
            var cell = new GridCell(5, 1, 5);
            var west = new RouteNode(Guid.NewGuid(), cell, 0, new QuarterPoint(0, 1, 1));
            var center = new RouteNode(Guid.NewGuid(), cell, 0, new QuarterPoint(2, 2, 2));
            var east = new RouteNode(Guid.NewGuid(), cell, 0, new QuarterPoint(4, 1, 1));
            var left = new RouteSpan(Guid.NewGuid(), west.Id, center.Id);
            var right = new RouteSpan(Guid.NewGuid(), center.Id, east.Id);
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { west, center, east }, new[] { left, right });
            var original = new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                new[] { route }, Array.Empty<ElectricalJoin>());

            var result = OneBitTopologyEdits.BreakSpan(original, route.Id, right.Id);
            Assert.That(result.RetiredConnectorId, Is.EqualTo(route.Id));
            Assert.That(result.Design.Connectors[0].Nodes[0].Channel, Is.EqualTo(0));
            Assert.That(result.Design.Connectors[1].Nodes[0].Id, Is.EqualTo(east.Id));
            Assert.That(result.Design.Connectors[1].Nodes[0].Channel, Is.EqualTo(1));
            Assert.That(result.Graph.Nets.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidJoinCannotPartiallyPublish()
        {
            var aNode = new RouteNode(Guid.NewGuid(), new GridCell(1, 1, 1), 0,
                new QuarterPoint(2, 2, 2));
            var bNode = new RouteNode(Guid.NewGuid(), new GridCell(3, 1, 1), 0,
                new QuarterPoint(2, 2, 2));
            var a = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { aNode }, Array.Empty<RouteSpan>());
            var b = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { bNode }, Array.Empty<RouteSpan>());
            var original = new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                new[] { a, b }, Array.Empty<ElectricalJoin>());
            var join = new ElectricalJoin(Guid.NewGuid(), new[]
            {
                JoinMember.ConnectorNode(a.Id, aNode.Id),
                JoinMember.ConnectorNode(b.Id, bNode.Id)
            });
            Assert.Throws<ArgumentException>(() => OneBitTopologyEdits.AddJoin(original, join));
            Assert.That(original.Joins.Count, Is.Zero);
            Assert.That(OneBitTopologyGraphBuilder.Build(original).Nets.Count, Is.EqualTo(2));
        }
    }
}
