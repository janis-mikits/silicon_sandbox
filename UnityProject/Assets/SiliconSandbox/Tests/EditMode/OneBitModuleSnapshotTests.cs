using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitModuleSnapshotTests
    {
        [Test]
        public void CapturedSrAndClockStubHaveFreshLocalIdentitiesAndLeaveSourceUntouched()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(5, 3, 3)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 0), GridOrientation.Default);
            var sourceComponent = world.Components[0];
            world = OneBitWorldEdits.AttachWorldClockPin(world, sourceComponent.Id);
            var sourceStub = world.Topology.Connectors[0];

            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 0), new GridCell(2, 1, 0)));
            var copied = snapshot.Components[0];
            var copiedStub = snapshot.Topology.Connectors[0];
            var graph = OneBitTopologyGraphBuilder.Build(snapshot.Topology);

            Assert.That(snapshot.SizeCells, Is.EqualTo(new GridCell(1, 1, 1)));
            Assert.That(copied.AnchorCell, Is.EqualTo(new GridCell(0, 0, 0)));
            Assert.That(copied.Id, Is.Not.EqualTo(sourceComponent.Id));
            Assert.That(copied.PinIds["CLK"], Is.Not.EqualTo(sourceComponent.PinIds["CLK"]));
            Assert.That(copiedStub.Id, Is.Not.EqualTo(sourceStub.Id));
            Assert.That(copiedStub.Nodes[0].Id, Is.Not.EqualTo(sourceStub.Nodes[0].Id));
            Assert.That(copiedStub.Kind, Is.EqualTo("netLink"));
            Assert.That(graph.Connected(
                JoinMember.ComponentPin(copied.Id, copied.PinIds["CLK"]),
                JoinMember.ConnectorNode(copiedStub.Id, copiedStub.Nodes[0].Id)), Is.True);
            Assert.That(world.Components[0].Id, Is.EqualTo(sourceComponent.Id));
            Assert.That(world.Topology.Connectors[0].Id, Is.EqualTo(sourceStub.Id));
            Assert.That(snapshot.PortCandidates.Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void UnconnectedRouteInsideSelectionIsNotCaptured()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var orphan = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { new RouteNode(Guid.NewGuid(), new GridCell(3, 1, 3), 0,
                    new QuarterPoint(2, 2, 2)) }, Array.Empty<RouteSpan>());
            world = OneBitWorldEdits.PlaceConnector(world, orphan,
                Array.Empty<ElectricalJoin>());

            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(3, 1, 3)));
            Assert.That(snapshot.Components.Count, Is.EqualTo(1));
            Assert.That(snapshot.Topology.Connectors.Count, Is.EqualTo(0),
                "A route with no internal component connection is excluded.");
            Assert.That(world.Topology.Connectors.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectedBoundaryCrossingIsExcluded()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var inside = new RouteNode(Guid.NewGuid(), new GridCell(3, 1, 2), 0,
                new QuarterPoint(4, 1, 1));
            var outside = new RouteNode(Guid.NewGuid(), new GridCell(4, 1, 2), 0,
                new QuarterPoint(0, 1, 1));
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { inside, outside },
                new[] { new RouteSpan(Guid.NewGuid(), inside.Id, outside.Id) });
            world = OneBitWorldEdits.PlaceConnector(world, route,
                Array.Empty<ElectricalJoin>());
            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2),
                    new GridCell(3, 1, 2)));
            Assert.That(snapshot.Topology.Connectors.Count, Is.EqualTo(0));
            Assert.That(world.Topology.Connectors.Count, Is.EqualTo(1));
        }

        [Test]
        public void CrossingConnectedRouteTerminatesAtExactInsideBoundaryFace()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var source = world.Components[0];
            var a = new RouteNode(Guid.NewGuid(), new GridCell(2, 1, 2), 0,
                new QuarterPoint(4, 1, 1));
            var b = new RouteNode(Guid.NewGuid(), new GridCell(3, 1, 2), 0,
                new QuarterPoint(0, 1, 1));
            var c = new RouteNode(Guid.NewGuid(), new GridCell(3, 1, 2), 0,
                new QuarterPoint(4, 1, 1));
            var d = new RouteNode(Guid.NewGuid(), new GridCell(4, 1, 2), 0,
                new QuarterPoint(0, 1, 1));
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { a, b, c, d }, new[]
                {
                    new RouteSpan(Guid.NewGuid(), a.Id, b.Id),
                    new RouteSpan(Guid.NewGuid(), b.Id, c.Id),
                    new RouteSpan(Guid.NewGuid(), c.Id, d.Id)
                });
            world = OneBitWorldEdits.PlaceConnector(world, route,
                new[] { new ElectricalJoin(Guid.NewGuid(), new[]
                {
                    JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                    JoinMember.ConnectorNode(route.Id, a.Id)
                }) });

            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(3, 1, 2)));
            var copied = snapshot.Topology.Connectors[0];
            Assert.That(copied.Nodes.Count, Is.EqualTo(3));
            Assert.That(copied.Spans.Count, Is.EqualTo(2));
            Assert.That(copied.Nodes[2].Cell, Is.EqualTo(new GridCell(1, 0, 0)));
            Assert.That(copied.Nodes[2].PointQ,
                Is.EqualTo(new QuarterPoint(4, 1, 1)));
            var expectedEndpoint = JoinMember.ConnectorNode(copied.Id,
                copied.Nodes[2].Id);
            var candidateFound = false;
            foreach (var candidate in snapshot.PortCandidates)
                if (candidate.InternalEndpoint.Equals(expectedEndpoint))
                    candidateFound = true;
            Assert.That(candidateFound, Is.True);
            Assert.That(world.Topology.Connectors[0].Nodes.Count, Is.EqualTo(4));
        }

        [Test]
        public void ReenteringRouteDoesNotJoinItsInsidePiecesThroughOmittedOutsidePath()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(9, 9, 4)), BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.And,
                new GridCell(5, 1, 2), GridOrientation.Default);
            var source = world.Components[0];
            var gate = world.Components[1];
            var cells = new[]
            {
                new GridCell(2,1,2), new GridCell(3,1,2), new GridCell(3,1,2),
                new GridCell(3,1,3), new GridCell(3,1,3), new GridCell(4,1,3),
                new GridCell(4,1,3), new GridCell(4,1,2), new GridCell(4,1,2),
                new GridCell(5,1,2)
            };
            var points = new[]
            {
                new QuarterPoint(4,1,1), new QuarterPoint(0,1,1),
                new QuarterPoint(1,1,4), new QuarterPoint(1,1,0),
                new QuarterPoint(4,1,1), new QuarterPoint(0,1,1),
                new QuarterPoint(1,1,0), new QuarterPoint(1,1,4),
                new QuarterPoint(4,1,1), new QuarterPoint(0,1,1)
            };
            var nodes = new RouteNode[cells.Length];
            var spans = new RouteSpan[cells.Length - 1];
            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new RouteNode(Guid.NewGuid(), cells[i], 0, points[i]);
                if (i > 0)
                    spans[i - 1] = new RouteSpan(Guid.NewGuid(),
                        nodes[i - 1].Id, nodes[i].Id);
            }
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1, nodes, spans);
            world = OneBitWorldEdits.PlaceConnector(world, route,
                new[]
                {
                    new ElectricalJoin(Guid.NewGuid(), new[]
                    {
                        JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                        JoinMember.ConnectorNode(route.Id, nodes[0].Id)
                    }),
                    new ElectricalJoin(Guid.NewGuid(), new[]
                    {
                        JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]),
                        JoinMember.ConnectorNode(route.Id, nodes[9].Id)
                    })
                });

            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(5, 1, 2)));
            Assert.That(snapshot.Topology.Connectors.Count, Is.EqualTo(2));
            var graph = OneBitTopologyGraphBuilder.Build(snapshot.Topology);
            var copiedSource = snapshot.Components[0];
            var copiedGate = snapshot.Components[1];
            Assert.That(graph.Connected(
                JoinMember.ComponentPin(copiedSource.Id, copiedSource.PinIds["OUT"]),
                JoinMember.ComponentPin(copiedGate.Id, copiedGate.PinIds["A"])),
                Is.False);
            Assert.That(world.Topology.Connectors.Count, Is.EqualTo(1));
        }
    }
}
