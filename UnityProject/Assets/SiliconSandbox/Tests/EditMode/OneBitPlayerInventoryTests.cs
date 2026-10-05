using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitPlayerInventoryTests
    {
        [Test]
        public void NewFreeplayHasExactCatalogReferencesAndThirtySixSaveSlots()
        {
            var inventory = OneBitPlayerInventory.NewFreeplay();
            Assert.That(inventory.Slots.Count, Is.EqualTo(36));
            Assert.That(inventory.Slots[0].ItemTypeId,
                Is.EqualTo(BuiltInPinCatalog.Source));
            Assert.That(inventory.Slots[1].ItemTypeId,
                Is.EqualTo("builtin.wire"));
            Assert.That(inventory.Slots[2].ItemTypeId,
                Is.EqualTo(BuiltInPinCatalog.And));
            Assert.That(inventory.Slots[3].ItemTypeId,
                Is.EqualTo(BuiltInPinCatalog.SrFlipFlop));
            Assert.That(inventory.Slots[4].ItemTypeId,
                Is.EqualTo("builtin.world_clock_link"));
            Assert.That(inventory.Slots[5], Is.Null);
            inventory.SelectHotbar(3);
            var reopened = new OneBitPlayerInventory(inventory.Slots,
                inventory.SelectedHotbarSlot);
            Assert.That(reopened.SelectedHotbarSlot, Is.EqualTo(3));
            Assert.That(reopened.SelectedItem.ItemTypeId,
                Is.EqualTo(BuiltInPinCatalog.SrFlipFlop));
        }

        [Test]
        public void FixedModuleReferenceOccupiesFirstFreeSlotWithoutDuplication()
        {
            var inventory = OneBitPlayerInventory.NewFreeplay();
            var family = Guid.NewGuid();
            var version = Guid.NewGuid();
            var slot = inventory.AddModuleVersion(family, version);
            Assert.That(slot, Is.EqualTo(5));
            Assert.That(inventory.AddModuleVersion(family, version),
                Is.EqualTo(5));
            Assert.That(inventory.Slots[5].Kind,
                Is.EqualTo(SavedInventoryKind.ModuleVersion));
            Assert.That(inventory.Slots[5].FamilyId, Is.EqualTo(family));
            Assert.That(inventory.Slots[5].VersionId, Is.EqualTo(version));
            Assert.That(inventory.Slots[6], Is.Null);
        }
    }
}
