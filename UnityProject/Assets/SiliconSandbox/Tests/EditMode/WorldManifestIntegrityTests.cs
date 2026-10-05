using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldManifestIntegrityTests
    {
        private static readonly byte[] Abc = Encoding.UTF8.GetBytes("abc");
        private const string AbcSha =
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        [Test]
        public void Sha256MatchesIndependentAbcReferenceVector()
        {
            Assert.That(WorldManifestIntegrity.Sha256Hex(Abc), Is.EqualTo(AbcSha));
        }

        [Test]
        public void ExactManifestDistinguishesDamagedWorldFromDamagedModule()
        {
            var family = Guid.NewGuid();
            var version = Guid.NewGuid();
            var payload = new ModuleArchivePayload(family, version,
                Array.Empty<Guid>(), Abc);
            var manifest = WorldManifestIntegrity.Build(Guid.NewGuid(), Abc,
                new[] { payload });
            Assert.That(manifest.Entries.Count, Is.EqualTo(2));
            foreach (var entry in manifest.Entries)
            {
                Assert.That(entry.UncompressedBytes, Is.EqualTo(3));
                Assert.That(entry.Sha256, Is.EqualTo(AbcSha));
            }
            var valid = new WorldArchiveRecords(new byte[0], Abc,
                new Dictionary<Guid, byte[]> { [version] = Abc });
            Assert.That(WorldManifestIntegrity.Assess(manifest, valid)
                .DamagedModuleVersionIds.Count, Is.EqualTo(0));

            var damagedModule = new WorldArchiveRecords(new byte[0], Abc,
                new Dictionary<Guid, byte[]>
                { [version] = Encoding.UTF8.GetBytes("abd") });
            Assert.That(WorldManifestIntegrity.Assess(manifest, damagedModule)
                .DamagedModuleVersionIds, Is.EquivalentTo(new[] { version }));
            var absentModule = new WorldArchiveRecords(new byte[0], Abc,
                new Dictionary<Guid, byte[]>());
            Assert.That(WorldManifestIntegrity.Assess(manifest, absentModule)
                .DamagedModuleVersionIds, Is.EquivalentTo(new[] { version }));
            var damagedWorld = new WorldArchiveRecords(new byte[0],
                Encoding.UTF8.GetBytes("abd"),
                new Dictionary<Guid, byte[]> { [version] = Abc });
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestIntegrity.Assess(manifest, damagedWorld));
        }

        [Test]
        public void MissingAndRecursiveChildVersionsRejectBeforePublication()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestIntegrity.Build(Guid.NewGuid(), Abc,
                    new[] { new ModuleArchivePayload(Guid.NewGuid(), a,
                        new[] { b }, Abc) }));
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestIntegrity.Build(Guid.NewGuid(), Abc,
                    new[]
                    {
                        new ModuleArchivePayload(Guid.NewGuid(), a,
                            new[] { b }, Abc),
                        new ModuleArchivePayload(Guid.NewGuid(), b,
                            new[] { a }, Abc)
                    }));
        }
    }
}
