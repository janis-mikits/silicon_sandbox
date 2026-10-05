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
            Assert.That(fixture.AuthoredTopology, Is.Not.Null);
            var routeParts = Object.FindObjectsByType<RoutePartIdentity>(FindObjectsSortMode.None);
            Assert.That(routeParts.Length, Is.GreaterThan(0));
            foreach (var part in routeParts)
            {
                Assert.That(part.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(part.SpanId != System.Guid.Empty || part.NodeId != System.Guid.Empty,
                    Is.True);
                Assert.That(part.ConnectorId, Is.Not.EqualTo(System.Guid.Empty));
            }

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
                Assert.That(fixture.A.Value, Is.EqualTo(sample[0]));
                Assert.That(fixture.B.Value, Is.EqualTo(sample[1]));
                Assert.That(fixture.Y.Value, Is.EqualTo(sample[2]));
                Assert.That(GameObject.Find("Fixture Y Label").GetComponent<TextMesh>().text,
                    Is.EqualTo("Y = " + sample[2].ToSymbol()));
            }
        }

        [UnityTest]
        public IEnumerator BreakingVisibleSpanSplitsSimulationAndRedrawsSurvivingRoutes()
        {
            SceneManager.LoadScene("FlatWorld");
            yield return null;
            var fixture = GameObject.Find(FlatWorldSmoke.FloorName).GetComponent<AndCircuitFixture>();
            fixture.SetInputs(LogicBit.One, LogicBit.One);
            Assert.That(fixture.Y.Value, Is.EqualTo(LogicBit.One));
            var oldRoute = fixture.AuthoredTopology.Connectors[1];
            var firstNodeId = oldRoute.Nodes[0].Id;
            Assert.That(fixture.BreakSpan(oldRoute.Id, oldRoute.Spans[6].Id), Is.True);
            yield return null;

            Assert.That(fixture.AuthoredTopology.Connectors.Count, Is.EqualTo(4));
            Assert.That(fixture.B.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(fixture.Y.Value, Is.EqualTo(LogicBit.X));
            Assert.That(GameObject.Find(AndCircuitFixture.BConnectorName), Is.Null);
            Assert.That(GameObject.Find("Fixture Y Label").GetComponent<TextMesh>().text,
                Is.EqualTo("Y = X"));
            var sourcePieceFound = false;
            foreach (var route in fixture.AuthoredTopology.Connectors)
            {
                Assert.That(route.Id, Is.Not.EqualTo(oldRoute.Id));
                foreach (var node in route.Nodes)
                    if (node.Id == firstNodeId)
                    {
                        sourcePieceFound = true;
                        var inspected = fixture.Inspector.InspectConnector(route.Id);
                        Assert.That(inspected.Value, Is.EqualTo(LogicBit.One));
                    }
            }
            Assert.That(sourcePieceFound, Is.True);
            fixture.SetInputs(LogicBit.Zero, LogicBit.One);
            Assert.That(fixture.Y.Value, Is.EqualTo(LogicBit.Zero));
        }
    }
}
