using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Bootstrap;
using SiliconSandbox.Contracts;
using SiliconSandbox.Interaction;
using SiliconSandbox.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class ShiftPlacementPlayTests
    {
        [UnityTest]
        public IEnumerator ShiftRightClickPlacesBlockAboveSourceWithoutTogglingIt()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = Bootstrap();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = bootstrap.Session.Design.Components[0];
            yield return null;

            var interaction = bootstrap.Interaction;
            bootstrap.Inventory.SelectHotbar(2); // AND block
            AimAtSourceTop(interaction);
            Invoke(interaction, "UpdateGhost");
            Invoke(interaction, "RightClickWithModifier", false);
            Assert.That(bootstrap.Session.Design.Components.Count, Is.EqualTo(1),
                "Ordinary right-click must keep the source-use action.");
            Assert.That(SourceValue(bootstrap, source), Is.EqualTo(LogicBit.One));

            Invoke(interaction, "RightClickWithModifier", true);
            Assert.That(bootstrap.Session.Design.Components.Count, Is.EqualTo(2));
            Assert.That(bootstrap.Session.Design.Components[1].TypeId,
                Is.EqualTo(BuiltInPinCatalog.And));
            Assert.That(bootstrap.Session.Design.Components[1].AnchorCell,
                Is.EqualTo(new GridCell(6, 2, 6)));
            Assert.That(SourceValue(bootstrap, source), Is.EqualTo(LogicBit.One),
                "Forced placement must not toggle the targeted source.");
        }

        [UnityTest]
        public IEnumerator ShiftRightClickPlacesOpenWireAboveSource()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = Bootstrap();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = bootstrap.Session.Design.Components[0];
            yield return null;

            var interaction = bootstrap.Interaction;
            bootstrap.Inventory.SelectHotbar(1); // ordinary wire
            AimAtSourceTop(interaction);
            Invoke(interaction, "RightClickWithModifier", true);
            var topology = bootstrap.Session.Design.Topology;
            Assert.That(topology.Connectors.Count, Is.EqualTo(1));
            Assert.That(topology.Joins.Count, Is.Zero,
                "An open wire must not join the source by proximity.");
            var route = topology.Connectors[0];
            Assert.That(route.Kind, Is.EqualTo("wire"));
            Assert.That(route.Spans.Count, Is.EqualTo(1));
            Assert.That(route.Nodes.Count, Is.EqualTo(2));
            foreach (var node in route.Nodes)
                Assert.That(node.Cell, Is.EqualTo(new GridCell(6, 2, 6)));
            Assert.That(SourceValue(bootstrap, source), Is.EqualTo(LogicBit.Zero),
                "Forced placement must not toggle the source.");
        }

        [UnityTest]
        public IEnumerator ShiftRightClickPlacesOpenWorldClockLinkAboveSource()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = Bootstrap();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            yield return null;

            bootstrap.Inventory.SelectHotbar(4); // World Clock Link
            AimAtSourceTop(bootstrap.Interaction);
            Invoke(bootstrap.Interaction, "RightClickWithModifier", true);
            var topology = bootstrap.Session.Design.Topology;
            Assert.That(topology.Connectors.Count, Is.EqualTo(1));
            Assert.That(topology.Joins.Count, Is.Zero);
            var route = topology.Connectors[0];
            Assert.That(route.Kind, Is.EqualTo("netLink"));
            Assert.That(route.LinkName, Is.EqualTo("@world-clock"));
            Assert.That(route.Nodes[0].Cell,
                Is.EqualTo(new GridCell(6, 2, 6)));
        }

        [UnityTest]
        public IEnumerator ShiftRightClickOnPinKeepsWireConnectionFlow()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = Bootstrap();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var source = bootstrap.Session.Design.Components[0];
            yield return null;

            WorldSelectablePart pin = null;
            foreach (var part in UnityEngine.Object.FindObjectsByType<
                WorldSelectablePart>(FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ComponentPin &&
                    part.OwnerId == source.Id)
                { pin = part; break; }
            Assert.That(pin, Is.Not.Null);
            bootstrap.Inventory.SelectHotbar(1); // ordinary wire
            var interaction = bootstrap.Interaction;
            SetField(interaction, "hovered", pin);
            Invoke(interaction, "RightClickWithModifier", true);
            Assert.That(bootstrap.Session.Design.Topology.Connectors.Count,
                Is.Zero, "A first pin click starts a route, not an open stub.");
            Assert.That(GetField(interaction, "wireStart"), Is.Not.Null);
            Assert.That(SourceValue(bootstrap, source), Is.EqualTo(LogicBit.Zero));
        }

        [UnityTest]
        public IEnumerator ShiftConnectorTargetDoesNotCreateOrdinaryJunction()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var bootstrap = Bootstrap();
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(6, 1, 6), GridOrientation.Default);
            bootstrap.Session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(10, 1, 6), GridOrientation.Default);
            var first = bootstrap.Session.Design.Components[0];
            var second = bootstrap.Session.Design.Components[1];
            bootstrap.Session.PlaceWireStub(JoinMember.ComponentPin(first.Id,
                first.PinIds["OUT"]), new GridCell(8, 1, 6));
            yield return null;

            WorldSelectablePart target = null;
            foreach (var part in UnityEngine.Object.FindObjectsByType<
                WorldSelectablePart>(FindObjectsSortMode.None))
                if (part.Kind == WorldPartKind.ConnectorNode &&
                    part.OwnerId == bootstrap.Session.Design.Topology.Connectors[0].Id)
                { target = part; break; }
            Assert.That(target, Is.Not.Null);
            var interaction = bootstrap.Interaction;
            bootstrap.Inventory.SelectHotbar(1); // ordinary wire
            SetField(interaction, "hovered", target);
            SetField(interaction, "wireStart", (JoinMember?)
                JoinMember.ComponentPin(second.Id, second.PinIds["OUT"]));
            var connectorCount = bootstrap.Session.Design.Topology.Connectors.Count;
            var joinCount = bootstrap.Session.Design.Topology.Joins.Count;
            Invoke(interaction, "RightClickWithModifier", true);
            Assert.That(bootstrap.Session.Design.Topology.Connectors.Count,
                Is.EqualTo(connectorCount));
            Assert.That(bootstrap.Session.Design.Topology.Joins.Count,
                Is.EqualTo(joinCount),
                "Reserved concatenation must not silently become a junction.");
        }

        private static PlayableWorldBootstrap Bootstrap() =>
            GameObject.Find(FlatWorldSmoke.FloorName)
                .GetComponent<PlayableWorldBootstrap>();

        private static LogicBit SourceValue(PlayableWorldBootstrap bootstrap,
            PlacedOneBitComponent source) =>
            bootstrap.Session.Inspector.InspectPin(source.Id,
                source.PinIds["OUT"]).Value;

        private static void AimAtSourceTop(OneBitWorldInteraction interaction)
        {
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            camera.transform.SetPositionAndRotation(
                new Vector3(6.5f, 4.5f, 6.5f),
                Quaternion.Euler(90f, 0f, 0f));
            Physics.SyncTransforms();
            Invoke(interaction, "TargetAtCrosshair");
            Assert.That(interaction.HoveredPart, Is.Not.Null);
            Assert.That(interaction.HoveredPart.Kind,
                Is.EqualTo(WorldPartKind.ComponentBody));
        }

        private static void Invoke(OneBitWorldInteraction interaction,
            string methodName, params object[] arguments)
        {
            var method = typeof(OneBitWorldInteraction).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(interaction, arguments);
        }

        private static void SetField(OneBitWorldInteraction interaction,
            string name, object value)
        {
            var field = typeof(OneBitWorldInteraction).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(interaction, value);
        }

        private static object GetField(OneBitWorldInteraction interaction,
            string name)
        {
            var field = typeof(OneBitWorldInteraction).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(interaction);
        }
    }
}
