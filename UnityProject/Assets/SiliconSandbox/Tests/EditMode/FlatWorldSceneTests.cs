using NUnit.Framework;
using SiliconSandbox.Bootstrap;
using SiliconSandbox.EditorBuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class FlatWorldSceneTests
    {
        [Test]
        public void StandardPresetHasUncappedReferenceOptions()
        {
            var standard = System.Array.IndexOf(QualitySettings.names, "Standard");
            Assert.That(standard, Is.GreaterThanOrEqualTo(0));
            var before = QualitySettings.GetQualityLevel();
            try
            {
                QualitySettings.SetQualityLevel(standard, true);
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
                Assert.That(QualitySettings.antiAliasing, Is.EqualTo(0));
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(20f));
            }
            finally { QualitySettings.SetQualityLevel(before, true); }
        }

        [Test]
        public void AuthoredSmokeSceneHasLevelFloorAndCamera()
        {
            var scene = EditorSceneManager.OpenScene(SliceZeroBuild.ScenePath);
            Assert.That(scene.IsValid(), Is.True);

            var floor = GameObject.Find(FlatWorldSmoke.FloorName);
            Assert.That(floor, Is.Not.Null);
            Assert.That(floor.transform.localScale.x, Is.GreaterThan(0));
            Assert.That(floor.transform.localScale.z, Is.GreaterThan(0));
            Assert.That(floor.transform.position.y - floor.transform.localScale.y / 2, Is.EqualTo(0).Within(0.001f));
            Assert.That(floor.transform.position.x,
                Is.EqualTo(floor.transform.localScale.x * 0.5f).Within(0.001f));
            Assert.That(floor.transform.position.z,
                Is.EqualTo(floor.transform.localScale.z * 0.5f).Within(0.001f));
            Assert.That(floor.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(GameObject.FindWithTag("MainCamera").GetComponent<Camera>(), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(2));
            Assert.That(EditorBuildSettings.scenes[0].path,
                Is.EqualTo(SliceZeroBuild.PlayableScenePath));
            Assert.That(EditorBuildSettings.scenes[1].path,
                Is.EqualTo(SliceZeroBuild.ScenePath));
        }
    }
}
