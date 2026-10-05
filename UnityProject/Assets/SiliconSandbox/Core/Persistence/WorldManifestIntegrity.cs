using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SiliconSandbox.Persistence
{
    public sealed class ManifestEntryRecord
    {
        public string Path { get; }
        public long UncompressedBytes { get; }
        public string Sha256 { get; }

        public ManifestEntryRecord(string path, long uncompressedBytes,
            string sha256)
        {
            Path = path;
            UncompressedBytes = uncompressedBytes;
            Sha256 = sha256;
        }
    }

    public sealed class ManifestModuleVersionRecord
    {
        public Guid FamilyId { get; }
        public Guid VersionId { get; }
        public string Path { get; }
        public IReadOnlyList<Guid> ChildVersionIds { get; }

        public ManifestModuleVersionRecord(Guid familyId, Guid versionId,
            IEnumerable<Guid> childVersionIds)
        {
            if (familyId == Guid.Empty || versionId == Guid.Empty ||
                childVersionIds == null)
                throw new ArgumentException("Invalid manifest module identity.");
            var children = new List<Guid>(childVersionIds);
            for (var i = 0; i < children.Count; i++)
                if (children[i] == Guid.Empty ||
                    i > 0 && children[i - 1].CompareTo(children[i]) >= 0)
                    throw new ArgumentException(
                        "Child versions must be sorted, unique, and nonempty.");
            FamilyId = familyId;
            VersionId = versionId;
            Path = "modules/" + versionId.ToString("D") + ".json";
            ChildVersionIds = children.AsReadOnly();
        }
    }

    public sealed class WorldManifestRecord
    {
        public int FormatVersion => 1;
        public Guid WorldId { get; }
        public IReadOnlyList<ManifestEntryRecord> Entries { get; }
        public IReadOnlyList<ManifestModuleVersionRecord> ModuleVersions { get; }

        public WorldManifestRecord(Guid worldId,
            IEnumerable<ManifestEntryRecord> entries,
            IEnumerable<ManifestModuleVersionRecord> moduleVersions)
        {
            if (worldId == Guid.Empty || entries == null || moduleVersions == null)
                throw new ArgumentException("Invalid world manifest.");
            WorldId = worldId;
            Entries = new List<ManifestEntryRecord>(entries).AsReadOnly();
            ModuleVersions = new List<ManifestModuleVersionRecord>(
                moduleVersions).AsReadOnly();
        }
    }

    public sealed class ModuleArchivePayload
    {
        public Guid FamilyId { get; }
        public Guid VersionId { get; }
        public IReadOnlyList<Guid> ChildVersionIds { get; }
        public byte[] JsonBytes { get; }

        public ModuleArchivePayload(Guid familyId, Guid versionId,
            IEnumerable<Guid> childVersionIds, byte[] jsonBytes)
        {
            var metadata = new ManifestModuleVersionRecord(familyId, versionId,
                childVersionIds);
            if (jsonBytes == null) throw new ArgumentNullException(nameof(jsonBytes));
            FamilyId = familyId; VersionId = versionId;
            ChildVersionIds = metadata.ChildVersionIds;
            JsonBytes = (byte[])jsonBytes.Clone();
        }
    }

    public sealed class WorldManifestAssessment
    {
        public IReadOnlyList<Guid> DamagedModuleVersionIds { get; }
        internal WorldManifestAssessment(List<Guid> damaged)
        { DamagedModuleVersionIds = damaged.AsReadOnly(); }
    }

    // The manifest is a typed record here; a separate strict JSON reader must
    // validate its serialized fields before calling Assess.
    public static class WorldManifestIntegrity
    {
        public static WorldManifestRecord Build(Guid worldId, byte[] worldJson,
            IEnumerable<ModuleArchivePayload> modules)
        {
            if (worldJson == null || modules == null) throw new ArgumentNullException();
            var entries = new List<ManifestEntryRecord>
            { Entry("world.json", worldJson) };
            var versions = new List<ManifestModuleVersionRecord>();
            var seen = new HashSet<Guid>();
            foreach (var module in modules)
            {
                if (module == null || !seen.Add(module.VersionId))
                    throw new ArgumentException("Duplicate module version.");
                var version = new ManifestModuleVersionRecord(module.FamilyId,
                    module.VersionId, module.ChildVersionIds);
                versions.Add(version);
                entries.Add(Entry(version.Path, module.JsonBytes));
            }
            versions.Sort((first, second) =>
                first.VersionId.CompareTo(second.VersionId));
            entries.Sort((first, second) =>
                string.CompareOrdinal(first.Path, second.Path));
            var manifest = new WorldManifestRecord(worldId, entries, versions);
            ValidateStructure(manifest);
            return manifest;
        }

        public static WorldManifestAssessment Assess(WorldManifestRecord manifest,
            WorldArchiveRecords records)
        {
            if (manifest == null || records == null) throw new ArgumentNullException();
            ValidateStructure(manifest);
            var entries = new Dictionary<string, ManifestEntryRecord>(
                StringComparer.Ordinal);
            foreach (var entry in manifest.Entries) entries.Add(entry.Path, entry);
            if (!Matches(entries["world.json"], records.WorldJson))
                throw new InvalidDataException("World entry is damaged.");
            var expectedVersions = new HashSet<Guid>();
            var damaged = new List<Guid>();
            foreach (var version in manifest.ModuleVersions)
            {
                expectedVersions.Add(version.VersionId);
                if (!records.Modules.TryGetValue(version.VersionId, out var bytes) ||
                    !Matches(entries[version.Path], bytes))
                    damaged.Add(version.VersionId);
            }
            foreach (var id in records.Modules.Keys)
                if (!expectedVersions.Contains(id))
                    throw new InvalidDataException(
                        "Archive contains an unlisted module version.");
            return new WorldManifestAssessment(damaged);
        }

        public static string Sha256Hex(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var chars = new char[hash.Length * 2];
                const string hex = "0123456789abcdef";
                for (var i = 0; i < hash.Length; i++)
                {
                    chars[i * 2] = hex[hash[i] >> 4];
                    chars[i * 2 + 1] = hex[hash[i] & 15];
                }
                return new string(chars);
            }
        }

        private static ManifestEntryRecord Entry(string path, byte[] bytes) =>
            new ManifestEntryRecord(path, bytes.LongLength, Sha256Hex(bytes));

        private static bool Matches(ManifestEntryRecord entry, byte[] bytes) =>
            entry.UncompressedBytes == bytes.LongLength &&
            entry.Sha256 == Sha256Hex(bytes);

        internal static void ValidateStructure(WorldManifestRecord manifest)
        {
            if (manifest.WorldId == Guid.Empty)
                throw new InvalidDataException("Empty world identity.");
            var entries = new Dictionary<string, ManifestEntryRecord>(
                StringComparer.Ordinal);
            foreach (var entry in manifest.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Path) ||
                    entry.UncompressedBytes < 0 || !ValidSha(entry.Sha256) ||
                    entries.ContainsKey(entry.Path))
                    throw new InvalidDataException("Invalid or duplicate manifest entry.");
                entries.Add(entry.Path, entry);
            }
            if (!entries.ContainsKey("world.json") ||
                entries.ContainsKey("manifest.json"))
                throw new InvalidDataException("Manifest entry list is incomplete.");
            var versions = new Dictionary<Guid, ManifestModuleVersionRecord>();
            foreach (var version in manifest.ModuleVersions)
            {
                if (version == null || versions.ContainsKey(version.VersionId) ||
                    !entries.ContainsKey(version.Path))
                    throw new InvalidDataException("Invalid module manifest entry.");
                versions.Add(version.VersionId, version);
            }
            if (entries.Count != versions.Count + 1)
                throw new InvalidDataException("Manifest has unlisted entries.");
            var visiting = new HashSet<Guid>();
            var visited = new HashSet<Guid>();
            foreach (var version in manifest.ModuleVersions)
                Visit(version.VersionId);

            void Visit(Guid id)
            {
                if (visited.Contains(id)) return;
                if (!versions.TryGetValue(id, out var version) || !visiting.Add(id))
                    throw new InvalidDataException(
                        "Missing or recursive module dependency.");
                foreach (var child in version.ChildVersionIds) Visit(child);
                visiting.Remove(id);
                visited.Add(id);
            }
        }

        private static bool ValidSha(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (var value in text)
                if (value < '0' || value > '9' && value < 'a' || value > 'f')
                    return false;
            return true;
        }
    }
}
