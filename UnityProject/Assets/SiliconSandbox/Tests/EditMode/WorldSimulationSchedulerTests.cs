using System;
using System.Numerics;
using NUnit.Framework;
using SiliconSandbox.Contracts;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldSimulationSchedulerTests
    {
        [Test]
        public void InitialStopAndSameTimestampSourceChangePrecedeSrSampling()
        {
            var setup = Setup();
            Assert.That(setup.scheduler.ClockRunning, Is.False);
            Assert.That(setup.scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
            Assert.That(setup.scheduler.Now, Is.EqualTo(SimulationTime.Zero));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.X));
            setup.scheduler.QueueSourceChange(setup.set, true, At(50_000_000_000));
            setup.scheduler.StartClock();
            setup.scheduler.AdvanceUntil(At(49_000_000_000));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.X));
            setup.scheduler.AdvanceUntil(At(50_000_000_000));
            Assert.That(setup.scheduler.ClockLevel, Is.EqualTo(LogicBit.One));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.One));
            Assert.That(setup.scheduler.ClockEdgesProcessed, Is.EqualTo(new BigInteger(1)));
        }

        [Test]
        public void AdvancingPastOffscreenIntervalsProcessesEveryEdge()
        {
            var setup = Setup();
            setup.scheduler.QueueSourceChange(setup.set, true, SimulationTime.Zero);
            setup.scheduler.QueueSourceChange(setup.set, false, At(300_000_000_000));
            setup.scheduler.QueueSourceChange(setup.reset, true, At(300_000_000_000));
            setup.scheduler.StartClock();
            setup.scheduler.AdvanceUntil(At(250_000_000_000));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.One));
            Assert.That(setup.scheduler.ClockEdgesProcessed, Is.EqualTo(new BigInteger(5)));
            // No camera, renderer, or Unity frame call occurs during this advance.
            setup.scheduler.AdvanceUntil(At(350_000_000_000));
            Assert.That(setup.scheduler.ClockEdgesProcessed, Is.EqualTo(new BigInteger(7)));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.Zero));
        }

        [Test]
        public void ManualStepSettlesOneEdgeOrFullCycleAndKeepsClockStopped()
        {
            var setup = Setup();
            setup.scheduler.PauseSimulation();
            setup.scheduler.StepClockEdge();
            Assert.That(setup.scheduler.Now, Is.EqualTo(At(50_000_000_000)));
            Assert.That(setup.scheduler.ClockLevel, Is.EqualTo(LogicBit.One));
            Assert.That(setup.scheduler.ClockRunning, Is.False);
            Assert.That(setup.scheduler.IsPaused, Is.True);
            setup.scheduler.StepClockCycle();
            Assert.That(setup.scheduler.Now, Is.EqualTo(At(150_000_000_000)));
            Assert.That(setup.scheduler.ClockLevel, Is.EqualTo(LogicBit.One));
            Assert.That(setup.scheduler.ClockEdgesProcessed, Is.EqualTo(new BigInteger(3)));
            Assert.That(setup.scheduler.ClockRunning, Is.False);
        }

        [Test]
        public void FrequencyChangeCompletesCurrentIntervalThenUsesNewHalfPeriod()
        {
            var setup = Setup();
            setup.scheduler.StartClock();
            setup.scheduler.AdvanceUntil(At(20_000_000_000));
            setup.scheduler.SetFrequency("20");
            Assert.That(setup.scheduler.NextClockEdge, Is.EqualTo(At(50_000_000_000)));
            setup.scheduler.AdvanceUntil(At(50_000_000_000));
            Assert.That(setup.scheduler.FrequencyHz, Is.EqualTo("20"));
            Assert.That(setup.scheduler.NextClockEdge, Is.EqualTo(At(75_000_000_000)));
            setup.scheduler.AdvanceUntil(At(75_000_000_000));
            Assert.That(setup.scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
        }

        [Test]
        public void StopFreezesRemainingIntervalAndStoppedFrequencyChangeRestartsIt()
        {
            var setup = Setup();
            setup.scheduler.StartClock();
            setup.scheduler.AdvanceUntil(At(20_000_000_000));
            setup.scheduler.StopClock();
            setup.scheduler.AdvanceUntil(At(100_000_000_000));
            Assert.That(setup.scheduler.ClockEdgesProcessed, Is.EqualTo(BigInteger.Zero));
            setup.scheduler.StartClock();
            Assert.That(setup.scheduler.NextClockEdge, Is.EqualTo(At(130_000_000_000)));
            setup.scheduler.StopClock();
            setup.scheduler.SetFrequency("20");
            setup.scheduler.StartClock();
            Assert.That(setup.scheduler.NextClockEdge, Is.EqualTo(At(125_000_000_000)));
        }

        [Test]
        public void ResetRestartsOrdinaryStateTimeAndClockButRetainsFrequency()
        {
            var setup = Setup();
            setup.scheduler.SetFrequency("20");
            setup.scheduler.QueueSourceChange(setup.set, true, SimulationTime.Zero);
            setup.scheduler.StartClock();
            setup.scheduler.AdvanceUntil(At(25_000_000_000));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.One));
            setup.scheduler.ResetSimulation();
            Assert.That(setup.scheduler.Now, Is.EqualTo(SimulationTime.Zero));
            Assert.That(setup.scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
            Assert.That(setup.scheduler.ClockRunning, Is.False);
            Assert.That(setup.scheduler.FrequencyHz, Is.EqualTo("20"));
            Assert.That(setup.scheduler.ClockEdgesProcessed, Is.EqualTo(BigInteger.Zero));
            Assert.That(setup.circuit.Storage(setup.sr).Q, Is.EqualTo(LogicBit.X));
            Assert.That(setup.circuit.Source(setup.set).IsOn, Is.False);
        }

        [Test]
        public void TimestampLimitPausesSafelyInsteadOfWrapping()
        {
            var setup = Setup();
            setup.scheduler.AdvanceUntil(new SimulationTime(ulong.MaxValue,
                999_999_999_999));
            setup.scheduler.StartClock();
            Assert.That(setup.scheduler.IsPaused, Is.True);
            Assert.That(setup.scheduler.ClockRunning, Is.False);
            Assert.That(setup.scheduler.Diagnostic, Does.Contain("time limit"));
            Assert.That(setup.scheduler.Now.WholeSeconds, Is.EqualTo(ulong.MaxValue));
        }

        private static (WorldSimulationScheduler scheduler, GraphDrivenOneBitCircuit circuit,
            Guid set, Guid reset, Guid sr) Setup()
        {
            var set = Guid.NewGuid(); var reset = Guid.NewGuid(); var sr = Guid.NewGuid();
            var plan = new OneBitCircuitPlan(5,
                new[] { new SourceBinding(set, 0), new SourceBinding(reset, 1) },
                Array.Empty<AndBinding>(),
                new[] { new SrBinding(sr, 0, 1, 2, 3, 4,
                    Guid.NewGuid(), Guid.NewGuid()) },
                new WorldClockBinding(Guid.NewGuid(), Guid.NewGuid(), 2));
            var circuit = new GraphDrivenOneBitCircuit(plan);
            return (new WorldSimulationScheduler(circuit), circuit, set, reset, sr);
        }

        private static SimulationTime At(long picoseconds) =>
            new SimulationTime(0, picoseconds);
    }
}
