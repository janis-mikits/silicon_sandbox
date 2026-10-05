using System;
using System.Collections;
using SiliconSandbox.Application;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Bootstrap;
using SiliconSandbox.Contracts;
using SiliconSandbox.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class PlayableWorldPlayTests
    {
        [UnityTest]
        public IEnumerator OpenAndOutputWireHasSelectableVisibleEnd()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var gate = session.Design.Components[0];
            session.PlaceWireStub(JoinMember.ComponentPin(gate.Id,
                gate.PinIds["Y"]), new GridCell(8, 1, 6));
            var route = session.Design.Topology.Connectors[0];
            yield return null;
            var foundEnd = false;
            foreach (var part in UnityEngine.Object.FindObjectsByType<
                WorldSelectablePart>(FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ConnectorNode &&
                    part.OwnerId == route.Id &&
                    part.PartId == route.Nodes[route.Nodes.Count - 1].Id)
                    foundEnd = true;
            Assert.That(foundEnd, Is.True);
            Assert.That(session.Inspector.InspectConnector(route.Id).Value,
                Is.EqualTo(LogicBit.X));
        }

        [UnityTest]
        public IEnumerator InvalidFlashRestoresLatestSettledSignalColor()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = session.Design.Components[0];
            WorldSelectablePart pin = null;
            yield return null;
            foreach (var part in UnityEngine.Object.FindObjectsByType<
                WorldSelectablePart>(FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ComponentPin &&
                    part.OwnerId == source.Id)
                    pin = part;
            Assert.That(pin, Is.Not.Null);
            pin.FlashInvalid();
            session.ToggleSource(source.Id);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return null;
            var properties = new MaterialPropertyBlock();
            pin.GetComponent<Renderer>().GetPropertyBlock(properties);
            var shown = properties.GetColor("_Color");
            Assert.That(shown.g, Is.GreaterThan(0.8f),
                "The settled 1 color must return after red invalid feedback.");
            Assert.That(shown.r, Is.LessThan(0.3f));
        }

        [UnityTest]
        public IEnumerator LocalEditKeepsUnchangedGraphicsAndTargets()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            yield return null;
            var first = session.Design.Components[0];
            var firstObject = GameObject.Find("Component " + first.Id.ToString("D"));
            Assert.That(firstObject, Is.Not.Null);
            var firstName = firstObject.name;

            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(18, 1, 18), GridOrientation.Default);
            yield return null;
            var second = session.Design.Components[1];
            var secondObject = GameObject.Find("Component " + second.Id.ToString("D"));
            Assert.That(GameObject.Find(firstName), Is.SameAs(firstObject),
                "A distant edit must not rebuild the existing visual object.");
            Assert.That(firstObject.GetComponent<WorldSelectablePart>().OwnerId,
                Is.EqualTo(first.Id));

            session.ConfigureSource(first.Id, LogicBit.Zero, false);
            yield return null;
            Assert.That(GameObject.Find(secondObject.name), Is.SameAs(secondObject));
            Assert.That(GameObject.Find(firstName), Is.Not.SameAs(firstObject));
            Assert.That(GameObject.Find(firstName)
                .GetComponent<WorldSelectablePart>().OwnerId, Is.EqualTo(first.Id));
        }

        [UnityTest]
        public IEnumerator CapturedWorldReopensWithDesignButFreshSimulation()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default,
                LogicBit.One, false);
            var source = bootstrap.Session.Design.Components[0];
            bootstrap.Session.ToggleSource(source.Id);
            bootstrap.Inventory.SelectHotbar(3);
            bootstrap.Interaction.transform.position = new Vector3(7f, 2f, 8f);
            var saved = bootstrap.CaptureCurrentWorld();
            Assert.That(bootstrap.Session.Circuit.Source(source.Id).IsOn, Is.True);

            bootstrap.OpenWorld(saved);
            Assert.That(bootstrap.Session.Scheduler.Now,
                Is.EqualTo(SiliconSandbox.Simulation.SimulationTime.Zero),
                "Reopen starts at time zero before the next live frame advances.");
            yield return null;
            Assert.That(bootstrap.Session.Design.Components[0].Id,
                Is.EqualTo(source.Id));
            Assert.That(bootstrap.Session.Circuit.Source(source.Id).IsOn,
                Is.False);
            Assert.That(bootstrap.Session.Scheduler.ClockLevel,
                Is.EqualTo(LogicBit.Zero));
            Assert.That(bootstrap.Inventory.SelectedHotbarSlot, Is.EqualTo(3));
            Assert.That(bootstrap.WorldView.Session,
                Is.SameAs(bootstrap.Session));
            Assert.That(bootstrap.Interaction.Session,
                Is.SameAs(bootstrap.Session));
            Assert.That(bootstrap.Interaction.transform.position.x,
                Is.EqualTo(7f).Within(0.01f));
            Assert.That(GameObject.Find("Component " + source.Id.ToString("D")),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator GeneratedFloorAndFourWallsMatchAuthoredWorldBounds()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var floor = GameObject.Find(FlatWorldSmoke.FloorName);
            var bounds = floor.GetComponent<PlayableWorldBootstrap>()
                .Session.Design.Bounds;
            Assert.That(floor.transform.position.x,
                Is.EqualTo(bounds.WidthCells * 0.5f).Within(0.001f));
            Assert.That(floor.transform.position.z,
                Is.EqualTo(bounds.LengthCells * 0.5f).Within(0.001f));
            Assert.That(floor.transform.localScale.x,
                Is.EqualTo(bounds.WidthCells));
            Assert.That(floor.transform.localScale.z,
                Is.EqualTo(bounds.LengthCells));
            foreach (var face in new[] { "West", "East", "South", "North" })
            {
                var wall = GameObject.Find("Sandbox boundary " + face);
                Assert.That(wall, Is.Not.Null);
                Assert.That(wall.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(wall.GetComponent<WorldSelectablePart>(), Is.Null,
                    "Generated world boundaries cannot be broken as authored objects.");
            }
            var player = floor.GetComponent<PlayableWorldBootstrap>()
                .Interaction.transform;
            player.position = new Vector3(-5f, 50f, -5f);
            yield return null;
            Assert.That(player.position.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(player.position.z, Is.GreaterThanOrEqualTo(0f));
            Assert.That(player.position.y,
                Is.LessThanOrEqualTo(bounds.HeightCells - 3f));
        }

        [UnityTest]
        public IEnumerator TwoModulesKeepExpectedStateWhileViewAndCameraAreAway()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>();
            var version = SrVersion();
            var session = bootstrap.Session;
            session.PlaceModule(version, "A", new GridCell(6, 1, 6),
                GridOrientation.Default);
            session.PlaceModule(version, "B", new GridCell(16, 1, 6),
                GridOrientation.Default);
            var a = session.Design.Modules[0];
            var b = session.Design.Modules[1];
            var sources = new[]
            {
                new GridCell(4,1,6), new GridCell(4,1,8),
                new GridCell(14,1,6), new GridCell(14,1,8)
            };
            foreach (var cell in sources)
                session.PlaceComponent(BuiltInPinCatalog.Source, cell,
                    GridOrientation.Default, LogicBit.One, false);
            var aS = session.Design.Components[0];
            var aR = session.Design.Components[1];
            var bS = session.Design.Components[2];
            var bR = session.Design.Components[3];
            session.ConnectPins(SourceOut(aS), Port(a, version.Ports[0].Id));
            session.ConnectPins(SourceOut(aR), Port(a, version.Ports[1].Id));
            session.ConnectPins(SourceOut(bS), Port(b, version.Ports[0].Id));
            session.ConnectPins(SourceOut(bR), Port(b, version.Ports[1].Id));
            session.AttachWorldClockPort(a.Id, version.Ports[2].Id);
            session.AttachWorldClockPort(b.Id, version.Ports[2].Id);
            yield return null;

            var foundA = false; var foundB = false;
            foreach (var part in UnityEngine.Object.FindObjectsByType<WorldSelectablePart>(
                FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ModulePort &&
                    part.PartId == version.Ports[3].Id)
                {
                    if (part.OwnerId == a.Id) foundA = true;
                    if (part.OwnerId == b.Id) foundB = true;
                }
            Assert.That(foundA && foundB, Is.True);

            session.ToggleSource(aS.Id);
            session.Scheduler.StepClockEdge();
            session.ToggleSource(aS.Id);
            session.ToggleSource(bR.Id);
            session.Scheduler.StepClockEdge();
            session.Scheduler.StepClockEdge();
            Assert.That(session.Inspector.InspectModulePort(a.Id,
                version.Ports[3].Id).Value, Is.EqualTo(LogicBit.One));
            Assert.That(session.Inspector.InspectModulePort(b.Id,
                version.Ports[3].Id).Value, Is.EqualTo(LogicBit.Zero));

            session.ToggleSource(aR.Id);
            session.ToggleSource(bR.Id);
            bootstrap.Interaction.transform.position = new Vector3(30f, 2f, 30f);
            bootstrap.WorldView.gameObject.SetActive(false);
            session.Scheduler.ResumeSimulation();
            session.Scheduler.StartClock();
            session.Scheduler.AdvanceUntil(session.Scheduler.NextClockEdge.Value);
            session.Scheduler.AdvanceUntil(session.Scheduler.NextClockEdge.Value);
            session.Scheduler.StopClock();
            session.Scheduler.PauseSimulation();
            Assert.That(session.Inspector.InspectModulePort(a.Id,
                version.Ports[3].Id).Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Inspector.InspectModulePort(b.Id,
                version.Ports[3].Id).Value, Is.EqualTo(LogicBit.Zero));

            bootstrap.WorldView.gameObject.SetActive(true);
            yield return null;
            Assert.That(session.Inspector.InspectModulePort(a.Id,
                version.Ports[3].Id).Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Inspector.InspectModulePort(b.Id,
                version.Ports[3].Id).Value, Is.EqualTo(LogicBit.Zero));
        }

        [UnityTest]
        public IEnumerator BlankWorldPlacesASelectableSourceAndShowsLiveValue()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var floor = GameObject.Find(FlatWorldSmoke.FloorName);
            var bootstrap = floor.GetComponent<PlayableWorldBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Session.Design.Components.Count, Is.EqualTo(0));
            Assert.That(bootstrap.WorldView.Session, Is.SameAs(bootstrap.Session));
            Assert.That(bootstrap.Interaction.Session, Is.SameAs(bootstrap.Session));

            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            yield return null;
            var source = bootstrap.Session.Design.Components[0];
            Assert.That(GameObject.Find("Component " + source.Id.ToString("D")),
                Is.Not.Null);
            var pickedPin = false;
            foreach (var part in UnityEngine.Object.FindObjectsByType<WorldSelectablePart>(
                FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ComponentPin &&
                    part.OwnerId == source.Id &&
                    part.PartId == source.PinIds["OUT"])
                    pickedPin = true;
            Assert.That(pickedPin, Is.True);

            bootstrap.Session.ToggleSource(source.Id);
            var inspected = bootstrap.Session.Inspector.InspectPin(
                source.Id, source.PinIds["OUT"]);
            Assert.That(inspected.Value, Is.EqualTo(LogicBit.One));
            Assert.That(bootstrap.Session.Design.Components[0].SourceInitialOn, Is.False,
                "Live operation must not change saved startup configuration.");
        }

        [UnityTest]
        public IEnumerator WorldClockStubIsVisibleAndUpdatesAfterSettledStep()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var sr = bootstrap.Session.Design.Components[0];
            bootstrap.Session.AttachWorldClockPin(sr.Id);
            yield return null;
            var stub = bootstrap.Session.Design.Topology.Connectors[0];
            var found = false;
            foreach (var part in UnityEngine.Object.FindObjectsByType<WorldSelectablePart>(
                FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ConnectorNode && part.OwnerId == stub.Id)
                    found = true;
            Assert.That(found, Is.True);

            bootstrap.Session.Scheduler.StepClockEdge();
            var clockPin = bootstrap.Session.Inspector.InspectPin(
                sr.Id, sr.PinIds["CLK"]);
            Assert.That(clockPin.Value, Is.EqualTo(LogicBit.One));
        }

        private static OneBitModuleVersion SrVersion()
        {
            var source = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(source,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var sr = snapshot.Components[0];
            return OneBitModuleVersionFactory.Create(snapshot, Guid.NewGuid(), "SR",
                new[]
                {
                    PortChoice("S", OneBitPortDirection.Input, sr,
                        new QuarterPoint(0, 1, 1)),
                    PortChoice("R", OneBitPortDirection.Input, sr,
                        new QuarterPoint(0, 3, 1)),
                    PortChoice("CLK", OneBitPortDirection.Input, sr,
                        new QuarterPoint(1, 1, 0)),
                    PortChoice("Q", OneBitPortDirection.Output, sr,
                        new QuarterPoint(4, 1, 1))
                });
        }

        private static OneBitPortChoice PortChoice(string name,
            OneBitPortDirection direction, PlacedOneBitComponent sr,
            QuarterPoint point) => new OneBitPortChoice(name, direction,
                new GridCell(0, 0, 0), point,
                JoinMember.ComponentPin(sr.Id, sr.PinIds[name]));

        private static JoinMember SourceOut(PlacedOneBitComponent source) =>
            JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);

        private static JoinMember Port(PlacedOneBitModuleInstance module,
            Guid portId) => JoinMember.ModulePortBit(module.Id, portId, 0);
    }
}
