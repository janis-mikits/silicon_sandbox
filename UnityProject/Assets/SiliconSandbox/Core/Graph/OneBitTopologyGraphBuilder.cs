using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Graph
{
    public sealed class DerivedOneBitNet
    {
        public IReadOnlyList<JoinMember> Members { get; }
        public IReadOnlyList<Guid> ConnectorIds { get; }
        public string Tag { get; }

        internal DerivedOneBitNet(List<JoinMember> members, HashSet<Guid> connectorIds, string tag)
        {
            members.Sort(OneBitTopologyGraphBuilder.CompareMembers);
            Members = members.AsReadOnly();
            var sortedConnectors = new List<Guid>(connectorIds);
            sortedConnectors.Sort();
            ConnectorIds = sortedConnectors.AsReadOnly();
            Tag = tag;
        }
    }

    public sealed class OneBitTopologyGraph
    {
        private readonly Dictionary<JoinMember, DerivedOneBitNet> netsByMember;
        public IReadOnlyList<DerivedOneBitNet> Nets { get; }

        internal OneBitTopologyGraph(List<DerivedOneBitNet> nets,
            Dictionary<JoinMember, DerivedOneBitNet> netsByMember)
        {
            Nets = nets.AsReadOnly();
            this.netsByMember = netsByMember;
        }

        public DerivedOneBitNet NetFor(JoinMember member) => netsByMember[member];
        public bool Connected(JoinMember first, JoinMember second) =>
            ReferenceEquals(NetFor(first), NetFor(second));
    }

    // Derives electrical nets only from saved spans and joins. Coordinates validate
    // authored attachments, but touching or crossing geometry never creates a net.
    public static class OneBitTopologyGraphBuilder
    {
        public static OneBitTopologyGraph Build(OneBitAuthoredTopology design)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            var allIds = new HashSet<Guid>();
            var members = new List<JoinMember>();
            var memberIndexes = new Dictionary<JoinMember, int>();
            var points = new Dictionary<JoinMember, (GridCell cell, QuarterPoint point)>();
            var routes = new Dictionary<Guid, ConnectorRoute>();
            var objectIds = new HashSet<Guid>();
            var clockAnchorMembers = new List<JoinMember>();

            foreach (var pin in design.Pins)
            {
                if (pin == null) throw Invalid("Null pin.");
                if (objectIds.Add(pin.ObjectId)) AddId(allIds, pin.ObjectId);
                AddId(allIds, pin.PinId);
                if (!pin.PointQ.IsFacePoint) throw Invalid("Pin must occupy a face quadrant.");
                AddMember(JoinMember.ComponentPin(pin.ObjectId, pin.PinId), pin.Cell, pin.PointQ);
            }

            foreach (var port in design.ModulePorts)
            {
                if (port == null) throw Invalid("Null module port bit.");
                if (objectIds.Add(port.ObjectId)) AddId(allIds, port.ObjectId);
                if (port.PortId == Guid.Empty) throw Invalid("Empty module port ID.");
                if (port.BitIndex != 0 || !port.PointQ.IsFacePoint)
                    throw Invalid("Invalid one-bit module port position or bit index.");
                AddMember(JoinMember.ModulePortBit(port.ObjectId, port.PortId, 0),
                    port.Cell, port.PointQ);
            }

            foreach (var route in design.Connectors)
            {
                if (route == null) throw Invalid("Null connector.");
                AddId(allIds, route.Id);
                routes.Add(route.Id, route);
                if (route.Width != 1 || route.Nodes.Count == 0)
                    throw Invalid("Unsupported or empty one-bit connector.");
                if (route.Kind == "wire")
                {
                    if (route.LinkName != null || route.LinkScope != null ||
                        route.SourceKind != null)
                        throw Invalid("An ordinary wire cannot claim Net Link identity.");
                }
                else if (route.Kind == "netLink")
                {
                    if (route.LinkName != "@world-clock" || route.LinkScope != "world" ||
                        route.SourceKind != "worldClock")
                        throw Invalid("Only the reserved world-clock Net Link is supported in version 1.");
                    clockAnchorMembers.Add(JoinMember.ConnectorNode(route.Id, route.Nodes[0].Id));
                }
                else throw Invalid("Unsupported one-bit connector kind.");
                if (route.Tag == null || (route.IdentityColor != null && !ValidColor(route.IdentityColor)))
                    throw Invalid("Invalid connector presentation fields.");
                foreach (var node in route.Nodes)
                {
                    if (node == null) throw Invalid("Null route node.");
                    AddId(allIds, node.Id);
                    if (node.Channel < 0 || node.Channel > 3 ||
                        (!node.PointQ.IsFacePoint && !(route.GeometryVersion == 2
                            ? node.PointQ.IsInteriorQuarterPoint : node.PointQ.IsCenter)))
                        throw Invalid("Invalid channel or route point.");
                    AddMember(JoinMember.ConnectorNode(route.Id, node.Id), node.Cell, node.PointQ);
                }
            }

            var parents = new int[members.Count];
            for (var i = 0; i < parents.Length; i++) parents[i] = i;
            foreach (var route in design.Connectors)
            {
                var nodeIds = new HashSet<Guid>();
                foreach (var node in route.Nodes) nodeIds.Add(node.Id);
                foreach (var span in route.Spans)
                {
                    if (span == null) throw Invalid("Null route span.");
                    AddId(allIds, span.Id);
                    if (span.FromNodeId == span.ToNodeId || !nodeIds.Contains(span.FromNodeId) ||
                        !nodeIds.Contains(span.ToNodeId))
                        throw Invalid("Span references missing or identical node.");
                    var first = JoinMember.ConnectorNode(route.Id, span.FromNodeId);
                    var second = JoinMember.ConnectorNode(route.Id, span.ToNodeId);
                    if (!ValidSpan(points[first], points[second]) ||
                        route.GeometryVersion == 2 && points[first].cell.Equals(points[second].cell) &&
                        (points[first].point.X != points[second].point.X ? 1 : 0) +
                        (points[first].point.Y != points[second].point.Y ? 1 : 0) +
                        (points[first].point.Z != points[second].point.Z ? 1 : 0) != 1)
                        throw Invalid("Span is neither within one cell nor across a shared face.");
                    Union(parents, memberIndexes[first], memberIndexes[second]);
                }
                var firstNode = memberIndexes[JoinMember.ConnectorNode(route.Id, route.Nodes[0].Id)];
                foreach (var node in route.Nodes)
                    if (Find(parents, firstNode) != Find(parents,
                            memberIndexes[JoinMember.ConnectorNode(route.Id, node.Id)]))
                        throw Invalid("Connector node/span graph is disconnected.");
            }
            for (var i = 1; i < clockAnchorMembers.Count; i++)
                Union(parents, memberIndexes[clockAnchorMembers[0]],
                    memberIndexes[clockAnchorMembers[i]]);

            var attachedPins = new Dictionary<JoinMember, Guid>();
            foreach (var join in design.Joins)
            {
                if (join == null) throw Invalid("Null electrical join.");
                AddId(allIds, join.Id);
                if (join.Members.Count < 2) throw Invalid("Join needs at least two members.");
                var unique = new HashSet<JoinMember>();
                var connectorIds = new HashSet<Guid>();
                (GridCell cell, QuarterPoint point)? firstPoint = null;
                foreach (var member in join.Members)
                {
                    if (!unique.Add(member) || !memberIndexes.ContainsKey(member))
                        throw Invalid("Join repeats or references an unavailable member.");
                    var point = points[member];
                    if (firstPoint.HasValue && !SameWorldPoint(firstPoint.Value, point))
                        throw Invalid("Join members do not share a physical point.");
                    firstPoint = point;
                    if (member.Kind == JoinTargetKind.ConnectorNode) connectorIds.Add(member.OwnerId);
                }
                if (connectorIds.Count == 0) throw Invalid("Pins cannot join without a connector.");
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ComponentPin ||
                        member.Kind == JoinTargetKind.ModulePortBit)
                    {
                        if (connectorIds.Count != 1 || attachedPins.ContainsKey(member))
                            throw Invalid("A pin accepts one physical connector attachment.");
                        attachedPins.Add(member, First(connectorIds));
                    }
                var first = memberIndexes[join.Members[0]];
                for (var i = 1; i < join.Members.Count; i++)
                    Union(parents, first, memberIndexes[join.Members[i]]);
            }

            // Channel occupancy and net membership are separate authored facts.
            var occupied = new Dictionary<(GridCell cell, int channel), int>();
            foreach (var route in design.Connectors)
                foreach (var node in route.Nodes)
                {
                    var key = (node.Cell, node.Channel);
                    var root = Find(parents, memberIndexes[JoinMember.ConnectorNode(route.Id, node.Id)]);
                    if (occupied.TryGetValue(key, out var previous) && Find(parents, previous) != root)
                        throw Invalid("Unjoined paths occupy the same cell channel.");
                    occupied[key] = root;
                }

            var groups = new Dictionary<int, List<JoinMember>>();
            foreach (var member in members)
            {
                var root = Find(parents, memberIndexes[member]);
                if (!groups.TryGetValue(root, out var group)) groups.Add(root, group = new List<JoinMember>());
                group.Add(member);
            }
            var nets = new List<DerivedOneBitNet>();
            var byMember = new Dictionary<JoinMember, DerivedOneBitNet>();
            foreach (var group in groups.Values)
            {
                var connectorIds = new HashSet<Guid>();
                string tag = null;
                foreach (var member in group)
                    if (member.Kind == JoinTargetKind.ConnectorNode && connectorIds.Add(member.OwnerId))
                    {
                        var routeTag = routes[member.OwnerId].Tag;
                        if (tag != null && tag != routeTag)
                            throw Invalid("Joined connectors have conflicting authored tags.");
                        tag = routeTag;
                    }
                var net = new DerivedOneBitNet(group, connectorIds, tag ?? "");
                nets.Add(net);
                foreach (var member in group) byMember.Add(member, net);
            }
            nets.Sort((first, second) => CompareMembers(first.Members[0], second.Members[0]));
            return new OneBitTopologyGraph(nets, byMember);

            void AddMember(JoinMember member, GridCell cell, QuarterPoint point)
            {
                if (memberIndexes.ContainsKey(member)) throw Invalid("Duplicate endpoint.");
                memberIndexes.Add(member, members.Count);
                members.Add(member);
                points.Add(member, (cell, point));
            }
        }

        private static bool ValidSpan((GridCell cell, QuarterPoint point) first,
            (GridCell cell, QuarterPoint point) second)
        {
            if (first.cell.Equals(second.cell)) return !first.point.Equals(second.point);
            var distance = Math.Abs(first.cell.X - second.cell.X) +
                Math.Abs(first.cell.Y - second.cell.Y) + Math.Abs(first.cell.Z - second.cell.Z);
            return distance == 1 && first.point.IsFacePoint && second.point.IsFacePoint &&
                SameWorldPoint(first, second);
        }

        private static bool SameWorldPoint((GridCell cell, QuarterPoint point) first,
            (GridCell cell, QuarterPoint point) second) =>
            first.cell.X * 4L + first.point.X == second.cell.X * 4L + second.point.X &&
            first.cell.Y * 4L + first.point.Y == second.cell.Y * 4L + second.point.Y &&
            first.cell.Z * 4L + first.point.Z == second.cell.Z * 4L + second.point.Z;

        private static bool ValidColor(string color)
        {
            if (color.Length != 7 || color[0] != '#') return false;
            for (var i = 1; i < 7; i++) if (!Uri.IsHexDigit(color[i])) return false;
            return true;
        }

        private static Guid First(HashSet<Guid> values)
        {
            foreach (var value in values) return value;
            throw Invalid("Missing connector.");
        }

        private static void AddId(HashSet<Guid> ids, Guid id)
        {
            var value = id.ToString("D");
            if (id == Guid.Empty || value[14] != '4' || "89ab".IndexOf(value[19]) < 0 || !ids.Add(id))
                throw Invalid("Identity must be a unique UUIDv4.");
        }

        private static int Find(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }
            return index;
        }

        private static void Union(int[] parents, int first, int second)
        {
            first = Find(parents, first);
            second = Find(parents, second);
            if (first != second) parents[second] = first;
        }

        private static ArgumentException Invalid(string message) => new ArgumentException(message);

        internal static int CompareMembers(JoinMember first, JoinMember second)
        {
            var order = first.Kind.CompareTo(second.Kind);
            if (order != 0) return order;
            order = first.OwnerId.CompareTo(second.OwnerId);
            if (order != 0) return order;
            order = first.PartId.CompareTo(second.PartId);
            return order != 0 ? order : first.BitIndex.CompareTo(second.BitIndex);
        }
    }
}
