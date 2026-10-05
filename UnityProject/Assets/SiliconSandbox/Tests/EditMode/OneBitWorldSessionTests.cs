using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitWorldSessionTests
    {
        [Test]
        public void SafePlacementPublishesOneRevisionAndKeepsUnrelatedSourceState()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(20, 20, 10)));
            session.PlaceComponent(BuiltInPinCatalog.Source, new GridCell(3, 1, 3),
                GridOrientation.Default, LogicBit.One, true);
            var sourceId = session.Design.Components[0].Id;
            Assert.That(session.Revision, Is.EqualTo(1UL));
            Assert.That(session.Scheduler.IsPaused, Is.True);
            Assert.That(session.Circuit.Source(sourceId).IsOn, Is.True);
            session.Circuit.SetSourceOn(sourceId, false);
            session.Circuit.AdvanceToSettled();

            session.PlaceComponent(BuiltInPinCatalog.And, new GridCell(7, 1, 3),
                GridOrientation.Default);
            Assert.That(session.Revision, Is.EqualTo(2UL));
            Assert.That(session.Design.Components.Count, Is.EqualTo(2));
            Assert.That(session.Circuit.Source(sourceId).IsOn, Is.False,
                "An unrelated placement does not reset a live source.");
        }

        [Test]
        public void RejectedPlacementLeavesRevisionDesignAndSimulationUnchanged()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(20, 20, 10)));
            session.PlaceComponent(BuiltInPinCatalog.Source, new GridCell(3, 1, 3),
                GridOrientation.Default);
            var before = session.Design;
            var sourceId = before.Components[0].Id;
            session.Circuit.SetSourceOn(sourceId, true);
            session.Circuit.AdvanceToSettled();
            Assert.Throws<ArgumentException>(() => session.PlaceComponent(
                BuiltInPinCatalog.And, new GridCell(3, 1, 3), GridOrientation.Default));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(1UL));
            Assert.That(session.Circuit.Source(sourceId).IsOn, Is.True);
            Assert.That(session.Scheduler.IsPaused, Is.True,
                "The safe pause may remain after a rejected edit.");
        }
    }
}
