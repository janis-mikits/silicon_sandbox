using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class GraphDrivenCircuitTests
    {
        [Test]
        public void EmptyWorldHasNoInventedNetsOrComponents()
        {
            var topology = new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                Array.Empty<ConnectorRoute>(), Array.Empty<ElectricalJoin>());
            var built = OneBitCircuitPlanBuilder.Build(topology, Array.Empty<OneBitComponent>());
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            circuit.AdvanceToSettled();
            Assert.That(built.Plan.NetCount, Is.EqualTo(0));
            Assert.That(built.Graph.Nets.Count, Is.EqualTo(0));
        }

        [Test]
        public void AuthoredGraphDrivesAllSixteenAndCasesWithLiteralExpectedValues()
        {
            var fixture = AndFixtureDesign.Create();
            var built = Build(fixture, fixture.Topology);
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            var inputs = new[] { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.Z };
            var expected = new[]
            {
                new[] { LogicBit.Zero, LogicBit.Zero, LogicBit.Zero, LogicBit.Zero },
                new[] { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.X },
                new[] { LogicBit.Zero, LogicBit.X, LogicBit.X, LogicBit.X },
                new[] { LogicBit.Zero, LogicBit.X, LogicBit.X, LogicBit.X }
            };
            var y = built.NetIndex(Pin(fixture.Gate.Pin("Y")));
            for (var a = 0; a < 4; a++)
                for (var b = 0; b < 4; b++)
                {
                    circuit.ConfigureSource(fixture.SourceA.Id, inputs[a], true);
                    circuit.ConfigureSource(fixture.SourceB.Id, inputs[b], true);
                    circuit.AdvanceToSettled();
                    Assert.That(circuit.Net(y).Value, Is.EqualTo(expected[a][b]),
                        "A=" + inputs[a] + " B=" + inputs[b]);
                }
        }

        [Test]
        public void BreakRebuildsConnectivityAndKeepsUnrelatedLiveSourceState()
        {
            var fixture = AndFixtureDesign.Create();
            var before = Build(fixture, fixture.Topology);
            var circuit = new GraphDrivenOneBitCircuit(before.Plan);
            circuit.ConfigureSource(fixture.SourceA.Id, LogicBit.One, true);
            circuit.ConfigureSource(fixture.SourceB.Id, LogicBit.One, true);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Net(before.NetIndex(Pin(fixture.Gate.Pin("Y")))).Value,
                Is.EqualTo(LogicBit.One));

            var bRoute = fixture.Topology.Connectors[1];
            var edit = OneBitTopologyEdits.BreakSpan(fixture.Topology, bRoute.Id,
                bRoute.Spans[6].Id);
            var after = Build(fixture, edit.Design);
            circuit.ReplacePlan(after.Plan);

            Assert.That(circuit.Source(fixture.SourceA.Id).IsOn, Is.True);
            Assert.That(circuit.Source(fixture.SourceB.Id).IsOn, Is.True);
            Assert.That(circuit.Net(after.NetIndex(Pin(fixture.SourceA.Pin("OUT")))).Value,
                Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Net(after.NetIndex(Pin(fixture.SourceB.Pin("OUT")))).Value,
                Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Net(after.NetIndex(Pin(fixture.Gate.Pin("B")))).Value,
                Is.EqualTo(LogicBit.Z), "Disconnected input is undriven.");
            Assert.That(circuit.Net(after.NetIndex(Pin(fixture.Gate.Pin("Y")))).Value,
                Is.EqualTo(LogicBit.X), "1 AND undriven is X.");
            Assert.That(circuit.Net(after.NetIndex(Pin(fixture.Gate.Pin("Y")))).Cause,
                Is.EqualTo(ResolutionCause.UnknownDriver));

            circuit.SetSourceOn(fixture.SourceA.Id, false);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Net(after.NetIndex(Pin(fixture.Gate.Pin("Y")))).Value,
                Is.EqualTo(LogicBit.Zero), "Known zero dominates undriven input.");
        }

        [Test]
        public void PlanRejectsMissingComponentPinInsteadOfInventingConnection()
        {
            var fixture = AndFixtureDesign.Create();
            var components = Components(fixture);
            components.RemoveAt(1);
            Assert.Throws<ArgumentException>(() =>
                OneBitCircuitPlanBuilder.Build(fixture.Topology, components));
        }

        [Test]
        public void InspectionUsesGraphMembershipAndDistinguishesReleasedNetFromUncertainOutput()
        {
            var fixture = AndFixtureDesign.Create();
            var components = Components(fixture);
            var built = OneBitCircuitPlanBuilder.Build(fixture.Topology, components);
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            circuit.ConfigureSource(fixture.SourceA.Id, LogicBit.One, true);
            circuit.ConfigureSource(fixture.SourceB.Id, LogicBit.Z, true);
            circuit.AdvanceToSettled();
            var inspector = new OneBitCircuitInspection(fixture.Topology, components, built, circuit);
            var b = inspector.InspectConnector(fixture.Connectors[1].Id);
            var y = inspector.InspectConnector(fixture.Connectors[2].Id);
            Assert.That(b.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(b.ResolutionCause, Is.EqualTo(ResolutionCause.Undriven));
            Assert.That(b.ActiveDrivers.Count, Is.EqualTo(0));
            Assert.That(b.ConnectedPins, Does.Contain(Pin(fixture.SourceB.Pin("OUT"))));
            Assert.That(b.ConnectedPins, Does.Contain(Pin(fixture.Gate.Pin("B"))));
            Assert.That(y.Value, Is.EqualTo(LogicBit.X));
            Assert.That(y.ResolutionCause, Is.EqualTo(ResolutionCause.UnknownDriver));
            Assert.That(y.Explanation, Does.Contain("input B is Z"));
            Assert.That(y.Explanation, Does.Not.Contain("Conflicting"));
        }

        private static BuiltOneBitCircuitPlan Build(AndFixtureDesign fixture,
            OneBitAuthoredTopology topology) =>
            OneBitCircuitPlanBuilder.Build(topology, Components(fixture));

        private static List<OneBitComponent> Components(AndFixtureDesign fixture) =>
            new List<OneBitComponent>
            {
                Component(fixture.SourceA, "OUT"), Component(fixture.SourceB, "OUT"),
                Component(fixture.Gate, "A", "B", "Y")
            };

        private static OneBitComponent Component(FixtureComponent component, params string[] keys)
        {
            var pins = new Dictionary<string, Guid>();
            foreach (var key in keys) pins.Add(key, component.Pin(key).PinId);
            return new OneBitComponent(component.Id, component.TypeId, component.TypeVersion, pins);
        }

        private static JoinMember Pin(FixturePinRef pin) =>
            JoinMember.ComponentPin(pin.ObjectId, pin.PinId);
    }
}
