using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SiliconSandbox.Persistence
{
    public sealed class PackagePublicationReceipt
    {
        public Guid WorldId { get; }
        public Guid VersionId { get; }
        public string WorldSha256 { get; }
        public string IndexSha256 { get; }
        public string DefinitionSha256 { get; }

        internal PackagePublicationReceipt(Guid worldId, Guid versionId,
            string worldSha256, string indexSha256, string definitionSha256)
        {
            WorldId = worldId;
            VersionId = versionId;
            WorldSha256 = worldSha256;
            IndexSha256 = indexSha256;
            DefinitionSha256 = definitionSha256;
        }
    }

    // One recoverable transaction for a new fixed library definition, its
    // library index, and the world file whose inventory references it. The
    // caller provides strict semantic validators for all three prepared files.
    public static class AtomicPackageFilePublisher
    {
        private const string JournalName = "package-publication.pending";
        private const string JournalHeader = "SSPKG1";

        private sealed class Journal
        {
            public Guid WorldId;
            public Guid VersionId;
            public string Nonce;
            public string OldWorldHash;
            public string OldIndexHash;
            public string NewWorldHash;
            public string NewIndexHash;
            public string NewDefinitionHash;
        }

        public static PackagePublicationReceipt Publish(string storageRoot,
            Guid worldId,
            Guid versionId, byte[] worldArchive, byte[] libraryIndexJson,
            byte[] definitionJson, Action<Stream> verifyWorld,
            Action<Stream> verifyIndex, Action<Stream> verifyDefinition,
            Action<string> afterPhase = null)
        {
            if (worldId == Guid.Empty || versionId == Guid.Empty ||
                worldArchive == null || libraryIndexJson == null ||
                definitionJson == null || verifyWorld == null ||
                verifyIndex == null || verifyDefinition == null)
                throw new ArgumentException("Incomplete package publication.");
            var root = Root(storageRoot);
            if (File.Exists(JournalPath(root))) Recover(root);
            var journal = new Journal
            {
                WorldId = worldId,
                VersionId = versionId,
                Nonce = Guid.NewGuid().ToString("N"),
                NewWorldHash = WorldManifestIntegrity.Sha256Hex(worldArchive),
                NewIndexHash = WorldManifestIntegrity.Sha256Hex(libraryIndexJson),
                NewDefinitionHash = WorldManifestIntegrity.Sha256Hex(definitionJson)
            };
            var world = WorldPath(root, journal);
            var index = IndexPath(root);
            var definition = DefinitionPath(root, journal);
            if (File.Exists(definition))
                throw new InvalidOperationException(
                    "A fixed version file already uses this identity.");
            Directory.CreateDirectory(Path.GetDirectoryName(world));
            Directory.CreateDirectory(Path.GetDirectoryName(index));
            Directory.CreateDirectory(Path.GetDirectoryName(definition));
            journal.OldWorldHash = File.Exists(world) ? HashFile(world) : "-";
            journal.OldIndexHash = File.Exists(index) ? HashFile(index) : "-";
            var worldPending = Pending(world, journal);
            var indexPending = Pending(index, journal);
            var definitionPending = Pending(definition, journal);
            try
            {
                Prepare(worldPending, worldArchive, verifyWorld);
                Prepare(indexPending, libraryIndexJson, verifyIndex);
                Prepare(definitionPending, definitionJson, verifyDefinition);
                WriteJournal(root, journal);
                afterPhase?.Invoke("prepared");
                File.Move(definitionPending, definition);
                afterPhase?.Invoke("definition");
                ReplaceOrMove(indexPending, index, Backup(index, journal));
                afterPhase?.Invoke("index");
                ReplaceOrMove(worldPending, world, Backup(world, journal));
                afterPhase?.Invoke("world");
                if (!AllNew(root, journal))
                    throw new InvalidDataException(
                        "Published package bytes disagree with prepared bytes.");
                Finish(root, journal);
                return new PackagePublicationReceipt(worldId, versionId,
                    journal.NewWorldHash, journal.NewIndexHash,
                    journal.NewDefinitionHash);
            }
            catch
            {
                // The journal remains for recovery before any library or
                // world load. A caller must not announce package success.
                if (!File.Exists(JournalPath(root)))
                {
                    DeleteIfPresent(worldPending);
                    DeleteIfPresent(indexPending);
                    DeleteIfPresent(definitionPending);
                }
                throw;
            }
        }

        public static bool Recover(string storageRoot)
        {
            var root = Root(storageRoot);
            if (!File.Exists(JournalPath(root))) return false;
            var journal = ReadJournal(root);
            if (AllNew(root, journal)) Finish(root, journal);
            else RollBack(root, journal);
            return true;
        }

        private static void RollBack(string root, Journal journal)
        {
            var world = WorldPath(root, journal);
            var index = IndexPath(root);
            var definition = DefinitionPath(root, journal);
            RestoreOld(world, journal.OldWorldHash, journal.NewWorldHash,
                Backup(world, journal));
            RestoreOld(index, journal.OldIndexHash, journal.NewIndexHash,
                Backup(index, journal));
            if (File.Exists(definition))
            {
                if (HashFile(definition) != journal.NewDefinitionHash)
                    throw new InvalidDataException(
                        "Unexpected module bytes prevent safe rollback.");
                File.Delete(definition);
            }
            Cleanup(root, journal);
        }

        private static void RestoreOld(string target, string oldHash,
            string newHash, string backup)
        {
            if (oldHash == "-")
            {
                if (File.Exists(target))
                {
                    if (HashFile(target) != newHash)
                        throw new InvalidDataException(
                            "Unexpected target bytes prevent safe rollback.");
                    File.Delete(target);
                }
                return;
            }
            if (File.Exists(target) && HashFile(target) == oldHash) return;
            if (!File.Exists(backup) || HashFile(backup) != oldHash)
                throw new InvalidDataException(
                    "Original file is unavailable for package rollback.");
            File.Copy(backup, target, true);
            if (HashFile(target) != oldHash)
                throw new InvalidDataException("Package rollback verification failed.");
        }

        private static bool AllNew(string root, Journal journal) =>
            Matches(WorldPath(root, journal), journal.NewWorldHash) &&
            Matches(IndexPath(root), journal.NewIndexHash) &&
            Matches(DefinitionPath(root, journal),
                journal.NewDefinitionHash);

        private static void Finish(string root, Journal journal)
        {
            var world = WorldPath(root, journal);
            var index = IndexPath(root);
            PreservePrevious(Backup(world, journal), world + ".previous");
            PreservePrevious(Backup(index, journal), index + ".previous");
            Cleanup(root, journal);
        }

        private static void PreservePrevious(string backup, string previous)
        {
            if (!File.Exists(backup)) return;
            File.Copy(backup, previous, true);
        }

        private static void Cleanup(string root, Journal journal)
        {
            var world = WorldPath(root, journal);
            var index = IndexPath(root);
            var definition = DefinitionPath(root, journal);
            DeleteIfPresent(Pending(world, journal));
            DeleteIfPresent(Pending(index, journal));
            DeleteIfPresent(Pending(definition, journal));
            DeleteIfPresent(Backup(world, journal));
            DeleteIfPresent(Backup(index, journal));
            DeleteIfPresent(JournalPath(root));
        }

        private static void Prepare(string path, byte[] bytes,
            Action<Stream> verify)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read)) verify(stream);
        }

        private static void ReplaceOrMove(string pending, string target,
            string backup)
        {
            if (File.Exists(target)) File.Replace(pending, target, backup);
            else File.Move(pending, target);
        }

        private static void WriteJournal(string root, Journal journal)
        {
            var lines = new[] { JournalHeader,
                journal.WorldId.ToString("D"),
                journal.VersionId.ToString("D"), journal.Nonce,
                journal.OldWorldHash, journal.OldIndexHash,
                journal.NewWorldHash, journal.NewIndexHash,
                journal.NewDefinitionHash, "" };
            var bytes = new UTF8Encoding(false, true).GetBytes(
                string.Join("\n", lines));
            var staging = JournalPath(root) + ".staging";
            try
            {
                using (var stream = new FileStream(staging,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(staging, JournalPath(root));
            }
            finally { DeleteIfPresent(staging); }
        }

        private static Journal ReadJournal(string root)
        {
            var raw = new UTF8Encoding(false, true).GetString(
                File.ReadAllBytes(JournalPath(root)));
            var lines = raw.Split('\n');
            if (lines.Length != 10 || lines[0] != JournalHeader ||
                lines[9] != "" || !CanonicalGuid(lines[1], out var worldId) ||
                !CanonicalGuid(lines[2], out var versionId) ||
                !Guid.TryParseExact(lines[3], "N", out var nonceId) ||
                lines[3] != nonceId.ToString("N") ||
                !OldHash(lines[4]) || !OldHash(lines[5]) ||
                !ValidHash(lines[6]) || !ValidHash(lines[7]) ||
                !ValidHash(lines[8]))
                throw new InvalidDataException("Invalid package transaction journal.");
            return new Journal
            {
                WorldId = worldId, VersionId = versionId,
                Nonce = lines[3], OldWorldHash = lines[4],
                OldIndexHash = lines[5], NewWorldHash = lines[6],
                NewIndexHash = lines[7], NewDefinitionHash = lines[8]
            };
        }

        private static bool CanonicalGuid(string text, out Guid value) =>
            Guid.TryParseExact(text, "D", out value) &&
            value != Guid.Empty && text == value.ToString("D");

        private static bool OldHash(string text) => text == "-" || ValidHash(text);
        private static bool ValidHash(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (var c in text)
                if (c < '0' || c > '9' && c < 'a' || c > 'f') return false;
            return true;
        }

        private static string Root(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Storage root is required.");
            var root = Path.GetFullPath(path);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Storage root is missing.");
            return root;
        }

        private static string WorldPath(string root, Journal journal) =>
            Path.Combine(root, "worlds", journal.WorldId.ToString("D") +
                ".ssworld");
        private static string IndexPath(string root) =>
            Path.Combine(root, "library", "library-index.json");
        private static string DefinitionPath(string root, Journal journal) =>
            Path.Combine(root, "library", "versions",
                journal.VersionId.ToString("D") + ".json");
        private static string JournalPath(string root) =>
            Path.Combine(root, JournalName);
        private static string Pending(string target, Journal journal) =>
            target + "." + journal.Nonce + ".pending";
        private static string Backup(string target, Journal journal) =>
            target + "." + journal.Nonce + ".previous";
        private static void DeleteIfPresent(string path)
        { if (File.Exists(path)) File.Delete(path); }
        private static bool Matches(string path, string hash) =>
            File.Exists(path) && HashFile(path) == hash;

        private static string HashFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
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
    }
}
