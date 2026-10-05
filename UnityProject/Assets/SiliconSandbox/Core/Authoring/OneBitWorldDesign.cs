using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Authoring
{
    public readonly struct WorldBounds
    {
        public int WidthCells { get; }
        public int LengthCells { get; }
        public int HeightCells { get; }
        public bool IsValid => WidthCells >= 1 && LengthCells >= 1 && HeightCells >= 2;

        public WorldBounds(int widthCells, int lengthCells, int heightCells)
        {
            if (widthCells < 1 || lengthCells < 1 || heightCells < 2)
                throw new ArgumentOutOfRangeException("World dimensions must contain a floor and placeable space.");
            WidthCells = widthCells;
            LengthCells = lengthCells;
            HeightCells = heightCells;
        }

        public bool ContainsPlaceable(GridCell cell) => cell.X >= 0 &&
            cell.X < WidthCells && cell.Y >= 1 && cell.Y < HeightCells &&
            cell.Z >= 0 && cell.Z < LengthCells;
    }

    public sealed class PlacedOneBitComponent
    {
        public Guid Id { get; }
        public string TypeId { get; }
        public int TypeVersion { get; }
        public GridCell AnchorCell { get; }
        public GridOrientation Orientation { get; }
        public IReadOnlyDictionary<string, Guid> PinIds { get; }
        public LogicBit SourceOnValue { get; }
        public bool SourceInitialOn { get; }
        public LogicBit? SrInitialQ { get; }
        public string Tag { get; }

        public PlacedOneBitComponent(Guid id, string typeId, int typeVersion,
            GridCell anchorCell, GridOrientation orientation,
            IReadOnlyDictionary<string, Guid> pinIds,
            LogicBit sourceOnValue = LogicBit.One, bool sourceInitialOn = false,
            LogicBit? srInitialQ = null, string tag = "")
        {
            if (id == Guid.Empty || typeId == null || pinIds == null || tag == null ||
                !orientation.IsValid ||
                sourceOnValue > LogicBit.Z)
                throw new ArgumentException("Invalid placed component fields.");
            var geometry = BuiltInPinCatalog.Pins(typeId, typeVersion);
            if (pinIds.Count != geometry.Count)
                throw new ArgumentException("Saved pin count disagrees with type version.");
            var copied = new Dictionary<string, Guid>();
            var uniquePinIds = new HashSet<Guid>();
            foreach (var pin in geometry)
            {
                if (!pinIds.TryGetValue(pin.Key, out var pinId) || pinId == Guid.Empty ||
                    !uniquePinIds.Add(pinId))
                    throw new ArgumentException("Missing or duplicate versioned pin identity.");
                copied.Add(pin.Key, pinId);
            }
            if (typeId != BuiltInPinCatalog.Source &&
                (sourceOnValue != LogicBit.One || sourceInitialOn))
                throw new ArgumentException("Only a source has source configuration.");
            if (typeId != BuiltInPinCatalog.SrFlipFlop && srInitialQ.HasValue)
                throw new ArgumentException("Only SR storage has initial Q.");
            if (srInitialQ.HasValue && srInitialQ.Value > LogicBit.Z)
                throw new ArgumentException("Invalid initial Q.");

            Id = id;
            TypeId = typeId;
            TypeVersion = typeVersion;
            AnchorCell = anchorCell;
            Orientation = orientation;
            PinIds = new System.Collections.ObjectModel.ReadOnlyDictionary<string, Guid>(copied);
            SourceOnValue = sourceOnValue;
            SourceInitialOn = sourceInitialOn;
            SrInitialQ = srInitialQ;
            Tag = tag;
        }

        public IReadOnlyList<AuthoredPin> BuildPins()
        {
            var result = new List<AuthoredPin>();
            foreach (var geometry in BuiltInPinCatalog.Pins(TypeId, TypeVersion))
                result.Add(new AuthoredPin(Id, PinIds[geometry.Key], AnchorCell,
                    Orientation.TransformPoint(new QuarterPoint(geometry.Qx,
                        geometry.Qy, geometry.Qz))));
            return result.AsReadOnly();
        }

        public OneBitComponent RuntimeDescriptor() =>
            new OneBitComponent(Id, TypeId, TypeVersion, PinIds, SrInitialQ,
                SourceOnValue, SourceInitialOn);
    }

    // One-cell first-playable component subset. This immutable revision owns
    // authored placement and topology; derived graphs and scenes are rebuilds.
    public sealed class OneBitWorldDesign
    {
        public WorldBounds Bounds { get; }
        public IReadOnlyList<PlacedOneBitComponent> Components { get; }
        public IReadOnlyList<PlacedOneBitModuleInstance> Modules { get; }
        public OneBitAuthoredTopology Topology { get; }

        public OneBitWorldDesign(WorldBounds bounds,
            IEnumerable<PlacedOneBitComponent> components,
            OneBitAuthoredTopology topology,
            IEnumerable<PlacedOneBitModuleInstance> modules = null)
        {
            if (components == null || topology == null) throw new ArgumentNullException();
            if (!bounds.IsValid) throw new ArgumentException("Invalid world bounds.");
            Bounds = bounds;
            var objects = new List<PlacedOneBitComponent>(components);
            var occupied = new HashSet<GridCell>();
            var objectIds = new HashSet<Guid>();
            var expectedPins = new Dictionary<JoinMember, AuthoredPin>();
            var expectedPorts = new Dictionary<JoinMember, AuthoredModulePortBit>();
            foreach (var component in objects)
            {
                if (component == null || !objectIds.Add(component.Id) ||
                    !bounds.ContainsPlaceable(component.AnchorCell) ||
                    !occupied.Add(component.AnchorCell))
                    throw new ArgumentException("Component footprint is occupied or out of world bounds.");
                foreach (var pin in component.BuildPins())
                {
                    var key = JoinMember.ComponentPin(pin.ObjectId, pin.PinId);
                    if (expectedPins.ContainsKey(key))
                        throw new ArgumentException("Duplicate component pin identity.");
                    expectedPins.Add(key, pin);
                }
            }
            if (topology.Pins.Count != expectedPins.Count)
                throw new ArgumentException("Topology pin count disagrees with placed components.");
            var seenPins = new HashSet<JoinMember>();
            foreach (var pin in topology.Pins)
            {
                var key = JoinMember.ComponentPin(pin.ObjectId, pin.PinId);
                if (!seenPins.Add(key) || !expectedPins.TryGetValue(key, out var expected) ||
                    !expected.Cell.Equals(pin.Cell) || !expected.PointQ.Equals(pin.PointQ))
                    throw new ArgumentException("Topology pin position disagrees with type/orientation.");
            }
            var placedModules = new List<PlacedOneBitModuleInstance>(
                modules ?? Array.Empty<PlacedOneBitModuleInstance>());
            var instanceIds = new HashSet<Guid>();
            var instanceNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in placedModules)
            {
                if (module == null || !objectIds.Add(module.Id) ||
                    !instanceIds.Add(module.InstanceId) ||
                    !instanceNames.Add(module.InstanceName))
                    throw new ArgumentException("Duplicate or invalid placed module.");
                foreach (var cell in module.OccupiedCells())
                    if (!bounds.ContainsPlaceable(cell) || !occupied.Add(cell))
                        throw new ArgumentException("Module footprint is occupied or out of bounds.");
                foreach (var port in module.BuildPortBits())
                {
                    var key = JoinMember.ModulePortBit(port.ObjectId,
                        port.PortId, port.BitIndex);
                    if (expectedPorts.ContainsKey(key))
                        throw new ArgumentException("Duplicate module port bit.");
                    expectedPorts.Add(key, port);
                }
            }
            if (topology.ModulePorts.Count != expectedPorts.Count)
                throw new ArgumentException("Topology module ports disagree with placements.");
            var seenPorts = new HashSet<JoinMember>();
            foreach (var port in topology.ModulePorts)
            {
                var key = JoinMember.ModulePortBit(port.ObjectId,
                    port.PortId, port.BitIndex);
                if (!seenPorts.Add(key) || !expectedPorts.TryGetValue(key, out var expected) ||
                    !expected.Cell.Equals(port.Cell) ||
                    !expected.PointQ.Equals(port.PointQ))
                    throw new ArgumentException("Module port position disagrees with interface snapshot.");
            }
            foreach (var route in topology.Connectors)
            {
                var routeNodes = new Dictionary<Guid, RouteNode>();
                foreach (var node in route.Nodes)
                {
                    if (!bounds.ContainsPlaceable(node.Cell))
                        throw new ArgumentException("Connector route leaves world bounds.");
                    routeNodes.Add(node.Id, node);
                    // A rotation may leave an old wire end on a component
                    // face. It stays physically in place but is not joined to
                    // the moved pin. Interior connector spans remain forbidden.
                    if (occupied.Contains(node.Cell) &&
                        !node.PointQ.IsFacePoint)
                        throw new ArgumentException("A connector inside an occupied cell must end on a face.");
                }
                foreach (var span in route.Spans)
                    if (routeNodes.TryGetValue(span.FromNodeId, out var from) &&
                        routeNodes.TryGetValue(span.ToNodeId, out var to) &&
                        from.Cell.Equals(to.Cell) && occupied.Contains(from.Cell))
                        throw new ArgumentException("Connector spans cannot cross a component interior.");
            }
            Components = objects.AsReadOnly();
            Modules = placedModules.AsReadOnly();
            Topology = topology;
        }

        public static OneBitWorldDesign Empty(WorldBounds bounds) =>
            new OneBitWorldDesign(bounds, Array.Empty<PlacedOneBitComponent>(),
                new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                    Array.Empty<ConnectorRoute>(), Array.Empty<ElectricalJoin>()));
    }
}
