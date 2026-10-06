using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    public sealed class OneBitPortChoice
    {
        public string Name { get; }
        public OneBitPortDirection Direction { get; }
        public GridCell LocalCell { get; }
        public QuarterPoint PointQ { get; }
        public JoinMember BitZeroTarget { get; }

        public OneBitPortChoice(string name, OneBitPortDirection direction,
            GridCell localCell, QuarterPoint pointQ, JoinMember bitZeroTarget)
        {
            Name = name;
            Direction = direction;
            LocalCell = localCell;
            PointQ = pointQ;
            BitZeroTarget = bitZeroTarget;
        }
    }

    public static class OneBitModuleVersionFactory
    {
        public static OneBitModuleVersion Create(OneBitModuleSnapshot snapshot,
            Guid familyId, string name, IEnumerable<OneBitPortChoice> choices)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return Create(snapshot, snapshot.SizeCells, familyId, name, choices);
        }

        public static OneBitModuleVersion Create(OneBitModuleSnapshot snapshot,
            GridCell exteriorSizeCells, Guid familyId, string name,
            IEnumerable<OneBitPortChoice> choices)
        {
            if (snapshot == null || choices == null)
                throw new ArgumentNullException();
            var graph = OneBitTopologyGraphBuilder.Build(snapshot.Topology);
            var ports = new List<OneBitModulePort>();
            foreach (var choice in choices)
            {
                if (choice == null) throw new ArgumentException("Null port choice.");
                // Every target must be an exact authored endpoint in this
                // snapshot. A transient simulator net number is never stored.
                try { graph.NetFor(choice.BitZeroTarget); }
                catch (KeyNotFoundException exception)
                {
                    throw new ArgumentException(
                        "Port target is absent from this fixed snapshot.", exception);
                }
                ports.Add(new OneBitModulePort(Guid.NewGuid(), choice.Name,
                    choice.Direction, choice.LocalCell, choice.PointQ,
                    choice.BitZeroTarget));
            }
            return new OneBitModuleVersion(familyId, Guid.NewGuid(), name,
                snapshot.SizeCells, exteriorSizeCells, snapshot.Components,
                snapshot.Topology, ports);
        }
    }
}
