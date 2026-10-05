using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldV1JsonWriterTests
    {
        [Test]
        public void EmptyWorldMatchesHandWrittenVersionOneRecord()
        {
            var id = Guid.Parse("11111111-1111-4111-8111-111111111111");
            var snapshot = Snapshot(id, "A \"world\"",
                OneBitWorldDesign.Empty(new WorldBounds(8, 9, 4)));
            var bytes = WorldV1JsonWriter.WriteWorld(snapshot);
            var actual = Encoding.UTF8.GetString(bytes);
            var expected =
                "{\"worldId\":\"11111111-1111-4111-8111-111111111111\"," +
                "\"worldName\":\"A \\\"world\\\"\",\"mode\":\"freeplay\"," +
                "\"worldSettings\":{\"widthCells\":8,\"lengthCells\":9," +
                "\"heightCells\":4,\"floorMaterialId\":" +
                "\"builtin.smooth_sandstone\",\"floorThicknessCells\":1," +
                "\"wallStyleId\":\"builtin.wood_sandbox_wall\"," +
                "\"worldClockFrequencyHz\":\"10\"}," +
                "\"player\":{\"position\":[1.25,2,3.75]," +
                "\"lookDirection\":[0,0,1]}," +
                "\"inventory\":{\"slots\":[" +
                string.Join(",", new string[36].Fill("null")) +
                "],\"selectedHotbarSlot\":0}," +
                "\"design\":{\"objects\":[],\"connectors\":[],\"joins\":[]}}";
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(bytes[0], Is.EqualTo((byte)'{'));
        }

        [Test]
        public void SourceRecordContainsAuthoredConfigurationAndVersionedPinSnapshot()
        {
            var design = OneBitWorldEdits.PlaceComponent(
                OneBitWorldDesign.Empty(new WorldBounds(8, 8, 4)),
                BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default, LogicBit.Z, true);
            var source = design.Components[0];
            var json = Encoding.UTF8.GetString(WorldV1JsonWriter.WriteWorld(
                Snapshot(Guid.NewGuid(), "Source", design)));
            Assert.That(json, Does.Contain("\"kind\":\"component\""));
            Assert.That(json, Does.Contain("\"typeVersion\":1"));
            Assert.That(json, Does.Contain(
                "\"configuration\":{\"width\":1," +
                "\"onValueBits\":[\"Z\"],\"initialOn\":true}"));
            Assert.That(json, Does.Contain(
                "\"pinKey\":\"OUT\",\"direction\":\"output\"," +
                "\"width\":1,\"cellOffset\":[0,0,0]," +
                "\"pointQ\":[4,1,1]"));
            Assert.That(json, Does.Contain(source.PinIds["OUT"].ToString("D")));
            Assert.That(json, Does.Contain(
                "\"appearance\":{\"styleId\":\"builtin.component.default\"}"));
            Assert.That(json, Does.Not.Contain("simulationTime"));
            Assert.That(json, Does.Not.Contain("currentOn"));
        }

        [Test]
        public void ModulePortMapsBitZeroToTypedAuthoredEndpoint()
        {
            var design = OneBitWorldEdits.PlaceComponent(
                OneBitWorldDesign.Empty(new WorldBounds(5, 5, 4)),
                BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(design,
                new CellRegion(new GridCell(2, 1, 2),
                    new GridCell(2, 1, 2)));
            var copied = snapshot.Components[0];
            var version = OneBitModuleVersionFactory.Create(snapshot,
                Guid.NewGuid(), "Source module", new[]
                {
                    new OneBitPortChoice("OUT", OneBitPortDirection.Output,
                        new GridCell(0, 0, 0), new QuarterPoint(4, 1, 1),
                        JoinMember.ComponentPin(copied.Id, copied.PinIds["OUT"]))
                });
            var json = Encoding.UTF8.GetString(
                WorldV1JsonWriter.WriteModule(version));
            Assert.That(json, Does.Contain(
                "\"bitOrder\":\"lsb0\",\"localCell\":[0,0,0]," +
                "\"pointQ\":[4,1,1],\"bitTargets\":[{" +
                "\"portBitIndex\":0,\"target\":{" +
                "\"targetKind\":\"componentPin\""));
            Assert.That(json, Does.Contain(
                "\"pinId\":\"" + copied.PinIds["OUT"].ToString("D") + "\""));
            Assert.That(json, Does.Contain("\"childVersionIds\":[]"));
            Assert.That(json, Does.Not.Contain("currentOn"));
        }

        private static WorldSaveSnapshot Snapshot(Guid id, string name,
            OneBitWorldDesign design) => new WorldSaveSnapshot(id, name,
                "builtin.smooth_sandstone", "builtin.wood_sandbox_wall",
                "10", new SavedPlayerPose(1.25, 2, 3.75, 0, 0, 1),
                new SavedInventoryItem[36], 0, design,
                new Dictionary<Guid, OneBitModuleVersion>());
    }

    internal static class ArrayFixtureExtensions
    {
        public static T[] Fill<T>(this T[] array, T value)
        {
            for (var i = 0; i < array.Length; i++) array[i] = value;
            return array;
        }
    }
}
