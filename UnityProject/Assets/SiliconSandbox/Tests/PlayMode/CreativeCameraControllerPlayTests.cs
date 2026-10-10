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

        [UnityTest]
        public IEnumerator HeldJumpWaitsForLandingAnd150MillisecondPause()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var player = GameObject.Find("Creative player");
            Assert.That(player, Is.Not.Null);
            var controller = player.GetComponent<CreativeCameraController>();
            var character = player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(character, Is.Not.Null);

            var floorPosition = new Vector3(15.5f, 1.05f, 4f);
            controller.Teleport(floorPosition);
            Physics.SyncTransforms();
            Assert.That(controller.TryJump(), Is.True);
            Assert.That(controller.TryHeldJump(), Is.False,
                "Holding Space must not create a second jump before takeoff.");

            yield return null;
            Assert.That(player.transform.position.y,
                Is.GreaterThan(floorPosition.y));
            var airborne = !character.isGrounded;
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!character.isGrounded && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.TryHeldJump(), Is.False,
                    "Holding Space must not cause a midair jump.");
                airborne = true;
                yield return null;
            }
            Assert.That(airborne, Is.True, "The first jump must leave the floor.");
            Assert.That(character.isGrounded, Is.True,
                "The player must land before the held jump can repeat.");
            Assert.That(controller.TryHeldJump(), Is.False,
                "A held jump must wait after landing.");

            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(controller.TryHeldJump(), Is.False,
                "The landing pause must last longer than 80 ms.");
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(controller.TryHeldJump(), Is.True,
                "Holding Space should jump again after the 150 ms landing pause.");
            Assert.That(controller.TryHeldJump(), Is.False,
                "The repeated jump must not retrigger before another landing.");
            yield return null;
            Assert.That(player.transform.position.y,
                Is.GreaterThan(floorPosition.y));
        }

        [UnityTest]
        public IEnumerator HoldingSpaceBeforeLandingAlsoWaits150Milliseconds()
        {
            SceneManager.LoadScene("PlayableWorld");
            yield return null;
            var player = GameObject.Find("Creative player");
            Assert.That(player, Is.Not.Null);
            var controller = player.GetComponent<CreativeCameraController>();
            var character = player.GetComponent<CharacterController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(character, Is.Not.Null);

            controller.Teleport(new Vector3(15.5f, 4f, 4f));
            Physics.SyncTransforms();
            Assert.That(controller.TryHeldJump(), Is.False,
                "Holding Space in midair must not jump.");
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!character.isGrounded && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.TryHeldJump(), Is.False);
                yield return null;
            }
            Assert.That(character.isGrounded, Is.True);
            Assert.That(controller.TryHeldJump(), Is.False,
                "Landing with Space already held must start the pause.");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.TryHeldJump(), Is.True);
        }
    }
}
