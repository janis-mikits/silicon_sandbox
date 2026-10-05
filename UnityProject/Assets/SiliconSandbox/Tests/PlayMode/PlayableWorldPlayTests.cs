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
    }
}
