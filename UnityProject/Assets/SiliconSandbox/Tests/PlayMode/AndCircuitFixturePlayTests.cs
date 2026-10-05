using System.Collections;
using NUnit.Framework;
using SiliconSandbox.Bootstrap;
using SiliconSandbox.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SiliconSandbox.Tests.PlayMode
{
    public sealed class AndCircuitFixturePlayTests
    {
        [UnityTest]
        public IEnumerator FlatWorldFixtureShowsSettledProfessorAndCases()
        {
            SceneManager.LoadScene("FlatWorld");
            yield return null;
            var floor = GameObject.Find(FlatWorldSmoke.FloorName);
            var fixture = floor.GetComponent<AndCircuitFixture>();
            Assert.That(fixture, Is.Not.Null);
            Assert.That(GameObject.Find(AndCircuitFixture.GateObjectName), Is.Not.Null);
            Assert.That(GameObject.Find(AndCircuitFixture.AConnectorName), Is.Not.Null);
            Assert.That(GameObject.Find(AndCircuitFixture.BConnectorName), Is.Not.Null);
            Assert.That(GameObject.Find(AndCircuitFixture.YConnectorName), Is.Not.Null);
            Assert.That(GameObject.Find(AndCircuitFixture.AConnectorName + " Identity Cap"), Is.Not.Null);

            var cases = new[]
            {
                new[] { LogicBit.Zero, LogicBit.One, LogicBit.Zero },
                new[] { LogicBit.One, LogicBit.One, LogicBit.One },
                new[] { LogicBit.Zero, LogicBit.Z, LogicBit.Zero },
                new[] { LogicBit.One, LogicBit.Z, LogicBit.X },
                new[] { LogicBit.One, LogicBit.X, LogicBit.X }
            };
            foreach (var sample in cases)
            {
                fixture.SetInputs(sample[0], sample[1]);
                Assert.That(fixture.Circuit.A.Value, Is.EqualTo(sample[0]));
                Assert.That(fixture.Circuit.B.Value, Is.EqualTo(sample[1]));
                Assert.That(fixture.Circuit.Y.Value, Is.EqualTo(sample[2]));
                Assert.That(GameObject.Find("Fixture Y Label").GetComponent<TextMesh>().text,
                    Is.EqualTo("Y = " + sample[2].ToSymbol()));
            }
        }
    }
}
