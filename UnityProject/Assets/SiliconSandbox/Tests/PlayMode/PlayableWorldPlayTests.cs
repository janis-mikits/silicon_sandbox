using System.Collections;
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
            foreach (var part in Object.FindObjectsByType<WorldSelectablePart>(
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
            foreach (var part in Object.FindObjectsByType<WorldSelectablePart>(
                FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ConnectorNode && part.OwnerId == stub.Id)
                    found = true;
            Assert.That(found, Is.True);

            bootstrap.Session.Scheduler.StepClockEdge();
            var clockPin = bootstrap.Session.Inspector.InspectPin(
                sr.Id, sr.PinIds["CLK"]);
            Assert.That(clockPin.Value, Is.EqualTo(LogicBit.One));
        }
    }
}
