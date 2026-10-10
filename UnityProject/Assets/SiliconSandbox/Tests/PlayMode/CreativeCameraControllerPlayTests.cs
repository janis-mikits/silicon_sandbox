using System.Collections;
using NUnit.Framework;
using SiliconSandbox.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class CreativeCameraControllerPlayTests
    {
        [UnityTest]
        public IEnumerator StationaryPlayerCanJumpFromFloorButNotMidair()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var player = GameObject.Find("Creative player");
            Assert.That(player, Is.Not.Null);
            var controller = player.GetComponent<CreativeCameraController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Flying, Is.False);

            // Teleport leaves the player standing on the generated floor,
            // without horizontal movement to refresh isGrounded.
            var resting = new Vector3(15.5f, 1.05f, 4f);
            controller.Teleport(resting);
            Physics.SyncTransforms();
            Assert.That(controller.TryJump(), Is.True);
            yield return null;
            Assert.That(player.transform.position.y,
                Is.GreaterThan(resting.y + 0.001f));
            Assert.That(player.transform.position.x,
                Is.EqualTo(resting.x).Within(0.001f));
            Assert.That(player.transform.position.z,
                Is.EqualTo(resting.z).Within(0.001f));

            controller.Teleport(new Vector3(15.5f, 4f, 4f));
            Physics.SyncTransforms();
            Assert.That(controller.TryJump(), Is.False,
                "A second jump must still require ground support.");
        }
    }
}
