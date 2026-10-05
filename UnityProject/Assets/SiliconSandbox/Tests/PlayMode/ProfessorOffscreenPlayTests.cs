using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Bootstrap;
using SiliconSandbox.Contracts;
using SiliconSandbox.Interaction;
using SiliconSandbox.Persistence;
using SiliconSandbox.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class ProfessorOffscreenPlayTests
    {
        [UnityTest]
        public IEnumerator FullProfessorJourneySurvivesOffscreenAndV1Reopen()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>();
            var testRoot = Environment.GetEnvironmentVariable(
                "SILICON_SANDBOX_TEST_ROOT");
            Assert.That(testRoot, Is.Not.Null.And.Not.Empty);
            var storage = Path.Combine(testRoot,
                "professor-journey-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            bootstrap.SetStorageRootForVerification(storage);
            var session = bootstrap.Session;
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(25, 1, 25), GridOrientation.Default);
            var sourceSrId = session.Design.Components[0].Id;
            var draft = session.PreviewPackage(new CellRegion(
                new GridCell(25, 1, 25), new GridCell(25, 1, 25)), "SR memory");
            for (var i = draft.Ports.Count - 1; i >= 0; i--)
                if (draft.Ports[i].Name == "Q_bar") draft.RemovePort(i);
            Assert.That(bootstrap.PublishPackage(draft),
                Does.Contain("Published module"));
            OneBitModuleVersion version = null;
            foreach (var item in session.ModuleVersions.Values) version = item;
            Assert.That(version, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(storage, "library", "versions",
                version.VersionId.ToString("D") + ".json")), Is.True);
            Assert.That(session.Design.Components[0].Id, Is.EqualTo(sourceSrId));
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
            AssertQ(session, a, b, qPort, LogicBit.X, LogicBit.X);

            // The separate professor logic circuit uses visible, authored
            // connectors and literal four-state expectations.
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(4, 1, 20), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(4, 1, 22), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(6, 1, 20), GridOrientation.Default);
            var sourceA = session.Design.Components[5];
            var sourceB = session.Design.Components[6];
            var gate = session.Design.Components[7];
            session.ConnectPins(Out(sourceA), JoinMember.ComponentPin(
                gate.Id, gate.PinIds["A"]));
            session.ConnectPins(Out(sourceB), JoinMember.ComponentPin(
                gate.Id, gate.PinIds["B"]));
            var bConnector = session.Design.Topology.Connectors[
                session.Design.Topology.Connectors.Count - 1];
            session.PlaceWireStub(JoinMember.ComponentPin(gate.Id,
                gate.PinIds["Y"]), new GridCell(8, 1, 20));
            var yConnector = session.Design.Topology.Connectors[
                session.Design.Topology.Connectors.Count - 1];
            session.ToggleSource(sourceA.Id);
            session.ToggleSource(sourceB.Id);
            CheckAnd(session, sourceA, sourceB, yConnector.Id,
                LogicBit.Zero, LogicBit.One, LogicBit.Zero);
            CheckAnd(session, sourceA, sourceB, yConnector.Id,
                LogicBit.One, LogicBit.One, LogicBit.One);
            CheckAnd(session, sourceA, sourceB, yConnector.Id,
                LogicBit.Zero, LogicBit.Z, LogicBit.Zero);
            CheckAnd(session, sourceA, sourceB, yConnector.Id,
                LogicBit.One, LogicBit.Z, LogicBit.X);
            var bInspection = session.Inspector.InspectConnector(bConnector.Id);
            Assert.That(bInspection.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(bInspection.ConnectedPins.Count, Is.EqualTo(2));
            Assert.That(bInspection.ActiveDrivers, Is.Empty);
            Assert.That(session.Inspector.InspectConnector(yConnector.Id)
                .Explanation, Does.Contain("input B is Z"));
            CheckAnd(session, sourceA, sourceB, yConnector.Id,
                LogicBit.One, LogicBit.X, LogicBit.X);

            session.ToggleSource(aS.Id);
            session.Scheduler.StepClockEdge();
            AssertQ(session, a, b, qPort, LogicBit.One, LogicBit.X);
            session.ToggleSource(aS.Id);
            session.ToggleSource(bR.Id);
            session.Scheduler.StepClockEdge();
            session.Scheduler.StepClockEdge();
            AssertQ(session, a, b, qPort, LogicBit.One, LogicBit.Zero);
            session.Scheduler.StopClock();
            session.ToggleSource(aR.Id);
            session.ToggleSource(bR.Id);

            var camera = Camera.main;
            var controller = bootstrap.Interaction.GetComponent<
                CreativeCameraController>();
            controller.Teleport(new Vector3(30f, 1f, 30f));
            controller.SetViewDirection(Vector3.forward);
            yield return null;
            Assert.That(camera.WorldToViewportPoint(new Vector3(6f, 1f, 6f)).z,
                Is.LessThan(0f));
            Assert.That(camera.WorldToViewportPoint(new Vector3(16f, 1f, 6f)).z,
                Is.LessThan(0f));
            Assert.That(session.Scheduler.Diagnostic, Is.Null);
            session.Scheduler.ResumeSimulation();
            session.Scheduler.StartClock();
            session.Scheduler.AdvanceUntil(
                session.Scheduler.NextClockEdge.Value); // falling
            session.Scheduler.AdvanceUntil(
                session.Scheduler.NextClockEdge.Value); // third rise
            AssertQ(session, a, b, qPort, LogicBit.Zero, LogicBit.Zero);
            var internalSr = version.Components[0];
            Assert.That(session.Inspector.InspectInternalPin(a.InstanceId,
                internalSr.Id, internalSr.PinIds["Q_bar"]),
                Is.EqualTo(LogicBit.One));
            Assert.That(session.Inspector.InspectInternalPin(b.InstanceId,
                internalSr.Id, internalSr.PinIds["Q_bar"]),
                Is.EqualTo(LogicBit.One));
            controller.Teleport(new Vector3(11f, 1f, 2f));
            controller.SetViewDirection(Vector3.forward);
            yield return null;
            AssertQ(session, a, b, qPort, LogicBit.Zero, LogicBit.Zero);
            Assert.That(bootstrap.WorldView.Session, Is.SameAs(session));

            var worldId = bootstrap.Context.WorldId;
            var connectorCount = session.Design.Topology.Connectors.Count;
            bootstrap.SaveCurrentWorldFile();
            Assert.That(File.Exists(ModuleLibraryStore.WorldPath(storage,
                worldId)), Is.True);
            bootstrap.ReopenCurrentWorldFile();
            session = bootstrap.Session;
            Assert.That(bootstrap.Context.WorldId, Is.EqualTo(worldId));
            Assert.That(session.Design.Components.Count, Is.EqualTo(8));
            Assert.That(session.Design.Components[0].Id, Is.EqualTo(sourceSrId));
            Assert.That(session.Design.Components[7].Id, Is.EqualTo(gate.Id));
            Assert.That(session.Design.Modules[0].InstanceId,
                Is.EqualTo(a.InstanceId));
            Assert.That(session.Design.Modules[1].InstanceId,
                Is.EqualTo(b.InstanceId));
            Assert.That(session.Design.Topology.Connectors.Count,
                Is.EqualTo(connectorCount));
            Assert.That(session.ModuleVersions[version.VersionId]
                .Components[0].AnchorCell,
                Is.EqualTo(version.Components[0].AnchorCell));
            Assert.That(session.Scheduler.Now,
                Is.EqualTo(SiliconSandbox.Simulation.SimulationTime.Zero));
            Assert.That(session.Scheduler.ClockRunning, Is.False);
            Assert.That(session.Scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Scheduler.FrequencyHz, Is.EqualTo("10"));
            foreach (var source in new[] { aS, aR, bS, bR })
                Assert.That(session.Circuit.Source(source.Id).IsOn, Is.False);
            AssertQ(session, a, b, qPort, LogicBit.X, LogicBit.X);
            session.Scheduler.StepClockEdge();
            AssertQ(session, a, b, qPort, LogicBit.X, LogicBit.X);
            Directory.Delete(storage, true);
        }

        private static void CheckAnd(OneBitWorldSession session,
            PlacedOneBitComponent a, PlacedOneBitComponent b,
            Guid yConnector, LogicBit valueA, LogicBit valueB,
            LogicBit expectedY)
        {
            session.ConfigureSource(a.Id, valueA, false);
            session.ConfigureSource(b.Id, valueB, false);
            Assert.That(session.Inspector.InspectConnector(yConnector).Value,
                Is.EqualTo(expectedY));
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

        private static void AssertQ(OneBitWorldSession session,
            PlacedOneBitModuleInstance a, PlacedOneBitModuleInstance b,
            Guid q, LogicBit expectedA, LogicBit expectedB)
        {
            Assert.That(session.Inspector.InspectModulePort(a.Id, q).Value,
                Is.EqualTo(expectedA));
            Assert.That(session.Inspector.InspectModulePort(b.Id, q).Value,
                Is.EqualTo(expectedB));
        }
    }
}
