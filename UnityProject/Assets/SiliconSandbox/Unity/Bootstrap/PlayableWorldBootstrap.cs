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

        public OneBitWorldSession Session { get; private set; }
        public OneBitPlayerInventory Inventory { get; private set; }
        public OneBitWorldView WorldView { get; private set; }
        public OneBitWorldInteraction Interaction { get; private set; }

        private void Start()
        {
            Session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(32, 32, 16)));
            Inventory = OneBitPlayerInventory.NewFreeplay();
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
    }
}
