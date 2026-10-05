using System;
using System.IO;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Persistence;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class ProfessorStatefulSequenceTests
    {
        [Test]
        public void PackagedSrInstancesFollowThreeRisesAndReopenFresh()
        {
            var context = OneBitWorldContext.NewFreeplay("Professor stateful demo",
                new WorldBounds(32, 32, 8));
            var session = context.Session;
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(25, 1, 25), GridOrientation.Default);
            var originalSr = session.Design.Components[0];
            var draft = session.PreviewPackage(new CellRegion(
                originalSr.AnchorCell, originalSr.AnchorCell), "SR memory");
            for (var i = draft.Ports.Count - 1; i >= 0; i--)
                if (draft.Ports[i].Name == "Q_bar") draft.RemovePort(i);
            var staged = OneBitPackageStager.Prepare(context, draft,
                Guid.NewGuid(), new SavedPlayerPose(8, 2, 8, 0, 0, 1));
            var version = staged.Version;
            context = OneBitWorldContext.Open(staged.SavedWorld);
            session = context.Session;
            Assert.That(session.Design.Components[0].Id, Is.EqualTo(originalSr.Id),
                "Packaging leaves the source circuit in the world.");
            Assert.That(version.Ports.Count, Is.EqualTo(4));

            session.PlaceModule(version, "A", new GridCell(6, 1, 6),
                GridOrientation.Default);
            session.PlaceModule(version, "B", new GridCell(16, 1, 6),
                GridOrientation.Default);
            var a = session.Design.Modules[0];
            var b = session.Design.Modules[1];
            foreach (var cell in new[]
            {
                new GridCell(4,1,6), new GridCell(4,1,8),
                new GridCell(14,1,6), new GridCell(14,1,8)
            })
                session.PlaceComponent(BuiltInPinCatalog.Source, cell,
                    GridOrientation.Default, LogicBit.One, false);
            var aS = session.Design.Components[1];
            var aR = session.Design.Components[2];
            var bS = session.Design.Components[3];
            var bR = session.Design.Components[4];
            var sPort = Port(version, "S");
            var rPort = Port(version, "R");
            var clkPort = Port(version, "CLK");
            var qPort = Port(version, "Q");
            session.ConnectPins(Out(aS), End(a, sPort));
            session.ConnectPins(Out(aR), End(a, rPort));
            session.ConnectPins(Out(bS), End(b, sPort));
            session.ConnectPins(Out(bR), End(b, rPort));
            session.AttachWorldClockPort(a.Id, clkPort);
            session.AttachWorldClockPort(b.Id, clkPort);
            AssertStates(session, a, b, qPort, LogicBit.X, LogicBit.X);
            AssertInternalQBar(session, version, a, b,
                LogicBit.X, LogicBit.X);

            session.ToggleSource(aS.Id);
            session.Scheduler.StepClockEdge(); // first rising edge
            AssertStates(session, a, b, qPort, LogicBit.One, LogicBit.X);
            AssertInternalQBar(session, version, a, b,
                LogicBit.Zero, LogicBit.X);
            session.ToggleSource(aS.Id);
            session.ToggleSource(bR.Id);
            session.Scheduler.StepClockEdge(); // fall; no SR sample
            session.Scheduler.StepClockEdge(); // second rising edge
            AssertStates(session, a, b, qPort, LogicBit.One, LogicBit.Zero);
            AssertInternalQBar(session, version, a, b,
                LogicBit.Zero, LogicBit.One);
            session.ToggleSource(aR.Id);
            session.ToggleSource(bR.Id);
            session.Scheduler.StepClockEdge(); // fall
            session.Scheduler.StepClockEdge(); // third rising edge
            AssertStates(session, a, b, qPort, LogicBit.Zero, LogicBit.Zero);
            AssertInternalQBar(session, version, a, b,
                LogicBit.One, LogicBit.One);

            var before = session.Design;
            var saved = context.Capture(new SavedPlayerPose(20, 2, 20, 0, 0, 1));
            var archive = WorldV1ArchiveCodec.Write(saved);
            LoadedWorldV1Archive loaded;
            using (var input = new MemoryStream(archive))
                loaded = WorldV1ArchiveCodec.Read(input);
            Assert.That(loaded.UnavailableModuleVersionIds, Is.Empty);
            var reopened = OneBitWorldContext.Open(loaded.Snapshot);
            Assert.That(reopened.Session.Design.Components.Count,
                Is.EqualTo(before.Components.Count));
            Assert.That(reopened.Session.Design.Modules.Count,
                Is.EqualTo(before.Modules.Count));
            Assert.That(reopened.Session.ModuleVersions.ContainsKey(
                version.VersionId), Is.True);
            Assert.That(reopened.Session.Design.Modules[0].InstanceId,
                Is.EqualTo(a.InstanceId));
            Assert.That(reopened.Session.Design.Modules[1].InstanceId,
                Is.EqualTo(b.InstanceId));
            Assert.That(reopened.Session.Design.Topology.Connectors.Count,
                Is.EqualTo(before.Topology.Connectors.Count));
            Assert.That(reopened.Session.Scheduler.Now, Is.EqualTo(SimulationTime.Zero));
            Assert.That(reopened.Session.Scheduler.ClockRunning, Is.False);
            Assert.That(reopened.Session.Scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
            foreach (var source in new[] { aS, aR, bS, bR })
                Assert.That(reopened.Session.Circuit.Source(source.Id).IsOn, Is.False);
            AssertStates(reopened.Session, a, b, qPort, LogicBit.X, LogicBit.X);
            AssertInternalQBar(reopened.Session, version, a, b,
                LogicBit.X, LogicBit.X);
            reopened.Session.Scheduler.StepClockEdge();
            AssertStates(reopened.Session, a, b, qPort, LogicBit.X, LogicBit.X);
        }

        private static Guid Port(OneBitModuleVersion version, string name)
        {
            foreach (var port in version.Ports)
                if (port.Name == name) return port.Id;
            throw new AssertionException("Package omitted " + name + " port.");
        }

        private static JoinMember Out(PlacedOneBitComponent source) =>
            JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);

        private static JoinMember End(PlacedOneBitModuleInstance instance,
            Guid portId) => JoinMember.ModulePortBit(instance.Id, portId, 0);

        private static void AssertStates(OneBitWorldSession session,
            PlacedOneBitModuleInstance a, PlacedOneBitModuleInstance b,
            Guid qPort, LogicBit expectedA, LogicBit expectedB)
        {
            Assert.That(session.Inspector.InspectModulePort(a.Id, qPort).Value,
                Is.EqualTo(expectedA));
            Assert.That(session.Inspector.InspectModulePort(b.Id, qPort).Value,
                Is.EqualTo(expectedB));
        }

        private static void AssertInternalQBar(OneBitWorldSession session,
            OneBitModuleVersion version, PlacedOneBitModuleInstance a,
            PlacedOneBitModuleInstance b, LogicBit expectedA,
            LogicBit expectedB)
        {
            var component = version.Components[0];
            var pin = component.PinIds["Q_bar"];
            Assert.That(session.Inspector.InspectInternalPin(
                a.InstanceId, component.Id, pin), Is.EqualTo(expectedA));
            Assert.That(session.Inspector.InspectInternalPin(
                b.InstanceId, component.Id, pin), Is.EqualTo(expectedB));
        }
    }
}
