using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class AndFixtureInspectionTests
    {
        [Test]
        public void ReleasedBAndUncertainYExplainDifferentCausesAndActualConnections()
        {
            var design = AndFixtureDesign.Create();
            var circuit = new OneBitAndCircuit(AndFixtureGraphBuilder.Build(design));
            circuit.ConfigureA(LogicBit.One, true);
            circuit.ConfigureB(LogicBit.Z, true);
            circuit.AdvanceToSettled();
            var inspector = new AndFixtureInspection(design, circuit);

            var b = inspector.InspectConnector(design.Connectors[1].Id);
            Assert.That(b.Width, Is.EqualTo(1));
            Assert.That(b.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(b.Explanation, Is.EqualTo("No active driver."));
            Assert.That(b.ActiveDrivers.Count, Is.EqualTo(0));
            Assert.That(b.ConnectedPins, Is.EquivalentTo(new[]
            {
                Pin(design.SourceB.Pin("OUT")), Pin(design.Gate.Pin("B"))
            }));

            var y = inspector.InspectConnector(design.Connectors[2].Id);
            Assert.That(y.Value, Is.EqualTo(LogicBit.X));
            Assert.That(y.Explanation, Does.Contain("input B is Z"));
            Assert.That(y.Explanation, Does.Not.Contain("Conflicting"));
            Assert.That(y.ConnectedPins, Is.EquivalentTo(new[] { Pin(design.Gate.Pin("Y")) }));
            Assert.That(y.ActiveDrivers.Count, Is.EqualTo(1));
            Assert.That(y.ActiveDrivers[0].Pin, Is.EqualTo(Pin(design.Gate.Pin("Y"))));
        }

        [Test]
        public void InspectionReadsLatestSettledValueAfterSourceChange()
        {
            var design = AndFixtureDesign.Create();
            var circuit = new OneBitAndCircuit(AndFixtureGraphBuilder.Build(design));
            var inspector = new AndFixtureInspection(design, circuit);
            Assert.That(inspector.InspectConnector(design.Connectors[2].Id).Value,
                Is.EqualTo(LogicBit.Zero));

            circuit.ConfigureA(LogicBit.One, true);
            circuit.ConfigureB(LogicBit.One, true);
            circuit.AdvanceToSettled();
            var after = inspector.InspectConnector(design.Connectors[2].Id);
            Assert.That(after.Value, Is.EqualTo(LogicBit.One));
            Assert.That(after.Explanation, Is.EqualTo("Known driven value."));
        }

        private static JoinMember Pin(FixturePinRef pin) =>
            JoinMember.ComponentPin(pin.ObjectId, pin.PinId);
    }
}
