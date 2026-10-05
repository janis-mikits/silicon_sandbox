using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class AtomicPackageFilePublisherTests
    {
        private sealed class Crash : Exception { }

        [Test]
        public void VerifiedPackageCommitsAllThreeFilesAndKeepsPreviousWorld()
        {
            InRepositoryTestRoot((root, worldId, versionId) =>
            {
                SeedOldFiles(root, worldId);
                Publish(root, worldId, versionId);
                Assert.That(File.ReadAllText(World(root, worldId)),
                    Is.EqualTo("new-world"));
                Assert.That(File.ReadAllText(Index(root)),
                    Is.EqualTo("new-index"));
                Assert.That(File.ReadAllText(Definition(root, versionId)),
                    Is.EqualTo("new-definition"));
                Assert.That(File.ReadAllText(World(root, worldId) + ".previous"),
                    Is.EqualTo("old-world"));
                Assert.That(AtomicPackageFilePublisher.Recover(root), Is.False);
            });
        }

        [Test]
        public void ValidationFailureDoesNotReplaceEitherOldFile()
        {
            InRepositoryTestRoot((root, worldId, versionId) =>
            {
                SeedOldFiles(root, worldId);
                Assert.Throws<InvalidDataException>(() => Publish(root,
                    worldId, versionId, null,
                    _ => throw new InvalidDataException("invalid index")));
                Assert.That(File.ReadAllText(World(root, worldId)),
                    Is.EqualTo("old-world"));
                Assert.That(File.ReadAllText(Index(root)),
                    Is.EqualTo("old-index"));
                Assert.That(File.Exists(Definition(root, versionId)), Is.False);
                Assert.That(AtomicPackageFilePublisher.Recover(root), Is.False);
            });
        }

        [Test]
        public void CrashAfterIndexReplacementRollsBackWorldLibraryAndDefinition()
        {
            InRepositoryTestRoot((root, worldId, versionId) =>
            {
                SeedOldFiles(root, worldId);
                Assert.Throws<Crash>(() => Publish(root, worldId,
                    versionId, phase =>
                    {
                        if (phase == "index") throw new Crash();
                    }));
                Assert.That(File.ReadAllText(Index(root)),
                    Is.EqualTo("new-index"));
                Assert.That(File.ReadAllText(World(root, worldId)),
                    Is.EqualTo("old-world"));
                Assert.That(AtomicPackageFilePublisher.Recover(root), Is.True);
                Assert.That(File.ReadAllText(Index(root)),
                    Is.EqualTo("old-index"));
                Assert.That(File.ReadAllText(World(root, worldId)),
                    Is.EqualTo("old-world"));
                Assert.That(File.Exists(Definition(root, versionId)), Is.False);
                Assert.That(AtomicPackageFilePublisher.Recover(root), Is.False);
            });
        }

        [Test]
        public void CrashAfterBothReplacementsCompletesValidatedPackage()
        {
            InRepositoryTestRoot((root, worldId, versionId) =>
            {
                SeedOldFiles(root, worldId);
                Assert.Throws<Crash>(() => Publish(root, worldId,
                    versionId, phase =>
                    {
                        if (phase == "world") throw new Crash();
                    }));
                Assert.That(AtomicPackageFilePublisher.Recover(root), Is.True);
                Assert.That(File.ReadAllText(Index(root)),
                    Is.EqualTo("new-index"));
                Assert.That(File.ReadAllText(World(root, worldId)),
                    Is.EqualTo("new-world"));
                Assert.That(File.ReadAllText(Definition(root, versionId)),
                    Is.EqualTo("new-definition"));
                Assert.That(File.ReadAllText(World(root, worldId) + ".previous"),
                    Is.EqualTo("old-world"));
            });
        }

        private static void Publish(string root, Guid worldId, Guid versionId,
            Action<string> phase = null, Action<Stream> indexValidator = null)
        {
            AtomicPackageFilePublisher.Publish(root, worldId, versionId,
                Bytes("new-world"), Bytes("new-index"),
                Bytes("new-definition"), Expect("new-world"),
                indexValidator ?? Expect("new-index"),
                Expect("new-definition"), phase);
        }

        private static Action<Stream> Expect(string expected) => stream =>
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8,
                false, 1024, true))
                if (reader.ReadToEnd() != expected)
                    throw new InvalidDataException("Prepared bytes are wrong.");
        };

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        private static void InRepositoryTestRoot(Action<string, Guid, Guid> test)
        {
            var parent = Environment.GetEnvironmentVariable(
                "SILICON_SANDBOX_TEST_ROOT");
            Assert.That(parent, Is.Not.Null.And.Not.Empty,
                "Verification wrapper must supply a repository-local test root.");
            var root = Path.Combine(parent,
                "package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try { test(root, Guid.NewGuid(), Guid.NewGuid()); }
            finally { Directory.Delete(root, true); }
        }

        private static void SeedOldFiles(string root, Guid worldId)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(World(root, worldId)));
            Directory.CreateDirectory(Path.GetDirectoryName(Index(root)));
            File.WriteAllText(World(root, worldId), "old-world");
            File.WriteAllText(Index(root), "old-index");
        }

        private static string World(string root, Guid id) =>
            Path.Combine(root, "worlds", id.ToString("D") + ".ssworld");
        private static string Index(string root) =>
            Path.Combine(root, "library", "library-index.json");
        private static string Definition(string root, Guid id) =>
            Path.Combine(root, "library", "versions", id.ToString("D") +
                ".json");
    }
}
