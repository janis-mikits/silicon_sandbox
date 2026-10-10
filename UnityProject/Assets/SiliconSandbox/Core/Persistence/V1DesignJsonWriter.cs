using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Persistence
{
    internal static class V1DesignJsonWriter
    {
        public static void Write(V1JsonWriter json,
            IReadOnlyList<PlacedOneBitComponent> components,
            IReadOnlyList<PlacedOneBitModuleInstance> modules,
            OneBitAuthoredTopology topology)
        {
            json.BeginObject();
            json.Name("objects"); json.BeginArray();
            foreach (var component in components) WriteComponent(json, component);
            foreach (var module in modules) WriteModuleInstance(json, module);
            json.EndArray();
            json.Name("connectors"); json.BeginArray();
            foreach (var route in topology.Connectors) WriteConnector(json, route);
            json.EndArray();
            json.Name("joins"); json.BeginArray();
            foreach (var join in topology.Joins) WriteJoin(json, join);
            json.EndArray();
            json.EndObject();
        }

        public static void Cell(V1JsonWriter json, GridCell cell)
        {
            json.BeginArray(); json.Integer(cell.X); json.Integer(cell.Y);
            json.Integer(cell.Z); json.EndArray();
        }

        public static void Point(V1JsonWriter json, QuarterPoint point)
        {
            json.BeginArray(); json.Integer(point.X); json.Integer(point.Y);
            json.Integer(point.Z); json.EndArray();
        }

        public static void Orientation(V1JsonWriter json, GridOrientation orientation)
        {
            json.BeginObject();
            json.Name("forward"); json.String(GridOrientation.FaceName(orientation.Forward));
            json.Name("up"); json.String(GridOrientation.FaceName(orientation.Up));
            json.EndObject();
        }

        public static void Endpoint(V1JsonWriter json, JoinMember member)
        {
            json.BeginObject();
            switch (member.Kind)
            {
                case JoinTargetKind.ConnectorNode:
                    json.Name("targetKind"); json.String("connectorNode");
                    Id(json, "connectorId", member.OwnerId);
                    Id(json, "nodeId", member.PartId);
                    break;
                case JoinTargetKind.ComponentPin:
                    json.Name("targetKind"); json.String("componentPin");
                    Id(json, "objectId", member.OwnerId);
                    Id(json, "pinId", member.PartId);
                    break;
                case JoinTargetKind.ModulePortBit:
                    json.Name("targetKind"); json.String("modulePortBit");
                    Id(json, "objectId", member.OwnerId);
                    Id(json, "portId", member.PartId);
                    json.Name("bitIndex"); json.Integer(member.BitIndex);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(member));
            }
            json.EndObject();
        }

        public static void Id(V1JsonWriter json, string name, Guid value)
        { json.Name(name); json.String(value.ToString("D")); }

        public static string Direction(OneBitPortDirection value)
        {
            switch (value)
            {
                case OneBitPortDirection.Input: return "input";
                case OneBitPortDirection.Output: return "output";
                case OneBitPortDirection.Inout: return "inout";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static void WriteComponent(V1JsonWriter json,
            PlacedOneBitComponent component)
        {
            json.BeginObject();
            json.Name("kind"); json.String("component");
            Id(json, "id", component.Id);
            json.Name("typeId"); json.String(component.TypeId);
            json.Name("typeVersion"); json.Integer(component.TypeVersion);
            json.Name("anchorCell"); Cell(json, component.AnchorCell);
            json.Name("orientation"); Orientation(json, component.Orientation);
            json.Name("configuration"); json.BeginObject();
            if (component.TypeId == BuiltInPinCatalog.Source)
            {
                json.Name("width"); json.Integer(1);
                json.Name("onValueBits"); json.BeginArray();
                json.String(component.SourceOnValue.ToSymbol());
                json.EndArray();
                json.Name("initialOn"); json.Boolean(component.SourceInitialOn);
            }
            else if (component.TypeId == BuiltInPinCatalog.And)
            { json.Name("width"); json.Integer(1); }
            else if (component.TypeId == BuiltInPinCatalog.SrFlipFlop)
            {
                json.Name("initialQ");
                if (component.SrInitialQ.HasValue)
                    json.String(component.SrInitialQ.Value.ToSymbol());
                else json.Null();
            }
            else throw new ArgumentException("Unsupported V1 component type.");
            json.EndObject();
            json.Name("pins"); json.BeginArray();
            foreach (var pin in BuiltInPinCatalog.Pins(component.TypeId,
                         component.TypeVersion))
            {
                json.BeginObject();
                Id(json, "id", component.PinIds[pin.Key]);
                json.Name("pinKey"); json.String(pin.Key);
                json.Name("direction"); json.String(pin.Direction == PinDirection.Input
                    ? "input" : "output");
                json.Name("width"); json.Integer(1);
                json.Name("cellOffset"); Cell(json, new GridCell(0, 0, 0));
                json.Name("pointQ"); Point(json,
                    new QuarterPoint(pin.Qx, pin.Qy, pin.Qz));
                json.EndObject();
            }
            json.EndArray();
            json.Name("appearance"); json.BeginObject();
            json.Name("styleId"); json.String("builtin.component.default");
            json.EndObject();
            json.Name("tag"); json.String(component.Tag);
            json.EndObject();
        }

        private static void WriteModuleInstance(V1JsonWriter json,
            PlacedOneBitModuleInstance module)
        {
            json.BeginObject();
            json.Name("kind"); json.String("moduleInstance");
            Id(json, "id", module.Id);
            Id(json, "instanceId", module.InstanceId);
            Id(json, "familyId", module.FamilyId);
            Id(json, "versionId", module.VersionId);
            json.Name("instanceName"); json.String(module.InstanceName);
            json.Name("anchorCell"); Cell(json, module.AnchorCell);
            json.Name("orientation"); Orientation(json, module.Orientation);
            json.Name("interfaceSnapshot"); json.BeginObject();
            json.Name("sizeCells"); Cell(json, module.SizeCells);
            json.Name("ports"); json.BeginArray();
            foreach (var port in module.InterfacePorts)
            {
                json.BeginObject();
                Id(json, "id", port.Id);
                json.Name("name"); json.String(port.Name);
                json.Name("direction"); json.String(Direction(port.Direction));
                json.Name("width"); json.Integer(1);
                json.Name("bitOrder"); json.String("lsb0");
                json.Name("localCell"); Cell(json, port.LocalCell);
                json.Name("pointQ"); Point(json, port.PointQ);
                json.EndObject();
            }
            json.EndArray(); json.EndObject();
            json.Name("tag"); json.String(module.Tag);
            json.EndObject();
        }

        private static void WriteConnector(V1JsonWriter json, ConnectorRoute route)
        {
            json.BeginObject();
            Id(json, "id", route.Id);
            if (route.GeometryVersion == 2)
            { json.Name("geometryVersion"); json.Integer(2); }
            json.Name("kind"); json.String(route.Kind);
            json.Name("width"); json.Integer(route.Width);
            json.Name("nodes"); json.BeginArray();
            foreach (var node in route.Nodes)
            {
                json.BeginObject();
                Id(json, "id", node.Id);
                json.Name("cell"); Cell(json, node.Cell);
                json.Name("channel"); json.Integer(node.Channel);
                json.Name("pointQ"); Point(json, node.PointQ);
                json.EndObject();
            }
            json.EndArray();
            json.Name("spans"); json.BeginArray();
            foreach (var span in route.Spans)
            {
                json.BeginObject();
                Id(json, "id", span.Id);
                Id(json, "fromNodeId", span.FromNodeId);
                Id(json, "toNodeId", span.ToNodeId);
                json.EndObject();
            }
            json.EndArray();
            json.Name("tag"); json.String(route.Tag);
            json.Name("identityColor");
            if (route.IdentityColor == null) json.Null();
            else json.String(route.IdentityColor);
            if (route.Kind == "netLink")
            {
                json.Name("linkName"); json.String(route.LinkName);
                json.Name("linkScope"); json.String(route.LinkScope);
                json.Name("sourceKind"); json.String(route.SourceKind);
            }
            json.EndObject();
        }

        private static void WriteJoin(V1JsonWriter json, ElectricalJoin join)
        {
            json.BeginObject();
            Id(json, "id", join.Id);
            json.Name("members"); json.BeginArray();
            foreach (var member in join.Members) Endpoint(json, member);
            json.EndArray(); json.EndObject();
        }
    }
}
