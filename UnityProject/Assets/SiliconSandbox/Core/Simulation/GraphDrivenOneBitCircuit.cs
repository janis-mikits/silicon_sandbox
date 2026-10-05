using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    // Event-driven one-bit subset. A world-time scheduler calls this at safe
    // slots; rendering and Unity frame rate never drive electrical propagation.
    public sealed class GraphDrivenOneBitCircuit
    {
        private enum WorkKind { Source, And, StorageOutput }

        private OneBitCircuitPlan plan;
        private OneBitNet[] nets;
        private Dictionary<Guid, ConstantLogicSource> sources;
        private Dictionary<Guid, OneBitSrFlipFlop> storage;
        private List<int>[] gatesByInput;
        private Queue<(WorkKind kind, int index)> pending;
        private bool initializing;

        public GraphDrivenOneBitCircuit(OneBitCircuitPlan initialPlan)
            : this(initialPlan, null, null, false) { }

        private GraphDrivenOneBitCircuit(OneBitCircuitPlan next,
            Dictionary<Guid, ConstantLogicSource> oldSources,
            Dictionary<Guid, OneBitSrFlipFlop> oldStorage, bool reset)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            plan = next;
            initializing = oldStorage == null || reset;
            sources = new Dictionary<Guid, ConstantLogicSource>();
            foreach (var binding in next.Sources)
                sources.Add(binding.ObjectId, oldSources != null &&
                    oldSources.TryGetValue(binding.ObjectId, out var existing)
                    ? reset ? new ConstantLogicSource(existing.ConfiguredOnValue,
                        existing.InitialOn) : existing
                    : new ConstantLogicSource());
            storage = new Dictionary<Guid, OneBitSrFlipFlop>();
            foreach (var binding in next.SrFlipFlops)
                storage.Add(binding.ObjectId, oldStorage != null &&
                    oldStorage.TryGetValue(binding.ObjectId, out var prior)
                    ? reset ? new OneBitSrFlipFlop(binding.InitialQ) :
                        prior.CopyWithInitialQ(binding.InitialQ)
                    : new OneBitSrFlipFlop(binding.InitialQ));
            nets = new OneBitNet[next.NetCount];
            for (var i = 0; i < nets.Length; i++) nets[i] = new OneBitNet(Guid.NewGuid());
            gatesByInput = new List<int>[next.NetCount];
            for (var i = 0; i < gatesByInput.Length; i++) gatesByInput[i] = new List<int>();
            for (var i = 0; i < next.AndGates.Count; i++)
            {
                gatesByInput[next.AndGates[i].InputA].Add(i);
                if (next.AndGates[i].InputB != next.AndGates[i].InputA)
                    gatesByInput[next.AndGates[i].InputB].Add(i);
            }
            pending = new Queue<(WorkKind kind, int index)>();
            for (var i = 0; i < plan.Sources.Count; i++) pending.Enqueue((WorkKind.Source, i));
            for (var i = 0; i < plan.AndGates.Count; i++) pending.Enqueue((WorkKind.And, i));
            for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                pending.Enqueue((WorkKind.StorageOutput, i));
            AdvanceToSettled();
        }

        public ResolvedBit Net(int index) => nets[index].Resolution;
        public ConstantLogicSource Source(Guid objectId) => sources[objectId];
        public OneBitSrFlipFlop Storage(Guid objectId) => storage[objectId];

        public void SetSourceOn(Guid objectId, bool on)
        {
            Source(objectId).SetOn(on);
            EnqueueSource(objectId);
        }

        public void ConfigureSource(Guid objectId, LogicBit onValue, bool initialOn)
        {
            Source(objectId).Configure(onValue, initialOn);
            EnqueueSource(objectId);
        }

        public void ResetSources()
        {
            foreach (var source in sources.Values) source.Reset();
            for (var i = 0; i < plan.Sources.Count; i++) pending.Enqueue((WorkKind.Source, i));
        }

        public void ResetSimulation()
        {
            if (pending.Count != 0)
                throw new InvalidOperationException("Reset requires a settled boundary.");
            Publish(new GraphDrivenOneBitCircuit(plan, sources, storage, true));
        }

        // Caller constructs and validates the whole candidate before publishing it.
        // Source runtime states survive a structural graph refresh by object identity.
        public void ReplacePlan(OneBitCircuitPlan next)
        {
            if (pending.Count != 0)
                throw new InvalidOperationException("Graph replacement requires a settled boundary.");
            Publish(new GraphDrivenOneBitCircuit(next, sources, storage, false));
        }

        private void Publish(GraphDrivenOneBitCircuit candidate)
        {
            plan = candidate.plan;
            sources = candidate.sources;
            storage = candidate.storage;
            nets = candidate.nets;
            gatesByInput = candidate.gatesByInput;
            pending = candidate.pending;
            initializing = candidate.initializing;
        }

        public void AdvanceToSettled(int maximumDeltaPasses = 1024)
        {
            if (maximumDeltaPasses < 1) throw new ArgumentOutOfRangeException(nameof(maximumDeltaPasses));
            for (var pass = 0; pass < maximumDeltaPasses; pass++)
            {
                long evaluations = 0;
                var limit = (long)maximumDeltaPasses *
                    (plan.Sources.Count + plan.AndGates.Count + plan.SrFlipFlops.Count +
                     plan.NetCount + 1);
                while (pending.Count > 0)
                {
                    if (++evaluations > limit)
                        throw new InvalidOperationException("Combinational graph did not settle.");
                    var work = pending.Dequeue();
                    switch (work.kind)
                    {
                        case WorkKind.Source:
                            var source = plan.Sources[work.index];
                            Drive(source.OutputNet, source.ObjectId,
                                sources[source.ObjectId].Drive);
                            break;
                        case WorkKind.And:
                            var gate = plan.AndGates[work.index];
                            Drive(gate.OutputY, gate.ObjectId,
                                OneBitLogic.And(nets[gate.InputA].Resolution.Value,
                                    nets[gate.InputB].Resolution.Value));
                            break;
                        case WorkKind.StorageOutput:
                            DriveStorageOutput(work.index);
                            break;
                    }
                }
                if (initializing)
                {
                    for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                    {
                        var binding = plan.SrFlipFlops[i];
                        storage[binding.ObjectId].InitializeClockBaseline(
                            nets[binding.Clock].Resolution.Value);
                    }
                    initializing = false;
                    return;
                }
                var clockValues = new LogicBit[plan.SrFlipFlops.Count];
                var nextQ = new LogicBit[plan.SrFlipFlops.Count];
                var changed = false;
                for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                {
                    var binding = plan.SrFlipFlops[i];
                    clockValues[i] = nets[binding.Clock].Resolution.Value;
                    var flipFlop = storage[binding.ObjectId];
                    flipFlop.PreviewClock(clockValues[i],
                        nets[binding.S].Resolution.Value,
                        nets[binding.R].Resolution.Value, out nextQ[i]);
                    if (nextQ[i] != flipFlop.Q) changed = true;
                }
                // Every preview reads the same pre-update net and storage state.
                for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                    storage[plan.SrFlipFlops[i].ObjectId].CommitClock(clockValues[i], nextQ[i]);
                if (!changed) return;
                for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                    DriveStorageOutput(i);
            }
            throw new InvalidOperationException("Feedback exceeded the delta-pass limit.");
        }

        private void EnqueueSource(Guid objectId)
        {
            for (var i = 0; i < plan.Sources.Count; i++)
                if (plan.Sources[i].ObjectId == objectId)
                { pending.Enqueue((WorkKind.Source, i)); return; }
            throw new KeyNotFoundException("Source is not in the execution plan.");
        }

        private void Drive(int netIndex, Guid driverId, LogicBit value)
        {
            var net = nets[netIndex];
            var before = net.Resolution;
            net.SetDriver(driverId, value);
            if (before.Value == net.Resolution.Value && before.Cause == net.Resolution.Cause) return;
            foreach (var gateIndex in gatesByInput[netIndex])
                pending.Enqueue((WorkKind.And, gateIndex));
        }

        private void DriveStorageOutput(int index)
        {
            var binding = plan.SrFlipFlops[index];
            var flipFlop = storage[binding.ObjectId];
            Drive(binding.Q, binding.QDriverId, flipFlop.Q);
            Drive(binding.QBar, binding.QBarDriverId, flipFlop.QBar);
        }
    }
}
