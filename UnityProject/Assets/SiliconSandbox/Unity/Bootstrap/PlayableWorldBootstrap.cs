using System;
using System.IO;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Interaction;
using SiliconSandbox.Presentation;
using UnityEngine;

namespace SiliconSandbox.Bootstrap
{
    public sealed class PlayableWorldBootstrap : MonoBehaviour
    {
        private const string SmokeOutputVariable = "SILICON_SANDBOX_SMOKE_OUTPUT";

        public OneBitWorldContext Context { get; private set; }
        public OneBitWorldSession Session { get; private set; }
        public OneBitPlayerInventory Inventory { get; private set; }
        public OneBitWorldView WorldView { get; private set; }
        public OneBitWorldInteraction Interaction { get; private set; }

        private void Start()
        {
            Context = OneBitWorldContext.NewFreeplay("New World",
                new WorldBounds(32, 32, 16));
            Session = Context.Session;
            Inventory = Context.Inventory;
            BuildGeneratedWorld(Session.Design.Bounds);
            var renderedWorld = new GameObject("Authored one-bit world");
            WorldView = renderedWorld.AddComponent<OneBitWorldView>();
            WorldView.Attach(Session);

            var playerObject = new GameObject("Creative player");
            playerObject.transform.position = new Vector3(15.5f, 1.05f, 4f);
            var capsule = playerObject.AddComponent<CharacterController>();
            capsule.height = 3f;
            capsule.radius = 0.32f;
            capsule.center = new Vector3(0f, 1.5f, 0f);
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Playable world needs a MainCamera.");
            camera.transform.SetParent(playerObject.transform, false);
            camera.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.fieldOfView = 70f;
            var controller = playerObject.AddComponent<CreativeCameraController>();
            controller.SetCameraPivot(camera.transform);
            controller.SetWorldBounds(Session.Design.Bounds);
            Interaction = playerObject.AddComponent<OneBitWorldInteraction>();
            Interaction.Attach(Session, controller, Inventory);

            var output = Environment.GetEnvironmentVariable(SmokeOutputVariable);
            if (string.IsNullOrEmpty(output)) return;
            var valid = Session.Circuit != null && Session.Design.Components.Count == 0 &&
                WorldView.Session == Session && Interaction.Session == Session &&
                GetComponent<Collider>() != null;
            File.WriteAllText(output, valid ? "PASS\n" : "FAIL\n");
            UnityEngine.Application.Quit(valid ? 0 : 1);
        }

        private void BuildGeneratedWorld(WorldBounds bounds)
        {
            transform.position = new Vector3(bounds.WidthCells * 0.5f,
                0.5f, bounds.LengthCells * 0.5f);
            transform.localScale = new Vector3(bounds.WidthCells, 1f,
                bounds.LengthCells);
            GetComponent<Renderer>().material.color =
                new Color(0.84f, 0.76f, 0.59f);
            var height = bounds.HeightCells;
            var middleY = height * 0.5f;
            Wall("West", new Vector3(-0.5f, middleY,
                bounds.LengthCells * 0.5f),
                new Vector3(1f, height, bounds.LengthCells + 2f));
            Wall("East", new Vector3(bounds.WidthCells + 0.5f, middleY,
                bounds.LengthCells * 0.5f),
                new Vector3(1f, height, bounds.LengthCells + 2f));
            Wall("South", new Vector3(bounds.WidthCells * 0.5f, middleY,
                -0.5f), new Vector3(bounds.WidthCells + 2f, height, 1f));
            Wall("North", new Vector3(bounds.WidthCells * 0.5f, middleY,
                bounds.LengthCells + 0.5f),
                new Vector3(bounds.WidthCells + 2f, height, 1f));
        }

        private static void Wall(string face, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sandbox boundary " + face;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material.color =
                new Color(0.44f, 0.27f, 0.13f);
        }
    }
}
