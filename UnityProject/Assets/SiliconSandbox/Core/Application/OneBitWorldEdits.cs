using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    // Build the entire candidate revision before returning it to the caller.
    // The caller owns safe-pause, runtime replacement, scene and undo publish.
    public static class OneBitWorldEdits
    {
        public static OneBitWorldDesign PlaceComponent(OneBitWorldDesign original,
            string typeId, GridCell anchorCell, GridOrientation orientation,
            LogicBit sourceOnValue = LogicBit.One, bool sourceInitialOn = false,
            LogicBit? srInitialQ = null, string tag = "", Func<Guid> allocateId = null)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (!original.Bounds.ContainsPlaceable(anchorCell))
                throw new ArgumentException("Placement leaves the configured world bounds.");
            foreach (var component in original.Components)
                if (component.AnchorCell.Equals(anchorCell))
                    throw new ArgumentException("The component cell is occupied.");
            foreach (var route in original.Topology.Connectors)
                foreach (var node in route.Nodes)
                    if (node.Cell.Equals(anchorCell))
                        throw new ArgumentException("The cell contains connector geometry.");

            var nextId = allocateId ?? Guid.NewGuid;
            var pinIds = new Dictionary<string, Guid>();
            foreach (var pin in BuiltInPinCatalog.Pins(typeId, 1))
                pinIds.Add(pin.Key, nextId());
            var placed = new PlacedOneBitComponent(nextId(), typeId, 1, anchorCell,
                orientation, pinIds, sourceOnValue, sourceInitialOn, srInitialQ, tag);
            var components = new List<PlacedOneBitComponent>(original.Components) { placed };
            var pins = new List<AuthoredPin>(original.Topology.Pins);
            pins.AddRange(placed.BuildPins());
            var topology = new OneBitAuthoredTopology(pins,
                original.Topology.Connectors, original.Topology.Joins);
            var candidate = new OneBitWorldDesign(original.Bounds, components, topology);
            OneBitTopologyGraphBuilder.Build(candidate.Topology);
            return candidate;
        }

        public static OneBitWorldDesign PlaceConnector(OneBitWorldDesign original,
            ConnectorRoute connector, IEnumerable<ElectricalJoin> joins)
        {
            if (original == null || connector == null || joins == null)
                throw new ArgumentNullException();
            var routes = new List<ConnectorRoute>(original.Topology.Connectors) { connector };
            var allJoins = new List<ElectricalJoin>(original.Topology.Joins);
            allJoins.AddRange(joins);
            var topology = new OneBitAuthoredTopology(original.Topology.Pins,
                routes, allJoins);
            var candidate = new OneBitWorldDesign(original.Bounds,
                original.Components, topology);
            OneBitTopologyGraphBuilder.Build(candidate.Topology);
            return candidate;
        }

        public static OneBitWorldDesign ConfigureSource(OneBitWorldDesign original,
            Guid sourceId, LogicBit onValue, bool initialOn)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            var components = new List<PlacedOneBitComponent>(original.Components);
            var found = false;
            for (var i = 0; i < components.Count; i++)
            {
                var previous = components[i];
                if (previous.Id != sourceId) continue;
                if (previous.TypeId != BuiltInPinCatalog.Source)
                    throw new ArgumentException("Target is not a Constant Logic Source.");
                components[i] = new PlacedOneBitComponent(previous.Id,
                    previous.TypeId, previous.TypeVersion, previous.AnchorCell,
                    previous.Orientation, previous.PinIds, onValue, initialOn,
                    null, previous.Tag);
                found = true;
                break;
            }
            if (!found) throw new ArgumentException("Unknown source identity.");
            var candidate = new OneBitWorldDesign(original.Bounds, components,
                original.Topology);
            OneBitTopologyGraphBuilder.Build(candidate.Topology);
            return candidate;
        }
    }
}
