using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    public enum OneBitPortDirection { Input, Output, Inout }

    public sealed class OneBitModulePort
    {
        public Guid Id { get; }
        public string Name { get; }
        public OneBitPortDirection Direction { get; }
        public int Width => 1;
        public GridCell LocalCell { get; }
        public QuarterPoint PointQ { get; }
        public JoinMember BitZeroTarget { get; }

        public OneBitModulePort(Guid id, string name, OneBitPortDirection direction,
            GridCell localCell, QuarterPoint pointQ, JoinMember bitZeroTarget)
        {
            Id = id;
            Name = name;
            Direction = direction;
            LocalCell = localCell;
            PointQ = pointQ;
            BitZeroTarget = bitZeroTarget;
        }
    }

    // Immutable first-playable version. It contains authored layout and exact
    // endpoint mappings, but no Q, source switch, event, or clock runtime state.
    public sealed class OneBitModuleVersion
    {
        public Guid FamilyId { get; }
        public Guid VersionId { get; }
        public string Name { get; }
        // Captured internal design bounds, retained for viewing/re-expansion.
        public GridCell SizeCells { get; }
        // Compact occupied exterior bounds, used for placement and ports.
        public GridCell ExteriorSizeCells { get; }
        public IReadOnlyList<PlacedOneBitComponent> Components { get; }
        public OneBitAuthoredTopology Topology { get; }
        public IReadOnlyList<OneBitModulePort> Ports { get; }
        public IReadOnlyList<Guid> ChildVersionIds => Array.Empty<Guid>();

        public OneBitModuleVersion(Guid familyId, Guid versionId, string name,
            GridCell sizeCells, IEnumerable<PlacedOneBitComponent> components,
            OneBitAuthoredTopology topology, IEnumerable<OneBitModulePort> ports)
            : this(familyId, versionId, name, sizeCells, sizeCells, components,
                topology, ports)
        {
        }

        public OneBitModuleVersion(Guid familyId, Guid versionId, string name,
            GridCell sizeCells, GridCell exteriorSizeCells,
            IEnumerable<PlacedOneBitComponent> components,
            OneBitAuthoredTopology topology, IEnumerable<OneBitModulePort> ports)
        {
            if (familyId == Guid.Empty || versionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(name) || components == null ||
                topology == null || ports == null || sizeCells.X < 1 ||
                sizeCells.Y < 1 || sizeCells.Z < 1 ||
                exteriorSizeCells.X < 1 || exteriorSizeCells.Y < 1 ||
                exteriorSizeCells.Z < 1)
                throw new ArgumentException("Incomplete fixed module version.");
            var copiedComponents = new List<PlacedOneBitComponent>(components);
            var copiedPorts = new List<OneBitModulePort>(ports);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var portIds = new HashSet<Guid>();
            var facePositions = new HashSet<(GridCell, QuarterPoint)>();
            var endpoints = new HashSet<JoinMember>();
            foreach (var pin in topology.Pins)
                endpoints.Add(JoinMember.ComponentPin(pin.ObjectId, pin.PinId));
            foreach (var route in topology.Connectors)
                foreach (var node in route.Nodes)
                    endpoints.Add(JoinMember.ConnectorNode(route.Id, node.Id));
            foreach (var port in copiedPorts)
            {
                if (port == null || port.Id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(port.Name) ||
                    !Enum.IsDefined(typeof(OneBitPortDirection), port.Direction) ||
                    !names.Add(port.Name) || !portIds.Add(port.Id) ||
                    !Exterior(exteriorSizeCells, port.LocalCell, port.PointQ) ||
                    !facePositions.Add((port.LocalCell, port.PointQ)) ||
                    !endpoints.Contains(port.BitZeroTarget))
                    throw new ArgumentException("Invalid, duplicate, or unmapped module port.");
            }
            FamilyId = familyId;
            VersionId = versionId;
            Name = name;
            SizeCells = sizeCells;
            ExteriorSizeCells = exteriorSizeCells;
            Components = copiedComponents.AsReadOnly();
            Topology = topology;
            Ports = copiedPorts.AsReadOnly();
        }

        private static bool Exterior(GridCell size, GridCell cell, QuarterPoint point) =>
            point.IsFacePoint && cell.X >= 0 && cell.Y >= 0 && cell.Z >= 0 &&
            cell.X < size.X && cell.Y < size.Y && cell.Z < size.Z &&
            (cell.X == 0 && point.X == 0 ||
             cell.X == size.X - 1 && point.X == 4 ||
             cell.Y == 0 && point.Y == 0 ||
             cell.Y == size.Y - 1 && point.Y == 4 ||
             cell.Z == 0 && point.Z == 0 ||
             cell.Z == size.Z - 1 && point.Z == 4);
    }
}
