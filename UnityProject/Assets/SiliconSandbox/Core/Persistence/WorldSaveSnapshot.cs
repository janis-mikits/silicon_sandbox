using System;
using System.Collections.Generic;
using System.Globalization;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Persistence
{
    public sealed class SavedPlayerPose
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double LookX { get; }
        public double LookY { get; }
        public double LookZ { get; }

        public SavedPlayerPose(double x, double y, double z,
            double lookX, double lookY, double lookZ)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z) ||
                !Finite(lookX) || !Finite(lookY) || !Finite(lookZ) ||
                Math.Abs(lookX * lookX + lookY * lookY + lookZ * lookZ - 1d)
                    > 0.000001d)
                throw new ArgumentException("Player pose must be finite with a normalized view.");
            X = x; Y = y; Z = z;
            LookX = lookX; LookY = lookY; LookZ = lookZ;
        }

        private static bool Finite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public enum SavedInventoryKind { CatalogItem, ModuleVersion }

    public sealed class SavedInventoryItem
    {
        public SavedInventoryKind Kind { get; }
        public string ItemTypeId { get; }
        public Guid FamilyId { get; }
        public Guid VersionId { get; }

        private SavedInventoryItem(SavedInventoryKind kind, string itemTypeId,
            Guid familyId, Guid versionId)
        {
            Kind = kind; ItemTypeId = itemTypeId;
            FamilyId = familyId; VersionId = versionId;
        }

        public static SavedInventoryItem Catalog(string itemTypeId)
        {
            if (string.IsNullOrWhiteSpace(itemTypeId))
                throw new ArgumentException("Catalog item ID is required.");
            return new SavedInventoryItem(SavedInventoryKind.CatalogItem,
                itemTypeId, Guid.Empty, Guid.Empty);
        }

        public static SavedInventoryItem Module(Guid familyId, Guid versionId)
        {
            if (familyId == Guid.Empty || versionId == Guid.Empty)
                throw new ArgumentException("Exact module inventory identity is required.");
            return new SavedInventoryItem(SavedInventoryKind.ModuleVersion,
                null, familyId, versionId);
        }
    }

    // A coherent authored save value. Ordinary simulation state is absent by
    // construction and is rebuilt from initial rules on Open.
    public sealed class WorldSaveSnapshot
    {
        public Guid WorldId { get; }
        public string WorldName { get; }
        public string Mode => "freeplay";
        public string FloorMaterialId { get; }
        public int FloorThicknessCells => 1;
        public string WallStyleId { get; }
        public string WorldClockFrequencyHz { get; }
        public SavedPlayerPose Player { get; }
        public IReadOnlyList<SavedInventoryItem> InventorySlots { get; }
        public int SelectedHotbarSlot { get; }
        public OneBitWorldDesign Design { get; }
        public IReadOnlyDictionary<Guid, OneBitModuleVersion> ModuleVersions { get; }

        public WorldSaveSnapshot(Guid worldId, string worldName,
            string floorMaterialId, string wallStyleId,
            string worldClockFrequencyHz, SavedPlayerPose player,
            IEnumerable<SavedInventoryItem> inventorySlots,
            int selectedHotbarSlot, OneBitWorldDesign design,
            IReadOnlyDictionary<Guid, OneBitModuleVersion> moduleVersions)
        {
            if (worldId == Guid.Empty || string.IsNullOrWhiteSpace(worldName) ||
                string.IsNullOrWhiteSpace(floorMaterialId) ||
                string.IsNullOrWhiteSpace(wallStyleId) ||
                worldClockFrequencyHz == null || player == null ||
                inventorySlots == null || design == null || moduleVersions == null ||
                selectedHotbarSlot < 0 || selectedHotbarSlot > 8)
                throw new ArgumentException("Incomplete world save snapshot.");
            if (!decimal.TryParse(worldClockFrequencyHz,
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                    out var hz) || hz < 0.1m || hz > 100m)
                throw new ArgumentException("Invalid first-playable clock frequency.");
            var slots = new List<SavedInventoryItem>(inventorySlots);
            if (slots.Count != 36)
                throw new ArgumentException("Version 1 inventory has exactly 36 slots.");
            var versions = new Dictionary<Guid, OneBitModuleVersion>();
            foreach (var pair in moduleVersions)
            {
                if (pair.Key == Guid.Empty || pair.Value == null ||
                    pair.Key != pair.Value.VersionId)
                    throw new ArgumentException("Inconsistent exact module version.");
                versions.Add(pair.Key, pair.Value);
            }
            WorldId = worldId; WorldName = worldName;
            FloorMaterialId = floorMaterialId; WallStyleId = wallStyleId;
            WorldClockFrequencyHz = worldClockFrequencyHz;
            Player = player; InventorySlots = slots.AsReadOnly();
            SelectedHotbarSlot = selectedHotbarSlot;
            Design = design;
            ModuleVersions = new System.Collections.ObjectModel.ReadOnlyDictionary<
                Guid, OneBitModuleVersion>(versions);
        }
    }
}
