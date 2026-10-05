using System;
using System.IO;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Interaction;
using SiliconSandbox.Persistence;
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
        private GameObject wallsRoot;
        private Transform playerTransform;
        private Camera playerCamera;
        private CreativeCameraController controller;

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
            playerTransform = playerObject.transform;
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
            playerCamera = camera;
            controller = playerObject.AddComponent<CreativeCameraController>();
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

        public WorldSaveSnapshot CaptureCurrentWorld()
        {
            if (Context == null || playerTransform == null || playerCamera == null)
                throw new InvalidOperationException("Playable world is not ready.");
            var position = playerTransform.position;
            var look = playerCamera.transform.forward.normalized;
            return Context.Capture(new SavedPlayerPose(position.x,
                position.y, position.z, look.x, look.y, look.z));
        }

        public void OpenWorld(WorldSaveSnapshot saved)
        {
            if (WorldView == null || Interaction == null || controller == null)
                throw new InvalidOperationException("Playable world is not ready.");
            Context = OneBitWorldContext.Open(saved);
            Session = Context.Session;
            Inventory = Context.Inventory;
            BuildGeneratedWorld(Session.Design.Bounds);
            controller.SetWorldBounds(Session.Design.Bounds);
            playerTransform.position = new Vector3((float)saved.Player.X,
                (float)saved.Player.Y, (float)saved.Player.Z);
            controller.SetViewDirection(new Vector3(
                (float)saved.Player.LookX, (float)saved.Player.LookY,
                (float)saved.Player.LookZ).normalized);
            WorldView.Attach(Session);
            Interaction.Attach(Session, controller, Inventory);
        }

        private void BuildGeneratedWorld(WorldBounds bounds)
        {
            if (wallsRoot != null)
            {
                wallsRoot.SetActive(false);
                Destroy(wallsRoot);
            }
            wallsRoot = new GameObject("Generated sandbox boundaries");
            transform.position = new Vector3(bounds.WidthCells * 0.5f,
                0.5f, bounds.LengthCells * 0.5f);
            transform.localScale = new Vector3(bounds.WidthCells, 1f,
                bounds.LengthCells);
            GetComponent<Renderer>().material.color =
                new Color(0.84f, 0.76f, 0.59f);
            var height = bounds.HeightCells;
            var middleY = height * 0.5f;
            Wall(wallsRoot.transform, "West", new Vector3(-0.5f, middleY,
                bounds.LengthCells * 0.5f),
                new Vector3(1f, height, bounds.LengthCells + 2f));
            Wall(wallsRoot.transform, "East", new Vector3(bounds.WidthCells + 0.5f, middleY,
                bounds.LengthCells * 0.5f),
                new Vector3(1f, height, bounds.LengthCells + 2f));
            Wall(wallsRoot.transform, "South", new Vector3(bounds.WidthCells * 0.5f, middleY,
                -0.5f), new Vector3(bounds.WidthCells + 2f, height, 1f));
            Wall(wallsRoot.transform, "North", new Vector3(bounds.WidthCells * 0.5f, middleY,
                bounds.LengthCells + 0.5f),
                new Vector3(bounds.WidthCells + 2f, height, 1f));
        }

        private static void Wall(Transform root, string face,
            Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sandbox boundary " + face;
            wall.transform.SetParent(root, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().material.color =
                new Color(0.44f, 0.27f, 0.13f);
        }
    }
}
