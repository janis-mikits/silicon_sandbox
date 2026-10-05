using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Persistence;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitWorldContextTests
    {
        [Test]
        public void AuthoredSnapshotRetainsWorldAndInventoryButRestartsLiveState()
        {
            var context = OneBitWorldContext.NewFreeplay("Professor demo",
                new WorldBounds(24, 18, 8));
            context.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(4, 1, 4), GridOrientation.Default,
                LogicBit.One, false);
            var source = context.Session.Design.Components[0];
            context.Session.ToggleSource(source.Id);
            context.Inventory.SelectHotbar(3);
            context.Session.Scheduler.SetFrequency("3");
            context.Session.Scheduler.ResumeSimulation();
            context.Session.Scheduler.StartClock();
            context.Session.Scheduler.AdvanceUntil(
                context.Session.Scheduler.NextClockEdge.Value);
            var player = new SavedPlayerPose(4.5, 2.0, 5.5, 0, 0, 1);

            var saved = context.Capture(player);
            var reopened = OneBitWorldContext.Open(saved);
            Assert.That(reopened.WorldId, Is.EqualTo(context.WorldId));
            Assert.That(reopened.WorldName, Is.EqualTo("Professor demo"));
            Assert.That(reopened.FloorMaterialId,
                Is.EqualTo("builtin.smooth_sandstone"));
            Assert.That(reopened.WallStyleId,
                Is.EqualTo("builtin.wood_sandbox_wall"));
            Assert.That(reopened.Session.Design.Bounds.WidthCells,
                Is.EqualTo(24));
            Assert.That(reopened.Inventory.SelectedHotbarSlot, Is.EqualTo(3));
            Assert.That(saved.Player.X, Is.EqualTo(4.5));
            Assert.That(reopened.Session.Scheduler.FrequencyHz, Is.EqualTo("3"));
            Assert.That(reopened.Session.Scheduler.Now,
                Is.EqualTo(SimulationTime.Zero));
            Assert.That(reopened.Session.Scheduler.ClockLevel,
                Is.EqualTo(LogicBit.Zero));
            Assert.That(reopened.Session.Scheduler.ClockRunning, Is.False);
            Assert.That(reopened.Session.Circuit.Source(source.Id).IsOn,
                Is.False);
            Assert.That(reopened.Session.Design.Components[0].Id,
                Is.EqualTo(source.Id));
        }
    }
}
