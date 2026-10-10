using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldManifestJsonReaderTests
    {
        private const string WorldId = "11111111-1111-4111-8111-111111111111";
        private static readonly string Valid =
            "{\"formatVersion\":1,\"worldId\":\"" + WorldId +
            "\",\"entries\":[{\"path\":\"world.json\"," +
            "\"uncompressedBytes\":3,\"sha256\":\"" +
            new string('a', 64) + "\"}],\"moduleVersions\":[]}";

        [Test]
        public void HandWrittenManifestLoadsExactTypedFields()
        {
            var result = WorldManifestJsonReader.Read(Bytes(Valid));
            Assert.That(result.WorldId, Is.EqualTo(Guid.Parse(WorldId)));
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].Path, Is.EqualTo("world.json"));
            Assert.That(result.Entries[0].UncompressedBytes, Is.EqualTo(3));
            Assert.That(result.Entries[0].Sha256,
                Is.EqualTo(new string('a', 64)));
            Assert.That(result.ModuleVersions.Count, Is.EqualTo(0));
        }

        [Test]
        public void UnknownAndMissingFieldsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    "\"formatVersion\":1,", "\"formatVersion\":1,\"extra\":0,"))));
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    "\"moduleVersions\":[]", "\"versions\":[]"))));
        }

        [Test]
        public void DuplicatePropertyAndUnsupportedVersionAreRejected()
        {
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    "\"formatVersion\":1,",
                    "\"formatVersion\":1,\"formatVersion\":1,"))));
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    "\"formatVersion\":1", "\"formatVersion\":3"))));
        }

        [Test]
        public void InvalidUtf8OrNoncanonicalIdentityIsRejected()
        {
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(new byte[] { 0xff, 0xfe }));
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    WorldId, "11111111-1111-1111-8111-111111111111"))));
        }

        [Test]
        public void CommentsAndTrailingCommasAreNotVersionOneJson()
        {
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    "\"formatVersion\":1,",
                    "\"formatVersion\":1,/* comment */"))));
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(Valid.Replace(
                    "\"moduleVersions\":[]}",
                    "\"moduleVersions\":[],}"))));
        }

        [Test]
        public void ModulePathCannotDisagreeWithExactVersionId()
        {
            const string versionId = "22222222-2222-4222-8222-222222222222";
            const string familyId = "33333333-3333-4333-8333-333333333333";
            var module = "{\"familyId\":\"" + familyId +
                "\",\"versionId\":\"" + versionId +
                "\",\"path\":\"modules/wrong.json\"," +
                "\"childVersionIds\":[]}";
            var actual = Valid.Replace("\"moduleVersions\":[]",
                "\"moduleVersions\":[" + module + "]");
            Assert.Throws<InvalidDataException>(() =>
                WorldManifestJsonReader.Read(Bytes(actual)));
        }

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    }
}
