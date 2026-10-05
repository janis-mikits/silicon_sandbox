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
    }
}
