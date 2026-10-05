using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    // Event-driven one-bit subset. A world-time scheduler calls this at safe
    // slots; rendering and Unity frame rate never drive electrical propagation.
    public sealed class GraphDrivenOneBitCircuit
    {
        private enum WorkKind { Source, And, StorageOutput, WorldClock }

        private OneBitCircuitPlan plan;
        private OneBitNet[] nets;
        private Dictionary<RuntimeObjectKey, ConstantLogicSource> sources;
        private Dictionary<RuntimeObjectKey, OneBitSrFlipFlop> storage;
        private List<int>[] gatesByInput;
        private Queue<(WorkKind kind, int index)> pending;
        private bool initializing;
        private HashSet<RuntimeObjectKey> newStorageIds;
        private LogicBit worldClockLevel;

        public GraphDrivenOneBitCircuit(OneBitCircuitPlan initialPlan)
            : this(initialPlan, null, null, LogicBit.Zero, false) { }

        private GraphDrivenOneBitCircuit(OneBitCircuitPlan next,
            Dictionary<RuntimeObjectKey, ConstantLogicSource> oldSources,
            Dictionary<RuntimeObjectKey, OneBitSrFlipFlop> oldStorage,
            LogicBit previousWorldClockLevel, bool reset,
            bool refreshAuthoredSourceConfiguration = false)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            plan = next;
            initializing = oldStorage == null || reset;
            worldClockLevel = reset ? LogicBit.Zero : previousWorldClockLevel;
            sources = new Dictionary<RuntimeObjectKey, ConstantLogicSource>();
            foreach (var binding in next.Sources)
            {
                ConstantLogicSource source;
                if (oldSources != null &&
                    oldSources.TryGetValue(binding.RuntimeKey, out var existing))
                {
                    if (reset || refreshAuthoredSourceConfiguration)
                    {
                        source = new ConstantLogicSource(binding.OnValue, binding.InitialOn);
                        if (!reset) source.SetOn(existing.IsOn);
                    }
                    else source = existing;
                }
                else
                    source = new ConstantLogicSource(binding.OnValue, binding.InitialOn);
                sources.Add(binding.RuntimeKey, source);
            }
            storage = new Dictionary<RuntimeObjectKey, OneBitSrFlipFlop>();
            newStorageIds = new HashSet<RuntimeObjectKey>();
            foreach (var binding in next.SrFlipFlops)
            {
                if (oldStorage == null || reset || !oldStorage.ContainsKey(binding.RuntimeKey))
                    newStorageIds.Add(binding.RuntimeKey);
                storage.Add(binding.RuntimeKey, oldStorage != null &&
                    oldStorage.TryGetValue(binding.RuntimeKey, out var prior)
                    ? reset ? new OneBitSrFlipFlop(binding.InitialQ) :
                        prior.CopyWithInitialQ(binding.InitialQ)
                    : new OneBitSrFlipFlop(binding.InitialQ));
            }
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
            if (plan.WorldClock.HasValue) pending.Enqueue((WorkKind.WorldClock, 0));
            AdvanceToSettled();
        }

        public ResolvedBit Net(int index) => nets[index].Resolution;
        public ConstantLogicSource Source(Guid objectId) =>
            Source(RuntimeObjectKey.World(objectId));
        public ConstantLogicSource Source(RuntimeObjectKey key) => sources[key];
        public OneBitSrFlipFlop Storage(Guid objectId) =>
            Storage(RuntimeObjectKey.World(objectId));
        public OneBitSrFlipFlop Storage(RuntimeObjectKey key) => storage[key];
        public LogicBit WorldClockLevel => worldClockLevel;

        public void SetWorldClockLevel(LogicBit level)
        {
            if (level != LogicBit.Zero && level != LogicBit.One)
                throw new ArgumentOutOfRangeException(nameof(level));
            worldClockLevel = level;
            if (plan.WorldClock.HasValue) pending.Enqueue((WorkKind.WorldClock, 0));
        }

        public void SetSourceOn(Guid objectId, bool on)
            => SetSourceOn(RuntimeObjectKey.World(objectId), on);

        public void SetSourceOn(RuntimeObjectKey key, bool on)
        {
            Source(key).SetOn(on);
            EnqueueSource(key);
        }

        public void ConfigureSource(Guid objectId, LogicBit onValue, bool initialOn)
            => ConfigureSource(RuntimeObjectKey.World(objectId), onValue, initialOn);

        public void ConfigureSource(RuntimeObjectKey key, LogicBit onValue, bool initialOn)
        {
            Source(key).Configure(onValue, initialOn);
            EnqueueSource(key);
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
            Publish(new GraphDrivenOneBitCircuit(plan, sources, storage,
                worldClockLevel, true));
        }

        // Caller constructs and validates the whole candidate before publishing it.
        // Source runtime states survive a structural graph refresh by object identity.
        public void ReplacePlan(OneBitCircuitPlan next)
        {
            if (pending.Count != 0)
                throw new InvalidOperationException("Graph replacement requires a settled boundary.");
            Publish(new GraphDrivenOneBitCircuit(next, sources, storage,
                worldClockLevel, false));
        }

        // Rebuild a fully settled candidate with edited authored source settings
        // while retaining each source's live On/Off choice and unrelated storage.
        public void ReplacePlanWithAuthoredSourceConfiguration(OneBitCircuitPlan next)
        {
            if (pending.Count != 0)
                throw new InvalidOperationException("Graph replacement requires a settled boundary.");
            Publish(new GraphDrivenOneBitCircuit(next, sources, storage,
                worldClockLevel, false, true));
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
            newStorageIds = candidate.newStorageIds;
            worldClockLevel = candidate.worldClockLevel;
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
                            Drive(source.OutputNet,
                                new RuntimeDriverKey(source.RuntimeKey, Guid.Empty),
                                sources[source.RuntimeKey].Drive);
                            break;
                        case WorkKind.And:
                            var gate = plan.AndGates[work.index];
                            Drive(gate.OutputY,
                                new RuntimeDriverKey(gate.RuntimeKey, Guid.Empty),
                                OneBitLogic.And(nets[gate.InputA].Resolution.Value,
                                    nets[gate.InputB].Resolution.Value));
                            break;
                        case WorkKind.StorageOutput:
                            DriveStorageOutput(work.index);
                            break;
                        case WorkKind.WorldClock:
                            var clock = plan.WorldClock.Value;
                            Drive(clock.OutputNet, new RuntimeDriverKey(
                                RuntimeObjectKey.World(clock.ConnectorId), Guid.Empty),
                                worldClockLevel);
                            break;
                    }
                }
                if (initializing)
                {
                    for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                    {
                        var binding = plan.SrFlipFlops[i];
                        storage[binding.RuntimeKey].InitializeClockBaseline(
                            nets[binding.Clock].Resolution.Value);
                    }
                    initializing = false;
                    newStorageIds.Clear();
                    return;
                }
                if (newStorageIds.Count > 0)
                {
                    foreach (var binding in plan.SrFlipFlops)
                        if (newStorageIds.Contains(binding.RuntimeKey))
                            storage[binding.RuntimeKey].InitializeClockBaseline(
                                nets[binding.Clock].Resolution.Value);
                    newStorageIds.Clear();
                }
                var clockValues = new LogicBit[plan.SrFlipFlops.Count];
                var nextQ = new LogicBit[plan.SrFlipFlops.Count];
                var changed = false;
                for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                {
                    var binding = plan.SrFlipFlops[i];
                    clockValues[i] = nets[binding.Clock].Resolution.Value;
                    var flipFlop = storage[binding.RuntimeKey];
                    flipFlop.PreviewClock(clockValues[i],
                        nets[binding.S].Resolution.Value,
                        nets[binding.R].Resolution.Value, out nextQ[i]);
                    if (nextQ[i] != flipFlop.Q) changed = true;
                }
                // Every preview reads the same pre-update net and storage state.
                for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                    storage[plan.SrFlipFlops[i].RuntimeKey].CommitClock(clockValues[i], nextQ[i]);
                if (!changed) return;
                for (var i = 0; i < plan.SrFlipFlops.Count; i++)
                    DriveStorageOutput(i);
            }
            throw new InvalidOperationException("Feedback exceeded the delta-pass limit.");
        }

        private void EnqueueSource(RuntimeObjectKey key)
        {
            for (var i = 0; i < plan.Sources.Count; i++)
                if (plan.Sources[i].RuntimeKey.Equals(key))
                { pending.Enqueue((WorkKind.Source, i)); return; }
            throw new KeyNotFoundException("Source is not in the execution plan.");
        }

        private void Drive(int netIndex, RuntimeDriverKey driverId, LogicBit value)
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
            var flipFlop = storage[binding.RuntimeKey];
            Drive(binding.Q, new RuntimeDriverKey(binding.RuntimeKey,
                binding.QDriverId), flipFlop.Q);
            Drive(binding.QBar, new RuntimeDriverKey(binding.RuntimeKey,
                binding.QBarDriverId), flipFlop.QBar);
        }
    }
}
