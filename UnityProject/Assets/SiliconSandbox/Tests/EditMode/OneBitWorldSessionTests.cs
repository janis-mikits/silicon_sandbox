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

        [Test]
        public void SourceConfigurationIsAuthoredWhileCurrentOnAndStorageSurvive()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(20, 20, 10)));
            session.PlaceComponent(BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop, new GridCell(5, 1, 2),
                GridOrientation.Default, srInitialQ: LogicBit.One);
            var source = session.Design.Components[0];
            var sr = session.Design.Components[1];
            session.Circuit.SetSourceOn(source.Id, true);
            session.Circuit.AdvanceToSettled();
            session.ConfigureSource(source.Id, LogicBit.Z, false);

            var output = JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);
            Assert.That(session.Design.Components[0].SourceOnValue, Is.EqualTo(LogicBit.Z));
            Assert.That(session.Design.Components[0].SourceInitialOn, Is.False);
            Assert.That(session.Circuit.Source(source.Id).IsOn, Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.Z), "An active source configured as Z releases its net.");
            Assert.That(session.Circuit.Storage(sr.Id).Q, Is.EqualTo(LogicBit.One));
            Assert.That(session.Revision, Is.EqualTo(3UL));

            session.Scheduler.ResetSimulation();
            Assert.That(session.Circuit.Source(source.Id).IsOn, Is.False);
            Assert.That(session.Circuit.Source(source.Id).ConfiguredOnValue, Is.EqualTo(LogicBit.Z));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.Zero), "The configured Off startup state drives zero.");
            Assert.That(session.Circuit.Storage(sr.Id).Q, Is.EqualTo(LogicBit.One));
        }

        [Test]
        public void InvalidSourceConfigurationDoesNotPublish()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(20, 20, 10)));
            session.PlaceComponent(BuiltInPinCatalog.And, new GridCell(2, 1, 2),
                GridOrientation.Default);
            var before = session.Design;
            var revision = session.Revision;
            Assert.Throws<ArgumentException>(() => session.ConfigureSource(
                before.Components[0].Id, LogicBit.Z, true));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(revision));
            Assert.That(session.Design.Components[0].TypeId, Is.EqualTo(BuiltInPinCatalog.And));
        }

        [Test]
        public void SourceBodyActionSettlesWhileClockStoppedWithoutEditingDesign()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(20, 20, 10)));
            session.PlaceComponent(BuiltInPinCatalog.Source, new GridCell(2, 1, 2),
                GridOrientation.Default, LogicBit.One, false);
            var source = session.Design.Components[0];
            var output = JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);
            var before = session.Design;
            var revision = session.Revision;
            Assert.That(session.Scheduler.IsPaused, Is.True);
            Assert.That(session.Scheduler.ClockRunning, Is.False);

            session.ToggleSource(source.Id);
            Assert.That(session.Circuit.Source(source.Id).IsOn, Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.One));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(revision));
            Assert.That(session.Scheduler.Now, Is.EqualTo(SiliconSandbox.Simulation.SimulationTime.Zero));

            session.ToggleSource(source.Id);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Design.Components[0].SourceInitialOn, Is.False);

            session.Scheduler.ResumeSimulation();
            session.ToggleSource(source.Id);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.One));
            Assert.That(session.Scheduler.Now, Is.EqualTo(SiliconSandbox.Simulation.SimulationTime.Zero));
            Assert.That(session.Revision, Is.EqualTo(revision));
        }
    }
}
