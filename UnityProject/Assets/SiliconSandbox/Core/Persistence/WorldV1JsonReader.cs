using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Persistence
{
    // Parses only the first-playable V1 subset. A later record variant must
    // receive its own versioned reader rather than being silently discarded.
    public static class WorldV1JsonReader
    {
        public static WorldSaveSnapshot ReadWorld(byte[] bytes,
            IReadOnlyDictionary<Guid, OneBitModuleVersion> versions)
        {
            if (versions == null) throw new ArgumentNullException(nameof(versions));
            var root = V1JsonReader.Object(V1JsonReader.Root(bytes),
                "worldId", "worldName", "mode", "worldSettings", "player",
                "inventory", "design");
            if (V1JsonReader.String(root["mode"]) != "freeplay")
                throw Invalid("Unsupported V1 world mode.");
            var settings = V1JsonReader.Object(root["worldSettings"],
                "widthCells", "lengthCells", "heightCells", "floorMaterialId",
                "floorThicknessCells", "wallStyleId", "worldClockFrequencyHz");
            if (V1JsonReader.Int32(settings["floorThicknessCells"]) != 1)
                throw Invalid("V1 floor thickness must be one cell.");
            var bounds = new WorldBounds(
                V1JsonReader.Int32(settings["widthCells"]),
                V1JsonReader.Int32(settings["lengthCells"]),
                V1JsonReader.Int32(settings["heightCells"]));
            var player = V1JsonReader.Object(root["player"],
                "position", "lookDirection");
            var position = Triple(player["position"]);
            var look = Triple(player["lookDirection"]);
            var pose = new SavedPlayerPose(position[0], position[1],
                position[2], look[0], look[1], look[2]);
            var inventory = V1JsonReader.Object(root["inventory"],
                "slots", "selectedHotbarSlot");
            var slots = ReadInventory(V1JsonReader.Array(inventory["slots"]));
            var records = ReadDesign(root["design"], true);
            var design = new OneBitWorldDesign(bounds, records.components,
                records.topology, records.modules);
            OneBitTopologyGraphBuilder.Build(design.Topology);
            var world = new WorldSaveSnapshot(
                V1JsonReader.Uuid(root["worldId"]),
                V1JsonReader.String(root["worldName"]),
                V1JsonReader.String(settings["floorMaterialId"]),
                V1JsonReader.String(settings["wallStyleId"]),
                V1JsonReader.String(settings["worldClockFrequencyHz"]),
                pose, slots, V1JsonReader.Int32(inventory["selectedHotbarSlot"]),
                design, versions);
            foreach (var instance in design.Modules)
                if (versions.TryGetValue(instance.VersionId, out var version))
                    ValidateInterface(instance, version);
            return world;
        }

        public static OneBitModuleVersion ReadModule(byte[] bytes)
        {
            var root = V1JsonReader.Object(V1JsonReader.Root(bytes),
                "familyId", "versionId", "name", "sizeCells",
                "exteriorSizeCells", "ports",
                "childVersionIds", "design");
            var size = Cell(root["sizeCells"]);
            var exteriorSize = Cell(root["exteriorSizeCells"]);
            var childIds = V1JsonReader.Array(root["childVersionIds"]);
            if (childIds.Count != 0)
                throw Invalid("Nested modules are not a V1 first-playable record.");
            if (size.X < 1 || size.Y < 1 || size.Z < 1 ||
                exteriorSize.X < 1 || exteriorSize.Y < 1 ||
                exteriorSize.Z < 1)
                throw Invalid("Module size must be positive.");
            var design = ReadDesign(root["design"], false);
            OneBitTopologyGraphBuilder.Build(design.topology);
            if (design.modules.Count != 0)
                throw Invalid("Nested modules are unsupported in this reader.");
            foreach (var component in design.components)
                if (component.AnchorCell.X < 0 || component.AnchorCell.Y < 0 ||
                    component.AnchorCell.Z < 0 || component.AnchorCell.X >= size.X ||
                    component.AnchorCell.Y >= size.Y || component.AnchorCell.Z >= size.Z)
                    throw Invalid("Module component leaves its local footprint.");
            var ports = new List<OneBitModulePort>();
            foreach (var token in V1JsonReader.Array(root["ports"]))
            {
                var item = V1JsonReader.Object(token, "id", "name", "direction",
                    "width", "bitOrder", "localCell", "pointQ", "bitTargets");
                RequireOneBitOrder(item);
                var mappings = V1JsonReader.Array(item["bitTargets"]);
                if (mappings.Count != 1)
                    throw Invalid("A V1 port needs exactly one bit target.");
                var mapping = V1JsonReader.Object(mappings[0],
                    "portBitIndex", "target");
                if (V1JsonReader.Int32(mapping["portBitIndex"]) != 0)
                    throw Invalid("V1 port bit index must be zero.");
                ports.Add(new OneBitModulePort(V1JsonReader.Uuid(item["id"]),
                    V1JsonReader.String(item["name"]),
                    Direction(item["direction"]), Cell(item["localCell"]),
                    Point(item["pointQ"]), Endpoint(mapping["target"])));
            }
            return new OneBitModuleVersion(V1JsonReader.Uuid(root["familyId"]),
                V1JsonReader.Uuid(root["versionId"]),
                V1JsonReader.String(root["name"]), size, exteriorSize,
                design.components, design.topology, ports);
        }

        private static (List<PlacedOneBitComponent> components,
            List<PlacedOneBitModuleInstance> modules,
            OneBitAuthoredTopology topology) ReadDesign(JToken token, bool world)
        {
            var design = V1JsonReader.Object(token, "objects", "connectors", "joins");
            var components = new List<PlacedOneBitComponent>();
            var modules = new List<PlacedOneBitModuleInstance>();
            foreach (var objectToken in V1JsonReader.Array(design["objects"]))
            {
                if (!(objectToken is JObject raw)) throw Invalid("Invalid V1 object.");
                var kind = V1JsonReader.String(raw["kind"]);
                if (kind == "component") components.Add(ReadComponent(raw));
                else if (kind == "moduleInstance" && world)
                    modules.Add(ReadModuleInstance(raw));
                else throw Invalid("Unsupported V1 object kind.");
            }
            var routes = new List<ConnectorRoute>();
            foreach (var route in V1JsonReader.Array(design["connectors"]))
                routes.Add(ReadConnector(route));
            var joins = new List<ElectricalJoin>();
            foreach (var tokenJoin in V1JsonReader.Array(design["joins"]))
            {
                var join = V1JsonReader.Object(tokenJoin, "id", "members");
                var members = new List<JoinMember>();
                foreach (var member in V1JsonReader.Array(join["members"]))
                    members.Add(Endpoint(member));
                joins.Add(new ElectricalJoin(V1JsonReader.Uuid(join["id"]), members));
            }
            var pins = new List<AuthoredPin>();
            foreach (var component in components) pins.AddRange(component.BuildPins());
            var portBits = new List<AuthoredModulePortBit>();
            foreach (var module in modules) portBits.AddRange(module.BuildPortBits());
            var topology = new OneBitAuthoredTopology(pins, routes, joins, portBits);
            return (components, modules, topology);
        }

        private static PlacedOneBitComponent ReadComponent(JObject raw)
        {
            var item = V1JsonReader.Object(raw, "kind", "id", "typeId",
                "typeVersion", "anchorCell", "orientation", "configuration",
                "pins", "appearance", "tag");
            var type = V1JsonReader.String(item["typeId"]);
            var version = V1JsonReader.Int32(item["typeVersion"]);
            var pinIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var token in V1JsonReader.Array(item["pins"]))
            {
                var pin = V1JsonReader.Object(token, "id", "pinKey", "direction",
                    "width", "cellOffset", "pointQ");
                var key = V1JsonReader.String(pin["pinKey"]);
                if (!pinIds.TryAdd(key, V1JsonReader.Uuid(pin["id"])) ||
                    V1JsonReader.Int32(pin["width"]) != 1 ||
                    !Cell(pin["cellOffset"]).Equals(new GridCell(0, 0, 0)))
                    throw Invalid("Invalid V1 pin identity or width.");
                var expected = FindPin(type, version, key);
                if (V1JsonReader.String(pin["direction"]) !=
                    (expected.Direction == PinDirection.Input ? "input" : "output") ||
                    !Point(pin["pointQ"]).Equals(new QuarterPoint(
                        expected.Qx, expected.Qy, expected.Qz)))
                    throw Invalid("Pin snapshot disagrees with built-in type version.");
            }
            var appearance = V1JsonReader.Object(item["appearance"], "styleId");
            if (V1JsonReader.String(appearance["styleId"]) !=
                "builtin.component.default")
                throw Invalid("Unknown V1 built-in component style.");
            var config = (JObject)item["configuration"];
            LogicBit onValue = LogicBit.One;
            bool initialOn = false;
            LogicBit? initialQ = null;
            if (type == BuiltInPinCatalog.Source)
            {
                V1JsonReader.Object(config, "width", "onValueBits", "initialOn");
                if (V1JsonReader.Int32(config["width"]) != 1)
                    throw Invalid("V1 source width must be one.");
                var bits = V1JsonReader.Array(config["onValueBits"]);
                if (bits.Count != 1) throw Invalid("V1 source needs one bit.");
                onValue = Bit(bits[0]);
                initialOn = V1JsonReader.Boolean(config["initialOn"]);
            }
            else if (type == BuiltInPinCatalog.And)
            {
                V1JsonReader.Object(config, "width");
                if (V1JsonReader.Int32(config["width"]) != 1)
                    throw Invalid("V1 AND width must be one.");
            }
            else if (type == BuiltInPinCatalog.SrFlipFlop)
            {
                V1JsonReader.Object(config, "initialQ");
                if (config["initialQ"].Type != JTokenType.Null)
                    initialQ = Bit(config["initialQ"]);
            }
            else throw Invalid("Unsupported component type.");
            return new PlacedOneBitComponent(V1JsonReader.Uuid(item["id"]),
                type, version, Cell(item["anchorCell"]),
                Orientation(item["orientation"]), pinIds,
                onValue, initialOn, initialQ, V1JsonReader.String(item["tag"]));
        }

        private static PinGeometry FindPin(string type, int version,
            string key)
        {
            foreach (var pin in BuiltInPinCatalog.Pins(type, version))
                if (pin.Key == key) return pin;
            throw Invalid("Unknown V1 pin key.");
        }

        private static PlacedOneBitModuleInstance ReadModuleInstance(JObject raw)
        {
            var item = V1JsonReader.Object(raw, "kind", "id", "instanceId",
                "familyId", "versionId", "instanceName", "anchorCell",
                "orientation", "interfaceSnapshot", "tag");
            var snapshot = V1JsonReader.Object(item["interfaceSnapshot"],
                "sizeCells", "ports");
            var ports = new List<OneBitPortInterface>();
            foreach (var token in V1JsonReader.Array(snapshot["ports"]))
            {
                var port = V1JsonReader.Object(token, "id", "name", "direction",
                    "width", "bitOrder", "localCell", "pointQ");
                RequireOneBitOrder(port);
                ports.Add(new OneBitPortInterface(V1JsonReader.Uuid(port["id"]),
                    V1JsonReader.String(port["name"]),
                    Direction(port["direction"]), Cell(port["localCell"]),
                    Point(port["pointQ"])));
            }
            return new PlacedOneBitModuleInstance(
                V1JsonReader.Uuid(item["id"]),
                V1JsonReader.Uuid(item["instanceId"]),
                V1JsonReader.Uuid(item["familyId"]),
                V1JsonReader.Uuid(item["versionId"]),
                V1JsonReader.String(item["instanceName"]),
                Cell(item["anchorCell"]), Orientation(item["orientation"]),
                Cell(snapshot["sizeCells"]), ports,
                V1JsonReader.String(item["tag"]));
        }

        private static ConnectorRoute ReadConnector(JToken token)
        {
            if (!(token is JObject raw)) throw Invalid("Invalid connector.");
            var geometryVersion = raw["geometryVersion"] == null ? 1 : V1JsonReader.Int32(raw["geometryVersion"]);
            if (geometryVersion != 1 && geometryVersion != 2) throw Invalid("Unsupported connector geometry version.");
            raw = (JObject)raw.DeepClone();
            raw.Remove("geometryVersion");
            var kind = V1JsonReader.String(raw["kind"]);
            var linked = kind == "netLink";
            var route = linked
                ? V1JsonReader.Object(raw, "id", "kind", "width", "nodes",
                    "spans", "tag", "identityColor", "linkName", "linkScope",
                    "sourceKind")
                : V1JsonReader.Object(raw, "id", "kind", "width", "nodes",
                    "spans", "tag", "identityColor");
            if (kind != "wire" && !linked ||
                V1JsonReader.Int32(route["width"]) != 1)
                throw Invalid("Unsupported V1 connector.");
            var nodes = new List<RouteNode>();
            foreach (var nodeToken in V1JsonReader.Array(route["nodes"]))
            {
                var node = V1JsonReader.Object(nodeToken, "id", "cell",
                    "channel", "pointQ");
                nodes.Add(new RouteNode(V1JsonReader.Uuid(node["id"]),
                    Cell(node["cell"]), V1JsonReader.Int32(node["channel"]),
                    Point(node["pointQ"])));
            }
            var spans = new List<RouteSpan>();
            foreach (var spanToken in V1JsonReader.Array(route["spans"]))
            {
                var span = V1JsonReader.Object(spanToken, "id",
                    "fromNodeId", "toNodeId");
                spans.Add(new RouteSpan(V1JsonReader.Uuid(span["id"]),
                    V1JsonReader.Uuid(span["fromNodeId"]),
                    V1JsonReader.Uuid(span["toNodeId"])));
            }
            var color = route["identityColor"].Type == JTokenType.Null
                ? null : V1JsonReader.String(route["identityColor"]);
            return new ConnectorRoute(V1JsonReader.Uuid(route["id"]), kind, 1,
                nodes, spans, V1JsonReader.String(route["tag"]), color,
                linked ? V1JsonReader.String(route["linkName"]) : null,
                linked ? V1JsonReader.String(route["linkScope"]) : null,
                linked ? V1JsonReader.String(route["sourceKind"]) : null, geometryVersion);
        }

        private static JoinMember Endpoint(JToken token)
        {
            if (!(token is JObject raw)) throw Invalid("Invalid typed endpoint.");
            var kind = V1JsonReader.String(raw["targetKind"]);
            if (kind == "connectorNode")
            {
                var item = V1JsonReader.Object(raw, "targetKind", "connectorId", "nodeId");
                return JoinMember.ConnectorNode(V1JsonReader.Uuid(item["connectorId"]),
                    V1JsonReader.Uuid(item["nodeId"]));
            }
            if (kind == "componentPin")
            {
                var item = V1JsonReader.Object(raw, "targetKind", "objectId", "pinId");
                return JoinMember.ComponentPin(V1JsonReader.Uuid(item["objectId"]),
                    V1JsonReader.Uuid(item["pinId"]));
            }
            if (kind == "modulePortBit")
            {
                var item = V1JsonReader.Object(raw, "targetKind", "objectId",
                    "portId", "bitIndex");
                if (V1JsonReader.Int32(item["bitIndex"]) != 0)
                    throw Invalid("V1 port bit index must be zero.");
                return JoinMember.ModulePortBit(V1JsonReader.Uuid(item["objectId"]),
                    V1JsonReader.Uuid(item["portId"]), 0);
            }
            throw Invalid("Unknown V1 endpoint kind.");
        }

        private static SavedInventoryItem[] ReadInventory(JArray array)
        {
            if (array.Count != 36) throw Invalid("V1 inventory needs 36 slots.");
            var slots = new SavedInventoryItem[36];
            for (var i = 0; i < slots.Length; i++)
            {
                if (array[i].Type == JTokenType.Null) continue;
                if (!(array[i] is JObject raw)) throw Invalid("Invalid inventory item.");
                var kind = V1JsonReader.String(raw["kind"]);
                if (kind == "catalogItem")
                {
                    var item = V1JsonReader.Object(raw, "kind", "itemTypeId");
                    slots[i] = SavedInventoryItem.Catalog(
                        V1JsonReader.String(item["itemTypeId"]));
                }
                else if (kind == "moduleVersion")
                {
                    var item = V1JsonReader.Object(raw, "kind", "familyId", "versionId");
                    slots[i] = SavedInventoryItem.Module(
                        V1JsonReader.Uuid(item["familyId"]),
                        V1JsonReader.Uuid(item["versionId"]));
                }
                else throw Invalid("Unknown V1 inventory item kind.");
            }
            return slots;
        }

        private static void ValidateInterface(PlacedOneBitModuleInstance instance,
            OneBitModuleVersion version)
        {
            if (instance.FamilyId != version.FamilyId ||
                !instance.SizeCells.Equals(version.ExteriorSizeCells) ||
                instance.InterfacePorts.Count != version.Ports.Count)
                throw Invalid("Placed interface disagrees with exact module version.");
            var byId = new Dictionary<Guid, OneBitModulePort>();
            foreach (var port in version.Ports) byId.Add(port.Id, port);
            foreach (var port in instance.InterfacePorts)
                if (!byId.TryGetValue(port.Id, out var expected) ||
                    port.Name != expected.Name ||
                    port.Direction != expected.Direction ||
                    !port.LocalCell.Equals(expected.LocalCell) ||
                    !port.PointQ.Equals(expected.PointQ))
                    throw Invalid("Placed port snapshot disagrees with exact version.");
        }

        private static void RequireOneBitOrder(JObject port)
        {
            if (V1JsonReader.Int32(port["width"]) != 1 ||
                V1JsonReader.String(port["bitOrder"]) != "lsb0")
                throw Invalid("V1 port must be width one with lsb0 bit order.");
        }

        private static OneBitPortDirection Direction(JToken token)
        {
            switch (V1JsonReader.String(token))
            {
                case "input": return OneBitPortDirection.Input;
                case "output": return OneBitPortDirection.Output;
                case "inout": return OneBitPortDirection.Inout;
                default: throw Invalid("Unknown V1 port direction.");
            }
        }

        private static LogicBit Bit(JToken token)
        {
            switch (V1JsonReader.String(token))
            {
                case "0": return LogicBit.Zero;
                case "1": return LogicBit.One;
                case "X": return LogicBit.X;
                case "Z": return LogicBit.Z;
                default: throw Invalid("Unknown V1 logic bit.");
            }
        }

        private static GridCell Cell(JToken token)
        {
            var values = V1JsonReader.Array(token);
            if (values.Count != 3) throw Invalid("Grid cell needs three integers.");
            return new GridCell(V1JsonReader.Int32(values[0]),
                V1JsonReader.Int32(values[1]), V1JsonReader.Int32(values[2]));
        }

        private static QuarterPoint Point(JToken token)
        {
            var values = V1JsonReader.Array(token);
            if (values.Count != 3) throw Invalid("Quarter point needs three integers.");
            return new QuarterPoint(V1JsonReader.Int32(values[0]),
                V1JsonReader.Int32(values[1]), V1JsonReader.Int32(values[2]));
        }

        private static double[] Triple(JToken token)
        {
            var values = V1JsonReader.Array(token);
            if (values.Count != 3) throw Invalid("Player vector needs three numbers.");
            return new[] { V1JsonReader.Real(values[0]),
                V1JsonReader.Real(values[1]), V1JsonReader.Real(values[2]) };
        }

        private static GridOrientation Orientation(JToken token)
        {
            var objectValue = V1JsonReader.Object(token, "forward", "up");
            return new GridOrientation(
                GridOrientation.ParseFace(V1JsonReader.String(objectValue["forward"])),
                GridOrientation.ParseFace(V1JsonReader.String(objectValue["up"])));
        }

        private static InvalidDataException Invalid(string message) =>
            new InvalidDataException(message);

    }
}
