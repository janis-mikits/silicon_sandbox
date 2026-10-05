using System;
using System.Collections.Generic;
using System.IO;

namespace SiliconSandbox.Persistence
{
    public sealed class LibraryVersionEntry
    {
        public Guid FamilyId { get; }
        public Guid VersionId { get; }
        public string Name { get; }
        public string DefinitionPath =>
            "versions/" + VersionId.ToString("D") + ".json";
        public string Sha256 { get; }
        public bool Archived { get; }

        public LibraryVersionEntry(Guid familyId, Guid versionId,
            string name, string sha256, bool archived)
        {
            if (!WorldManifestIntegrity.IsVersionFour(familyId) ||
                !WorldManifestIntegrity.IsVersionFour(versionId) ||
                string.IsNullOrWhiteSpace(name) || !ValidHash(sha256))
                throw new ArgumentException("Invalid exact library index entry.");
            FamilyId = familyId;
            VersionId = versionId;
            Name = name;
            Sha256 = sha256;
            Archived = archived;
        }

        private static bool ValidHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var c in value)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                    return false;
            return true;
        }
    }

    public static class ModuleLibraryIndexJson
    {
        public static byte[] Write(IEnumerable<LibraryVersionEntry> versions)
        {
            if (versions == null) throw new ArgumentNullException(nameof(versions));
            var ordered = new List<LibraryVersionEntry>(versions);
            ordered.Sort((first, second) =>
                first.VersionId.CompareTo(second.VersionId));
            var seen = new HashSet<Guid>();
            var writer = new V1JsonWriter();
            writer.BeginObject();
            writer.Name("formatVersion"); writer.Integer(1);
            writer.Name("versions"); writer.BeginArray();
            foreach (var item in ordered)
            {
                if (item == null || !seen.Add(item.VersionId))
                    throw new InvalidDataException("Duplicate library version.");
                writer.BeginObject();
                writer.Name("familyId"); writer.String(item.FamilyId.ToString("D"));
                writer.Name("versionId"); writer.String(item.VersionId.ToString("D"));
                writer.Name("name"); writer.String(item.Name);
                writer.Name("definitionPath"); writer.String(item.DefinitionPath);
                writer.Name("sha256"); writer.String(item.Sha256);
                writer.Name("archived"); writer.Boolean(item.Archived);
                writer.EndObject();
            }
            writer.EndArray(); writer.EndObject();
            return writer.ToUtf8();
        }

        public static IReadOnlyList<LibraryVersionEntry> Read(byte[] bytes)
        {
            var root = V1JsonReader.Object(V1JsonReader.Root(bytes),
                "formatVersion", "versions");
            if (V1JsonReader.Integer(root["formatVersion"]) != 1)
                throw new InvalidDataException("Unsupported module library index version.");
            var result = new List<LibraryVersionEntry>();
            var seen = new HashSet<Guid>();
            foreach (var token in V1JsonReader.Array(root["versions"]))
            {
                var item = V1JsonReader.Object(token, "familyId", "versionId",
                    "name", "definitionPath", "sha256", "archived");
                var entry = new LibraryVersionEntry(
                    V1JsonReader.Uuid(item["familyId"]),
                    V1JsonReader.Uuid(item["versionId"]),
                    V1JsonReader.String(item["name"]),
                    V1JsonReader.Sha256(item["sha256"]),
                    V1JsonReader.Boolean(item["archived"]));
                if (!seen.Add(entry.VersionId) ||
                    V1JsonReader.String(item["definitionPath"]) != entry.DefinitionPath)
                    throw new InvalidDataException("Duplicate or unsafe library path.");
                result.Add(entry);
            }
            return result.AsReadOnly();
        }
    }
}
