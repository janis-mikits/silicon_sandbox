using System;
using System.Collections.Generic;
using System.IO;

namespace SiliconSandbox.Persistence
{
    // Reads only files named by the validated index. Orphaned files are never
    // usable versions, and a global copy cannot silently replace a different
    // exact world version.
    public static class ModuleLibraryStore
    {
        public static IReadOnlyList<LibraryVersionEntry> ReadIndex(string storageRoot)
        {
            var path = IndexPath(storageRoot);
            if (!File.Exists(path)) return Array.Empty<LibraryVersionEntry>();
            return ModuleLibraryIndexJson.Read(File.ReadAllBytes(path));
        }

        public static byte[] WithAddedVersion(string storageRoot,
            byte[] definitionBytes)
        {
            if (definitionBytes == null)
                throw new ArgumentNullException(nameof(definitionBytes));
            var version = WorldV1JsonReader.ReadModule(definitionBytes);
            var entries = new List<LibraryVersionEntry>(ReadIndex(storageRoot));
            foreach (var entry in entries)
                if (entry.VersionId == version.VersionId)
                    throw new InvalidDataException(
                        "Exact module version already exists in the library index.");
            entries.Add(new LibraryVersionEntry(version.FamilyId,
                version.VersionId, version.Name,
                WorldManifestIntegrity.Sha256Hex(definitionBytes), false));
            return ModuleLibraryIndexJson.Write(entries);
        }

        public static IReadOnlyDictionary<Guid, byte[]> ExactCopies(
            string storageRoot, IEnumerable<Guid> requestedIds)
        {
            if (requestedIds == null) throw new ArgumentNullException(nameof(requestedIds));
            var index = new Dictionary<Guid, LibraryVersionEntry>();
            foreach (var item in ReadIndex(storageRoot))
                index.Add(item.VersionId, item);
            var copies = new Dictionary<Guid, byte[]>();
            var root = LibraryPath(storageRoot);
            foreach (var id in requestedIds)
            {
                if (!index.TryGetValue(id, out var entry) || copies.ContainsKey(id))
                    continue;
                var path = Path.Combine(root, "versions", id.ToString("D") + ".json");
                if (!File.Exists(path)) continue;
                var bytes = File.ReadAllBytes(path);
                if (WorldManifestIntegrity.Sha256Hex(bytes) != entry.Sha256)
                    continue;
                try
                {
                    var decoded = WorldV1JsonReader.ReadModule(bytes);
                    if (decoded.VersionId != entry.VersionId ||
                        decoded.FamilyId != entry.FamilyId ||
                        decoded.Name != entry.Name)
                        continue;
                }
                catch (Exception error) when (error is InvalidDataException ||
                    error is ArgumentException || error is InvalidCastException ||
                    error is OverflowException)
                { continue; }
                copies.Add(id, bytes);
            }
            return copies;
        }

        public static string WorldPath(string storageRoot, Guid worldId)
        {
            if (!WorldManifestIntegrity.IsVersionFour(worldId))
                throw new ArgumentException("World file needs a UUIDv4 identity.");
            return Path.Combine(Root(storageRoot), "worlds",
                worldId.ToString("D") + ".ssworld");
        }

        public static string IndexPath(string storageRoot) =>
            Path.Combine(LibraryPath(storageRoot), "library-index.json");

        private static string LibraryPath(string storageRoot) =>
            Path.Combine(Root(storageRoot), "library");

        private static string Root(string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
                throw new ArgumentException("Game storage root is required.");
            return Path.GetFullPath(storageRoot);
        }
    }
}
