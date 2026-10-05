using System;
using System.Collections.Generic;
using System.Numerics;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    // Owns simulated time and event slots, never Unity Update or camera visibility.
    // All source actions at a timestamp are applied in insertion order before
    // its clock edge; the circuit settles once before SR sampling.
    public sealed class WorldSimulationScheduler
    {
        private readonly GraphDrivenOneBitCircuit circuit;
        private readonly SortedDictionary<SimulationTime, List<SourceChange>> sourceChanges =
            new SortedDictionary<SimulationTime, List<SourceChange>>();
        private WorldClockEdgeSchedule activeSchedule;
        private BigInteger nextEdgeIndex;
        private SimulationTime? nextEdgeTime;
        private BigInteger? suspendedRemainingPicoseconds;
        private string pendingFrequencyHz;

        private readonly struct SourceChange
        {
            public Guid ObjectId { get; }
            public bool On { get; }
            public SourceChange(Guid objectId, bool on) { ObjectId = objectId; On = on; }
        }

        public SimulationTime Now { get; private set; } = SimulationTime.Zero;
        public string FrequencyHz { get; private set; }
        public bool ClockRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public string Diagnostic { get; private set; }
        public SimulationTime? NextClockEdge => nextEdgeTime;
        public LogicBit ClockLevel => circuit.WorldClockLevel;
        public BigInteger ClockEdgesProcessed { get; private set; }

        public WorldSimulationScheduler(GraphDrivenOneBitCircuit circuit,
            string frequencyHz = "10")
        {
            this.circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
            new WorldClockEdgeSchedule(frequencyHz, SimulationTime.Zero);
            FrequencyHz = frequencyHz;
            if (circuit.WorldClockLevel != LogicBit.Zero)
                throw new ArgumentException("A new world clock must start at zero.");
        }

        public void QueueSourceChange(Guid objectId, bool on, SimulationTime when)
        {
            if (when.CompareTo(Now) < 0)
                throw new ArgumentOutOfRangeException(nameof(when));
            circuit.Source(objectId); // reject an unknown source before queuing it
            if (!sourceChanges.TryGetValue(when, out var group))
                sourceChanges.Add(when, group = new List<SourceChange>());
            group.Add(new SourceChange(objectId, on));
        }

        public void StartClock()
        {
            if (ClockRunning) return;
            try
            {
                if (suspendedRemainingPicoseconds.HasValue)
                {
                    nextEdgeTime = Now.AddPicoseconds(suspendedRemainingPicoseconds.Value);
                    suspendedRemainingPicoseconds = null;
                    activeSchedule = null;
                }
                else
                {
                    activeSchedule = new WorldClockEdgeSchedule(FrequencyHz, Now);
                    nextEdgeIndex = BigInteger.One;
                    nextEdgeTime = activeSchedule.EdgeTime(nextEdgeIndex);
                }
                ClockRunning = true;
            }
            catch (OverflowException)
            {
                PauseAtTimeLimit();
            }
        }

        public void StopClock()
        {
            if (!ClockRunning) return;
            suspendedRemainingPicoseconds = Now.PicosecondsUntil(nextEdgeTime.Value);
            ClockRunning = false;
            nextEdgeTime = null;
            activeSchedule = null;
        }

        public void PauseSimulation() => IsPaused = true;
        public void ResumeSimulation()
        {
            if (Diagnostic != null)
                throw new InvalidOperationException("Resolve the simulation diagnostic before resuming.");
            IsPaused = false;
        }

        public void SetFrequency(string requestedHz)
        {
            new WorldClockEdgeSchedule(requestedHz, SimulationTime.Zero);
            if (ClockRunning) pendingFrequencyHz = requestedHz;
            else
            {
                FrequencyHz = requestedHz;
                pendingFrequencyHz = null;
                suspendedRemainingPicoseconds = null;
            }
        }

        public void AdvanceUntil(SimulationTime target)
        {
            if (IsPaused) throw new InvalidOperationException("Simulation is paused.");
            if (target.CompareTo(Now) < 0)
                throw new ArgumentOutOfRangeException(nameof(target));
            while (TryNextSlot(out var slot) && slot.CompareTo(target) <= 0)
            {
                Now = slot;
                if (sourceChanges.TryGetValue(slot, out var changes))
                {
                    foreach (var change in changes)
                        circuit.SetSourceOn(change.ObjectId, change.On);
                    sourceChanges.Remove(slot);
                }
                var clockEdge = ClockRunning && nextEdgeTime.HasValue &&
                    nextEdgeTime.Value.Equals(slot);
                if (clockEdge)
                {
                    circuit.SetWorldClockLevel(circuit.WorldClockLevel == LogicBit.Zero
                        ? LogicBit.One : LogicBit.Zero);
                    ClockEdgesProcessed++;
                }
                circuit.AdvanceToSettled();
                if (clockEdge) ScheduleAfterEdge();
                if (IsPaused) return;
            }
            Now = target;
        }

        public void StepClockEdge()
        {
            var wasPaused = IsPaused;
            if (wasPaused && Diagnostic != null)
                throw new InvalidOperationException("Resolve the simulation diagnostic before stepping.");
            IsPaused = false;
            if (!ClockRunning) StartClock();
            if (IsPaused) return;
            var target = nextEdgeTime.Value;
            AdvanceUntil(target);
            StopClock();
            if (wasPaused) IsPaused = true;
        }

        public void StepClockCycle()
        {
            StepClockEdge();
            StepClockEdge();
        }

        public void ResetSimulation()
        {
            circuit.ResetSimulation();
            Now = SimulationTime.Zero;
            ClockRunning = false;
            IsPaused = false;
            Diagnostic = null;
            nextEdgeTime = null;
            activeSchedule = null;
            suspendedRemainingPicoseconds = null;
            pendingFrequencyHz = null;
            sourceChanges.Clear();
            ClockEdgesProcessed = BigInteger.Zero;
        }

        private bool TryNextSlot(out SimulationTime slot)
        {
            var hasSource = false;
            slot = default;
            foreach (var item in sourceChanges)
            { slot = item.Key; hasSource = true; break; }
            if (ClockRunning && nextEdgeTime.HasValue &&
                (!hasSource || nextEdgeTime.Value.CompareTo(slot) < 0))
                slot = nextEdgeTime.Value;
            else if (!hasSource) return false;
            return true;
        }

        private void ScheduleAfterEdge()
        {
            try
            {
                if (pendingFrequencyHz != null)
                {
                    FrequencyHz = pendingFrequencyHz;
                    pendingFrequencyHz = null;
                    activeSchedule = new WorldClockEdgeSchedule(FrequencyHz, Now);
                    nextEdgeIndex = BigInteger.One;
                }
                else if (activeSchedule == null)
                {
                    activeSchedule = new WorldClockEdgeSchedule(FrequencyHz, Now);
                    nextEdgeIndex = BigInteger.One;
                }
                else nextEdgeIndex++;
                nextEdgeTime = activeSchedule.EdgeTime(nextEdgeIndex);
            }
            catch (OverflowException)
            {
                PauseAtTimeLimit();
            }
        }

        private void PauseAtTimeLimit()
        {
            ClockRunning = false;
            nextEdgeTime = null;
            IsPaused = true;
            Diagnostic = "Simulation time limit reached; clock paused before timestamp overflow.";
        }
    }
}
