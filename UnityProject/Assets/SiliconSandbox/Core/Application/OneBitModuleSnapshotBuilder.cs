using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    // Extracts a one-bit component selection into an independent local blueprint.
    // Exterior port configuration and durable publication are later stages.
    public static class OneBitModuleSnapshotBuilder
    {
        public static OneBitModuleSnapshot Preview(OneBitWorldDesign world,
            CellRegion region)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.Bounds.ContainsPlaceable(region.Min) ||
                !world.Bounds.ContainsPlaceable(region.Max))
                throw new ArgumentException("Package selection leaves placeable world bounds.");

            var components = new List<PlacedOneBitComponent>();
            var componentIds = new Dictionary<Guid, Guid>();
            var pinIds = new Dictionary<Guid, Guid>();
            foreach (var source in world.Components)
            {
                if (!region.Contains(source.AnchorCell)) continue;
                var newId = Guid.NewGuid();
                componentIds.Add(source.Id, newId);
                var copiedPins = new Dictionary<string, Guid>();
                foreach (var pair in source.PinIds)
                {
                    var copiedPin = Guid.NewGuid();
                    pinIds.Add(pair.Value, copiedPin);
                    copiedPins.Add(pair.Key, copiedPin);
                }
                components.Add(new PlacedOneBitComponent(newId, source.TypeId,
                    source.TypeVersion, region.Local(source.AnchorCell),
                    source.Orientation, copiedPins, source.SourceOnValue,
                    source.SourceInitialOn, source.SrInitialQ, source.Tag));
            }
            if (components.Count == 0)
                throw new ArgumentException("Package selection contains no circuit component.");

            var routes = new List<ConnectorRoute>();
            var copiedNodeRefs = new Dictionary<JoinMember, JoinMember>();
            foreach (var source in world.Topology.Connectors)
            {
                var inside = new HashSet<Guid>();
                foreach (var node in source.Nodes)
                    if (region.Contains(node.Cell)) inside.Add(node.Id);
                if (inside.Count == 0) continue;
                var neighbors = new Dictionary<Guid, List<Guid>>();
                foreach (var node in source.Nodes)
                    if (inside.Contains(node.Id)) neighbors.Add(node.Id, new List<Guid>());
                foreach (var span in source.Spans)
                    if (inside.Contains(span.FromNodeId) && inside.Contains(span.ToNodeId))
                    {
                        neighbors[span.FromNodeId].Add(span.ToNodeId);
                        neighbors[span.ToNodeId].Add(span.FromNodeId);
                    }
                var remaining = new HashSet<Guid>(inside);
                while (remaining.Count > 0)
                {
                    var seed = Guid.Empty;
                    foreach (var node in source.Nodes)
                        if (remaining.Contains(node.Id)) { seed = node.Id; break; }
                    var piece = new HashSet<Guid>();
                    var pending = new Queue<Guid>();
                    pending.Enqueue(seed);
                    remaining.Remove(seed);
                    while (pending.Count > 0)
                    {
                        var current = pending.Dequeue();
                        piece.Add(current);
                        foreach (var adjacent in neighbors[current])
                            if (remaining.Remove(adjacent)) pending.Enqueue(adjacent);
                    }
                    var newRouteId = Guid.NewGuid();
                    var nodes = new List<RouteNode>();
                    var spans = new List<RouteSpan>();
                    var localIds = new Dictionary<Guid, Guid>();
                    foreach (var node in source.Nodes)
                        if (piece.Contains(node.Id))
                        {
                            var copiedNode = Guid.NewGuid();
                            localIds.Add(node.Id, copiedNode);
                            copiedNodeRefs.Add(JoinMember.ConnectorNode(source.Id, node.Id),
                                JoinMember.ConnectorNode(newRouteId, copiedNode));
                            nodes.Add(new RouteNode(copiedNode, region.Local(node.Cell),
                                node.Channel, node.PointQ));
                        }
                    foreach (var span in source.Spans)
                        if (piece.Contains(span.FromNodeId) &&
                            piece.Contains(span.ToNodeId))
                            spans.Add(new RouteSpan(Guid.NewGuid(),
                                localIds[span.FromNodeId], localIds[span.ToNodeId]));
                    var retainsLink = source.Kind == "netLink" &&
                        piece.Contains(source.Nodes[0].Id);
                    routes.Add(new ConnectorRoute(newRouteId,
                        retainsLink ? "netLink" : "wire", source.Width, nodes,
                        spans, source.Tag, source.IdentityColor,
                        retainsLink ? source.LinkName : null,
                        retainsLink ? source.LinkScope : null,
                        retainsLink ? source.SourceKind : null));
                }
            }

            var pins = new List<AuthoredPin>();
            foreach (var component in components) pins.AddRange(component.BuildPins());
            var joins = new List<ElectricalJoin>();
            foreach (var join in world.Topology.Joins)
            {
                var members = new List<JoinMember>();
                foreach (var member in join.Members)
                {
                    if (member.Kind == JoinTargetKind.ComponentPin &&
                        componentIds.TryGetValue(member.OwnerId, out var objectId))
                        members.Add(JoinMember.ComponentPin(objectId, pinIds[member.PartId]));
                    else if (member.Kind == JoinTargetKind.ConnectorNode &&
                        copiedNodeRefs.TryGetValue(member, out var copiedNode))
                        members.Add(copiedNode);
                }
                if (members.Count >= 2) joins.Add(new ElectricalJoin(Guid.NewGuid(), members));
            }
            var provisional = new OneBitAuthoredTopology(pins, routes, joins);
            var provisionalGraph = OneBitTopologyGraphBuilder.Build(provisional);
            var retainedRoutes = new List<ConnectorRoute>();
            var retainedIds = new HashSet<Guid>();
            foreach (var route in routes)
            {
                var connectedInside = false;
                foreach (var member in provisionalGraph.NetFor(
                    JoinMember.ConnectorNode(route.Id, route.Nodes[0].Id)).Members)
                    if (member.Kind == JoinTargetKind.ComponentPin)
                    {
                        connectedInside = true;
                        break;
                    }
                if (!connectedInside) continue;
                retainedRoutes.Add(route);
                retainedIds.Add(route.Id);
            }
            var retainedJoins = new List<ElectricalJoin>();
            foreach (var join in joins)
            {
                var keep = true;
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ConnectorNode &&
                        !retainedIds.Contains(member.OwnerId))
                        keep = false;
                if (keep) retainedJoins.Add(join);
            }
            var topology = new OneBitAuthoredTopology(pins, retainedRoutes,
                retainedJoins);
            var graph = OneBitTopologyGraphBuilder.Build(topology);
            var candidates = new List<OneBitPortCandidate>();
            foreach (var pin in pins)
                if (OnExterior(region.SizeCells, pin.Cell, pin.PointQ))
                    candidates.Add(new OneBitPortCandidate(
                        JoinMember.ComponentPin(pin.ObjectId, pin.PinId),
                        pin.Cell, pin.PointQ));
            // A clipped route ends at its exact inside boundary face. Only a
            // piece connected to selected circuitry can expose a port.
            foreach (var route in retainedRoutes)
                foreach (var node in route.Nodes)
                {
                    if (!OnExterior(region.SizeCells, node.Cell, node.PointQ)) continue;
                    var endpoint = JoinMember.ConnectorNode(route.Id, node.Id);
                    var connectedInside = false;
                    foreach (var member in graph.NetFor(endpoint).Members)
                        if (member.Kind == JoinTargetKind.ComponentPin)
                        {
                            connectedInside = true;
                            break;
                        }
                    if (connectedInside)
                        candidates.Add(new OneBitPortCandidate(endpoint, node.Cell,
                            node.PointQ));
                }
            return new OneBitModuleSnapshot(region.SizeCells, components, topology,
                candidates);
        }

        private static bool OnExterior(GridCell size, GridCell cell, QuarterPoint point) =>
            point.IsFacePoint &&
            (cell.X == 0 && point.X == 0 || cell.X == size.X - 1 && point.X == 4 ||
             cell.Y == 0 && point.Y == 0 || cell.Y == size.Y - 1 && point.Y == 4 ||
             cell.Z == 0 && point.Z == 0 || cell.Z == size.Z - 1 && point.Z == 4);
    }
}
