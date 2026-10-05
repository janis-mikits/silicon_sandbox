using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class ModuleLibraryIndexJsonTests
    {
        private const string Family = "11111111-1111-4111-8111-111111111111";
        private const string Version = "22222222-2222-4222-8222-222222222222";
        private static readonly string Hash = new string('a', 64);

        [Test]
        public void HandWrittenIndexRetainsExactIdentityAndDigest()
        {
            var json = "{\"formatVersion\":1,\"versions\":[{" +
                "\"familyId\":\"" + Family + "\",\"versionId\":\"" +
                Version + "\",\"name\":\"SR\",\"definitionPath\":" +
                "\"versions/" + Version + ".json\",\"sha256\":\"" +
                Hash + "\",\"archived\":false}]}";
            var loaded = ModuleLibraryIndexJson.Read(Encoding.UTF8.GetBytes(json));
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].FamilyId, Is.EqualTo(Guid.Parse(Family)));
            Assert.That(loaded[0].VersionId, Is.EqualTo(Guid.Parse(Version)));
            Assert.That(loaded[0].Name, Is.EqualTo("SR"));
            Assert.That(loaded[0].Sha256, Is.EqualTo(Hash));
            Assert.That(loaded[0].Archived, Is.False);
            Assert.That(Encoding.UTF8.GetString(ModuleLibraryIndexJson.Write(loaded)),
                Is.EqualTo(json));
        }

        [Test]
        public void UnsafeOrUnknownIndexDataIsRejected()
        {
            var valid = Encoding.UTF8.GetString(ModuleLibraryIndexJson.Write(
                new[] { new LibraryVersionEntry(Guid.Parse(Family),
                    Guid.Parse(Version), "SR", Hash, false) }));
            Assert.Throws<InvalidDataException>(() => ModuleLibraryIndexJson.Read(
                Encoding.UTF8.GetBytes(valid.Replace("versions/" + Version + ".json",
                    "../outside.json"))));
            Assert.Throws<InvalidDataException>(() => ModuleLibraryIndexJson.Read(
                Encoding.UTF8.GetBytes(valid.Replace("\"archived\":false",
                    "\"archived\":false,\"extra\":0"))));
        }
    }
}
