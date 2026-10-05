using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitUndoRedoTests
    {
        [Test]
        public void UndoRedoRestoresAuthoredIdentityWithoutRewindingLiveSource()
        {
            var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0,
                TimeSpan.Zero);
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), utcNow: () => now);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var sourceId = session.Design.Components[0].Id;
            session.ToggleSource(sourceId);
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(5, 1, 2), GridOrientation.Default);
            var gateId = session.Design.Components[1].Id;
            session.Scheduler.StepClockEdge();
            var timeBeforeUndo = session.Scheduler.Now;
            Assert.That(session.TryUndo(), Is.True);
            Assert.That(session.Design.Components.Count, Is.EqualTo(1));
            Assert.That(session.Design.Components[0].Id, Is.EqualTo(sourceId));
            Assert.That(session.Circuit.Source(sourceId).IsOn, Is.True);
            Assert.That(session.Scheduler.Now, Is.EqualTo(timeBeforeUndo));
            Assert.That(session.TryRedo(), Is.True);
            Assert.That(session.Design.Components[1].Id, Is.EqualTo(gateId));
            Assert.That(session.Circuit.Source(sourceId).IsOn, Is.True);

            now = now.AddMinutes(5).AddSeconds(1);
            Assert.That(session.TryUndo(), Is.False);
            Assert.That(session.Design.Components.Count, Is.EqualTo(2));
        }

        [Test]
        public void UndoAuthoredSourceConfigurationRefreshesDriveButKeepsLiveSwitch()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var source = session.Design.Components[0];
            var output = JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);
            session.ToggleSource(source.Id);
            session.ConfigureSource(source.Id, LogicBit.Z, false);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.Z));
            Assert.That(session.TryUndo(), Is.True);
            Assert.That(session.Design.Components[0].SourceOnValue,
                Is.EqualTo(LogicBit.One));
            Assert.That(session.Circuit.Source(source.Id).IsOn, Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(output)).Value,
                Is.EqualTo(LogicBit.One));
        }
    }
}
