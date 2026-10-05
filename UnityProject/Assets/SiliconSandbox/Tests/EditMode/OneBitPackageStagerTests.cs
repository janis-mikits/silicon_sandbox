using System;
using System.IO;
using System.Text;
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
        public void PublishingPreparedPackagePreservesLiveSimulationAndClock()
        {
            var context = OneBitWorldContext.NewFreeplay("Package test",
                new WorldBounds(16, 16, 8));
            context.Session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(5, 1, 5), GridOrientation.Default);
            context.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(10, 1, 10), GridOrientation.Default);
            var source = context.Session.Design.Components[1];
            var draft = context.Session.PreviewPackage(new CellRegion(
                new GridCell(5, 1, 5), new GridCell(5, 1, 5)), "AND copy");
            var staged = OneBitPackageStager.Prepare(context, draft,
                Guid.NewGuid(), Pose);
            context.Session.ToggleSource(source.Id);
            context.Session.Scheduler.ResumeSimulation();
            context.Session.Scheduler.StartClock();
            context.Session.Scheduler.AdvanceUntil(
                context.Session.Scheduler.NextClockEdge.Value);
            var beforeSession = context.Session;
            var beforeTime = beforeSession.Scheduler.Now;
            var beforeRevision = beforeSession.Revision;

            var testParent = Environment.GetEnvironmentVariable(
                "SILICON_SANDBOX_TEST_ROOT");
            Assert.That(testParent, Is.Not.Null.And.Not.Empty);
            var root = Path.Combine(testParent,
                "package-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                // Opaque bytes here only exercise the publication receipt.
                // Strict V1 semantic validation is tested with the codec.
                var bytes = Encoding.UTF8.GetBytes("test-payload");
                Action<Stream> verify = stream =>
                {
                    if (stream.Length != bytes.Length)
                        throw new InvalidDataException("Incomplete test payload.");
                };
                var receipt = AtomicPackageFilePublisher.Publish(root,
                    context.WorldId, staged.Version.VersionId,
                    bytes, bytes, bytes, verify, verify, verify);
                context.ApplyDurablyPublishedPackage(staged, receipt);
            }
            finally { Directory.Delete(root, true); }

            Assert.That(context.Session, Is.SameAs(beforeSession));
            Assert.That(context.Session.Revision, Is.EqualTo(beforeRevision));
            Assert.That(context.Session.Scheduler.Now, Is.EqualTo(beforeTime));
            Assert.That(context.Session.Scheduler.ClockRunning, Is.True);
            Assert.That(context.Session.Circuit.Source(source.Id).IsOn, Is.True);
            Assert.That(context.Session.HasModuleVersion(staged.Version.VersionId),
                Is.True);
            Assert.That(context.Inventory.Slots[staged.InventorySlot].VersionId,
                Is.EqualTo(staged.Version.VersionId));
        }

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
