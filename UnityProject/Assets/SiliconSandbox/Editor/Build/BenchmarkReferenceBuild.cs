using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;
using UnityEditor;

namespace SiliconSandbox.EditorBuild
{
    public static class BenchmarkReferenceBuild
    {
        private const string DirectoryPath =
            "Assets/StreamingAssets/Benchmarks";
        private const string ArchivePath = DirectoryPath +
            "/first-playable-1000-gates-compact-v1.ssworld";
        private const string HashPath = DirectoryPath +
            "/first-playable-1000-gates-compact-v1.sha256";
        private const string DenseArchivePath = DirectoryPath +
            "/first-playable-dense-diagnostic.ssworld";
        private const string DenseHashPath = DirectoryPath +
            "/first-playable-dense-diagnostic.sha256";

        public static void BuildDenseDiagnosticOnce()
        {
            if (File.Exists(DenseArchivePath) || File.Exists(DenseHashPath))
                throw new InvalidOperationException(
                    "Dense diagnostic is frozen; existing files cannot be regenerated.");
            var world = FirstPlayableBenchmarkFactory.CreateDenseDiagnostic();
            var snapshot = new WorldSaveSnapshot(Guid.NewGuid(),
                "Dense opaque 1000-AND diagnostic",
                "builtin.smooth_sandstone", "builtin.wood_sandbox_wall",
                "10", new SavedPlayerPose(20d, 0.3d, 34d, 0d, 0d, -1d),
                new SavedInventoryItem[36], 0, world,
                new Dictionary<Guid, OneBitModuleVersion>());
            var archive = WorldV1ArchiveCodec.Write(snapshot);
            using (var input = new MemoryStream(archive))
            {
                var check = WorldV1ArchiveCodec.Read(input);
                if (check.Snapshot.Design.Components.Count != 1000 ||
                    check.Snapshot.Design.Modules.Count != 0 ||
                    check.Snapshot.Design.Topology.Connectors.Count != 0 ||
                    check.UnavailableModuleVersionIds.Count != 0)
                    throw new InvalidDataException(
                        "Dense diagnostic distribution changed.");
            }
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllBytes(DenseArchivePath, archive);
            File.WriteAllText(DenseHashPath,
                WorldManifestIntegrity.Sha256Hex(archive) + "\n",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(DenseArchivePath);
            AssetDatabase.ImportAsset(DenseHashPath);
            AssetDatabase.SaveAssets();
        }

        public static void BuildOnce()
        {
            if (File.Exists(ArchivePath) || File.Exists(HashPath))
                throw new InvalidOperationException(
                    "Benchmark reference is frozen; existing files cannot be regenerated.");
            var fixture = FirstPlayableBenchmarkFactory.CreateSavedReference();
            var versions = new Dictionary<Guid, OneBitModuleVersion>
            { [fixture.Version.VersionId] = fixture.Version };
            var snapshot = new WorldSaveSnapshot(Guid.NewGuid(),
                "First-playable 1000-gate reference",
                "builtin.smooth_sandstone", "builtin.wood_sandbox_wall",
                "10", new SavedPlayerPose(30d, 0.3d, 12d, 1d, 0d, 0d),
                new SavedInventoryItem[36], 0, fixture.World, versions);
            var archive = WorldV1ArchiveCodec.Write(snapshot);
            using (var input = new MemoryStream(archive))
            {
                var check = WorldV1ArchiveCodec.Read(input);
                if (check.Snapshot.Design.Components.Count != 501 ||
                    check.Snapshot.Design.Modules.Count != 10 ||
                    check.Snapshot.ModuleVersions.Count != 1 ||
                    check.UnavailableModuleVersionIds.Count != 0)
                    throw new InvalidDataException(
                        "Reference archive does not contain the required distribution.");
            }
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllBytes(ArchivePath, archive);
            File.WriteAllText(HashPath,
                WorldManifestIntegrity.Sha256Hex(archive) + "\n",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ArchivePath);
            AssetDatabase.ImportAsset(HashPath);
            AssetDatabase.SaveAssets();
        }
    }
}
