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

    public sealed class OneBitCircuitPlan
    {
        public int NetCount { get; }
        public IReadOnlyList<SourceBinding> Sources { get; }
        public IReadOnlyList<AndBinding> AndGates { get; }

        public OneBitCircuitPlan(int netCount, IEnumerable<SourceBinding> sources,
            IEnumerable<AndBinding> andGates)
        {
            if (netCount < 0 || sources == null || andGates == null)
                throw new ArgumentException("Invalid circuit plan.");
            var sourceList = new List<SourceBinding>(sources);
            var gateList = new List<AndBinding>(andGates);
            var ids = new HashSet<Guid>();
            foreach (var source in sourceList)
            {
                if (source.ObjectId == Guid.Empty || !ids.Add(source.ObjectId))
                    throw new ArgumentException("Duplicate or empty component identity.");
                CheckNet(source.OutputNet);
            }
            foreach (var gate in gateList)
            {
                if (gate.ObjectId == Guid.Empty || !ids.Add(gate.ObjectId))
                    throw new ArgumentException("Duplicate or empty component identity.");
                CheckNet(gate.InputA);
                CheckNet(gate.InputB);
                CheckNet(gate.OutputY);
            }
            NetCount = netCount;
            Sources = sourceList.AsReadOnly();
            AndGates = gateList.AsReadOnly();

            void CheckNet(int index)
            {
                if (index < 0 || index >= netCount)
                    throw new ArgumentException("Component references a missing net.");
            }
        }
    }
}
