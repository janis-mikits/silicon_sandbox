using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Application
{
    // Authoring-space clearance, independent of Unity meshes and electrical nets.
    internal sealed class PinConnectionCorridors
    {
        private const double ClearanceQ = 0.15 * 4;
        // The pin front is 0.0625 cell beyond its authored attachment point.
        // Include that seated portion plus 0.25 cell of free approach space.
        private const double ReachQ = (0.0625 + 0.25) * 4;
        private readonly Dictionary<GridCell, List<Corridor>> cells =
            new Dictionary<GridCell, List<Corridor>>();

        private readonly struct Corridor
        {
            internal readonly double[] Min, Max;
            internal Corridor(GridCell origin, int direction)
            {
                Min = new[] { (double)origin.X, origin.Y, origin.Z };
                Max = (double[])Min.Clone();
                var axis = direction / 2;
                if ((direction & 1) == 0) Max[axis] += ReachQ;
                else Min[axis] -= ReachQ;
            }
        }

        internal PinConnectionCorridors(OneBitAuthoredTopology topology,
            IEnumerable<JoinMember> allowedPins = null, Guid? onlyOwner = null)
        {
            var allowed = allowedPins == null ? new HashSet<JoinMember>() :
                new HashSet<JoinMember>(allowedPins);
            foreach (var pin in topology.Pins)
                if ((!onlyOwner.HasValue || pin.ObjectId == onlyOwner.Value) &&
                    !allowed.Contains(JoinMember.ComponentPin(pin.ObjectId, pin.PinId)))
                    Add(pin.Cell, pin.PointQ);
            foreach (var port in topology.ModulePorts)
                if ((!onlyOwner.HasValue || port.ObjectId == onlyOwner.Value) &&
                    !allowed.Contains(JoinMember.ModulePortBit(port.ObjectId, port.PortId, port.BitIndex)))
                    Add(port.Cell, port.PointQ);
        }

        private void Add(GridCell cell, QuarterPoint point)
        {
            var corridor = new Corridor(QuarterWireRouter.Point(cell, point),
                QuarterWireRouter.Normal(point));
            var min = corridor.Min;
            var max = corridor.Max;
            for (var x = Cell(min[0] - ClearanceQ); x <= Cell(max[0] + ClearanceQ); x++)
            for (var y = Cell(min[1] - ClearanceQ); y <= Cell(max[1] + ClearanceQ); y++)
            for (var z = Cell(min[2] - ClearanceQ); z <= Cell(max[2] + ClearanceQ); z++)
            {
                var key = new GridCell(x, y, z);
                if (!cells.TryGetValue(key, out var entries))
                    cells[key] = entries = new List<Corridor>();
                entries.Add(corridor);
            }
        }

        private static int Cell(double quarterCoordinate) =>
            (int)Math.Floor(quarterCoordinate / 4);

        internal bool AllowsSegment(GridCell a, GridCell b) => AllowsSegment(
            new[] { (double)a.X, a.Y, a.Z }, new[] { (double)b.X, b.Y, b.Z });

        private bool AllowsSegment(double[] a, double[] b)
        {
            var min = new[] { Math.Min(a[0], b[0]), Math.Min(a[1], b[1]), Math.Min(a[2], b[2]) };
            var max = new[] { Math.Max(a[0], b[0]), Math.Max(a[1], b[1]), Math.Max(a[2], b[2]) };
            for (var x = Cell(min[0]); x <= Cell(max[0]); x++)
            for (var y = Cell(min[1]); y <= Cell(max[1]); y++)
            for (var z = Cell(min[2]); z <= Cell(max[2]); z++)
            {
                if (!cells.TryGetValue(new GridCell(x, y, z), out var entries)) continue;
                foreach (var corridor in entries)
                {
                    // Exact for axis-aligned routes. For a legacy diagonal
                    // center-to-face leg, its box is a conservative envelope.
                    var squared = 0d;
                    for (var axis = 0; axis < 3; axis++)
                    {
                        var gap = Math.Max(0, Math.Max(corridor.Min[axis] - max[axis],
                            min[axis] - corridor.Max[axis]));
                        squared += gap * gap;
                    }
                    if (squared < ClearanceQ * ClearanceQ - 1e-10) return false;
                }
            }
            return true;
        }

        private bool AllowsRoute(ConnectorRoute route, Dictionary<Guid, double[]> positions)
        {
            var nodes = new Dictionary<Guid, RouteNode>();
            foreach (var node in route.Nodes)
            {
                nodes.Add(node.Id, node);
                if (!AllowsSegment(positions[node.Id], positions[node.Id])) return false;
            }
            foreach (var span in route.Spans)
            {
                var from = nodes[span.FromNodeId]; var to = nodes[span.ToNodeId];
                var a = positions[from.Id]; var b = positions[to.Id];
                if (route.GeometryVersion == 2)
                {
                    if (!AllowsSegment(a, b)) return false;
                }
                else if (from.Cell.Equals(to.Cell) &&
                    (from.PointQ.IsCenter && to.PointQ.IsFacePoint || to.PointQ.IsCenter && from.PointQ.IsFacePoint))
                {
                    var center = from.PointQ.IsCenter ? a : b;
                    var face = from.PointQ.IsFacePoint ? a : b;
                    var normal = QuarterWireRouter.Normal(from.PointQ.IsFacePoint ? from.PointQ : to.PointQ);
                    var axis = normal / 2; var sign = (normal & 1) == 0 ? 1 : -1;
                    var first = (double[])center.Clone(); first[axis] += sign * 0.15625 * 4;
                    var second = (double[])face.Clone();
                    second[axis] -= sign * Math.Min(0.125 * 4, (face[axis] - center[axis]) * sign * 0.25);
                    if (!AllowsSegment(center, first) || !AllowsSegment(first, second) || !AllowsSegment(second, face)) return false;
                }
                else
                {
                    var xBend = new[] { b[0], a[1], a[2] };
                    var yBend = new[] { b[0], b[1], a[2] };
                    if (!AllowsSegment(a, xBend) || !AllowsSegment(xBend, yBend) || !AllowsSegment(yBend, b)) return false;
                }
            }
            return true;
        }

        private static double[] Position(GridCell cell, QuarterPoint point)
        {
            var p = QuarterWireRouter.Point(cell, point);
            return new[] { (double)p.X, p.Y, p.Z };
        }

        private static Dictionary<Guid, double[]> DisplayPositions(OneBitAuthoredTopology topology)
        {
            var positions = new Dictionary<Guid, double[]>();
            var exact = new HashSet<Guid>();
            var physical = new Dictionary<JoinMember, double[]>();
            foreach (var pin in topology.Pins)
                physical[JoinMember.ComponentPin(pin.ObjectId, pin.PinId)] = Position(pin.Cell, pin.PointQ);
            foreach (var port in topology.ModulePorts)
                physical[JoinMember.ModulePortBit(port.ObjectId, port.PortId, port.BitIndex)] = Position(port.Cell, port.PointQ);
            foreach (var route in topology.Connectors)
                foreach (var node in route.Nodes)
                {
                    var p = Position(node.Cell, node.PointQ);
                    if (route.GeometryVersion == 2) exact.Add(node.Id);
                    else
                    {
                        var q = new[] { node.PointQ.X, node.PointQ.Y, node.PointQ.Z };
                        for (var axis = 0; axis < 3; axis++)
                            if (!node.PointQ.IsFacePoint || q[axis] != 0 && q[axis] != 4)
                                p[axis] += (node.Channel - 1.5) * 0.14 * 4;
                    }
                    positions[node.Id] = p;
                }
            foreach (var join in topology.Joins)
            {
                double[] p = null;
                foreach (var member in join.Members)
                    if (physical.TryGetValue(member, out p)) break;
                if (p == null)
                    foreach (var member in join.Members)
                        if (member.Kind == JoinTargetKind.ConnectorNode && exact.Contains(member.PartId))
                        { p = positions[member.PartId]; break; }
                if (p == null)
                    foreach (var member in join.Members)
                        if (member.Kind == JoinTargetKind.ConnectorNode)
                        { p = positions[member.PartId]; break; }
                if (p == null) continue;
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ConnectorNode)
                    { positions[member.PartId] = p; exact.Add(member.PartId); }
            }
            bool changed;
            do
            {
                changed = false;
                foreach (var route in topology.Connectors)
                {
                    var nodes = new Dictionary<Guid, RouteNode>();
                    foreach (var node in route.Nodes) nodes[node.Id] = node;
                    foreach (var span in route.Spans)
                    {
                        var a = nodes[span.FromNodeId]; var b = nodes[span.ToNodeId];
                        if (!QuarterWireRouter.Point(a.Cell, a.PointQ).Equals(QuarterWireRouter.Point(b.Cell, b.PointQ))) continue;
                        if (exact.Contains(a.Id) && exact.Add(b.Id))
                        { positions[b.Id] = positions[a.Id]; changed = true; }
                        else if (exact.Contains(b.Id) && exact.Add(a.Id))
                        { positions[a.Id] = positions[b.Id]; changed = true; }
                    }
                }
            } while (changed);
            return positions;
        }

        internal static void ValidatePlacement(OneBitWorldDesign candidate, Guid objectId)
        {
            var corridors = new PinConnectionCorridors(candidate.Topology, onlyOwner: objectId);
            var positions = DisplayPositions(candidate.Topology);
            foreach (var route in candidate.Topology.Connectors)
                if (!corridors.AllowsRoute(route, positions))
                    throw new ArgumentException("An existing wire blocks a new pin's connection corridor.");
        }

        internal static void ValidateConnector(OneBitAuthoredTopology topology, ConnectorRoute route)
        {
            var allowed = new HashSet<JoinMember>();
            foreach (var join in topology.Joins)
            {
                var attachesRoute = false;
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ConnectorNode && member.OwnerId == route.Id)
                        attachesRoute = true;
                if (!attachesRoute) continue;
                foreach (var member in join.Members)
                    if (member.Kind != JoinTargetKind.ConnectorNode) allowed.Add(member);
            }
            if (!new PinConnectionCorridors(topology, allowed).AllowsRoute(route, DisplayPositions(topology)))
                throw new ArgumentException("The wire blocks an unrelated pin's connection corridor.");
        }
    }
}
