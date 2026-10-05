using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldArchiveContainerTests
    {
        [Test]
        public void WritesOnlyCanonicalEntriesAndReturnsTheirExactBytes()
        {
            var versionId = Guid.NewGuid();
            var manifest = Encoding.UTF8.GetBytes("{\"formatVersion\":1}");
            var world = Encoding.UTF8.GetBytes("{\"worldId\":\"test\"}");
            var module = Encoding.UTF8.GetBytes("{\"versionId\":\"test\"}");
            var records = new WorldArchiveRecords(manifest, world,
                new Dictionary<Guid, byte[]> { [versionId] = module });
            using (var stream = new MemoryStream())
            {
                WorldArchiveContainer.Write(stream, records);
                Assert.That(stream.CanRead, Is.True);
                stream.Position = 0;
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    Assert.That(zip.Entries.Count, Is.EqualTo(3));
                    Assert.That(zip.Entries[0].FullName, Is.EqualTo("manifest.json"));
                    Assert.That(zip.Entries[1].FullName, Is.EqualTo("world.json"));
                    Assert.That(zip.Entries[2].FullName,
                        Is.EqualTo("modules/" + versionId.ToString("D") + ".json"));
                }
                stream.Position = 0;
                var opened = WorldArchiveContainer.Read(stream);
                Assert.That(opened.ManifestJson, Is.EqualTo(manifest));
                Assert.That(opened.WorldJson, Is.EqualTo(world));
                Assert.That(opened.Modules[versionId], Is.EqualTo(module));
            }
        }

        [Test]
        public void DuplicateTraversalAndMissingRequiredPathsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => ReadCrafted(
                "manifest.json", "world.json", "world.json"));
            Assert.Throws<InvalidDataException>(() => ReadCrafted(
                "manifest.json", "world.json", "modules/../world.json"));
            Assert.Throws<InvalidDataException>(() => ReadCrafted("manifest.json"));
        }

        [Test]
        public void ExpandedLimitsRejectBeforeReturningAnyRecord()
        {
            var records = new WorldArchiveRecords(new byte[12], new byte[2],
                new Dictionary<Guid, byte[]>());
            Assert.Throws<InvalidDataException>(() =>
                WorldArchiveContainer.Write(new MemoryStream(), records,
                    new WorldArchiveLimits(100, 10, 100, 3)));
            using (var stream = new MemoryStream())
            {
                WorldArchiveContainer.Write(stream, records);
                stream.Position = 0;
                Assert.Throws<InvalidDataException>(() =>
                    WorldArchiveContainer.Read(stream,
                        new WorldArchiveLimits(10000, 10, 100, 3)));
            }
        }

        private static void ReadCrafted(params string[] paths)
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                    foreach (var path in paths)
                    {
                        var entry = zip.CreateEntry(path);
                        using (var writer = new StreamWriter(entry.Open()))
                            writer.Write("{}");
                    }
                stream.Position = 0;
                WorldArchiveContainer.Read(stream);
            }
        }
    }
}
