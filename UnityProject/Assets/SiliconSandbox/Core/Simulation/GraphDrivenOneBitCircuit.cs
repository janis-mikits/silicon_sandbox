using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    // Event-driven combinational subset. A world-time scheduler will call this at
    // safe slot boundaries; rendering and Unity frame rate never drive propagation.
    public sealed class GraphDrivenOneBitCircuit
    {
        private OneBitCircuitPlan plan;
        private OneBitNet[] nets;
        private Dictionary<Guid, ConstantLogicSource> sources;
        private List<int>[] gatesByInput;
        private Queue<(bool source, int index)> pending;

        public GraphDrivenOneBitCircuit(OneBitCircuitPlan initialPlan)
            : this(initialPlan, null) { }

        private GraphDrivenOneBitCircuit(OneBitCircuitPlan next,
            Dictionary<Guid, ConstantLogicSource> oldSources)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            plan = next;
            sources = new Dictionary<Guid, ConstantLogicSource>();
            foreach (var binding in next.Sources)
                sources.Add(binding.ObjectId, oldSources != null &&
                    oldSources.TryGetValue(binding.ObjectId, out var existing)
                    ? existing : new ConstantLogicSource());
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
            pending = new Queue<(bool source, int index)>();
            for (var i = 0; i < plan.Sources.Count; i++) pending.Enqueue((true, i));
            for (var i = 0; i < plan.AndGates.Count; i++) pending.Enqueue((false, i));
            AdvanceToSettled();
        }

        public ResolvedBit Net(int index) => nets[index].Resolution;
        public ConstantLogicSource Source(Guid objectId) => sources[objectId];

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
            for (var i = 0; i < plan.Sources.Count; i++) pending.Enqueue((true, i));
        }

        // Caller constructs and validates the whole candidate before publishing it.
        // Source runtime states survive a structural graph refresh by object identity.
        public void ReplacePlan(OneBitCircuitPlan next)
        {
            if (pending.Count != 0)
                throw new InvalidOperationException("Graph replacement requires a settled boundary.");
            var candidate = new GraphDrivenOneBitCircuit(next, sources);
            plan = candidate.plan;
            sources = candidate.sources;
            nets = candidate.nets;
            gatesByInput = candidate.gatesByInput;
            pending = candidate.pending;
        }

        public void AdvanceToSettled(int maximumDeltaPasses = 1024)
        {
            if (maximumDeltaPasses < 1) throw new ArgumentOutOfRangeException(nameof(maximumDeltaPasses));
            long evaluations = 0;
            var limit = (long)maximumDeltaPasses *
                (plan.Sources.Count + plan.AndGates.Count + plan.NetCount);
            while (pending.Count > 0)
            {
                if (++evaluations > limit)
                    throw new InvalidOperationException("Combinational graph did not settle.");
                var work = pending.Dequeue();
                if (work.source)
                {
                    var source = plan.Sources[work.index];
                    Drive(source.OutputNet, source.ObjectId, sources[source.ObjectId].Drive);
                }
                else
                {
                    var gate = plan.AndGates[work.index];
                    Drive(gate.OutputY, gate.ObjectId,
                        OneBitLogic.And(nets[gate.InputA].Resolution.Value,
                            nets[gate.InputB].Resolution.Value));
                }
            }
        }

        private void EnqueueSource(Guid objectId)
        {
            for (var i = 0; i < plan.Sources.Count; i++)
                if (plan.Sources[i].ObjectId == objectId)
                { pending.Enqueue((true, i)); return; }
            throw new KeyNotFoundException("Source is not in the execution plan.");
        }

        private void Drive(int netIndex, Guid driverId, LogicBit value)
        {
            var net = nets[netIndex];
            var before = net.Resolution;
            net.SetDriver(driverId, value);
            if (before.Value == net.Resolution.Value && before.Cause == net.Resolution.Cause) return;
            foreach (var gateIndex in gatesByInput[netIndex]) pending.Enqueue((false, gateIndex));
        }
    }
}
