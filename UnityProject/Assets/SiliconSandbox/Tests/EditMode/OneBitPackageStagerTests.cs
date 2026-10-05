using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitPackageStagerTests
    {
        private static readonly SavedPlayerPose Pose =
            new SavedPlayerPose(4, 2, 4, 0, 0, 1);

        [Test]
        public void StagingAddsExactLibraryReferenceOnlyToProposedWorld()
        {
            var context = OneBitWorldContext.NewFreeplay("Package test",
                new WorldBounds(16, 16, 8));
            context.Session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(5, 1, 5), GridOrientation.Default);
            var beforeDesign = context.Session.Design;
            var beforeInventory = context.Inventory.Slots[5];
            var draft = context.Session.PreviewPackage(new CellRegion(
                new GridCell(5, 1, 5), new GridCell(5, 1, 5)), "AND copy");
            var family = Guid.NewGuid();

            var staged = OneBitPackageStager.Prepare(context, draft, family,
                Pose);

            Assert.That(context.Session.Design, Is.SameAs(beforeDesign));
            Assert.That(context.Inventory.Slots[5], Is.SameAs(beforeInventory));
            Assert.That(context.Session.ModuleVersions.ContainsKey(
                staged.Version.VersionId), Is.False);
            Assert.That(staged.SavedWorld.Design, Is.SameAs(beforeDesign));
            Assert.That(staged.SavedWorld.InventorySlots[staged.InventorySlot]
                .VersionId, Is.EqualTo(staged.Version.VersionId));
            Assert.That(staged.SavedWorld.InventorySlots[staged.InventorySlot]
                .FamilyId, Is.EqualTo(family));
            Assert.That(staged.SavedWorld.ModuleVersions[
                staged.Version.VersionId], Is.SameAs(staged.Version));
            Assert.That(staged.Version.Components[0].Id,
                Is.Not.EqualTo(beforeDesign.Components[0].Id),
                "The fixed blueprint is an independent authored copy.");
        }

        [Test]
        public void StaleOrInvalidDraftPublishesNothing()
        {
            var context = OneBitWorldContext.NewFreeplay("Package test",
                new WorldBounds(16, 16, 8));
            context.Session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(5, 1, 5), GridOrientation.Default);
            var draft = context.Session.PreviewPackage(new CellRegion(
                new GridCell(5, 1, 5), new GridCell(5, 1, 5)), "AND copy");
            context.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(10, 1, 10), GridOrientation.Default);
            Assert.Throws<InvalidOperationException>(() =>
                OneBitPackageStager.Prepare(context, draft, Guid.NewGuid(),
                    Pose));
            Assert.That(context.Inventory.Slots[5], Is.Null);
            Assert.That(context.Session.ModuleVersions.Count, Is.EqualTo(0));
        }
    }
}
