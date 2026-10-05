using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Persistence;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitSaveBoundaryTests
    {
        [Test]
        public void SnapshotRejectsNonfiniteViewAndInvalidInventoryOrClockRange()
        {
            Assert.Throws<ArgumentException>(() => new SavedPlayerPose(
                0, 0, 0, double.NaN, 0, 1));
            Assert.Throws<ArgumentException>(() => new SavedPlayerPose(
                0, 0, 0, 1, 0, 1));
            var empty = OneBitWorldDesign.Empty(new WorldBounds(8, 8, 4));
            var player = new SavedPlayerPose(0, 2, 0, 0, 0, 1);
            var versions = new System.Collections.Generic.Dictionary<Guid,
                OneBitModuleVersion>();
            Assert.Throws<ArgumentException>(() => new WorldSaveSnapshot(
                Guid.NewGuid(), "Demo", "builtin.smooth_sandstone",
                "builtin.sandbox_wall", "101", player,
                new SavedInventoryItem[36], 0, empty, versions));
            Assert.Throws<ArgumentException>(() => new WorldSaveSnapshot(
                Guid.NewGuid(), "Demo", "builtin.smooth_sandstone",
                "builtin.sandbox_wall", "10", player,
                new SavedInventoryItem[35], 0, empty, versions));
        }

        [Test]
        public void InMemoryReopenKeepsAuthoredDataAndRestartsOrdinarySimulation()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(16, 16, 5)), "10");
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(5, 1, 5), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(3, 1, 5), GridOrientation.Default,
                LogicBit.One, false);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(3, 1, 7), GridOrientation.Default,
                LogicBit.One, false);
            var sr = session.Design.Components[0];
            var s = session.Design.Components[1];
            var r = session.Design.Components[2];
            session.ConnectPins(JoinMember.ComponentPin(s.Id, s.PinIds["OUT"]),
                JoinMember.ComponentPin(sr.Id, sr.PinIds["S"]));
            session.ConnectPins(JoinMember.ComponentPin(r.Id, r.PinIds["OUT"]),
                JoinMember.ComponentPin(sr.Id, sr.PinIds["R"]));
            session.AttachWorldClockPin(sr.Id);
            session.ToggleSource(s.Id);
            session.Scheduler.StepClockEdge();
            Assert.That(session.Circuit.Storage(sr.Id).Q, Is.EqualTo(LogicBit.One));
            Assert.That(session.Circuit.Source(s.Id).IsOn, Is.True);

            var version = SrVersion();
            session.PlaceModule(version, "A", new GridCell(10, 1, 5),
                GridOrientation.Default);
            var instanceId = session.Design.Modules[0].InstanceId;
            var slots = new SavedInventoryItem[36];
            slots[0] = SavedInventoryItem.Catalog(BuiltInPinCatalog.Source);
            slots[5] = SavedInventoryItem.Module(version.FamilyId,
                version.VersionId);
            var worldId = Guid.NewGuid();
            var snapshot = OneBitSaveBoundary.Capture(session, worldId,
                "Professor demo", "builtin.smooth_sandstone",
                "builtin.sandbox_wall", new SavedPlayerPose(4, 2, 4, 0, 0, 1),
                slots, 5);
            var reopened = OneBitSaveBoundary.Open(snapshot);

            Assert.That(snapshot.WorldId, Is.EqualTo(worldId));
            Assert.That(snapshot.Design, Is.SameAs(session.Design));
            Assert.That(snapshot.InventorySlots.Count, Is.EqualTo(36));
            Assert.That(snapshot.SelectedHotbarSlot, Is.EqualTo(5));
            Assert.That(snapshot.ModuleVersions.ContainsKey(version.VersionId), Is.True);
            Assert.That(reopened.Design.Topology.Connectors.Count,
                Is.EqualTo(session.Design.Topology.Connectors.Count));
            Assert.That(reopened.Design.Modules[0].InstanceId,
                Is.EqualTo(instanceId));
            Assert.That(reopened.Circuit.Source(s.Id).IsOn, Is.False);
            Assert.That(reopened.Circuit.Storage(sr.Id).Q, Is.EqualTo(LogicBit.X));
            Assert.That(reopened.Scheduler.Now, Is.EqualTo(SimulationTime.Zero));
            Assert.That(reopened.Scheduler.ClockRunning, Is.False);
            Assert.That(reopened.Scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
            Assert.That(reopened.Scheduler.FrequencyHz, Is.EqualTo("10"));
        }

        private static OneBitModuleVersion SrVersion()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var choices = OneBitPackageDefaults.ForSingleCell(snapshot);
            return OneBitModuleVersionFactory.Create(snapshot, Guid.NewGuid(), "SR",
                choices);
        }
    }
}
