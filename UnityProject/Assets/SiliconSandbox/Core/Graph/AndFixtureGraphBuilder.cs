using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Graph
{
    public static class AndFixtureGraphBuilder
    {
        public static AndCircuitWiring Build(AndFixtureDesign design)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (design.SourceA.TypeId != BuiltInPinCatalog.Source ||
                design.SourceB.TypeId != BuiltInPinCatalog.Source ||
                design.Gate.TypeId != BuiltInPinCatalog.And ||
                design.SourceA.Id == design.SourceB.Id || design.SourceA.Id == design.Gate.Id ||
                design.SourceB.Id == design.Gate.Id || design.Connectors.Count != 3)
                throw new ArgumentException("Invalid AND fixture components.");

            var usedConnectorIds = new HashSet<Guid>();
            var occupiedPins = new HashSet<FixturePinRef>();
            Guid aNet = Guid.Empty, bNet = Guid.Empty, yNet = Guid.Empty;
            foreach (var connector in design.Connectors)
            {
                if (!usedConnectorIds.Add(connector.Id)) throw new ArgumentException("Duplicate connector ID.");
                foreach (var pin in connector.AttachedPins)
                    if (!occupiedPins.Add(pin)) throw new ArgumentException("Pin has more than one attachment.");

                if (Matches(connector, design.SourceA.Pin("OUT"), design.Gate.Pin("A"))) aNet = connector.Id;
                else if (Matches(connector, design.SourceB.Pin("OUT"), design.Gate.Pin("B"))) bNet = connector.Id;
                else if (connector.AttachedPins.Count == 1 && connector.AttachedPins[0].Equals(design.Gate.Pin("Y")))
                    yNet = connector.Id;
                else throw new ArgumentException("Connector has an invalid explicit pin attachment.");
            }
            if (aNet == Guid.Empty || bNet == Guid.Empty || yNet == Guid.Empty)
                throw new ArgumentException("Incomplete AND fixture connectivity.");
            var topology = design.Topology;
            var graph = OneBitTopologyGraphBuilder.Build(topology);
            var a = graph.NetFor(Pin(design.SourceA.Pin("OUT")));
            var b = graph.NetFor(Pin(design.SourceB.Pin("OUT")));
            var y = graph.NetFor(Pin(design.Gate.Pin("Y")));
            if (!ReferenceEquals(a, graph.NetFor(Pin(design.Gate.Pin("A")))) ||
                !ReferenceEquals(b, graph.NetFor(Pin(design.Gate.Pin("B")))) ||
                ReferenceEquals(a, b) || ReferenceEquals(a, y) || ReferenceEquals(b, y))
                throw new ArgumentException("Authored AND topology does not make three expected nets.");
            return new AndCircuitWiring(design.SourceA.Id, design.SourceB.Id, design.Gate.Id, aNet, bNet, yNet);
        }

        private static JoinMember Pin(FixturePinRef pin) => JoinMember.ComponentPin(pin.ObjectId, pin.PinId);

        private static bool Matches(FixtureConnector connector, FixturePinRef first, FixturePinRef second)
        {
            return connector.AttachedPins.Count == 2 &&
                ((connector.AttachedPins[0].Equals(first) && connector.AttachedPins[1].Equals(second)) ||
                 (connector.AttachedPins[0].Equals(second) && connector.AttachedPins[1].Equals(first)));
        }
    }
}
