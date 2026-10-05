using System;
using System.Collections;
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
        public IEnumerator BothInstancesKeepSimulatingBehindTheRealCamera()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>();
            var session = bootstrap.Session;
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(25, 1, 25), GridOrientation.Default);
            var draft = session.PreviewPackage(new CellRegion(
                new GridCell(25, 1, 25), new GridCell(25, 1, 25)), "SR memory");
            for (var i = draft.Ports.Count - 1; i >= 0; i--)
                if (draft.Ports[i].Name == "Q_bar") draft.RemovePort(i);
            var staged = OneBitPackageStager.Prepare(bootstrap.Context, draft,
                Guid.NewGuid(), new SavedPlayerPose(8, 2, 8, 0, 0, 1));
            bootstrap.OpenWorld(staged.SavedWorld);
            session = bootstrap.Session;
            var version = staged.Version;
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
