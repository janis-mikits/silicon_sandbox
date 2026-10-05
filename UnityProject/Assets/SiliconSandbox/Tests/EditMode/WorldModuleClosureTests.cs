using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldModuleClosureTests
    {
        [Test]
        public void OnlyPlacedAndInventoryExactVersionsEnterWorldArchive()
        {
            var placed = EmptyVersion();
            var inventoried = EmptyVersion();
            var unused = EmptyVersion();
            var snapshot = Snapshot(placed, inventoried, unused,
                SavedInventoryItem.Module(inventoried.FamilyId,
                    inventoried.VersionId));

            var closure = WorldModuleClosure.RequiredFor(snapshot);
            Assert.That(closure.Count, Is.EqualTo(2));
            Assert.That(closure, Does.Contain(placed));
            Assert.That(closure, Does.Contain(inventoried));
            Assert.That(closure[0], Is.Not.SameAs(unused));
            Assert.That(closure[1], Is.Not.SameAs(unused),
                "A globally known but unreferenced version is not embedded.");
            Assert.That(closure[0].VersionId.CompareTo(closure[1].VersionId),
                Is.LessThan(0), "Archive ordering is deterministic.");
        }

        [Test]
        public void MissingExactVersionOrFamilyMismatchCannotBeSavedAsAnotherVersion()
        {
            var placed = EmptyVersion();
            var other = EmptyVersion();
            var missing = Snapshot(placed, other, null, null,
                includePlacedVersion: false);
            Assert.Throws<InvalidDataException>(() =>
                WorldModuleClosure.RequiredFor(missing));

            var mismatched = Snapshot(placed, other, null,
                SavedInventoryItem.Module(Guid.NewGuid(), other.VersionId));
            Assert.Throws<InvalidDataException>(() =>
                WorldModuleClosure.RequiredFor(mismatched));
        }

        private static WorldSaveSnapshot Snapshot(OneBitModuleVersion placed,
            OneBitModuleVersion other, OneBitModuleVersion unused,
            SavedInventoryItem inventoryItem, bool includePlacedVersion = true)
        {
            var instance = new PlacedOneBitModuleInstance(Guid.NewGuid(),
                Guid.NewGuid(), placed.FamilyId, placed.VersionId, "Placed",
                new GridCell(2, 1, 2), GridOrientation.Default,
                new GridCell(1, 1, 1), Array.Empty<OneBitPortInterface>());
            var design = new OneBitWorldDesign(new WorldBounds(8, 8, 4),
                Array.Empty<PlacedOneBitComponent>(), EmptyTopology(),
                new[] { instance });
            var versions = new Dictionary<Guid, OneBitModuleVersion>();
            if (includePlacedVersion) versions.Add(placed.VersionId, placed);
            versions.Add(other.VersionId, other);
            if (unused != null) versions.Add(unused.VersionId, unused);
            var slots = new SavedInventoryItem[36];
            slots[0] = inventoryItem;
            return new WorldSaveSnapshot(Guid.NewGuid(), "Closure fixture",
                "builtin.smooth_sandstone", "builtin.wood_sandbox_wall",
                "10", new SavedPlayerPose(2, 2, 2, 0, 0, 1),
                slots, 0, design, versions);
        }

        private static OneBitModuleVersion EmptyVersion() =>
            new OneBitModuleVersion(Guid.NewGuid(), Guid.NewGuid(), "Empty",
                new GridCell(1, 1, 1),
                Array.Empty<PlacedOneBitComponent>(), EmptyTopology(),
                Array.Empty<OneBitModulePort>());

        private static OneBitAuthoredTopology EmptyTopology() =>
            new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                Array.Empty<ConnectorRoute>(), Array.Empty<ElectricalJoin>());
    }
}
