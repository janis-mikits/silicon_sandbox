using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Graph
{
    public sealed class OneBitVisibleJunction
    {
        public Guid ConnectorId { get; }
        public Guid NodeId { get; }
        public GridCell Cell { get; }
        public QuarterPoint PointQ { get; }
        public int Channel { get; }
        public int DirectionCount { get; }

        internal OneBitVisibleJunction(Guid connectorId, RouteNode node,
            int directionCount)
        {
            ConnectorId = connectorId;
            NodeId = node.Id;
            Cell = node.Cell;
            PointQ = node.PointQ;
            Channel = node.Channel;
            DirectionCount = directionCount;
        }
    }

    // Local visual incidence, distinct from global net membership. Separate
    // paths that cross at one coordinate remain separate without a saved join.
    public static class OneBitVisualTopology
    {
        private sealed class NodeView
        {
            public Guid ConnectorId;
            public RouteNode Node;
            public HashSet<GridDirection> Directions = new HashSet<GridDirection>();
        }

        public static IReadOnlyList<OneBitVisibleJunction> Junctions(
            OneBitAuthoredTopology topology)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            var nodes = new List<NodeView>();
            var indexes = new Dictionary<JoinMember, int>();
            var routeNodes = new Dictionary<Guid, Dictionary<Guid, int>>();
            foreach (var route in topology.Connectors)
            {
                var local = new Dictionary<Guid, int>();
                routeNodes.Add(route.Id, local);
                foreach (var node in route.Nodes)
                {
                    var index = nodes.Count;
                    nodes.Add(new NodeView { ConnectorId = route.Id, Node = node });
                    local.Add(node.Id, index);
                    indexes.Add(JoinMember.ConnectorNode(route.Id, node.Id), index);
                }
            }
            var parent = new int[nodes.Count];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            foreach (var route in topology.Connectors)
                foreach (var span in route.Spans)
                {
                    var from = routeNodes[route.Id][span.FromNodeId];
                    var to = routeNodes[route.Id][span.ToNodeId];
                    var difference = Difference(nodes[from].Node, nodes[to].Node);
                    if (difference.X == 0 && difference.Y == 0 &&
                        difference.Z == 0)
                    {
                        Union(parent, from, to);
                        continue;
                    }
                    nodes[from].Directions.Add(EndpointDirection(
                        nodes[from].Node, nodes[to].Node, difference, true));
                    nodes[to].Directions.Add(EndpointDirection(
                        nodes[to].Node, nodes[from].Node, difference, false));
                }
            foreach (var join in topology.Joins)
            {
                int? first = null;
                foreach (var member in join.Members)
                {
                    if (member.Kind != JoinTargetKind.ConnectorNode) continue;
                    var index = indexes[member];
                    if (first.HasValue) Union(parent, first.Value, index);
                    else first = index;
                }
            }

            var directions = new Dictionary<int, HashSet<GridDirection>>();
            var representative = new Dictionary<int, int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var root = Find(parent, i);
                if (!directions.TryGetValue(root, out var group))
                {
                    group = new HashSet<GridDirection>();
                    directions.Add(root, group);
                    representative.Add(root, i);
                }
                group.UnionWith(nodes[i].Directions);
            }
            var result = new List<OneBitVisibleJunction>();
            foreach (var pair in directions)
                if (pair.Value.Count >= 3)
                {
                    var chosen = nodes[representative[pair.Key]];
                    result.Add(new OneBitVisibleJunction(chosen.ConnectorId,
                        chosen.Node, pair.Value.Count));
                }
            result.Sort((a, b) =>
            {
                var order = a.ConnectorId.CompareTo(b.ConnectorId);
                return order != 0 ? order : a.NodeId.CompareTo(b.NodeId);
            });
            return result.AsReadOnly();
        }

        private static GridCell Difference(RouteNode from, RouteNode to) =>
            new GridCell((to.Cell.X - from.Cell.X) * 4 +
                    to.PointQ.X - from.PointQ.X,
                (to.Cell.Y - from.Cell.Y) * 4 +
                    to.PointQ.Y - from.PointQ.Y,
                (to.Cell.Z - from.Cell.Z) * 4 +
                    to.PointQ.Z - from.PointQ.Z);

        private static GridDirection FirstDirection(GridCell delta)
        {
            if (delta.X != 0)
                return delta.X > 0 ? GridDirection.East : GridDirection.West;
            if (delta.Y != 0)
                return delta.Y > 0 ? GridDirection.Up : GridDirection.Down;
            return delta.Z > 0 ? GridDirection.North : GridDirection.South;
        }

        private static GridDirection LastReverseDirection(GridCell delta)
        {
            if (delta.Z != 0)
                return delta.Z > 0 ? GridDirection.South : GridDirection.North;
            if (delta.Y != 0)
                return delta.Y > 0 ? GridDirection.Down : GridDirection.Up;
            return delta.X > 0 ? GridDirection.West : GridDirection.East;
        }

        private static GridDirection EndpointDirection(RouteNode endpoint,
            RouteNode other, GridCell delta, bool first)
        {
            if (endpoint.Cell.Equals(other.Cell))
            {
                if (endpoint.PointQ.IsCenter && other.PointQ.IsFacePoint)
                    return Face(other.PointQ);
                if (endpoint.PointQ.IsFacePoint && other.PointQ.IsCenter)
                    return Opposite(Face(endpoint.PointQ));
            }
            return first ? FirstDirection(delta) : LastReverseDirection(delta);
        }

        private static GridDirection Face(QuarterPoint point)
        {
            if (point.X == 0) return GridDirection.West;
            if (point.X == 4) return GridDirection.East;
            if (point.Y == 0) return GridDirection.Down;
            if (point.Y == 4) return GridDirection.Up;
            if (point.Z == 0) return GridDirection.South;
            if (point.Z == 4) return GridDirection.North;
            throw new ArgumentException("Route endpoint is not on a face.");
        }

        private static GridDirection Opposite(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.East: return GridDirection.West;
                case GridDirection.West: return GridDirection.East;
                case GridDirection.North: return GridDirection.South;
                case GridDirection.South: return GridDirection.North;
                case GridDirection.Up: return GridDirection.Down;
                case GridDirection.Down: return GridDirection.Up;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }
            return index;
        }

        private static void Union(int[] parent, int first, int second)
        {
            first = Find(parent, first);
            second = Find(parent, second);
            if (first != second) parent[second] = first;
        }
    }
}
