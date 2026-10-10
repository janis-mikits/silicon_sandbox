using System;
using System.Collections.Generic;
using System.IO;

namespace SiliconSandbox.Persistence
{
    public static class WorldManifestJsonReader
    {
        public static WorldManifestRecord Read(byte[] bytes)
        {
            var root = V1JsonReader.Object(V1JsonReader.Root(bytes),
                "formatVersion", "worldId", "entries", "moduleVersions");
            var formatVersion = V1JsonReader.Int32(root["formatVersion"]);
            if (formatVersion != 1 && formatVersion != 2)
                throw new InvalidDataException("Unsupported world format version.");
            var worldId = V1JsonReader.Uuid(root["worldId"]);
            var entries = new List<ManifestEntryRecord>();
            foreach (var token in V1JsonReader.Array(root["entries"]))
            {
                var item = V1JsonReader.Object(token, "path",
                    "uncompressedBytes", "sha256");
                entries.Add(new ManifestEntryRecord(
                    V1JsonReader.String(item["path"]),
                    V1JsonReader.Integer(item["uncompressedBytes"]),
                    V1JsonReader.Sha256(item["sha256"])));
            }
            var versions = new List<ManifestModuleVersionRecord>();
            foreach (var token in V1JsonReader.Array(root["moduleVersions"]))
            {
                var item = V1JsonReader.Object(token, "familyId",
                    "versionId", "path", "childVersionIds");
                var children = new List<Guid>();
                foreach (var child in V1JsonReader.Array(item["childVersionIds"]))
                    children.Add(V1JsonReader.Uuid(child));
                var version = new ManifestModuleVersionRecord(
                    V1JsonReader.Uuid(item["familyId"]),
                    V1JsonReader.Uuid(item["versionId"]), children);
                if (V1JsonReader.String(item["path"]) != version.Path)
                    throw new InvalidDataException("Module path disagrees with version identity.");
                versions.Add(version);
            }
            var manifest = new WorldManifestRecord(worldId, entries, versions, formatVersion);
            WorldManifestIntegrity.ValidateStructure(manifest);
            return manifest;
        }
    }
}
