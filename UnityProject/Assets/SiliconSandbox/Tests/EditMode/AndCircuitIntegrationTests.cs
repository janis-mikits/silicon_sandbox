using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class AndCircuitIntegrationTests
    {
        [Test]
        public void SourceChangesSettleThroughExplicitFixtureConnectorsWithClockStopped()
        {
            var design = AndFixtureDesign.Create();
            var wiring = AndFixtureGraphBuilder.Build(design);
            var circuit = new OneBitAndCircuit(wiring);
            Assert.That(circuit.A.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.B.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.Y.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.ANetConnectorId, Is.EqualTo(design.Connectors[0].Id));
            Assert.That(circuit.BNetConnectorId, Is.EqualTo(design.Connectors[1].Id));
            Assert.That(circuit.YNetConnectorId, Is.EqualTo(design.Connectors[2].Id));

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
                circuit.ConfigureA(sample[0], true);
                circuit.ConfigureB(sample[1], true);
                circuit.AdvanceToSettled();
                Assert.That(circuit.A.Value, Is.EqualTo(sample[0]));
                Assert.That(circuit.B.Value, Is.EqualTo(sample[1]));
                Assert.That(circuit.Y.Value, Is.EqualTo(sample[2]));
            }
            Assert.That(circuit.B.Cause, Is.EqualTo(ResolutionCause.UnknownDriver));
        }

        [Test]
        public void ReleasedSourceIsZAndDoesNotBecomeConflict()
        {
            var circuit = new OneBitAndCircuit(AndFixtureGraphBuilder.Build(AndFixtureDesign.Create()));
            circuit.ConfigureA(LogicBit.One, true);
            circuit.ConfigureB(LogicBit.Z, true);
            circuit.AdvanceToSettled();
            Assert.That(circuit.B.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.B.Cause, Is.EqualTo(ResolutionCause.Undriven));
            Assert.That(circuit.Y.Value, Is.EqualTo(LogicBit.X));
            Assert.That(circuit.Y.Cause, Is.EqualTo(ResolutionCause.UnknownDriver));
        }

        [Test]
        public void SourceOffDrivesZeroAndResetRestoresConfiguredInitialState()
        {
            var circuit = new OneBitAndCircuit(AndFixtureGraphBuilder.Build(AndFixtureDesign.Create()));
            circuit.ConfigureA(LogicBit.Z, false);
            circuit.ConfigureB(LogicBit.One, true);
            circuit.SetAOn(true);
            circuit.AdvanceToSettled();
            Assert.That(circuit.A.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.Y.Value, Is.EqualTo(LogicBit.X));
            circuit.ResetSources();
            circuit.AdvanceToSettled();
            Assert.That(circuit.A.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.B.Value, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Y.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.SourceA.ConfiguredOnValue, Is.EqualTo(LogicBit.Z));
        }

        [Test]
        public void GraphRejectsAnOccupiedPinOrUnexpectedConnection()
        {
            var original = AndFixtureDesign.Create();
            var bad = new AndFixtureDesign(original.SourceA, original.SourceB, original.Gate,
                original.Connectors[0], original.Connectors[1],
                new FixtureConnector(Guid.Parse("727c38bd-1043-4fc5-b424-2b5e31daa713"),
                    original.SourceA.Pin("OUT"), original.Gate.Pin("Y")));
            Assert.Throws<ArgumentException>(() => AndFixtureGraphBuilder.Build(bad));
        }

        [Test]
        public void ApprovedVersionOnePinGeometryIsExact()
        {
            AssertPins(BuiltInPinCatalog.Source, new[] { "OUT:4,1,1:Output" });
            AssertPins(BuiltInPinCatalog.And, new[] { "A:0,1,1:Input", "B:0,3,1:Input", "Y:4,1,1:Output" });
            AssertPins(BuiltInPinCatalog.SrFlipFlop, new[]
            {
                "S:0,1,1:Input", "R:0,3,1:Input", "CLK:1,1,0:Input",
                "Q:4,1,1:Output", "Q_bar:4,3,1:Output"
            });
        }

        private static void AssertPins(string type, IReadOnlyList<string> expected)
        {
            var pins = BuiltInPinCatalog.Pins(type, 1);
            Assert.That(pins.Count, Is.EqualTo(expected.Count));
            for (var i = 0; i < pins.Count; i++)
                Assert.That(pins[i].Key + ":" + pins[i].Qx + "," + pins[i].Qy + "," + pins[i].Qz + ":" + pins[i].Direction,
                    Is.EqualTo(expected[i]));
        }
    }
}
