using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    public sealed class TopologyEditResult
    {
        public OneBitAuthoredTopology Design { get; }
        public OneBitTopologyGraph Graph { get; }
        public Guid RetiredConnectorId { get; }
        public IReadOnlyList<Guid> ReplacementConnectorIds { get; }

        internal TopologyEditResult(OneBitAuthoredTopology design, OneBitTopologyGraph graph,
            Guid retiredConnectorId, List<Guid> replacementConnectorIds)
        {
            Design = design;
            Graph = graph;
            RetiredConnectorId = retiredConnectorId;
            ReplacementConnectorIds = replacementConnectorIds.AsReadOnly();
        }
    }

    // Candidate revisions are built and fully checked before a caller publishes one.
    // The simulation coordinator will enforce the safe settled pause boundary.
    public static class OneBitTopologyEdits
    {
        public static TopologyEditResult AddJoin(OneBitAuthoredTopology original,
            ElectricalJoin join, string chosenTag = null)
        {
            if (original == null || join == null) throw new ArgumentNullException();
            var before = OneBitTopologyGraphBuilder.Build(original);
            var affected = new HashSet<Guid>();
            var nonemptyTags = new HashSet<string>();
            foreach (var member in join.Members)
            {
                var net = before.NetFor(member);
                foreach (var id in net.ConnectorIds) affected.Add(id);
                if (net.Tag.Length > 0) nonemptyTags.Add(net.Tag);
            }
            if (nonemptyTags.Count > 1 && chosenTag == null)
                throw new ArgumentException("Conflicting tags require a player choice before joining.");
            var retainedTag = "";
            foreach (var tag in nonemptyTags) retainedTag = tag;
            if (nonemptyTags.Count > 1)
                retainedTag = chosenTag;
            if (retainedTag == null) throw new ArgumentException("Tag choice cannot be null.");

            var routes = new List<ConnectorRoute>();
            foreach (var route in original.Connectors)
                routes.Add(affected.Contains(route.Id) && route.Tag != retainedTag
                    ? new ConnectorRoute(route.Id, route.Kind, route.Width, route.Nodes,
                        route.Spans, retainedTag, route.IdentityColor,
                        route.LinkName, route.LinkScope, route.SourceKind)
                    : route);
            var joins = new List<ElectricalJoin>(original.Joins) { join };
            return Validated(original.Pins, original.ModulePorts, routes, joins,
                Guid.Empty, new List<Guid>());
        }

        public static TopologyEditResult BreakSpan(OneBitAuthoredTopology original,
            Guid connectorId, Guid spanId, Func<Guid> allocateId = null)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            OneBitTopologyGraphBuilder.Build(original);
            ConnectorRoute target = null;
            foreach (var route in original.Connectors)
                if (route.Id == connectorId) target = route;
            if (target == null) throw new ArgumentException("Connector does not exist.");
            var remaining = new List<RouteSpan>();
            var found = false;
            foreach (var span in target.Spans)
                if (span.Id == spanId) found = true;
                else remaining.Add(span);
            if (!found) throw new ArgumentException("Span does not exist on connector.");

            var adjacent = new Dictionary<Guid, List<Guid>>();
            foreach (var node in target.Nodes) adjacent.Add(node.Id, new List<Guid>());
            foreach (var span in remaining)
            {
                adjacent[span.FromNodeId].Add(span.ToNodeId);
                adjacent[span.ToNodeId].Add(span.FromNodeId);
            }
            var pieces = new List<HashSet<Guid>>();
            var visited = new HashSet<Guid>();
            foreach (var node in target.Nodes)
            {
                if (!visited.Add(node.Id)) continue;
                var piece = new HashSet<Guid> { node.Id };
                var queue = new Queue<Guid>();
                queue.Enqueue(node.Id);
                while (queue.Count > 0)
                {
                    foreach (var neighbor in adjacent[queue.Dequeue()])
                        if (visited.Add(neighbor))
                        {
                            piece.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                }
                // Discard a zero-length wire tail only when it is attached
                // to a pin and no other connector: otherwise it would keep
                // the pin occupied after its last visible span broke. A
                // one-node bridge to another visible connector is retained.
                // Preserve the clock Net Link anchor under its identity rule.
                var hasSpan = remaining.Exists(span =>
                    piece.Contains(span.FromNodeId));
                var occupiesPin = false;
                var bridgesConnector = false;
                if (!hasSpan && target.Kind == "wire")
                    foreach (var join in original.Joins)
                    {
                        var hasNode = false;
                        var hasPin = false;
                        foreach (var member in join.Members)
                        {
                            if (member.Kind == JoinTargetKind.ConnectorNode &&
                                member.OwnerId == connectorId &&
                                piece.Contains(member.PartId)) hasNode = true;
                            if (member.Kind == JoinTargetKind.ComponentPin ||
                                member.Kind == JoinTargetKind.ModulePortBit)
                                hasPin = true;
                        }
                        if (hasNode && hasPin) occupiesPin = true;
                        if (hasNode)
                            foreach (var member in join.Members)
                                if (member.Kind == JoinTargetKind.ConnectorNode &&
                                    member.OwnerId != connectorId)
                                    bridgesConnector = true;
                    }
                if (!occupiesPin || bridgesConnector)
                    pieces.Add(piece);
            }

            var replacementIds = new List<Guid>();
            var nodeOwners = new Dictionary<Guid, Guid>();
            var routes = new List<ConnectorRoute>();
            var occupied = new Dictionary<GridCell, HashSet<int>>();
            foreach (var route in original.Connectors)
                if (route.Id != connectorId)
                    foreach (var node in route.Nodes)
                        Channels(occupied, node.Cell).Add(node.Channel);
            foreach (var route in original.Connectors)
            {
                if (route.Id != connectorId) { routes.Add(route); continue; }
                foreach (var piece in pieces)
                {
                    var id = pieces.Count == 1 ? connectorId : (allocateId ?? Guid.NewGuid)();
                    if (pieces.Count > 1) replacementIds.Add(id);
                    var nodes = new List<RouteNode>();
                    var spans = new List<RouteSpan>();
                    var channelsForPiece = new Dictionary<GridCell, int>();
                    foreach (var candidate in target.Nodes)
                        if (piece.Contains(candidate.Id))
                        {
                            var channel = candidate.Channel;
                            if (pieces.Count > 1)
                            {
                                if (!channelsForPiece.TryGetValue(candidate.Cell, out channel))
                                {
                                    var taken = Channels(occupied, candidate.Cell);
                                    channel = candidate.Channel;
                                    if (taken.Contains(channel))
                                    {
                                        channel = 0;
                                        while (channel < 4 && taken.Contains(channel)) channel++;
                                        if (channel == 4)
                                            throw new ArgumentException("No free channel for split connector.");
                                    }
                                    channelsForPiece.Add(candidate.Cell, channel);
                                    taken.Add(channel);
                                }
                            }
                            nodes.Add(channel == candidate.Channel ? candidate :
                                new RouteNode(candidate.Id, candidate.Cell, channel, candidate.PointQ));
                            nodeOwners.Add(candidate.Id, id);
                        }
                    foreach (var candidate in remaining)
                        if (piece.Contains(candidate.FromNodeId)) spans.Add(candidate);
                    var keepsLink = route.Kind == "netLink" &&
                        piece.Contains(route.Nodes[0].Id);
                    routes.Add(new ConnectorRoute(id, keepsLink ? "netLink" : "wire",
                        route.Width, nodes, spans, route.Tag, route.IdentityColor,
                        keepsLink ? route.LinkName : null,
                        keepsLink ? route.LinkScope : null,
                        keepsLink ? route.SourceKind : null));
                }
            }
            var joins = new List<ElectricalJoin>();
            foreach (var join in original.Joins)
            {
                var members = new List<JoinMember>();
                foreach (var member in join.Members)
                {
                    if (member.Kind == JoinTargetKind.ConnectorNode &&
                        member.OwnerId == connectorId)
                    {
                        if (nodeOwners.TryGetValue(member.PartId, out var owner))
                            members.Add(JoinMember.ConnectorNode(owner, member.PartId));
                    }
                    else members.Add(member);
                }
                if (members.Count >= 2)
                    joins.Add(new ElectricalJoin(join.Id, members));
            }
            return Validated(original.Pins, original.ModulePorts, routes, joins,
                pieces.Count != 1 ? connectorId : Guid.Empty, replacementIds);
        }

        private static TopologyEditResult Validated(IEnumerable<AuthoredPin> pins,
            IEnumerable<AuthoredModulePortBit> modulePorts,
            IEnumerable<ConnectorRoute> routes, IEnumerable<ElectricalJoin> joins,
            Guid retired, List<Guid> replacements)
        {
            var candidate = new OneBitAuthoredTopology(pins, routes, joins,
                modulePorts);
            var graph = OneBitTopologyGraphBuilder.Build(candidate);
            return new TopologyEditResult(candidate, graph, retired, replacements);
        }

        private static HashSet<int> Channels(Dictionary<GridCell, HashSet<int>> occupied, GridCell cell)
        {
            if (!occupied.TryGetValue(cell, out var channels))
                occupied.Add(cell, channels = new HashSet<int>());
            return channels;
        }
    }
}
