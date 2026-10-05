using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitRotationTests
    {
        [Test]
        public void PreviewLeavesWorldUntouchedAndConfirmedTurnKeepsOpenWireEnd()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(10, 10, 4)), BuiltInPinCatalog.Source,
                new GridCell(3, 1, 3), GridOrientation.Default);
            var source = world.Components[0];
            var endpoint = new RouteNode(Guid.NewGuid(), source.AnchorCell, 0,
                new QuarterPoint(4, 1, 1));
            var wire = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { endpoint }, Array.Empty<RouteSpan>());
            var join = new ElectricalJoin(Guid.NewGuid(), new[]
            {
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                JoinMember.ConnectorNode(wire.Id, endpoint.Id)
            });
            world = OneBitWorldEdits.PlaceConnector(world, wire,
                new[] { join });
            var session = new OneBitWorldSession(world);

            var preview = session.PreviewRotation(source.Id,
                GridOrientation.Default.ClockwiseYaw());
            Assert.That(preview.LostJoinIds, Is.EquivalentTo(new[] { join.Id }));
            Assert.That(session.Design, Is.SameAs(world));
            Assert.That(session.Design.Topology.Joins.Count, Is.EqualTo(1));

            session.ConfirmRotation(preview);
            var turned = session.Design.Components[0];
            Assert.That(turned.Id, Is.EqualTo(source.Id));
            Assert.That(turned.PinIds["OUT"], Is.EqualTo(source.PinIds["OUT"]));
            Assert.That(turned.BuildPins()[0].PointQ,
                Is.EqualTo(new QuarterPoint(1, 1, 0)));
            Assert.That(session.Design.Topology.Joins.Count, Is.EqualTo(0));
            Assert.That(session.Design.Topology.Connectors[0], Is.SameAs(wire));
            Assert.That(session.Design.Topology.Connectors[0].Nodes[0].PointQ,
                Is.EqualTo(new QuarterPoint(4, 1, 1)));
            var graph = OneBitTopologyGraphBuilder.Build(session.Design.Topology);
            Assert.That(graph.Connected(
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                JoinMember.ConnectorNode(wire.Id, endpoint.Id)), Is.False);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(
                JoinMember.ConnectorNode(wire.Id, endpoint.Id))).Value,
                Is.EqualTo(LogicBit.Z));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]))).Value,
                Is.EqualTo(LogicBit.Zero));
        }

        [Test]
        public void TurnOfModuleKeepsPlacementAndInstanceIdentity()
        {
            var design = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(design,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var local = snapshot.Components[0];
            var version = OneBitModuleVersionFactory.Create(snapshot,
                Guid.NewGuid(), "Source module", new[]
                {
                    new OneBitPortChoice("OUT", OneBitPortDirection.Output,
                        new GridCell(0, 0, 0), new QuarterPoint(4, 1, 1),
                        JoinMember.ComponentPin(local.Id, local.PinIds["OUT"]))
                });
            var world = OneBitWorldEdits.PlaceModule(OneBitWorldDesign.Empty(
                new WorldBounds(10, 10, 4)), version, "M",
                new GridCell(4, 1, 4), GridOrientation.Default);
            var old = world.Modules[0];
            var preview = OneBitRotationEdits.Preview(world, old.Id,
                old.Orientation.ClockwiseYaw());
            var rotated = preview.Candidate.Modules[0];
            Assert.That(rotated.Id, Is.EqualTo(old.Id));
            Assert.That(rotated.InstanceId, Is.EqualTo(old.InstanceId));
            Assert.That(rotated.VersionId, Is.EqualTo(old.VersionId));
            Assert.That(rotated.InterfacePorts[0].Id,
                Is.EqualTo(old.InterfacePorts[0].Id));
            Assert.That(rotated.BuildPortBits()[0].PointQ,
                Is.EqualTo(new QuarterPoint(1, 1, 0)));
            Assert.That(world.Modules[0].Orientation,
                Is.EqualTo(GridOrientation.Default));
        }

        [Test]
        public void StalePreviewCannotOverwriteLaterAuthoredRevision()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(10, 10, 4)), BuiltInPinCatalog.Source,
                new GridCell(3, 1, 3), GridOrientation.Default);
            var session = new OneBitWorldSession(world);
            var preview = session.PreviewRotation(world.Components[0].Id,
                GridOrientation.Default.ClockwiseYaw());
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(7, 1, 7), GridOrientation.Default);
            Assert.Throws<InvalidOperationException>(() =>
                session.ConfirmRotation(preview));
            Assert.That(session.Design.Components.Count, Is.EqualTo(2));
            Assert.That(session.Design.Components[0].Orientation,
                Is.EqualTo(GridOrientation.Default));
        }
    }
}
