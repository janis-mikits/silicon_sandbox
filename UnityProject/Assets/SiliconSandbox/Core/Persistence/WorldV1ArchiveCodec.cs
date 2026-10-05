using System;
using System.Collections.Generic;
using System.IO;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Persistence
{
    public sealed class LoadedWorldV1Archive
    {
        public WorldSaveSnapshot Snapshot { get; }
        public WorldManifestRecord Manifest { get; }
        public IReadOnlyList<Guid> UnavailableModuleVersionIds { get; }

        internal LoadedWorldV1Archive(WorldSaveSnapshot snapshot,
            WorldManifestRecord manifest, List<Guid> unavailable)
        {
            Snapshot = snapshot;
            Manifest = manifest;
            UnavailableModuleVersionIds = unavailable.AsReadOnly();
        }
    }

    // Composes the accepted ZIP envelope, byte hashes, strict V1 records, and
    // exact-version closure. It does not persist or restore ordinary runtime.
    public static class WorldV1ArchiveCodec
    {
        public static byte[] Write(WorldSaveSnapshot snapshot,
            WorldArchiveLimits limits = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var required = WorldModuleClosure.RequiredFor(snapshot);
            var payloads = new List<ModuleArchivePayload>();
            var entries = new Dictionary<Guid, byte[]>();
            foreach (var version in required)
            {
                var bytes = WorldV1JsonWriter.WriteModule(version);
                payloads.Add(new ModuleArchivePayload(version.FamilyId,
                    version.VersionId, version.ChildVersionIds, bytes));
                entries.Add(version.VersionId, bytes);
            }
            var world = WorldV1JsonWriter.WriteWorld(snapshot);
            var manifest = WorldManifestIntegrity.Build(snapshot.WorldId,
                world, payloads);
            var records = new WorldArchiveRecords(
                WorldManifestJsonWriter.Write(manifest), world, entries);
            using (var output = new MemoryStream())
            {
                WorldArchiveContainer.Write(output, records, limits);
                return output.ToArray();
            }
        }

        public static LoadedWorldV1Archive Read(Stream stream,
            IReadOnlyDictionary<Guid, byte[]> exactLibraryCopies = null,
            WorldArchiveLimits limits = null)
        {
            var records = WorldArchiveContainer.Read(stream, limits);
            var manifest = WorldManifestJsonReader.Read(records.ManifestJson);
            var assessment = WorldManifestIntegrity.Assess(manifest, records);
            var damaged = new HashSet<Guid>(assessment.DamagedModuleVersionIds);
            var versions = new Dictionary<Guid, OneBitModuleVersion>();
            var unavailable = new List<Guid>();
            foreach (var versionRecord in manifest.ModuleVersions)
            {
                var id = versionRecord.VersionId;
                byte[] definition = null;
                if (!damaged.Contains(id)) definition = records.Modules[id];
                else if (exactLibraryCopies != null &&
                    exactLibraryCopies.TryGetValue(id, out var candidate) &&
                    candidate != null && MatchesManifestBytes(manifest,
                        versionRecord.Path, candidate))
                    definition = candidate;
                if (definition == null)
                {
                    unavailable.Add(id);
                    continue;
                }
                OneBitModuleVersion decoded;
                try { decoded = WorldV1JsonReader.ReadModule(definition); }
                catch (Exception error) when (error is InvalidDataException ||
                    error is ArgumentException || error is InvalidCastException ||
                    error is OverflowException)
                {
                    unavailable.Add(id);
                    continue;
                }
                if (decoded.FamilyId != versionRecord.FamilyId ||
                    decoded.VersionId != id ||
                    !SameChildren(decoded.ChildVersionIds,
                        versionRecord.ChildVersionIds))
                {
                    unavailable.Add(id);
                    continue;
                }
                versions.Add(id, decoded);
            }
            WorldSaveSnapshot snapshot;
            try { snapshot = WorldV1JsonReader.ReadWorld(records.WorldJson, versions); }
            catch (Exception error) when (error is ArgumentException ||
                error is InvalidCastException || error is OverflowException)
            { throw new InvalidDataException("World authored record is invalid.", error); }
            if (snapshot.WorldId != manifest.WorldId)
                throw new InvalidDataException("World identity disagrees with manifest.");
            ValidateClosure(snapshot, manifest);
            return new LoadedWorldV1Archive(snapshot, manifest, unavailable);
        }

        private static bool MatchesManifestBytes(WorldManifestRecord manifest,
            string path, byte[] bytes)
        {
            foreach (var entry in manifest.Entries)
                if (entry.Path == path)
                    return entry.UncompressedBytes == bytes.LongLength &&
                        entry.Sha256 == WorldManifestIntegrity.Sha256Hex(bytes);
            return false;
        }

        private static bool SameChildren(IReadOnlyList<Guid> actual,
            IReadOnlyList<Guid> expected)
        {
            if (actual.Count != expected.Count) return false;
            for (var i = 0; i < actual.Count; i++)
                if (actual[i] != expected[i]) return false;
            return true;
        }

        private static void ValidateClosure(WorldSaveSnapshot world,
            WorldManifestRecord manifest)
        {
            var listed = new Dictionary<Guid, ManifestModuleVersionRecord>();
            foreach (var entry in manifest.ModuleVersions)
                listed.Add(entry.VersionId, entry);
            var reached = new HashSet<Guid>();
            foreach (var module in world.Design.Modules)
                Visit(module.VersionId, module.FamilyId);
            foreach (var slot in world.InventorySlots)
                if (slot != null && slot.Kind == SavedInventoryKind.ModuleVersion)
                    Visit(slot.VersionId, slot.FamilyId);
            if (reached.Count != listed.Count)
                throw new InvalidDataException("Archive embeds an unreferenced module version.");

            void Visit(Guid id, Guid familyId)
            {
                if (!listed.TryGetValue(id, out var version) ||
                    version.FamilyId != familyId)
                    throw new InvalidDataException(
                        "World module reference disagrees with manifest.");
                if (!reached.Add(id)) return;
                foreach (var child in version.ChildVersionIds)
                {
                    if (!listed.TryGetValue(child, out var childVersion))
                        throw new InvalidDataException("Missing exact child version.");
                    Visit(child, childVersion.FamilyId);
                }
            }
        }
    }
}
