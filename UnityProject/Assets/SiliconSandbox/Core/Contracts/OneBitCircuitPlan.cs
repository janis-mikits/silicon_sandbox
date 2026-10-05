using System;
using System.Collections.Generic;

namespace SiliconSandbox.Contracts
{
    // A renderer-free projection of an authored graph. Net indexes are transient;
    // authored object and pin identities remain in the design, not this execution cache.
    public readonly struct SourceBinding
    {
        public Guid ObjectId { get; }
        public int OutputNet { get; }
        public SourceBinding(Guid objectId, int outputNet)
        { ObjectId = objectId; OutputNet = outputNet; }
    }

    public readonly struct AndBinding
    {
        public Guid ObjectId { get; }
        public int InputA { get; }
        public int InputB { get; }
        public int OutputY { get; }
        public AndBinding(Guid objectId, int inputA, int inputB, int outputY)
        { ObjectId = objectId; InputA = inputA; InputB = inputB; OutputY = outputY; }
    }

    public readonly struct SrBinding
    {
        public Guid ObjectId { get; }
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
        {
            ObjectId = objectId;
            S = s; R = r; Clock = clock; Q = q; QBar = qBar;
            QDriverId = qDriverId; QBarDriverId = qBarDriverId;
            InitialQ = initialQ;
        }
    }

    public sealed class OneBitCircuitPlan
    {
        public int NetCount { get; }
        public IReadOnlyList<SourceBinding> Sources { get; }
        public IReadOnlyList<AndBinding> AndGates { get; }
        public IReadOnlyList<SrBinding> SrFlipFlops { get; }

        public OneBitCircuitPlan(int netCount, IEnumerable<SourceBinding> sources,
            IEnumerable<AndBinding> andGates, IEnumerable<SrBinding> srFlipFlops = null)
        {
            if (netCount < 0 || sources == null || andGates == null)
                throw new ArgumentException("Invalid circuit plan.");
            var sourceList = new List<SourceBinding>(sources);
            var gateList = new List<AndBinding>(andGates);
            var srList = srFlipFlops == null ? new List<SrBinding>() :
                new List<SrBinding>(srFlipFlops);
            var ids = new HashSet<Guid>();
            var outputDriverIds = new HashSet<Guid>();
            foreach (var source in sourceList)
            {
                if (source.ObjectId == Guid.Empty || !ids.Add(source.ObjectId))
                    throw new ArgumentException("Duplicate or empty component identity.");
                CheckNet(source.OutputNet);
                outputDriverIds.Add(source.ObjectId);
            }
            foreach (var gate in gateList)
            {
                if (gate.ObjectId == Guid.Empty || !ids.Add(gate.ObjectId))
                    throw new ArgumentException("Duplicate or empty component identity.");
                CheckNet(gate.InputA);
                CheckNet(gate.InputB);
                CheckNet(gate.OutputY);
                outputDriverIds.Add(gate.ObjectId);
            }
            foreach (var storage in srList)
            {
                if (storage.ObjectId == Guid.Empty || !ids.Add(storage.ObjectId) ||
                    storage.QDriverId == Guid.Empty || storage.QBarDriverId == Guid.Empty ||
                    !outputDriverIds.Add(storage.QDriverId) ||
                    !outputDriverIds.Add(storage.QBarDriverId) ||
                    storage.InitialQ.HasValue && storage.InitialQ.Value > LogicBit.Z)
                    throw new ArgumentException("Invalid SR identity or initialization.");
                CheckNet(storage.S); CheckNet(storage.R); CheckNet(storage.Clock);
                CheckNet(storage.Q); CheckNet(storage.QBar);
            }
            NetCount = netCount;
            Sources = sourceList.AsReadOnly();
            AndGates = gateList.AsReadOnly();
            SrFlipFlops = srList.AsReadOnly();

            void CheckNet(int index)
            {
                if (index < 0 || index >= netCount)
                    throw new ArgumentException("Component references a missing net.");
            }
        }
    }
}
