using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Application
{
    public sealed class OneBitPinRouteProposal
    {
        public ConnectorRoute Route { get; }
        public IReadOnlyList<ElectricalJoin> Joins { get; }

        internal OneBitPinRouteProposal(ConnectorRoute route, ElectricalJoin first,
            ElectricalJoin second)
        {
            Route = route;
            Joins = Array.AsReadOnly(new[] { first, second });
        }
    }

    // Two-target first-playable wire tool. It chooses a shortest route through
    // empty grid cells, then records exact face points and explicit joins.
    // This is an authoring convenience; the topology, not this pathfinder,
    // remains authoritative for electrical connectivity.
    public static class OneBitPinRoutePlanner
    {
        private static readonly GridDirection[] SearchOrder =
        {
            GridDirection.East, GridDirection.West,
            GridDirection.North, GridDirection.South,
            GridDirection.Up, GridDirection.Down
        };

        public static OneBitPinRouteProposal Plan(OneBitWorldDesign design,
            JoinMember first, JoinMember second)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (first.Kind != JoinTargetKind.ComponentPin ||
                second.Kind != JoinTargetKind.ComponentPin || first.Equals(second))
                throw new ArgumentException("Choose two distinct component pins.");
            var firstPin = FindPin(design, first);
            var secondPin = FindPin(design, second);
            foreach (var join in design.Topology.Joins)
                foreach (var member in join.Members)
                    if (member.Equals(first) || member.Equals(second))
                        throw new ArgumentException("A selected pin already has a connector.");

            if (SameWorldPoint(firstPin, secondPin))
            {
                var proposal = DirectFaceBridge(design, first, firstPin,
                    second, secondPin);
                OneBitWorldEdits.PlaceConnector(design, proposal.Route, proposal.Joins);
                return proposal;
            }

            var firstFace = Face(firstPin.PointQ);
            var secondFace = Face(secondPin.PointQ);
            var start = Move(firstPin.Cell, firstFace);
            var finish = Move(secondPin.Cell, secondFace);
            var blockedCells = new HashSet<GridCell>();
            foreach (var component in design.Components)
                blockedCells.Add(component.AnchorCell);
            for (var channel = 0; channel < 4; channel++)
            {
                var occupied = new HashSet<GridCell>(blockedCells);
                foreach (var connector in design.Topology.Connectors)
                    foreach (var node in connector.Nodes)
                        if (node.Channel == channel) occupied.Add(node.Cell);
                var path = FindPath(design.Bounds, start, finish, occupied);
                if (path == null) continue;
                var proposal = Build(path, channel, first, firstPin, second,
                    FacePoint(Opposite(Face(secondPin.PointQ)), secondPin.PointQ));
                // Validate the complete route/joins against the current design
                // before offering it to the caller for one atomic publication.
                OneBitWorldEdits.PlaceConnector(design, proposal.Route, proposal.Joins);
                return proposal;
            }
            throw new ArgumentException("No free one-bit connector route or channel.");
        }

        public static OneBitPinRouteProposal PlanToConnectorNode(
            OneBitWorldDesign design, JoinMember pin, JoinMember targetNode)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (pin.Kind != JoinTargetKind.ComponentPin ||
                targetNode.Kind != JoinTargetKind.ConnectorNode)
                throw new ArgumentException("Choose a free component pin and a connector node.");
            var sourcePin = FindPin(design, pin);
            RouteNode target = null;
            foreach (var route in design.Topology.Connectors)
                if (route.Id == targetNode.OwnerId)
                    foreach (var node in route.Nodes)
                        if (node.Id == targetNode.PartId) { target = node; break; }
            if (target == null) throw new ArgumentException("Unknown connector node.");
            foreach (var component in design.Components)
                if (component.AnchorCell.Equals(target.Cell))
                    throw new ArgumentException("Target a connector node outside a component body.");
            foreach (var join in design.Topology.Joins)
                foreach (var member in join.Members)
                    if (member.Equals(pin))
                        throw new ArgumentException("The selected pin already has a connector.");

            var start = Move(sourcePin.Cell, Face(sourcePin.PointQ));
            var blockedComponents = new HashSet<GridCell>();
            foreach (var component in design.Components)
                blockedComponents.Add(component.AnchorCell);
            for (var channel = 0; channel < 4; channel++)
            {
                var occupied = new HashSet<GridCell>(blockedComponents);
                foreach (var route in design.Topology.Connectors)
                    foreach (var node in route.Nodes)
                        if (node.Channel == channel && !node.Cell.Equals(target.Cell))
                            occupied.Add(node.Cell);
                var path = FindPath(design.Bounds, start, target.Cell, occupied);
                if (path == null) continue;
                var proposal = Build(path, channel, pin, sourcePin,
                    targetNode, target.PointQ);
                try
                {
                    OneBitWorldEdits.PlaceConnector(design, proposal.Route, proposal.Joins);
                    return proposal;
                }
                catch (ArgumentException)
                {
                    // Another path may fit a different physical channel.
                }
            }
            throw new ArgumentException("No valid route to the targeted connector node.");
        }

        private static OneBitPinRouteProposal DirectFaceBridge(
            OneBitWorldDesign design, JoinMember first, AuthoredPin firstPin,
            JoinMember second, AuthoredPin secondPin)
        {
            if (firstPin.Cell.Equals(secondPin.Cell))
                throw new ArgumentException("Distinct pins in one component cannot share a face point.");
            for (var channel = 0; channel < 4; channel++)
            {
                var available = true;
                foreach (var existingRoute in design.Topology.Connectors)
                    foreach (var node in existingRoute.Nodes)
                        if (node.Channel == channel &&
                            (node.Cell.Equals(firstPin.Cell) ||
                             node.Cell.Equals(secondPin.Cell)))
                            available = false;
                if (!available) continue;
                var firstNode = new RouteNode(Guid.NewGuid(), firstPin.Cell,
                    channel, firstPin.PointQ);
                var secondNode = new RouteNode(Guid.NewGuid(), secondPin.Cell,
                    channel, secondPin.PointQ);
                var routeId = Guid.NewGuid();
                var route = new ConnectorRoute(routeId, "wire", 1,
                    new[] { firstNode, secondNode },
                    new[] { new RouteSpan(Guid.NewGuid(), firstNode.Id, secondNode.Id) });
                return new OneBitPinRouteProposal(route,
                    new ElectricalJoin(Guid.NewGuid(), new[]
                    {
                        first, JoinMember.ConnectorNode(routeId, firstNode.Id)
                    }),
                    new ElectricalJoin(Guid.NewGuid(), new[]
                    {
                        second, JoinMember.ConnectorNode(routeId, secondNode.Id)
                    }));
            }
            throw new ArgumentException("No free channel on the shared component face.");
        }

        private static bool SameWorldPoint(AuthoredPin first, AuthoredPin second) =>
            first.Cell.X * 4L + first.PointQ.X == second.Cell.X * 4L + second.PointQ.X &&
            first.Cell.Y * 4L + first.PointQ.Y == second.Cell.Y * 4L + second.PointQ.Y &&
            first.Cell.Z * 4L + first.PointQ.Z == second.Cell.Z * 4L + second.PointQ.Z;

        private static OneBitPinRouteProposal Build(IReadOnlyList<GridCell> path,
            int channel, JoinMember first, AuthoredPin firstPin,
            JoinMember second, QuarterPoint finalPoint)
        {
            var connectorId = Guid.NewGuid();
            var nodes = new List<RouteNode>();
            var spans = new List<RouteSpan>();
            RouteNode previousExit = null;
            RouteNode firstNode = null;
            RouteNode lastNode = null;
            for (var i = 0; i < path.Count; i++)
            {
                var cell = path[i];
                var entry = i == 0
                    ? FacePoint(Opposite(Face(firstPin.PointQ)), firstPin.PointQ)
                    : FacePoint(Direction(cell, path[i - 1]), null);
                var exit = i == path.Count - 1
                    ? finalPoint
                    : FacePoint(Direction(cell, path[i + 1]), null);
                var entryNode = new RouteNode(Guid.NewGuid(), cell, channel, entry);
                nodes.Add(entryNode);
                if (firstNode == null) firstNode = entryNode;
                if (previousExit != null)
                    spans.Add(new RouteSpan(Guid.NewGuid(), previousExit.Id, entryNode.Id));
                lastNode = entryNode;
                if (!entry.Equals(exit))
                {
                    var exitNode = new RouteNode(Guid.NewGuid(), cell, channel, exit);
                    nodes.Add(exitNode);
                    spans.Add(new RouteSpan(Guid.NewGuid(), entryNode.Id, exitNode.Id));
                    lastNode = exitNode;
                }
                previousExit = lastNode;
            }
            var route = new ConnectorRoute(connectorId, "wire", 1, nodes, spans);
            return new OneBitPinRouteProposal(route,
                new ElectricalJoin(Guid.NewGuid(), new[]
                {
                    first, JoinMember.ConnectorNode(connectorId, firstNode.Id)
                }),
                new ElectricalJoin(Guid.NewGuid(), new[]
                {
                    second, JoinMember.ConnectorNode(connectorId, lastNode.Id)
                }));
        }

        private static List<GridCell> FindPath(WorldBounds bounds, GridCell start,
            GridCell finish, HashSet<GridCell> occupied)
        {
            if (!bounds.ContainsPlaceable(start) || !bounds.ContainsPlaceable(finish) ||
                occupied.Contains(start) || occupied.Contains(finish))
                return null;
            var queue = new Queue<GridCell>();
            var previous = new Dictionary<GridCell, GridCell>();
            queue.Enqueue(start);
            previous.Add(start, start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell.Equals(finish))
                {
                    var path = new List<GridCell>();
                    for (var current = finish; ; current = previous[current])
                    {
                        path.Add(current);
                        if (current.Equals(start)) break;
                    }
                    path.Reverse();
                    return path;
                }
                foreach (var direction in SearchOrder)
                {
                    var next = Move(cell, direction);
                    if (!bounds.ContainsPlaceable(next) || occupied.Contains(next) ||
                        previous.ContainsKey(next))
                        continue;
                    previous.Add(next, cell);
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        private static AuthoredPin FindPin(OneBitWorldDesign design, JoinMember member)
        {
            foreach (var pin in design.Topology.Pins)
                if (member.Equals(JoinMember.ComponentPin(pin.ObjectId, pin.PinId)))
                    return pin;
            throw new ArgumentException("Unknown component pin.");
        }

        private static GridDirection Face(QuarterPoint point)
        {
            if (point.X == 0) return GridDirection.West;
            if (point.X == 4) return GridDirection.East;
            if (point.Y == 0) return GridDirection.Down;
            if (point.Y == 4) return GridDirection.Up;
            if (point.Z == 0) return GridDirection.South;
            if (point.Z == 4) return GridDirection.North;
            throw new ArgumentException("Pin is not on a cell face.");
        }

        private static QuarterPoint FacePoint(GridDirection face, QuarterPoint? reference)
        {
            var x = reference.HasValue ? reference.Value.X : 1;
            var y = reference.HasValue ? reference.Value.Y : 1;
            var z = reference.HasValue ? reference.Value.Z : 1;
            switch (face)
            {
                case GridDirection.East: x = 4; break;
                case GridDirection.West: x = 0; break;
                case GridDirection.Up: y = 4; break;
                case GridDirection.Down: y = 0; break;
                case GridDirection.North: z = 4; break;
                case GridDirection.South: z = 0; break;
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
            // A cross-cell point must have quadrant coordinates on the other
            // axes, even when the source face used the axis now being changed.
            if (face == GridDirection.East || face == GridDirection.West)
            {
                if (y == 0 || y == 4) y = 1;
                if (z == 0 || z == 4) z = 1;
            }
            else if (face == GridDirection.Up || face == GridDirection.Down)
            {
                if (x == 0 || x == 4) x = 1;
                if (z == 0 || z == 4) z = 1;
            }
            else
            {
                if (x == 0 || x == 4) x = 1;
                if (y == 0 || y == 4) y = 1;
            }
            return new QuarterPoint(x, y, z);
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

        private static GridCell Move(GridCell cell, GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.East: return new GridCell(cell.X + 1, cell.Y, cell.Z);
                case GridDirection.West: return new GridCell(cell.X - 1, cell.Y, cell.Z);
                case GridDirection.North: return new GridCell(cell.X, cell.Y, cell.Z + 1);
                case GridDirection.South: return new GridCell(cell.X, cell.Y, cell.Z - 1);
                case GridDirection.Up: return new GridCell(cell.X, cell.Y + 1, cell.Z);
                case GridDirection.Down: return new GridCell(cell.X, cell.Y - 1, cell.Z);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static GridDirection Direction(GridCell from, GridCell to)
        {
            if (to.X == from.X + 1 && to.Y == from.Y && to.Z == from.Z)
                return GridDirection.East;
            if (to.X == from.X - 1 && to.Y == from.Y && to.Z == from.Z)
                return GridDirection.West;
            if (to.Y == from.Y + 1 && to.X == from.X && to.Z == from.Z)
                return GridDirection.Up;
            if (to.Y == from.Y - 1 && to.X == from.X && to.Z == from.Z)
                return GridDirection.Down;
            if (to.Z == from.Z + 1 && to.X == from.X && to.Y == from.Y)
                return GridDirection.North;
            if (to.Z == from.Z - 1 && to.X == from.X && to.Y == from.Y)
                return GridDirection.South;
            throw new ArgumentException("Route cells are not face-adjacent.");
        }
    }
}
