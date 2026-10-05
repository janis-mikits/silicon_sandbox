using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Application
{
    public static class OneBitCatalogItemIds
    {
        public const string Wire = "builtin.wire";
        public const string WorldClockLink = "builtin.world_clock_link";
    }

    public sealed class OneBitPlayerInventory
    {
        private readonly SavedInventoryItem[] slots;
        private readonly IReadOnlyList<SavedInventoryItem> readOnlySlots;

        public IReadOnlyList<SavedInventoryItem> Slots => readOnlySlots;
        public int SelectedHotbarSlot { get; private set; }
        public SavedInventoryItem SelectedItem => slots[SelectedHotbarSlot];

        public OneBitPlayerInventory(IEnumerable<SavedInventoryItem> savedSlots,
            int selectedHotbarSlot)
        {
            if (savedSlots == null) throw new ArgumentNullException(nameof(savedSlots));
            slots = new List<SavedInventoryItem>(savedSlots).ToArray();
            if (slots.Length != 36 || selectedHotbarSlot < 0 ||
                selectedHotbarSlot > 8)
                throw new ArgumentException("Inventory requires 36 slots and a hotbar choice.");
            readOnlySlots = Array.AsReadOnly(slots);
            SelectedHotbarSlot = selectedHotbarSlot;
        }

        public static OneBitPlayerInventory NewFreeplay()
        {
            var slots = new SavedInventoryItem[36];
            slots[0] = SavedInventoryItem.Catalog(BuiltInPinCatalog.Source);
            slots[1] = SavedInventoryItem.Catalog(OneBitCatalogItemIds.Wire);
            slots[2] = SavedInventoryItem.Catalog(BuiltInPinCatalog.And);
            slots[3] = SavedInventoryItem.Catalog(BuiltInPinCatalog.SrFlipFlop);
            slots[4] = SavedInventoryItem.Catalog(
                OneBitCatalogItemIds.WorldClockLink);
            return new OneBitPlayerInventory(slots, 0);
        }

        public void SelectHotbar(int index)
        {
            if (index < 0 || index > 8)
                throw new ArgumentOutOfRangeException(nameof(index));
            SelectedHotbarSlot = index;
        }

        public int AddModuleVersion(Guid familyId, Guid versionId)
        {
            var item = SavedInventoryItem.Module(familyId, versionId);
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] != null &&
                    slots[i].Kind == SavedInventoryKind.ModuleVersion &&
                    slots[i].FamilyId == familyId &&
                    slots[i].VersionId == versionId)
                    return i;
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] == null)
                { slots[i] = item; return i; }
            throw new InvalidOperationException("No free inventory slot for module.");
        }
    }
}
