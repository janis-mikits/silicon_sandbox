using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace SiliconSandbox.Persistence
{
    public sealed class WorldArchiveLimits
    {
        public long MaximumCompressedBytes { get; }
        public long MaximumEntryBytes { get; }
        public long MaximumTotalExpandedBytes { get; }
        public int MaximumEntries { get; }

        public WorldArchiveLimits(long maximumCompressedBytes = 512L * 1024 * 1024,
            long maximumEntryBytes = 64L * 1024 * 1024,
            long maximumTotalExpandedBytes = 512L * 1024 * 1024,
            int maximumEntries = 10000)
        {
            if (maximumCompressedBytes < 1 || maximumEntryBytes < 1 ||
                maximumTotalExpandedBytes < 1 || maximumEntries < 2)
                throw new ArgumentOutOfRangeException("Invalid archive limits.");
            MaximumCompressedBytes = maximumCompressedBytes;
            MaximumEntryBytes = maximumEntryBytes;
            MaximumTotalExpandedBytes = maximumTotalExpandedBytes;
            MaximumEntries = maximumEntries;
        }
    }

    public sealed class WorldArchiveRecords
    {
        public byte[] ManifestJson { get; }
        public byte[] WorldJson { get; }
        public IReadOnlyDictionary<Guid, byte[]> Modules { get; }

        public WorldArchiveRecords(byte[] manifestJson, byte[] worldJson,
            IReadOnlyDictionary<Guid, byte[]> modules)
        {
            if (manifestJson == null || worldJson == null || modules == null)
                throw new ArgumentNullException();
            ManifestJson = (byte[])manifestJson.Clone();
            WorldJson = (byte[])worldJson.Clone();
            var copied = new Dictionary<Guid, byte[]>();
            foreach (var pair in modules)
            {
                if (pair.Key == Guid.Empty || pair.Value == null)
                    throw new ArgumentException("Invalid module archive entry.");
                copied.Add(pair.Key, (byte[])pair.Value.Clone());
            }
            Modules = new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, byte[]>(copied);
        }
    }

    // ZIP envelope only. The versioned JSON parser validates hashes, references,
    // UUID values, topology, and recovery semantics before showing a world.
    public static class WorldArchiveContainer
    {
        public static void Write(Stream destination, WorldArchiveRecords records,
            WorldArchiveLimits limits = null)
        {
            if (destination == null || records == null) throw new ArgumentNullException();
            if (!destination.CanWrite) throw new ArgumentException("Archive stream is not writable.");
            limits = limits ?? new WorldArchiveLimits();
            CheckRecordLimits(records, limits);
            using (var zip = new ZipArchive(destination, ZipArchiveMode.Create, true))
            {
                WriteEntry(zip, "manifest.json", records.ManifestJson);
                WriteEntry(zip, "world.json", records.WorldJson);
                var ids = new List<Guid>(records.Modules.Keys);
                ids.Sort();
                foreach (var id in ids)
                    WriteEntry(zip, ModulePath(id), records.Modules[id]);
            }
            if (destination.CanSeek && destination.Length > limits.MaximumCompressedBytes)
                throw new InvalidDataException("Compressed archive exceeds its limit.");
        }

        public static WorldArchiveRecords Read(Stream source,
            WorldArchiveLimits limits = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanRead || !source.CanSeek)
                throw new ArgumentException("Archive input must be a readable seekable stream.");
            limits = limits ?? new WorldArchiveLimits();
            if (source.Length - source.Position > limits.MaximumCompressedBytes)
                throw new InvalidDataException("Compressed archive exceeds its limit.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var modules = new Dictionary<Guid, byte[]>();
            byte[] manifest = null;
            byte[] world = null;
            long expanded = 0;
            using (var zip = new ZipArchive(source, ZipArchiveMode.Read, true))
            {
                if (zip.Entries.Count > limits.MaximumEntries)
                    throw new InvalidDataException("Too many archive entries.");
                foreach (var entry in zip.Entries)
                {
                    var path = entry.FullName;
                    if (!paths.Add(path))
                        throw new InvalidDataException("Duplicate archive path.");
                    if (entry.Length > limits.MaximumEntryBytes ||
                        entry.Length > limits.MaximumTotalExpandedBytes - expanded)
                        throw new InvalidDataException("Expanded archive exceeds its limit.");
                    var bytes = ReadEntry(entry, limits.MaximumEntryBytes,
                        limits.MaximumTotalExpandedBytes - expanded);
                    expanded = checked(expanded + bytes.Length);
                    if (path == "manifest.json") manifest = bytes;
                    else if (path == "world.json") world = bytes;
                    else if (TryModulePath(path, out var id))
                    {
                        if (modules.ContainsKey(id))
                            throw new InvalidDataException("Duplicate module version entry.");
                        modules.Add(id, bytes);
                    }
                    else throw new InvalidDataException("Unsupported archive path.");
                }
            }
            if (manifest == null || world == null)
                throw new InvalidDataException("World archive is missing a required entry.");
            return new WorldArchiveRecords(manifest, world, modules);
        }

        private static void CheckRecordLimits(WorldArchiveRecords records,
            WorldArchiveLimits limits)
        {
            if (records.Modules.Count + 2 > limits.MaximumEntries)
                throw new InvalidDataException("Too many archive entries.");
            long total = 0;
            Check(records.ManifestJson);
            Check(records.WorldJson);
            foreach (var entry in records.Modules) Check(entry.Value);
            void Check(byte[] bytes)
            {
                if (bytes.LongLength > limits.MaximumEntryBytes ||
                    bytes.LongLength > limits.MaximumTotalExpandedBytes - total)
                    throw new InvalidDataException("Expanded archive exceeds its limit.");
                total = checked(total + bytes.LongLength);
            }
        }

        private static void WriteEntry(ZipArchive zip, string path, byte[] bytes)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open()) stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] ReadEntry(ZipArchiveEntry entry, long perEntry,
            long remainingTotal)
        {
            using (var stream = entry.Open())
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int amount;
                while ((amount = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (amount > perEntry - output.Length ||
                        amount > remainingTotal - output.Length)
                        throw new InvalidDataException("Expanded ZIP entry exceeds its limit.");
                    output.Write(buffer, 0, amount);
                }
                if (output.Length != entry.Length)
                    throw new InvalidDataException("ZIP entry length disagrees with content.");
                return output.ToArray();
            }
        }

        private static string ModulePath(Guid id) =>
            "modules/" + id.ToString("D") + ".json";

        private static bool TryModulePath(string path, out Guid id)
        {
            id = Guid.Empty;
            if (!path.StartsWith("modules/", StringComparison.Ordinal) ||
                !path.EndsWith(".json", StringComparison.Ordinal)) return false;
            var text = path.Substring(8, path.Length - 13);
            return Guid.TryParseExact(text, "D", out id) &&
                text == id.ToString("D") &&
                text[14] == '4' && "89ab".IndexOf(text[19]) >= 0;
        }
    }
}
