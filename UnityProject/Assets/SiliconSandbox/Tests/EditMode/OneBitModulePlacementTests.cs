using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitModulePlacementTests
    {
        [Test]
        public void TwoPlacementsShareFixedPortIdsButHaveSeparateInstanceAndWorldObjectIds()
        {
            var version = SrVersion();
            var world = OneBitWorldDesign.Empty(new WorldBounds(12, 12, 5));
            world = OneBitWorldEdits.PlaceModule(world, version, "A",
                new GridCell(3, 1, 3), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceModule(world, version, "B",
                new GridCell(7, 1, 3), GridOrientation.Default.ClockwiseYaw());
            var first = world.Modules[0];
            var second = world.Modules[1];
            var graph = OneBitTopologyGraphBuilder.Build(world.Topology);

            Assert.That(first.VersionId, Is.EqualTo(version.VersionId));
            Assert.That(second.VersionId, Is.EqualTo(version.VersionId));
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(first.InterfacePorts[0].Id,
                Is.EqualTo(second.InterfacePorts[0].Id));
            Assert.That(world.Topology.ModulePorts.Count, Is.EqualTo(8));
            Assert.That(graph.Connected(
                JoinMember.ModulePortBit(first.Id, version.Ports[0].Id, 0),
                JoinMember.ModulePortBit(second.Id, version.Ports[0].Id, 0)),
                Is.False);
            Assert.That(second.BuildPortBits()[0].PointQ,
                Is.EqualTo(new QuarterPoint(1, 1, 4)));
            Assert.That(world.Components.Count, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateNameAndOccupiedFootprintRejectWithoutChangingWorld()
        {
            var version = SrVersion();
            var empty = OneBitWorldDesign.Empty(new WorldBounds(8, 8, 4));
            var first = OneBitWorldEdits.PlaceModule(empty, version, "A",
                new GridCell(3, 1, 3), GridOrientation.Default);
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceModule(
                first, version, "A", new GridCell(6, 1, 3),
                GridOrientation.Default));
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceModule(
                first, version, "B", new GridCell(3, 1, 3),
                GridOrientation.Default));
            Assert.That(first.Modules.Count, Is.EqualTo(1));
            Assert.That(empty.Modules.Count, Is.EqualTo(0));
        }

        private static OneBitModuleVersion SrVersion()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var sr = snapshot.Components[0];
            return OneBitModuleVersionFactory.Create(snapshot, Guid.NewGuid(), "SR",
                new[]
                {
                    Port("S", OneBitPortDirection.Input, sr,
                        new QuarterPoint(0, 1, 1)),
                    Port("R", OneBitPortDirection.Input, sr,
                        new QuarterPoint(0, 3, 1)),
                    Port("CLK", OneBitPortDirection.Input, sr,
                        new QuarterPoint(1, 1, 0)),
                    Port("Q", OneBitPortDirection.Output, sr,
                        new QuarterPoint(4, 1, 1))
                });
        }

        private static OneBitPortChoice Port(string name,
            OneBitPortDirection direction, PlacedOneBitComponent sr,
            QuarterPoint point) => new OneBitPortChoice(name, direction,
                new GridCell(0, 0, 0), point,
                JoinMember.ComponentPin(sr.Id, sr.PinIds[name]));
    }
}
