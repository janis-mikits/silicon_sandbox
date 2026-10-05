using System;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Persistence
{
    // Authored V1 records only. Runtime Q/source state, simulated time, event
    // queue, derived nets, and renderer state have no serialization path here.
    public static class WorldV1JsonWriter
    {
        public static byte[] WriteWorld(WorldSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!WorldManifestIntegrity.IsVersionFour(snapshot.WorldId))
                throw new ArgumentException("V1 world identity must be UUIDv4.");
            WorldModuleClosure.RequiredFor(snapshot);
            var json = new V1JsonWriter();
            json.BeginObject();
            V1DesignJsonWriter.Id(json, "worldId", snapshot.WorldId);
            json.Name("worldName"); json.String(snapshot.WorldName);
            json.Name("mode"); json.String("freeplay");
            json.Name("worldSettings"); json.BeginObject();
            json.Name("widthCells"); json.Integer(snapshot.Design.Bounds.WidthCells);
            json.Name("lengthCells"); json.Integer(snapshot.Design.Bounds.LengthCells);
            json.Name("heightCells"); json.Integer(snapshot.Design.Bounds.HeightCells);
            json.Name("floorMaterialId"); json.String(snapshot.FloorMaterialId);
            json.Name("floorThicknessCells"); json.Integer(1);
            json.Name("wallStyleId"); json.String(snapshot.WallStyleId);
            json.Name("worldClockFrequencyHz");
            json.String(snapshot.WorldClockFrequencyHz);
            json.EndObject();
            json.Name("player"); json.BeginObject();
            json.Name("position"); json.BeginArray();
            json.Real(snapshot.Player.X); json.Real(snapshot.Player.Y);
            json.Real(snapshot.Player.Z); json.EndArray();
            json.Name("lookDirection"); json.BeginArray();
            json.Real(snapshot.Player.LookX); json.Real(snapshot.Player.LookY);
            json.Real(snapshot.Player.LookZ); json.EndArray();
            json.EndObject();
            json.Name("inventory"); json.BeginObject();
            json.Name("slots"); json.BeginArray();
            foreach (var item in snapshot.InventorySlots)
                WriteInventoryItem(json, item);
            json.EndArray();
            json.Name("selectedHotbarSlot");
            json.Integer(snapshot.SelectedHotbarSlot);
            json.EndObject();
            json.Name("design");
            V1DesignJsonWriter.Write(json, snapshot.Design.Components,
                snapshot.Design.Modules, snapshot.Design.Topology);
            json.EndObject();
            return json.ToUtf8();
        }

        public static byte[] WriteModule(OneBitModuleVersion version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            if (!WorldManifestIntegrity.IsVersionFour(version.FamilyId) ||
                !WorldManifestIntegrity.IsVersionFour(version.VersionId))
                throw new ArgumentException("V1 module identities must be UUIDv4.");
            var json = new V1JsonWriter();
            json.BeginObject();
            V1DesignJsonWriter.Id(json, "familyId", version.FamilyId);
            V1DesignJsonWriter.Id(json, "versionId", version.VersionId);
            json.Name("name"); json.String(version.Name);
            json.Name("sizeCells");
            V1DesignJsonWriter.Cell(json, version.SizeCells);
            json.Name("ports"); json.BeginArray();
            foreach (var port in version.Ports)
            {
                json.BeginObject();
                V1DesignJsonWriter.Id(json, "id", port.Id);
                json.Name("name"); json.String(port.Name);
                json.Name("direction");
                json.String(V1DesignJsonWriter.Direction(port.Direction));
                json.Name("width"); json.Integer(1);
                json.Name("bitOrder"); json.String("lsb0");
                json.Name("localCell");
                V1DesignJsonWriter.Cell(json, port.LocalCell);
                json.Name("pointQ");
                V1DesignJsonWriter.Point(json, port.PointQ);
                json.Name("bitTargets"); json.BeginArray();
                json.BeginObject();
                json.Name("portBitIndex"); json.Integer(0);
                json.Name("target");
                V1DesignJsonWriter.Endpoint(json, port.BitZeroTarget);
                json.EndObject();
                json.EndArray();
                json.EndObject();
            }
            json.EndArray();
            json.Name("childVersionIds"); json.BeginArray();
            foreach (var child in version.ChildVersionIds)
                json.String(child.ToString("D"));
            json.EndArray();
            json.Name("design");
            V1DesignJsonWriter.Write(json, version.Components,
                Array.Empty<PlacedOneBitModuleInstance>(), version.Topology);
            json.EndObject();
            return json.ToUtf8();
        }

        private static void WriteInventoryItem(V1JsonWriter json,
            SavedInventoryItem item)
        {
            if (item == null) { json.Null(); return; }
            json.BeginObject();
            if (item.Kind == SavedInventoryKind.CatalogItem)
            {
                json.Name("kind"); json.String("catalogItem");
                json.Name("itemTypeId"); json.String(item.ItemTypeId);
            }
            else if (item.Kind == SavedInventoryKind.ModuleVersion)
            {
                json.Name("kind"); json.String("moduleVersion");
                V1DesignJsonWriter.Id(json, "familyId", item.FamilyId);
                V1DesignJsonWriter.Id(json, "versionId", item.VersionId);
            }
            else throw new ArgumentException("Unsupported V1 inventory item.");
            json.EndObject();
        }
    }
}
