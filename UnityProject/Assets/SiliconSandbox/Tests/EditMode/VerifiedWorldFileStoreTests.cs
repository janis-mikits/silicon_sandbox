using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class VerifiedWorldFileStoreTests
    {
        [Test]
        public void RejectedCandidateKeepsCurrentAndVerifiedReplacementKeepsPrevious()
        {
            var root = Environment.GetEnvironmentVariable(
                "SILICON_SANDBOX_TEST_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty,
                "Verification wrapper must supply a repository-local test root.");
            var directory = Path.Combine(root,
                "atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var current = Path.Combine(directory, "world.siliconsandbox");
                var original = Encoding.UTF8.GetBytes("last-good-world");
                File.WriteAllBytes(current, original);
                var archive = ArchiveBytes();

                Assert.Throws<InvalidDataException>(() =>
                    VerifiedWorldFileStore.Save(current, archive,
                        _ => throw new InvalidDataException("candidate rejected")));
                Assert.That(File.ReadAllBytes(current), Is.EqualTo(original));
                Assert.That(File.Exists(current + ".previous"), Is.False);

                VerifiedWorldFileStore.Save(current, archive,
                    stream => WorldArchiveContainer.Read(stream));
                Assert.That(File.ReadAllBytes(current), Is.EqualTo(archive));
                Assert.That(File.ReadAllBytes(current + ".previous"),
                    Is.EqualTo(original));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static byte[] ArchiveBytes()
        {
            using (var stream = new MemoryStream())
            {
                WorldArchiveContainer.Write(stream, new WorldArchiveRecords(
                    Encoding.UTF8.GetBytes("{}"),
                    Encoding.UTF8.GetBytes("{}"),
                    new Dictionary<Guid, byte[]>()));
                return stream.ToArray();
            }
        }
    }
}
