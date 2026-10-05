using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    public sealed class OneBitRotationPreview
    {
        public Guid ObjectId { get; }
        public GridOrientation Orientation { get; }
        public OneBitWorldDesign Candidate { get; }
        public IReadOnlyList<Guid> LostJoinIds { get; }
        public ulong BaseRevision { get; }

        internal OneBitRotationPreview(Guid objectId, GridOrientation orientation,
            OneBitWorldDesign candidate, List<Guid> lostJoinIds,
            ulong baseRevision)
        {
            ObjectId = objectId;
            Orientation = orientation;
            Candidate = candidate;
            LostJoinIds = lostJoinIds.AsReadOnly();
            BaseRevision = baseRevision;
        }
    }

    // A rotation changes only authored object pose and pin/port geometry.
    // Connector nodes and spans remain at their exact saved positions.
    public static class OneBitRotationEdits
    {
        public static OneBitRotationPreview Preview(OneBitWorldDesign original,
            Guid objectId, GridOrientation orientation, ulong revision = 0)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (objectId == Guid.Empty || !orientation.IsValid)
                throw new ArgumentException("Invalid rotation target or orientation.");
            var components = new List<PlacedOneBitComponent>(original.Components);
            var modules = new List<PlacedOneBitModuleInstance>(original.Modules);
            var replacementPins = new List<AuthoredPin>();
            var replacementPorts = new List<AuthoredModulePortBit>();
            var found = false;
            for (var i = 0; i < components.Count; i++)
                if (components[i].Id == objectId)
                {
                    var old = components[i];
                    var next = new PlacedOneBitComponent(old.Id, old.TypeId,
                        old.TypeVersion, old.AnchorCell, orientation, old.PinIds,
                        old.SourceOnValue, old.SourceInitialOn, old.SrInitialQ,
                        old.Tag);
                    components[i] = next;
                    replacementPins.AddRange(next.BuildPins());
                    found = true;
                    break;
                }
            if (!found)
                for (var i = 0; i < modules.Count; i++)
                    if (modules[i].Id == objectId)
                    {
                        var old = modules[i];
                        var next = new PlacedOneBitModuleInstance(old.Id,
                            old.InstanceId, old.FamilyId, old.VersionId,
                            old.InstanceName, old.AnchorCell, orientation,
                            old.SizeCells, old.InterfacePorts, old.Tag);
                        modules[i] = next;
                        replacementPorts.AddRange(next.BuildPortBits());
                        found = true;
                        break;
                    }
            if (!found) throw new ArgumentException("Unknown rotation target.");

            var pins = new List<AuthoredPin>();
            var ports = new List<AuthoredModulePortBit>();
            foreach (var pin in original.Topology.Pins)
                if (pin.ObjectId != objectId) pins.Add(pin);
            pins.AddRange(replacementPins);
            foreach (var port in original.Topology.ModulePorts)
                if (port.ObjectId != objectId) ports.Add(port);
            ports.AddRange(replacementPorts);

            var points = new Dictionary<JoinMember, (GridCell, QuarterPoint)>();
            foreach (var pin in pins)
                points.Add(JoinMember.ComponentPin(pin.ObjectId, pin.PinId),
                    (pin.Cell, pin.PointQ));
            foreach (var port in ports)
                points.Add(JoinMember.ModulePortBit(port.ObjectId, port.PortId,
                    port.BitIndex), (port.Cell, port.PointQ));
            foreach (var route in original.Topology.Connectors)
                foreach (var node in route.Nodes)
                    points.Add(JoinMember.ConnectorNode(route.Id, node.Id),
                        (node.Cell, node.PointQ));

            var joins = new List<ElectricalJoin>();
            var lost = new List<Guid>();
            foreach (var join in original.Topology.Joins)
            {
                var anchor = points[join.Members[0]];
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ConnectorNode)
                    { anchor = points[member]; break; }
                var kept = new List<JoinMember>();
                var detached = false;
                foreach (var member in join.Members)
                {
                    var point = points[member];
                    if (member.OwnerId == objectId &&
                        member.Kind != JoinTargetKind.ConnectorNode &&
                        !SameWorldPoint(anchor, point))
                    { detached = true; continue; }
                    kept.Add(member);
                }
                if (detached) lost.Add(join.Id);
                if (kept.Count >= 2)
                    joins.Add(detached ? new ElectricalJoin(join.Id, kept) : join);
            }
            var topology = new OneBitAuthoredTopology(pins,
                original.Topology.Connectors, joins, ports);
            var candidate = new OneBitWorldDesign(original.Bounds, components,
                topology, modules);
            OneBitTopologyGraphBuilder.Build(candidate.Topology);
            return new OneBitRotationPreview(objectId, orientation, candidate,
                lost, revision);
        }

        private static bool SameWorldPoint((GridCell cell, QuarterPoint point) a,
            (GridCell cell, QuarterPoint point) b) =>
            a.cell.X * 4L + a.point.X == b.cell.X * 4L + b.point.X &&
            a.cell.Y * 4L + a.point.Y == b.cell.Y * 4L + b.point.Y &&
            a.cell.Z * 4L + a.point.Z == b.cell.Z * 4L + b.point.Z;
    }
}
