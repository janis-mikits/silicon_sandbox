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
    public sealed class VisualArtPlayTests
    {
        [UnityTest]
        public IEnumerator SourceMarkingShowsDrivenZeroOneXAndZOnItsSurface()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            Assert.That(art, Is.Not.Null);
            Assert.That(art.IsComplete, Is.True);
            Assert.That(art.SourceBody.bounds.size.x, Is.EqualTo(0.78f).Within(0.001f));
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = session.Design.Components[0];
            yield return null;

            var body = GameObject.Find("Component " + source.Id.ToString("D"));
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(art.SourceBody));
            Assert.That(body.GetComponent<BoxCollider>().size.x,
                Is.EqualTo(0.78f).Within(0.001f));
            Assert.That(body.transform.Find("Component label"), Is.Null,
                "Built-in identity should be on the block, not floating above it.");
            AssertValue(source.Id.ToString("D"), art.SourceZero);

            session.ToggleSource(source.Id);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceOne);
            session.ConfigureSource(source.Id, LogicBit.X, false);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceX);
            session.ConfigureSource(source.Id, LogicBit.Z, false);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceZ);
            session.ToggleSource(source.Id);
            yield return null;
            AssertValue(source.Id.ToString("D"), art.SourceZero);
        }

        [UnityTest]
        public IEnumerator ImportedPinsAndWiresRetainExactSelectableParts()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            Assert.That(art.WireStraight.bounds.size.x,
                Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(art.Pin.bounds.max.y,
                Is.EqualTo(0.0625f).Within(0.001f));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = session.Design.Components[0];
            session.PlaceWireStub(JoinMember.ComponentPin(source.Id,
                source.PinIds["OUT"]), new GridCell(8, 1, 6));
            var route = session.Design.Topology.Connectors[0];
            yield return null;

            var pinFound = false;
            var spanFound = false;
            foreach (var part in Object.FindObjectsByType<WorldSelectablePart>(
                         FindObjectsSortMode.None))
            {
                if (part.Kind == WorldPartKind.ComponentPin &&
                    part.OwnerId == source.Id &&
                    part.PartId == source.PinIds["OUT"])
                {
                    pinFound = true;
                    Assert.That(part.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(art.Pin));
                    Assert.That(part.GetComponent<CapsuleCollider>().radius,
                        Is.EqualTo(0.1f).Within(0.001f));
                }
                if (part.Kind == WorldPartKind.ConnectorSpan &&
                    part.OwnerId == route.Id &&
                    part.GetComponent<MeshFilter>().sharedMesh == art.WireStraight)
                {
                    spanFound = true;
                    Assert.That(part.GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(art.WireStraight));
                    Assert.That(part.GetComponent<CapsuleCollider>().radius,
                        Is.EqualTo(0.125f).Within(0.001f));
                }
            }
            Assert.That(pinFound, Is.True);
            Assert.That(spanFound, Is.True);
        }

        [UnityTest]
        public IEnumerator OpposingDriversKeepTheirOwnMarkingsWhileTheWireIsX()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var session = GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>().Session;
            var art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(8, 1, 6),
                GridOrientation.Default.ClockwiseYaw().ClockwiseYaw());
            var first = session.Design.Components[0];
            var second = session.Design.Components[1];
            session.ConnectPins(JoinMember.ComponentPin(first.Id, first.PinIds["OUT"]),
                JoinMember.ComponentPin(second.Id, second.PinIds["OUT"]));
            // Sources start off and drive 0. Turning on only the first makes
            // literal opposing 1/0 drivers; initialOn is an authored restart
            // setting and does not toggle an already-running source.
            session.ToggleSource(first.Id);
            yield return null;

            var route = session.Design.Topology.Connectors[0];
            Assert.That(session.Inspector.InspectConnector(route.Id).Value,
                Is.EqualTo(LogicBit.X),
                "Conflicting 1 and 0 drivers resolve to X on their shared wire.");
            AssertValue(first.Id.ToString("D"), art.SourceOne);
            AssertValue(second.Id.ToString("D"), art.SourceZero);
        }

        private static void AssertValue(string sourceId, Mesh expected)
        {
            var value = GameObject.Find("Source value " + sourceId);
            var body = GameObject.Find("Component " + sourceId);
            Assert.That(value, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(value.transform.parent, Is.SameAs(body.transform));
            Assert.That(value.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(expected));
            Assert.That(value.GetComponent<Collider>(), Is.Null,
                "The decoration must not steal a pin or body target.");
        }
    }
}
