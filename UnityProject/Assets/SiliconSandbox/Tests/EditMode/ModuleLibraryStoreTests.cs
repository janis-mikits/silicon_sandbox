using System;
using System.IO;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class ModuleLibraryStoreTests
    {
        [Test]
        public void OnlyIndexedHashMatchingExactCopyIsAvailable()
        {
            var root = Path.Combine(TestRoot(), "library-store-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "library", "versions"));
            try
            {
                var version = new OneBitModuleVersion(Guid.NewGuid(),
                    Guid.NewGuid(), "Fixture", new GridCell(1, 1, 1),
                    Array.Empty<PlacedOneBitComponent>(),
                    new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                        Array.Empty<ConnectorRoute>(),
                        Array.Empty<ElectricalJoin>()),
                    Array.Empty<OneBitModulePort>());
                var bytes = WorldV1JsonWriter.WriteModule(version);
                var path = Path.Combine(root, "library", "versions",
                    version.VersionId.ToString("D") + ".json");
                File.WriteAllBytes(path, bytes);
                Assert.That(ModuleLibraryStore.ExactCopies(root,
                    new[] { version.VersionId }).Count, Is.Zero,
                    "An orphan version file must not become usable.");
                File.WriteAllBytes(ModuleLibraryStore.IndexPath(root),
                    ModuleLibraryStore.WithAddedVersion(root, bytes));
                Assert.That(ModuleLibraryStore.ExactCopies(root,
                    new[] { version.VersionId })[version.VersionId],
                    Is.EqualTo(bytes));
                File.WriteAllText(path, "{}");
                Assert.That(ModuleLibraryStore.ExactCopies(root,
                    new[] { version.VersionId }).Count, Is.Zero,
                    "Index hash must match exact definition bytes.");
            }
            finally { Directory.Delete(root, true); }
        }

        private static string TestRoot()
        {
            var value = Environment.GetEnvironmentVariable(
                "SILICON_SANDBOX_TEST_ROOT");
            if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value))
                throw new InvalidOperationException(
                    "Repository-local test root is required.");
            return value;
        }
    }
}
