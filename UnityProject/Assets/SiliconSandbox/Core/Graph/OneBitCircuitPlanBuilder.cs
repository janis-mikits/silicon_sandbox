using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Graph
{
    public sealed class BuiltOneBitCircuitPlan
    {
        private readonly Dictionary<JoinMember, int> netIndexes;
        public OneBitTopologyGraph Graph { get; }
        public OneBitCircuitPlan Plan { get; }

        internal BuiltOneBitCircuitPlan(OneBitTopologyGraph graph, OneBitCircuitPlan plan,
            Dictionary<JoinMember, int> netIndexes)
        { Graph = graph; Plan = plan; this.netIndexes = netIndexes; }

        public int NetIndex(JoinMember member) => netIndexes[member];
    }

    // Converts exact authored pin/route references into transient execution indexes.
    public static class OneBitCircuitPlanBuilder
    {
        public static BuiltOneBitCircuitPlan Build(OneBitAuthoredTopology topology,
            IEnumerable<OneBitComponent> components)
        {
            if (topology == null || components == null) throw new ArgumentNullException();
            var graph = OneBitTopologyGraphBuilder.Build(topology);
            var indexes = new Dictionary<JoinMember, int>();
            for (var i = 0; i < graph.Nets.Count; i++)
                foreach (var member in graph.Nets[i].Members) indexes.Add(member, i);

            var authoredPins = new Dictionary<JoinMember, AuthoredPin>();
            foreach (var pin in topology.Pins)
                authoredPins.Add(JoinMember.ComponentPin(pin.ObjectId, pin.PinId), pin);
            var consumedPins = new HashSet<JoinMember>();
            var objectIds = new HashSet<Guid>();
            var sources = new List<SourceBinding>();
            var gates = new List<AndBinding>();
            var storage = new List<SrBinding>();
            foreach (var component in components)
            {
                if (component == null || !objectIds.Add(component.Id))
                    throw new ArgumentException("Null or duplicate component.");
                var geometry = BuiltInPinCatalog.Pins(component.TypeId, component.TypeVersion);
                if (component.PinIds.Count != geometry.Count)
                    throw new ArgumentException("Component pin count disagrees with its type version.");
                foreach (var pin in geometry)
                {
                    if (!component.PinIds.TryGetValue(pin.Key, out var pinId) || pinId == Guid.Empty)
                        throw new ArgumentException("Missing versioned component pin.");
                    var member = JoinMember.ComponentPin(component.Id, pinId);
                    if (!authoredPins.ContainsKey(member) || !consumedPins.Add(member))
                        throw new ArgumentException("Component pin identity is absent or repeated.");
                }
                if (component.TypeId == BuiltInPinCatalog.Source)
                    sources.Add(new SourceBinding(component.Id, Index("OUT")));
                else if (component.TypeId == BuiltInPinCatalog.And)
                    gates.Add(new AndBinding(component.Id, Index("A"), Index("B"), Index("Y")));
                else if (component.TypeId == BuiltInPinCatalog.SrFlipFlop)
                    storage.Add(new SrBinding(component.Id, Index("S"), Index("R"),
                        Index("CLK"), Index("Q"), Index("Q_bar"),
                        component.Pin("Q"), component.Pin("Q_bar"), component.InitialQ));
                else
                    throw new ArgumentException("This execution plan does not yet support this component type.");

                int Index(string key) => indexes[JoinMember.ComponentPin(component.Id, component.Pin(key))];
            }
            if (consumedPins.Count != authoredPins.Count)
                throw new ArgumentException("Topology contains an unowned component pin.");
            return new BuiltOneBitCircuitPlan(graph,
                new OneBitCircuitPlan(graph.Nets.Count, sources, gates, storage), indexes);
        }
    }
}
