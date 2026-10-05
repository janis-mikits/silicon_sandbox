using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    // Version 1 one-bit route and attachment records. These are authored identities,
    // never renderer objects or simulator net indexes.
    public readonly struct GridCell : IEquatable<GridCell>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public GridCell(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool Equals(GridCell other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ (Y * 31) ^ Z;
    }

    public readonly struct QuarterPoint : IEquatable<QuarterPoint>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public QuarterPoint(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool IsCenter => X == 2 && Y == 2 && Z == 2;
        public bool IsFacePoint
        {
            get
            {
                var faces = (X == 0 || X == 4 ? 1 : 0) + (Y == 0 || Y == 4 ? 1 : 0) +
                    (Z == 0 || Z == 4 ? 1 : 0);
                return faces == 1 && (X == 0 || X == 4 || X == 1 || X == 3) &&
                    (Y == 0 || Y == 4 || Y == 1 || Y == 3) &&
                    (Z == 0 || Z == 4 || Z == 1 || Z == 3);
            }
        }
        public bool Equals(QuarterPoint other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is QuarterPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ (Y * 31) ^ Z;
    }

    public sealed class AuthoredPin
    {
        public Guid ObjectId { get; }
        public Guid PinId { get; }
        public GridCell Cell { get; }
        public QuarterPoint PointQ { get; }

        public AuthoredPin(Guid objectId, Guid pinId, GridCell cell, QuarterPoint pointQ)
        {
            ObjectId = objectId;
            PinId = pinId;
            Cell = cell;
            PointQ = pointQ;
        }
    }

    public sealed class AuthoredModulePortBit
    {
        public Guid ObjectId { get; }
        public Guid PortId { get; }
        public int BitIndex { get; }
        public GridCell Cell { get; }
        public QuarterPoint PointQ { get; }

        public AuthoredModulePortBit(Guid objectId, Guid portId, int bitIndex,
            GridCell cell, QuarterPoint pointQ)
        {
            ObjectId = objectId;
            PortId = portId;
            BitIndex = bitIndex;
            Cell = cell;
            PointQ = pointQ;
        }
    }

    public sealed class RouteNode
    {
        public Guid Id { get; }
        public GridCell Cell { get; }
        public int Channel { get; }
        public QuarterPoint PointQ { get; }

        public RouteNode(Guid id, GridCell cell, int channel, QuarterPoint pointQ)
        {
            Id = id;
            Cell = cell;
            Channel = channel;
            PointQ = pointQ;
        }
    }

    public sealed class RouteSpan
    {
        public Guid Id { get; }
        public Guid FromNodeId { get; }
        public Guid ToNodeId { get; }

        public RouteSpan(Guid id, Guid fromNodeId, Guid toNodeId)
        {
            Id = id;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }
    }

    public sealed class ConnectorRoute
    {
        public Guid Id { get; }
        public string Kind { get; }
        public int Width { get; }
        public IReadOnlyList<RouteNode> Nodes { get; }
        public IReadOnlyList<RouteSpan> Spans { get; }
        public string Tag { get; }
        public string IdentityColor { get; }
        public string LinkName { get; }
        public string LinkScope { get; }
        public string SourceKind { get; }

        public ConnectorRoute(Guid id, string kind, int width, IEnumerable<RouteNode> nodes,
            IEnumerable<RouteSpan> spans, string tag = "", string identityColor = null,
            string linkName = null, string linkScope = null, string sourceKind = null)
        {
            Id = id;
            Kind = kind;
            Width = width;
            Nodes = Array.AsReadOnly(new List<RouteNode>(nodes).ToArray());
            Spans = Array.AsReadOnly(new List<RouteSpan>(spans).ToArray());
            Tag = tag;
            IdentityColor = identityColor;
            LinkName = linkName;
            LinkScope = linkScope;
            SourceKind = sourceKind;
        }
    }

    public enum JoinTargetKind { ConnectorNode, ComponentPin, ModulePortBit }

    public readonly struct JoinMember : IEquatable<JoinMember>
    {
        public JoinTargetKind Kind { get; }
        public Guid OwnerId { get; }
        public Guid PartId { get; }
        public int BitIndex { get; }

        private JoinMember(JoinTargetKind kind, Guid ownerId, Guid partId, int bitIndex)
        {
            Kind = kind;
            OwnerId = ownerId;
            PartId = partId;
            BitIndex = bitIndex;
        }

        public static JoinMember ConnectorNode(Guid connectorId, Guid nodeId) =>
            new JoinMember(JoinTargetKind.ConnectorNode, connectorId, nodeId, 0);
        public static JoinMember ComponentPin(Guid objectId, Guid pinId) =>
            new JoinMember(JoinTargetKind.ComponentPin, objectId, pinId, 0);
        public static JoinMember ModulePortBit(Guid objectId, Guid portId, int bitIndex) =>
            new JoinMember(JoinTargetKind.ModulePortBit, objectId, portId, bitIndex);

        public bool Equals(JoinMember other) => Kind == other.Kind && OwnerId == other.OwnerId &&
            PartId == other.PartId && BitIndex == other.BitIndex;
        public override bool Equals(object obj) => obj is JoinMember other && Equals(other);
        public override int GetHashCode() => (((int)Kind * 397) ^ OwnerId.GetHashCode()) * 31 ^
            PartId.GetHashCode() ^ BitIndex;
    }

    public sealed class ElectricalJoin
    {
        public Guid Id { get; }
        public IReadOnlyList<JoinMember> Members { get; }

        public ElectricalJoin(Guid id, IEnumerable<JoinMember> members)
        {
            Id = id;
            Members = Array.AsReadOnly(new List<JoinMember>(members).ToArray());
        }
    }

    public sealed class OneBitAuthoredTopology
    {
        public IReadOnlyList<AuthoredPin> Pins { get; }
        public IReadOnlyList<AuthoredModulePortBit> ModulePorts { get; }
        public IReadOnlyList<ConnectorRoute> Connectors { get; }
        public IReadOnlyList<ElectricalJoin> Joins { get; }

        public OneBitAuthoredTopology(IEnumerable<AuthoredPin> pins,
            IEnumerable<ConnectorRoute> connectors, IEnumerable<ElectricalJoin> joins,
            IEnumerable<AuthoredModulePortBit> modulePorts = null)
        {
            Pins = Array.AsReadOnly(new List<AuthoredPin>(pins).ToArray());
            ModulePorts = Array.AsReadOnly(new List<AuthoredModulePortBit>(
                modulePorts ?? Array.Empty<AuthoredModulePortBit>()).ToArray());
            Connectors = Array.AsReadOnly(new List<ConnectorRoute>(connectors).ToArray());
            Joins = Array.AsReadOnly(new List<ElectricalJoin>(joins).ToArray());
        }
    }
}
