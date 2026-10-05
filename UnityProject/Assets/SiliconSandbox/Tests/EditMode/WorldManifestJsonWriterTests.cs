using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldManifestJsonWriterTests
    {
        [Test]
        public void EmptyWorldManifestUsesExactVersionOneFieldsAndKnownDigest()
        {
            var worldId = Guid.Parse("11111111-1111-4111-8111-111111111111");
            var manifest = WorldManifestIntegrity.Build(worldId,
                Array.Empty<byte>(), Array.Empty<ModuleArchivePayload>());
            var text = Encoding.UTF8.GetString(
                WorldManifestJsonWriter.Write(manifest));
            const string expected =
                "{\"formatVersion\":1," +
                "\"worldId\":\"11111111-1111-4111-8111-111111111111\"," +
                "\"entries\":[{\"path\":\"world.json\"," +
                "\"uncompressedBytes\":0," +
                "\"sha256\":\"e3b0c44298fc1c149afbf4c8996fb924" +
                "27ae41e4649b934ca495991b7852b855\"}]," +
                "\"moduleVersions\":[]}";
            Assert.That(text, Is.EqualTo(expected));
            Assert.That(WorldManifestJsonWriter.Write(manifest)[0],
                Is.EqualTo((byte)'{'), "The output is plain UTF-8, without a BOM.");
        }

        [Test]
        public void WriterRejectsStructurallyIncompleteManifest()
        {
            var incomplete = new WorldManifestRecord(Guid.NewGuid(),
                Array.Empty<ManifestEntryRecord>(),
                Array.Empty<ManifestModuleVersionRecord>());
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonWriter.Write(incomplete));
        }

        [Test]
        public void EmbeddedVersionKeepsExactIdentityAndArchivePath()
        {
            var family = Guid.Parse("22222222-2222-4222-8222-222222222222");
            var version = Guid.Parse("33333333-3333-4333-8333-333333333333");
            var manifest = WorldManifestIntegrity.Build(Guid.NewGuid(),
                new byte[] { (byte)'{' }, new[]
                {
                    new ModuleArchivePayload(family, version,
                        Array.Empty<Guid>(), new byte[] { (byte)'{' })
                });
            var json = Encoding.UTF8.GetString(
                WorldManifestJsonWriter.Write(manifest));
            Assert.That(json, Does.Contain(
                "\"path\":\"modules/33333333-3333-4333-8333-333333333333.json\""));
            Assert.That(json, Does.Contain(
                "\"familyId\":\"22222222-2222-4222-8222-222222222222\""));
            Assert.That(json, Does.Contain("\"childVersionIds\":[]"));
        }
    }
}
