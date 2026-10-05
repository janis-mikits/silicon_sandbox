using System;
using System.IO;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldRecoveryStoreTests
    {
        [Test]
        public void DamagedManualOffersPreviousAndLatestValidAutosaveWithoutReplacement()
        {
            var root = TestRoot();
            try
            {
                var snapshot = Snapshot();
                var path = ModuleLibraryStore.WorldPath(root, snapshot.WorldId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var good = WorldV1ArchiveCodec.Write(snapshot);
                VerifiedWorldFileStore.Save(path, good,
                    input => WorldV1ArchiveCodec.Read(input));
                VerifiedWorldFileStore.Save(path, good,
                    input => WorldV1ArchiveCodec.Read(input));
                var first = new DateTime(2026, 10, 5, 12, 0, 0,
                    DateTimeKind.Utc);
                WorldRecoveryStore.SaveAutosave(root, snapshot, first);
                var latest = WorldRecoveryStore.SaveAutosave(root, snapshot,
                    first.AddMinutes(5));
                File.WriteAllText(path, "damaged world; preserve for recovery");

                var choices = WorldRecoveryStore.List(root);
                Assert.That(choices.Count, Is.EqualTo(2));
                Assert.That(choices[0].Kind, Is.EqualTo(
                    WorldRecoveryKind.PreviousManual).Or.EqualTo(
                    WorldRecoveryKind.Autosave));
                Assert.That(choices, Has.Exactly(1).Matches<WorldRecoveryChoice>(
                    choice => choice.Kind == WorldRecoveryKind.PreviousManual &&
                        choice.WorldId == snapshot.WorldId));
                Assert.That(choices, Has.Exactly(1).Matches<WorldRecoveryChoice>(
                    choice => choice.Kind == WorldRecoveryKind.Autosave &&
                        choice.Path == latest &&
                        choice.SavedUtc == first.AddMinutes(5)));
                Assert.That(File.ReadAllText(path),
                    Is.EqualTo("damaged world; preserve for recovery"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void RetentionKeepsFourNewerValidatedBackups()
        {
            var root = TestRoot();
            try
            {
                var snapshot = Snapshot();
                var first = new DateTime(2026, 10, 5, 12, 0, 0,
                    DateTimeKind.Utc);
                var oldest = WorldRecoveryStore.SaveAutosave(root, snapshot,
                    first);
                for (var i = 1; i <= 5; i++)
                    WorldRecoveryStore.SaveAutosave(root, snapshot,
                        first.AddMinutes(i * 5));
                Assert.That(File.Exists(oldest), Is.False,
                    "Older than 20 minutes and at least four newer backups exist.");
                var directory = Path.GetDirectoryName(oldest);
                Assert.That(Directory.GetFiles(directory, "*.ssworld").Length,
                    Is.EqualTo(5));
            }
            finally { Directory.Delete(root, true); }
        }

        private static WorldSaveSnapshot Snapshot() =>
            OneBitWorldContext.NewFreeplay("Recovery test",
                new WorldBounds(8, 8, 4)).Capture(
                    new SavedPlayerPose(2, 1, 2, 1, 0, 0));

        private static string TestRoot()
        {
            var parent = Environment.GetEnvironmentVariable(
                "SILICON_SANDBOX_TEST_ROOT");
            Assert.That(parent, Is.Not.Null.And.Not.Empty);
            var path = Path.Combine(parent,
                "recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
