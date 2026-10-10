using System;
using System.Globalization;
using System.Text;

namespace SiliconSandbox.Persistence
{
    // Emits the accepted V1 manifest shape. Hashes cover uncompressed world
    // and module bytes; the manifest itself is deliberately not hashed.
    public static class WorldManifestJsonWriter
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Write(WorldManifestRecord manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            WorldManifestIntegrity.ValidateStructure(manifest);
            var json = new StringBuilder();
            json.Append("{\"formatVersion\":");
            json.Append(manifest.FormatVersion);
            json.Append(",\"worldId\":");
            Quoted(json, manifest.WorldId.ToString("D"));
            json.Append(",\"entries\":[");
            for (var i = 0; i < manifest.Entries.Count; i++)
            {
                if (i > 0) json.Append(',');
                var entry = manifest.Entries[i];
                json.Append("{\"path\":");
                Quoted(json, entry.Path);
                json.Append(",\"uncompressedBytes\":");
                json.Append(entry.UncompressedBytes.ToString(
                    CultureInfo.InvariantCulture));
                json.Append(",\"sha256\":");
                Quoted(json, entry.Sha256);
                json.Append('}');
            }
            json.Append("],\"moduleVersions\":[");
            for (var i = 0; i < manifest.ModuleVersions.Count; i++)
            {
                if (i > 0) json.Append(',');
                var version = manifest.ModuleVersions[i];
                json.Append("{\"familyId\":");
                Quoted(json, version.FamilyId.ToString("D"));
                json.Append(",\"versionId\":");
                Quoted(json, version.VersionId.ToString("D"));
                json.Append(",\"path\":");
                Quoted(json, version.Path);
                json.Append(",\"childVersionIds\":[");
                for (var j = 0; j < version.ChildVersionIds.Count; j++)
                {
                    if (j > 0) json.Append(',');
                    Quoted(json, version.ChildVersionIds[j].ToString("D"));
                }
                json.Append("]}");
            }
            json.Append("]}");
            return Utf8.GetBytes(json.ToString());
        }

        private static void Quoted(StringBuilder json, string value)
        {
            // Manifest paths, UUIDs, and SHA-256 digests are ASCII and already
            // validated, but escape defensively before emitting JSON strings.
            json.Append('"');
            foreach (var character in value)
            {
                if (character == '"' || character == '\\')
                {
                    json.Append('\\');
                    json.Append(character);
                }
                else if (character < 0x20)
                {
                    json.Append("\\u");
                    json.Append(((int)character).ToString("x4",
                        CultureInfo.InvariantCulture));
                }
                else json.Append(character);
            }
            json.Append('"');
        }
    }
}
