using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldV1JsonReaderTests
    {
        private const string WorldId = "11111111-1111-4111-8111-111111111111";
        private static readonly IReadOnlyDictionary<Guid, OneBitModuleVersion> EmptyVersions =
            new Dictionary<Guid, OneBitModuleVersion>();
        private static readonly string EmptyWorld =
            "{\"worldId\":\"" + WorldId + "\",\"worldName\":\"Hand record\"," +
            "\"mode\":\"freeplay\",\"worldSettings\":{" +
            "\"widthCells\":8,\"lengthCells\":9,\"heightCells\":4," +
            "\"floorMaterialId\":\"builtin.smooth_sandstone\"," +
            "\"floorThicknessCells\":1," +
            "\"wallStyleId\":\"builtin.wood_sandbox_wall\"," +
            "\"worldClockFrequencyHz\":\"10\"}," +
            "\"player\":{\"position\":[1.25,2,3.75]," +
            "\"lookDirection\":[0,0,1]}," +
            "\"inventory\":{\"slots\":[" +
            string.Join(",", new string[36].Fill("null")) +
            "],\"selectedHotbarSlot\":0}," +
            "\"design\":{\"objects\":[],\"connectors\":[],\"joins\":[]}}";

        [Test]
        public void HandWrittenEmptyWorldLoadsExactAuthoredFields()
        {
            var world = WorldV1JsonReader.ReadWorld(Bytes(EmptyWorld), EmptyVersions);
            Assert.That(world.WorldId, Is.EqualTo(Guid.Parse(WorldId)));
            Assert.That(world.WorldName, Is.EqualTo("Hand record"));
            Assert.That(world.Design.Bounds.WidthCells, Is.EqualTo(8));
            Assert.That(world.Design.Bounds.LengthCells, Is.EqualTo(9));
            Assert.That(world.Design.Bounds.HeightCells, Is.EqualTo(4));
            Assert.That(world.Player.X, Is.EqualTo(1.25));
            Assert.That(world.WorldClockFrequencyHz, Is.EqualTo("10"));
            Assert.That(world.Design.Components.Count, Is.Zero);
        }

        [Test]
        public void UnknownModeAndInventoryFieldCannotBeDiscarded()
        {
            Assert.Throws<InvalidDataException>(() => WorldV1JsonReader.ReadWorld(
                Bytes(EmptyWorld.Replace("\"mode\":\"freeplay\"",
                    "\"mode\":\"education\"")), EmptyVersions));
            Assert.Throws<InvalidDataException>(() => WorldV1JsonReader.ReadWorld(
                Bytes(EmptyWorld.Replace("\"selectedHotbarSlot\":0",
                    "\"selectedHotbarSlot\":0,\"transientQ\":1")), EmptyVersions));
        }

        [Test]
        public void SourceAndVisibleOpenConnectorKeepExactSavedIds()
        {
            var design = OneBitWorldEdits.PlaceComponent(
                OneBitWorldDesign.Empty(new WorldBounds(10, 10, 4)),
                BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default, LogicBit.X, true);
            var source = design.Components[0];
            var session = new OneBitWorldSession(design);
            session.PlaceWireStub(JoinMember.ComponentPin(source.Id,
                source.PinIds["OUT"]), new GridCell(4, 1, 2));
            var route = session.Design.Topology.Connectors[0];
            var snapshot = new WorldSaveSnapshot(Guid.NewGuid(), "Saved",
                "builtin.smooth_sandstone", "builtin.wood_sandbox_wall",
                "10", new SavedPlayerPose(1, 2, 3, 0, 0, 1),
                new SavedInventoryItem[36], 0, session.Design, EmptyVersions);
            var reopened = WorldV1JsonReader.ReadWorld(
                WorldV1JsonWriter.WriteWorld(snapshot), EmptyVersions);
            Assert.That(reopened.Design.Components[0].Id, Is.EqualTo(source.Id));
            Assert.That(reopened.Design.Components[0].PinIds["OUT"],
                Is.EqualTo(source.PinIds["OUT"]));
            Assert.That(reopened.Design.Components[0].SourceOnValue,
                Is.EqualTo(LogicBit.X));
            Assert.That(reopened.Design.Components[0].SourceInitialOn, Is.True);
            Assert.That(reopened.Design.Topology.Connectors[0].Id, Is.EqualTo(route.Id));
            Assert.That(reopened.Design.Topology.Connectors[0].Nodes[0].Id,
                Is.EqualTo(route.Nodes[0].Id));
            Assert.That(reopened.Design.Topology.Connectors[0].Spans[0].Id,
                Is.EqualTo(route.Spans[0].Id));
            Assert.That(reopened.Design.Topology.Joins.Count,
                Is.EqualTo(session.Design.Topology.Joins.Count));
        }

        [Test]
        public void ModulePortRetainsItsInternalAuthoredEndpoint()
        {
            var design = OneBitWorldEdits.PlaceComponent(
                OneBitWorldDesign.Empty(new WorldBounds(5, 5, 4)),
                BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default);
            var preview = OneBitModuleSnapshotBuilder.Preview(design,
                new CellRegion(new GridCell(2, 1, 2),
                    new GridCell(2, 1, 2)));
            var inside = preview.Components[0];
            var original = OneBitModuleVersionFactory.Create(preview,
                Guid.NewGuid(), "Output", new[]
                {
                    new OneBitPortChoice("OUT", OneBitPortDirection.Output,
                        new GridCell(0, 0, 0), new QuarterPoint(4, 1, 1),
                        JoinMember.ComponentPin(inside.Id, inside.PinIds["OUT"]))
                });
            var bytes = WorldV1JsonWriter.WriteModule(original);
            var loaded = WorldV1JsonReader.ReadModule(bytes);
            Assert.That(loaded.FamilyId, Is.EqualTo(original.FamilyId));
            Assert.That(loaded.VersionId, Is.EqualTo(original.VersionId));
            Assert.That(loaded.Components[0].AnchorCell,
                Is.EqualTo(new GridCell(0, 0, 0)));
            Assert.That(loaded.Ports[0].BitZeroTarget,
                Is.EqualTo(JoinMember.ComponentPin(inside.Id,
                    inside.PinIds["OUT"])));
            Assert.Throws<InvalidDataException>(() =>
                WorldV1JsonReader.ReadModule(Bytes(Encoding.UTF8.GetString(bytes)
                    .Replace("\"bitOrder\":\"lsb0\"",
                        "\"bitOrder\":\"msb0\""))));
        }

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    }
}
