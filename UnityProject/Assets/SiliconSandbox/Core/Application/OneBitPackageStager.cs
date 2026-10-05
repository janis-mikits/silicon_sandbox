using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Application
{
    public sealed class OneBitStagedPackage
    {
        public OneBitModuleVersion Version { get; }
        public WorldSaveSnapshot SavedWorld { get; }
        public int InventorySlot { get; }
        public ulong BaseRevision { get; }

        internal OneBitStagedPackage(OneBitModuleVersion version,
            WorldSaveSnapshot savedWorld, int inventorySlot,
            ulong baseRevision)
        {
            Version = version;
            SavedWorld = savedWorld;
            InventorySlot = inventorySlot;
            BaseRevision = baseRevision;
        }
    }

    // Validate the complete semantic package without publishing a live or
    // durable library/inventory reference. The V1 encoder and atomic file
    // publisher must succeed before a caller may expose this staged result.
    public static class OneBitPackageStager
    {
        public static OneBitStagedPackage Prepare(OneBitWorldContext context,
            OneBitPackageDraft draft, Guid familyId, SavedPlayerPose player)
        {
            if (context == null || draft == null || player == null)
                throw new ArgumentNullException();
            if (draft.BaseRevision != context.Session.Revision)
                throw new InvalidOperationException(
                    "Package draft is stale; preview the current world again.");
            var baseWorld = context.Capture(player);
            var version = draft.BuildCandidate(familyId);
            var inventory = new OneBitPlayerInventory(baseWorld.InventorySlots,
                baseWorld.SelectedHotbarSlot);
            var slot = inventory.AddModuleVersion(version.FamilyId,
                version.VersionId);
            var versions = new Dictionary<Guid, OneBitModuleVersion>(
                baseWorld.ModuleVersions);
            if (versions.ContainsKey(version.VersionId))
                throw new InvalidOperationException(
                    "Package version identity is already in use.");
            versions.Add(version.VersionId, version);
            var stagedWorld = new WorldSaveSnapshot(baseWorld.WorldId,
                baseWorld.WorldName, baseWorld.FloorMaterialId,
                baseWorld.WallStyleId, baseWorld.WorldClockFrequencyHz,
                baseWorld.Player, inventory.Slots,
                inventory.SelectedHotbarSlot, baseWorld.Design, versions);
            return new OneBitStagedPackage(version, stagedWorld, slot,
                draft.BaseRevision);
        }
    }
}
