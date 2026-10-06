using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldV1ArchiveCodecTests
    {
        [Test]
        public void EmptyWorldArchiveHasExactNamedEntriesAndReopensAtAuthoredState()
        {
            var snapshot = Snapshot(null);
            var archive = WorldV1ArchiveCodec.Write(snapshot);
            using (var input = new MemoryStream(archive))
            {
                var records = WorldArchiveContainer.Read(input);
                Assert.That(records.Modules.Count, Is.Zero);
                var manifest = WorldManifestJsonReader.Read(records.ManifestJson);
                Assert.That(manifest.Entries.Count, Is.EqualTo(1));
                Assert.That(manifest.Entries[0].Path, Is.EqualTo("world.json"));
                Assert.That(manifest.Entries[0].UncompressedBytes,
                    Is.EqualTo(records.WorldJson.LongLength));
                Assert.That(manifest.Entries[0].Sha256,
                    Is.EqualTo(WorldManifestIntegrity.Sha256Hex(records.WorldJson)));
            }
            using (var input = new MemoryStream(archive))
            {
                var loaded = WorldV1ArchiveCodec.Read(input);
                Assert.That(loaded.Snapshot.WorldId, Is.EqualTo(snapshot.WorldId));
                Assert.That(loaded.Snapshot.Design.Bounds.WidthCells, Is.EqualTo(8));
                Assert.That(loaded.UnavailableModuleVersionIds, Is.Empty);
            }
        }

        [Test]
        public void CorruptEmbeddedVersionUsesOnlyItsExactHashMatchingCopy()
        {
            var version = EmptyVersion();
            var snapshot = Snapshot(version);
            var expectedInstanceId = snapshot.Design.Modules[0].InstanceId;
            var archive = WorldV1ArchiveCodec.Write(snapshot);
            WorldArchiveRecords records;
            using (var input = new MemoryStream(archive))
                records = WorldArchiveContainer.Read(input);
            var exact = records.Modules[version.VersionId];
            var damaged = new Dictionary<Guid, byte[]>
            { [version.VersionId] = Encoding.UTF8.GetBytes("{damaged") };
            using (var output = new MemoryStream())
            {
                WorldArchiveContainer.Write(output, new WorldArchiveRecords(
                    records.ManifestJson, records.WorldJson, damaged));
                output.Position = 0;
                var placeholder = WorldV1ArchiveCodec.Read(output);
                Assert.That(placeholder.UnavailableModuleVersionIds,
                    Is.EquivalentTo(new[] { version.VersionId }));
                Assert.That(placeholder.Snapshot.Design.Modules.Count, Is.EqualTo(1));
                Assert.That(placeholder.Snapshot.Design.Modules[0].InstanceId,
                    Is.EqualTo(expectedInstanceId));
                output.Position = 0;
                var recovered = WorldV1ArchiveCodec.Read(output,
                    new Dictionary<Guid, byte[]> { [version.VersionId] = exact });
                Assert.That(recovered.UnavailableModuleVersionIds, Is.Empty);
                Assert.That(recovered.Snapshot.ModuleVersions[version.VersionId]
                    .VersionId, Is.EqualTo(version.VersionId));
                output.Position = 0;
                var wrong = WorldV1ArchiveCodec.Read(output,
                    new Dictionary<Guid, byte[]>
                    { [version.VersionId] = Encoding.UTF8.GetBytes("{}") });
                Assert.That(wrong.UnavailableModuleVersionIds,
                    Is.EquivalentTo(new[] { version.VersionId }));
            }
        }

        [Test]
        public void CompactExteriorRoundTripsWithoutReplacingCapturedBounds()
        {
            var version = new OneBitModuleVersion(Guid.NewGuid(),
                Guid.NewGuid(), "Compact", new GridCell(3, 1, 2),
                new GridCell(1, 1, 1),
                Array.Empty<PlacedOneBitComponent>(), EmptyTopology(),
                Array.Empty<OneBitModulePort>());
            var moduleJson = WorldV1JsonWriter.WriteModule(version);
            var record = Encoding.UTF8.GetString(moduleJson);
            Assert.That(record, Does.Contain("\"sizeCells\":[3,1,2]"));
            Assert.That(record,
                Does.Contain("\"exteriorSizeCells\":[1,1,1]"));
            var archive = WorldV1ArchiveCodec.Write(Snapshot(version));
            using (var input = new MemoryStream(archive))
            {
                var loaded = WorldV1ArchiveCodec.Read(input).Snapshot;
                Assert.That(loaded.ModuleVersions[version.VersionId].SizeCells,
                    Is.EqualTo(new GridCell(3, 1, 2)));
                Assert.That(loaded.ModuleVersions[version.VersionId]
                    .ExteriorSizeCells, Is.EqualTo(new GridCell(1, 1, 1)));
                Assert.That(loaded.Design.Modules[0].SizeCells,
                    Is.EqualTo(new GridCell(1, 1, 1)));
            }
            var missingExterior = record.Replace(
                "\"exteriorSizeCells\":[1,1,1],", "");
            Assert.That(missingExterior, Is.Not.EqualTo(record));
            Assert.Throws<InvalidDataException>(() => WorldV1JsonReader.ReadModule(
                Encoding.UTF8.GetBytes(missingExterior)));
        }

        [Test]
        public void DamagedWorldBytesCannotBecomeAPlaceholderWorld()
        {
            var original = WorldV1ArchiveCodec.Write(Snapshot(null));
            WorldArchiveRecords records;
            using (var input = new MemoryStream(original))
                records = WorldArchiveContainer.Read(input);
            using (var output = new MemoryStream())
            {
                WorldArchiveContainer.Write(output, new WorldArchiveRecords(
                    records.ManifestJson, Encoding.UTF8.GetBytes("{}"),
                    new Dictionary<Guid, byte[]>()));
                output.Position = 0;
                Assert.Throws<InvalidDataException>(() =>
                    WorldV1ArchiveCodec.Read(output));
            }
        }

        private static WorldSaveSnapshot Snapshot(OneBitModuleVersion version)
        {
            var modules = new List<PlacedOneBitModuleInstance>();
            var versions = new Dictionary<Guid, OneBitModuleVersion>();
            if (version != null)
            {
                modules.Add(new PlacedOneBitModuleInstance(Guid.NewGuid(),
                    Guid.NewGuid(), version.FamilyId, version.VersionId,
                    "Placed", new GridCell(2, 1, 2), GridOrientation.Default,
                    version.ExteriorSizeCells,
                    Array.Empty<OneBitPortInterface>()));
                versions.Add(version.VersionId, version);
            }
            var design = new OneBitWorldDesign(new WorldBounds(8, 8, 4),
                Array.Empty<PlacedOneBitComponent>(), EmptyTopology(), modules);
            return new WorldSaveSnapshot(Guid.NewGuid(), "Archive fixture",
                "builtin.smooth_sandstone", "builtin.wood_sandbox_wall",
                "10", new SavedPlayerPose(2, 2, 2, 0, 0, 1),
                new SavedInventoryItem[36], 0, design, versions);
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
