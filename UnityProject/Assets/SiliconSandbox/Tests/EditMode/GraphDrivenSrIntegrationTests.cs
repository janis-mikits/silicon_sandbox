using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class GraphDrivenSrIntegrationTests
    {
        [Test]
        public void VersionOneSrPinsProjectToFiveDistinctUndrivenNets()
        {
            var id = Guid.NewGuid();
            var pinIds = new Dictionary<string, Guid>();
            var pins = new List<AuthoredPin>();
            var cell = new GridCell(4, 1, 4);
            foreach (var geometry in BuiltInPinCatalog.Pins(BuiltInPinCatalog.SrFlipFlop, 1))
            {
                var pinId = Guid.NewGuid();
                pinIds.Add(geometry.Key, pinId);
                pins.Add(new AuthoredPin(id, pinId, cell,
                    new QuarterPoint(geometry.Qx, geometry.Qy, geometry.Qz)));
            }
            var topology = new OneBitAuthoredTopology(pins,
                Array.Empty<ConnectorRoute>(), Array.Empty<ElectricalJoin>());
            var built = OneBitCircuitPlanBuilder.Build(topology,
                new[] { new OneBitComponent(id, BuiltInPinCatalog.SrFlipFlop, 1,
                    pinIds, LogicBit.One) });
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            Assert.That(built.Plan.SrFlipFlops.Count, Is.EqualTo(1));
            Assert.That(built.Graph.Nets.Count, Is.EqualTo(5));
            Assert.That(circuit.Storage(id).Q, Is.EqualTo(LogicBit.One),
                "Initial undriven CLK is the baseline, not a live 0-to-Z event.");
            Assert.That(circuit.Net(built.Plan.SrFlipFlops[0].S).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.Net(built.Plan.SrFlipFlops[0].Clock).Value, Is.EqualTo(LogicBit.Z));
        }

        [Test]
        public void TwoSrComponentsFollowAcceptedThreeEdgeSequenceIndependently()
        {
            var aSet = Guid.NewGuid(); var aReset = Guid.NewGuid();
            var bSet = Guid.NewGuid(); var bReset = Guid.NewGuid();
            var clock = Guid.NewGuid(); var aId = Guid.NewGuid(); var bId = Guid.NewGuid();
            var plan = new OneBitCircuitPlan(9,
                new[]
                {
                    new SourceBinding(aSet, 0), new SourceBinding(aReset, 1),
                    new SourceBinding(bSet, 2), new SourceBinding(bReset, 3),
                    new SourceBinding(clock, 4)
                }, Array.Empty<AndBinding>(), new[]
                {
                    Sr(aId, 0, 1, 4, 5, 6), Sr(bId, 2, 3, 4, 7, 8)
                });
            var circuit = new GraphDrivenOneBitCircuit(plan);
            AssertQ(LogicBit.X, LogicBit.X);

            circuit.SetSourceOn(aSet, true);
            circuit.SetSourceOn(clock, true);
            circuit.AdvanceToSettled();
            AssertQ(LogicBit.One, LogicBit.X);

            circuit.SetSourceOn(clock, false);
            circuit.SetSourceOn(aSet, false);
            circuit.SetSourceOn(bReset, true);
            circuit.AdvanceToSettled();
            circuit.SetSourceOn(clock, true);
            circuit.AdvanceToSettled();
            AssertQ(LogicBit.One, LogicBit.Zero);

            circuit.SetSourceOn(clock, false);
            circuit.SetSourceOn(bReset, false);
            circuit.SetSourceOn(aReset, true);
            circuit.AdvanceToSettled();
            circuit.SetSourceOn(clock, true);
            circuit.AdvanceToSettled();
            AssertQ(LogicBit.Zero, LogicBit.Zero);

            circuit.ResetSimulation();
            AssertQ(LogicBit.X, LogicBit.X);
            Assert.That(circuit.Source(aReset).IsOn, Is.False);
            Assert.That(circuit.Source(clock).IsOn, Is.False);
            Assert.That(circuit.Net(4).Value, Is.EqualTo(LogicBit.Zero));
            circuit.SetSourceOn(clock, true);
            circuit.AdvanceToSettled();
            AssertQ(LogicBit.X, LogicBit.X);

            void AssertQ(LogicBit expectedA, LogicBit expectedB)
            {
                Assert.That(circuit.Storage(aId).Q, Is.EqualTo(expectedA));
                Assert.That(circuit.Storage(bId).Q, Is.EqualTo(expectedB));
                Assert.That(circuit.Net(5).Value, Is.EqualTo(expectedA));
                Assert.That(circuit.Net(7).Value, Is.EqualTo(expectedB));
            }
        }

        [Test]
        public void SameEdgeSamplesEveryFlipFlopBeforeAnyStateUpdate()
        {
            var setA = Guid.NewGuid(); var clock = Guid.NewGuid();
            var reset = Guid.NewGuid();
            var aId = Guid.NewGuid(); var bId = Guid.NewGuid();
            var plan = new OneBitCircuitPlan(7,
                new[] { new SourceBinding(setA, 0), new SourceBinding(clock, 1),
                    new SourceBinding(reset, 2) },
                Array.Empty<AndBinding>(), new[]
                {
                    Sr(aId, 0, 2, 1, 3, 4, LogicBit.Zero),
                    Sr(bId, 3, 2, 1, 5, 6, LogicBit.Zero)
                });
            var circuit = new GraphDrivenOneBitCircuit(plan);
            circuit.SetSourceOn(setA, true);
            circuit.SetSourceOn(clock, true);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Storage(aId).Q, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Storage(bId).Q, Is.EqualTo(LogicBit.Zero),
                "B must sample A's prior zero, not its new one.");
            circuit.SetSourceOn(clock, false);
            circuit.AdvanceToSettled();
            circuit.SetSourceOn(clock, true);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Storage(bId).Q, Is.EqualTo(LogicBit.One));
        }

        private static SrBinding Sr(Guid objectId, int s, int r, int clock,
            int q, int qBar, LogicBit? initialQ = null) =>
            new SrBinding(objectId, s, r, clock, q, qBar,
                Guid.NewGuid(), Guid.NewGuid(), initialQ);
    }
}
