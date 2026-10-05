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
                original.Topology.Connectors, original.Topology.Joins,
                original.Topology.ModulePorts);
            var candidate = new OneBitWorldDesign(original.Bounds, components,
                topology, original.Modules);
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
                routes, allJoins, original.Topology.ModulePorts);
            var candidate = new OneBitWorldDesign(original.Bounds,
                original.Components, topology, original.Modules);
            OneBitTopologyGraphBuilder.Build(candidate.Topology);
            return candidate;
        }

        public static OneBitWorldDesign PlaceModule(OneBitWorldDesign original,
            OneBitModuleVersion version, string instanceName,
            GridCell anchorCell, GridOrientation orientation,
            string tag = "")
        {
            if (original == null || version == null)
                throw new ArgumentNullException();
            var interfacePorts = new List<OneBitPortInterface>();
            foreach (var port in version.Ports)
                interfacePorts.Add(new OneBitPortInterface(port.Id, port.Name,
                    port.Direction, port.LocalCell, port.PointQ));
            var placed = new PlacedOneBitModuleInstance(Guid.NewGuid(),
                Guid.NewGuid(), version.FamilyId, version.VersionId,
                instanceName, anchorCell, orientation, version.SizeCells,
                interfacePorts, tag);
            var modules = new List<PlacedOneBitModuleInstance>(original.Modules)
            { placed };
            var ports = new List<AuthoredModulePortBit>(original.Topology.ModulePorts);
            ports.AddRange(placed.BuildPortBits());
            var topology = new OneBitAuthoredTopology(original.Topology.Pins,
                original.Topology.Connectors, original.Topology.Joins, ports);
            var candidate = new OneBitWorldDesign(original.Bounds,
                original.Components, topology, modules);
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
                original.Topology, original.Modules);
            OneBitTopologyGraphBuilder.Build(candidate.Topology);
            return candidate;
        }

        public static OneBitWorldDesign AttachWorldClockPin(
            OneBitWorldDesign original, Guid srObjectId)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            PlacedOneBitComponent sr = null;
            foreach (var component in original.Components)
                if (component.Id == srObjectId) { sr = component; break; }
            if (sr == null || sr.TypeId != BuiltInPinCatalog.SrFlipFlop)
                throw new ArgumentException("World-clock stub requires an SR CLK pin.");
            var clockPinRef = JoinMember.ComponentPin(sr.Id, sr.PinIds["CLK"]);
            AuthoredPin clockPin = null;
            foreach (var pin in original.Topology.Pins)
                if (pin.ObjectId == sr.Id && pin.PinId == sr.PinIds["CLK"])
                { clockPin = pin; break; }
            if (clockPin == null) throw new ArgumentException("SR CLK pin is missing.");
            return AttachWorldClockEndpoint(original, clockPinRef, clockPin);
        }

        public static OneBitWorldDesign AttachWorldClockPort(
            OneBitWorldDesign original, Guid moduleObjectId, Guid portId)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            PlacedOneBitModuleInstance module = null;
            foreach (var item in original.Modules)
                if (item.Id == moduleObjectId) { module = item; break; }
            if (module == null) throw new ArgumentException("Unknown module instance.");
            OneBitPortInterface clock = null;
            foreach (var item in module.InterfacePorts)
                if (item.Id == portId) { clock = item; break; }
            if (clock == null || clock.Name != "CLK" ||
                clock.Direction == OneBitPortDirection.Output)
                throw new ArgumentException("World clock requires an input CLK port.");
            var endpoint = JoinMember.ModulePortBit(module.Id, portId, 0);
            foreach (var port in original.Topology.ModulePorts)
                if (port.ObjectId == module.Id && port.PortId == portId)
                    return AttachWorldClockEndpoint(original, endpoint,
                        new AuthoredPin(port.ObjectId, port.PortId,
                            port.Cell, port.PointQ));
            throw new ArgumentException("CLK port geometry is missing.");
        }

        private static OneBitWorldDesign AttachWorldClockEndpoint(
            OneBitWorldDesign original, JoinMember endpoint, AuthoredPin clockPin)
        {
            foreach (var join in original.Topology.Joins)
                foreach (var member in join.Members)
                    if (member.Equals(endpoint))
                        throw new ArgumentException("The CLK port already has a connector.");

            var outside = AdjacentCell(clockPin);
            var useOutside = original.Bounds.ContainsPlaceable(outside);
            foreach (var component in original.Components)
                if (component.AnchorCell.Equals(outside)) useOutside = false;
            foreach (var module in original.Modules)
                foreach (var cell in module.OccupiedCells())
                    if (cell.Equals(outside)) useOutside = false;
            var stubCell = useOutside ? outside : clockPin.Cell;
            var stubPoint = useOutside
                ? OppositeFacePoint(clockPin.PointQ)
                : clockPin.PointQ;
            var channel = FreeChannel(original, stubCell);
            if (channel < 0 && useOutside)
            {
                stubCell = clockPin.Cell;
                stubPoint = clockPin.PointQ;
                channel = FreeChannel(original, stubCell);
            }
            if (channel < 0)
                throw new ArgumentException("No free channel for the world-clock stub.");

            var node = new RouteNode(Guid.NewGuid(), stubCell, channel, stubPoint);
            var route = new ConnectorRoute(Guid.NewGuid(), "netLink", 1,
                new[] { node }, Array.Empty<RouteSpan>(), "",
                null, "@world-clock", "world", "worldClock");
            var joinToPin = new ElectricalJoin(Guid.NewGuid(), new[]
            {
                endpoint, JoinMember.ConnectorNode(route.Id, node.Id)
            });
            return PlaceConnector(original, route, new[] { joinToPin });
        }

        private static int FreeChannel(OneBitWorldDesign design, GridCell cell)
        {
            for (var channel = 0; channel < 4; channel++)
            {
                var occupied = false;
                foreach (var route in design.Topology.Connectors)
                    foreach (var node in route.Nodes)
                        if (node.Cell.Equals(cell) && node.Channel == channel)
                            occupied = true;
                if (!occupied) return channel;
            }
            return -1;
        }

        private static GridCell AdjacentCell(AuthoredPin pin)
        {
            var cell = pin.Cell;
            var point = pin.PointQ;
            if (point.X == 0) return new GridCell(cell.X - 1, cell.Y, cell.Z);
            if (point.X == 4) return new GridCell(cell.X + 1, cell.Y, cell.Z);
            if (point.Y == 0) return new GridCell(cell.X, cell.Y - 1, cell.Z);
            if (point.Y == 4) return new GridCell(cell.X, cell.Y + 1, cell.Z);
            if (point.Z == 0) return new GridCell(cell.X, cell.Y, cell.Z - 1);
            if (point.Z == 4) return new GridCell(cell.X, cell.Y, cell.Z + 1);
            throw new ArgumentException("CLK pin is not on a cell face.");
        }

        private static QuarterPoint OppositeFacePoint(QuarterPoint point)
        {
            if (point.X == 0) return new QuarterPoint(4, point.Y, point.Z);
            if (point.X == 4) return new QuarterPoint(0, point.Y, point.Z);
            if (point.Y == 0) return new QuarterPoint(point.X, 4, point.Z);
            if (point.Y == 4) return new QuarterPoint(point.X, 0, point.Z);
            if (point.Z == 0) return new QuarterPoint(point.X, point.Y, 4);
            if (point.Z == 4) return new QuarterPoint(point.X, point.Y, 0);
            throw new ArgumentException("CLK pin is not on a cell face.");
        }
    }
}
