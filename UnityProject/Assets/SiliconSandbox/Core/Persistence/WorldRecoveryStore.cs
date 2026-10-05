using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SiliconSandbox.Persistence
{
    public enum WorldRecoveryKind { Manual, PreviousManual, Autosave }

    public sealed class WorldRecoveryChoice
    {
        public Guid WorldId { get; }
        public string WorldName { get; }
        public WorldRecoveryKind Kind { get; }
        public DateTime SavedUtc { get; }
        public string Path { get; }

        internal WorldRecoveryChoice(Guid worldId, string worldName,
            WorldRecoveryKind kind, DateTime savedUtc, string path)
        {
            WorldId = worldId;
            WorldName = worldName;
            Kind = kind;
            SavedUtc = savedUtc;
            Path = path;
        }
    }

    // Autosaves are separately verified snapshots. Listing validates the
    // archive before offering a choice, and never repairs a damaged file in place.
    public static class WorldRecoveryStore
    {
        private const string StampFormat = "yyyyMMddTHHmmssfffffffZ";

        public static string SaveAutosave(string root, WorldSaveSnapshot snapshot,
            DateTime utcNow)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (utcNow.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Autosave timestamp must be UTC.");
            var directory = Path.Combine(Path.GetFullPath(root), "worlds",
                "autosaves", snapshot.WorldId.ToString("D"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory,
                utcNow.ToString(StampFormat, CultureInfo.InvariantCulture) +
                ".ssworld");
            if (File.Exists(path))
                throw new IOException("An autosave already exists for this timestamp.");
            var archive = WorldV1ArchiveCodec.Write(snapshot);
            VerifiedWorldFileStore.Save(path, archive, input =>
            {
                var loaded = WorldV1ArchiveCodec.Read(input);
                if (loaded.Snapshot.WorldId != snapshot.WorldId ||
                    loaded.UnavailableModuleVersionIds.Count != 0)
                    throw new InvalidDataException("Autosave failed validation.");
            });
            PruneAutosaves(root, snapshot.WorldId, utcNow);
            return path;
        }

        public static IReadOnlyList<WorldRecoveryChoice> List(string root)
        {
            var directory = Path.Combine(Path.GetFullPath(root), "worlds");
            var choices = new List<WorldRecoveryChoice>();
            if (!Directory.Exists(directory)) return choices.AsReadOnly();
            foreach (var path in Directory.GetFiles(directory, "*.ssworld"))
            {
                if (!TryWorldId(Path.GetFileNameWithoutExtension(path), out var id))
                    continue;
                AddValid(choices, path, id, WorldRecoveryKind.Manual,
                    File.GetLastWriteTimeUtc(path));
                var previous = path + ".previous";
                if (File.Exists(previous))
                    AddValid(choices, previous, id,
                        WorldRecoveryKind.PreviousManual,
                        File.GetLastWriteTimeUtc(previous));
            }
            var autosaveRoot = Path.Combine(directory, "autosaves");
            if (Directory.Exists(autosaveRoot))
                foreach (var folder in Directory.GetDirectories(autosaveRoot))
                {
                    if (!TryWorldId(Path.GetFileName(folder), out var id))
                        continue;
                    WorldRecoveryChoice newest = null;
                    foreach (var path in Directory.GetFiles(folder, "*.ssworld"))
                    {
                        if (!TryStamp(Path.GetFileNameWithoutExtension(path),
                            out var stamp)) continue;
                        var valid = ValidChoice(path, id,
                            WorldRecoveryKind.Autosave, stamp);
                        if (valid != null && (newest == null ||
                            valid.SavedUtc > newest.SavedUtc)) newest = valid;
                    }
                    if (newest != null) choices.Add(newest);
                }
            choices.Sort((a, b) => b.SavedUtc.CompareTo(a.SavedUtc));
            return choices.AsReadOnly();
        }

        public static void PruneAutosaves(string root, Guid worldId,
            DateTime utcNow)
        {
            var directory = Path.Combine(Path.GetFullPath(root), "worlds",
                "autosaves", worldId.ToString("D"));
            if (!Directory.Exists(directory)) return;
            var valid = new List<WorldRecoveryChoice>();
            foreach (var path in Directory.GetFiles(directory, "*.ssworld"))
            {
                if (!TryStamp(Path.GetFileNameWithoutExtension(path),
                    out var stamp)) continue;
                var choice = ValidChoice(path, worldId,
                    WorldRecoveryKind.Autosave, stamp);
                if (choice != null) valid.Add(choice);
            }
            valid.Sort((a, b) => b.SavedUtc.CompareTo(a.SavedUtc));
            for (var i = 4; i < valid.Count; i++)
                if (utcNow - valid[i].SavedUtc > TimeSpan.FromMinutes(20))
                    File.Delete(valid[i].Path);
        }

        private static void AddValid(List<WorldRecoveryChoice> choices,
            string path, Guid id, WorldRecoveryKind kind, DateTime stamp)
        {
            var choice = ValidChoice(path, id, kind, stamp);
            if (choice != null) choices.Add(choice);
        }

        private static WorldRecoveryChoice ValidChoice(string path, Guid id,
            WorldRecoveryKind kind, DateTime stamp)
        {
            try
            {
                using (var input = File.OpenRead(path))
                {
                    var loaded = WorldV1ArchiveCodec.Read(input);
                    return loaded.Snapshot.WorldId == id
                        ? new WorldRecoveryChoice(id, loaded.Snapshot.WorldName,
                            kind, stamp, path)
                        : null;
                }
            }
            catch (Exception error) when (error is IOException ||
                error is InvalidDataException ||
                error is ArgumentException || error is InvalidOperationException ||
                error is OverflowException)
            { return null; }
        }

        private static bool TryWorldId(string text, out Guid id) =>
            Guid.TryParseExact(text, "D", out id) && text == id.ToString("D") &&
            WorldManifestIntegrity.IsVersionFour(id);

        private static bool TryStamp(string text, out DateTime stamp) =>
            DateTime.TryParseExact(text, StampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal, out stamp);
    }
}
