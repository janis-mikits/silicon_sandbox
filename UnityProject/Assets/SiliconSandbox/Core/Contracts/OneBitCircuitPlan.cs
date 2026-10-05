using System;
using System.Collections.Generic;

namespace SiliconSandbox.Contracts
{
    // A renderer-free projection of an authored graph. Net indexes are transient;
    // authored object and pin identities remain in the design, not this execution cache.
    public readonly struct SourceBinding
    {
        public RuntimeObjectKey RuntimeKey { get; }
        public Guid ObjectId => RuntimeKey.LocalObjectId;
        public int OutputNet { get; }
        public LogicBit OnValue { get; }
        public bool InitialOn { get; }
        public SourceBinding(Guid objectId, int outputNet,
            LogicBit onValue = LogicBit.One, bool initialOn = false)
            : this(RuntimeObjectKey.World(objectId), outputNet, onValue, initialOn) { }
        public SourceBinding(RuntimeObjectKey runtimeKey, int outputNet,
            LogicBit onValue = LogicBit.One, bool initialOn = false)
        {
            RuntimeKey = runtimeKey; OutputNet = outputNet;
            OnValue = onValue; InitialOn = initialOn;
        }
    }

    public readonly struct AndBinding
    {
        public RuntimeObjectKey RuntimeKey { get; }
        public Guid ObjectId => RuntimeKey.LocalObjectId;
        public int InputA { get; }
        public int InputB { get; }
        public int OutputY { get; }
        public AndBinding(Guid objectId, int inputA, int inputB, int outputY)
            : this(RuntimeObjectKey.World(objectId), inputA, inputB, outputY) { }
        public AndBinding(RuntimeObjectKey runtimeKey, int inputA, int inputB, int outputY)
        { RuntimeKey = runtimeKey; InputA = inputA; InputB = inputB; OutputY = outputY; }
    }

    public readonly struct SrBinding
    {
        public RuntimeObjectKey RuntimeKey { get; }
        public Guid ObjectId => RuntimeKey.LocalObjectId;
        public int S { get; }
        public int R { get; }
        public int Clock { get; }
        public int Q { get; }
        public int QBar { get; }
        public Guid QDriverId { get; }
        public Guid QBarDriverId { get; }
        public LogicBit? InitialQ { get; }

        public SrBinding(Guid objectId, int s, int r, int clock, int q, int qBar,
            Guid qDriverId, Guid qBarDriverId, LogicBit? initialQ = null)
            : this(RuntimeObjectKey.World(objectId), s, r, clock, q, qBar,
                qDriverId, qBarDriverId, initialQ) { }

        public SrBinding(RuntimeObjectKey runtimeKey, int s, int r, int clock,
            int q, int qBar, Guid qDriverId, Guid qBarDriverId,
            LogicBit? initialQ = null)
        {
            RuntimeKey = runtimeKey;
            S = s; R = r; Clock = clock; Q = q; QBar = qBar;
            QDriverId = qDriverId; QBarDriverId = qBarDriverId;
            InitialQ = initialQ;
        }
    }

    public readonly struct WorldClockBinding
    {
        public Guid ConnectorId { get; }
        public Guid AnchorNodeId { get; }
        public int OutputNet { get; }

        public WorldClockBinding(Guid connectorId, Guid anchorNodeId, int outputNet)
        { ConnectorId = connectorId; AnchorNodeId = anchorNodeId; OutputNet = outputNet; }
    }

    public readonly struct MissingModuleOutputBinding
    {
        public Guid InstanceObjectId { get; }
        public Guid PortId { get; }
        public int OutputNet { get; }

        public MissingModuleOutputBinding(Guid instanceObjectId, Guid portId,
            int outputNet)
        {
            InstanceObjectId = instanceObjectId;
            PortId = portId;
            OutputNet = outputNet;
        }
    }

    public sealed class OneBitCircuitPlan
    {
        public int NetCount { get; }
        public IReadOnlyList<SourceBinding> Sources { get; }
        public IReadOnlyList<AndBinding> AndGates { get; }
        public IReadOnlyList<SrBinding> SrFlipFlops { get; }
        public WorldClockBinding? WorldClock { get; }
        public IReadOnlyList<MissingModuleOutputBinding> MissingModuleOutputs { get; }

        public OneBitCircuitPlan(int netCount, IEnumerable<SourceBinding> sources,
            IEnumerable<AndBinding> andGates, IEnumerable<SrBinding> srFlipFlops = null,
            WorldClockBinding? worldClock = null,
            IEnumerable<MissingModuleOutputBinding> missingModuleOutputs = null)
        {
            if (netCount < 0 || sources == null || andGates == null)
                throw new ArgumentException("Invalid circuit plan.");
            var sourceList = new List<SourceBinding>(sources);
            var gateList = new List<AndBinding>(andGates);
            var srList = srFlipFlops == null ? new List<SrBinding>() :
                new List<SrBinding>(srFlipFlops);
            var missingList = missingModuleOutputs == null
                ? new List<MissingModuleOutputBinding>()
                : new List<MissingModuleOutputBinding>(missingModuleOutputs);
            var ids = new HashSet<RuntimeObjectKey>();
            var outputDriverIds = new HashSet<RuntimeDriverKey>();
            foreach (var source in sourceList)
            {
                if (source.ObjectId == Guid.Empty || !ids.Add(source.RuntimeKey) ||
                    source.OnValue > LogicBit.Z)
                    throw new ArgumentException("Duplicate or empty component identity.");
                CheckNet(source.OutputNet);
                outputDriverIds.Add(new RuntimeDriverKey(source.RuntimeKey, Guid.Empty));
            }
            foreach (var gate in gateList)
            {
                if (gate.ObjectId == Guid.Empty || !ids.Add(gate.RuntimeKey))
                    throw new ArgumentException("Duplicate or empty component identity.");
                CheckNet(gate.InputA);
                CheckNet(gate.InputB);
                CheckNet(gate.OutputY);
                outputDriverIds.Add(new RuntimeDriverKey(gate.RuntimeKey, Guid.Empty));
            }
            foreach (var storage in srList)
            {
                if (storage.ObjectId == Guid.Empty || !ids.Add(storage.RuntimeKey) ||
                    storage.QDriverId == Guid.Empty || storage.QBarDriverId == Guid.Empty ||
                    !outputDriverIds.Add(new RuntimeDriverKey(storage.RuntimeKey,
                        storage.QDriverId)) ||
                    !outputDriverIds.Add(new RuntimeDriverKey(storage.RuntimeKey,
                        storage.QBarDriverId)) ||
                    storage.InitialQ.HasValue && storage.InitialQ.Value > LogicBit.Z)
                    throw new ArgumentException("Invalid SR identity or initialization.");
                CheckNet(storage.S); CheckNet(storage.R); CheckNet(storage.Clock);
                CheckNet(storage.Q); CheckNet(storage.QBar);
            }
            if (worldClock.HasValue)
            {
                var clock = worldClock.Value;
                if (clock.ConnectorId == Guid.Empty || clock.AnchorNodeId == Guid.Empty ||
                    !outputDriverIds.Add(new RuntimeDriverKey(
                        RuntimeObjectKey.World(clock.ConnectorId), Guid.Empty)))
                    throw new ArgumentException("Invalid world-clock driver identity.");
                CheckNet(clock.OutputNet);
            }
            foreach (var missing in missingList)
            {
                if (missing.InstanceObjectId == Guid.Empty ||
                    missing.PortId == Guid.Empty ||
                    !outputDriverIds.Add(new RuntimeDriverKey(
                        RuntimeObjectKey.World(missing.InstanceObjectId),
                        missing.PortId)))
                    throw new ArgumentException("Invalid missing-module output identity.");
                CheckNet(missing.OutputNet);
            }
            NetCount = netCount;
            Sources = sourceList.AsReadOnly();
            AndGates = gateList.AsReadOnly();
            SrFlipFlops = srList.AsReadOnly();
            WorldClock = worldClock;
            MissingModuleOutputs = missingList.AsReadOnly();

            void CheckNet(int index)
            {
                if (index < 0 || index >= netCount)
                    throw new ArgumentException("Component references a missing net.");
            }
        }
    }
}
