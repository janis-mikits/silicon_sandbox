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
            "/first-playable-1000-gates.ssworld";
        private const string HashPath = DirectoryPath +
            "/first-playable-1000-gates.sha256";

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
