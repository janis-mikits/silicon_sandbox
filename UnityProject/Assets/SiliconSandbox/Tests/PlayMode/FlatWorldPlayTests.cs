using System.Collections;
using NUnit.Framework;
using SiliconSandbox.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class FlatWorldPlayTests
    {
        [UnityTest]
        public IEnumerator FlatWorldSceneRunsWithSolidFloorAndCamera()
        {
            SceneManager.LoadScene("FlatWorld");
            yield return null;

            var floor = GameObject.Find(FlatWorldSmoke.FloorName);
            Assert.That(floor, Is.Not.Null);
            Assert.That(floor.GetComponent<Collider>().enabled, Is.True);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("FlatWorld"));
        }
    }
}
