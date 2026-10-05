using System;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Application
{
    public static class OneBitWorldStyleIds
    {
        public const string SmoothSandstone = "builtin.smooth_sandstone";
        public const string WoodSandboxWall = "builtin.wood_sandbox_wall";
    }

    // The authored world, fixed world identity/settings, and player inventory
    // are the source for a coherent save. Live signal state stays in Session.
    public sealed class OneBitWorldContext
    {
        public Guid WorldId { get; }
        public string WorldName { get; }
        public string FloorMaterialId { get; }
        public string WallStyleId { get; }
        public OneBitWorldSession Session { get; }
        public OneBitPlayerInventory Inventory { get; }

        private OneBitWorldContext(Guid worldId, string worldName,
            string floorMaterialId, string wallStyleId,
            OneBitWorldSession session, OneBitPlayerInventory inventory)
        {
            if (worldId == Guid.Empty || string.IsNullOrWhiteSpace(worldName) ||
                string.IsNullOrWhiteSpace(floorMaterialId) ||
                string.IsNullOrWhiteSpace(wallStyleId) || session == null ||
                inventory == null)
                throw new ArgumentException("Incomplete authored world context.");
            WorldId = worldId;
            WorldName = worldName;
            FloorMaterialId = floorMaterialId;
            WallStyleId = wallStyleId;
            Session = session;
            Inventory = inventory;
        }

        public static OneBitWorldContext NewFreeplay(string worldName,
            WorldBounds bounds)
        {
            return new OneBitWorldContext(Guid.NewGuid(), worldName,
                OneBitWorldStyleIds.SmoothSandstone,
                OneBitWorldStyleIds.WoodSandboxWall,
                new OneBitWorldSession(OneBitWorldDesign.Empty(bounds)),
                OneBitPlayerInventory.NewFreeplay());
        }

        public static OneBitWorldContext Open(WorldSaveSnapshot saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            return new OneBitWorldContext(saved.WorldId, saved.WorldName,
                saved.FloorMaterialId, saved.WallStyleId,
                OneBitSaveBoundary.Open(saved),
                new OneBitPlayerInventory(saved.InventorySlots,
                    saved.SelectedHotbarSlot));
        }

        public WorldSaveSnapshot Capture(SavedPlayerPose player) =>
            OneBitSaveBoundary.Capture(Session, WorldId, WorldName,
                FloorMaterialId, WallStyleId, player, Inventory.Slots,
                Inventory.SelectedHotbarSlot);
    }
}
