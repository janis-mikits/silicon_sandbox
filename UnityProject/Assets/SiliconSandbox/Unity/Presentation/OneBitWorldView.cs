using System;
using System.Collections.Generic;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using UnityEngine;

namespace SiliconSandbox.Presentation
{
    // Rebuildable presentation of one coherent authored revision. The view
    // never stores or derives electrical joins from renderer geometry.
    public sealed class OneBitWorldView : MonoBehaviour
    {
        private readonly Dictionary<Guid, List<Renderer>> routeBodies =
            new Dictionary<Guid, List<Renderer>>();
        private readonly Dictionary<Guid, JoinMember> routeFirstMembers =
            new Dictionary<Guid, JoinMember>();
        private readonly Dictionary<JoinMember, Renderer> pinRenderers =
            new Dictionary<JoinMember, Renderer>();
        private readonly MaterialPropertyBlock signalProperties =
            new MaterialPropertyBlock();
        private Transform generatedRoot;
        private ulong shownRevision = ulong.MaxValue;
        private OneBitWorldSession session;

        public OneBitWorldSession Session => session;

        public void Attach(OneBitWorldSession activeSession)
        {
            session = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
            shownRevision = ulong.MaxValue;
            RebuildIfNeeded();
            RefreshSignals();
        }

        private void Update()
        {
            if (session == null) return;
            RebuildIfNeeded();
            RefreshSignals();
        }

        private void RebuildIfNeeded()
        {
            if (shownRevision == session.Revision) return;
            if (generatedRoot != null)
            {
                generatedRoot.gameObject.SetActive(false);
                Destroy(generatedRoot.gameObject);
            }
            var root = new GameObject("Authored world revision " + session.Revision);
            root.transform.SetParent(transform, false);
            generatedRoot = root.transform;
            routeBodies.Clear();
            routeFirstMembers.Clear();
            pinRenderers.Clear();

            foreach (var component in session.Design.Components)
                DrawComponent(component);
            foreach (var module in session.Design.Modules)
                DrawModule(module);
            foreach (var route in session.Design.Topology.Connectors)
                DrawRoute(route);
            DrawJunctions();
            shownRevision = session.Revision;
        }

        private void DrawComponent(PlacedOneBitComponent component)
        {
            var body = Primitive("Component " + component.Id.ToString("D"),
                PrimitiveType.Cube, generatedRoot);
            body.transform.position = new Vector3(component.AnchorCell.X + 0.5f,
                component.AnchorCell.Y + 0.5f, component.AnchorCell.Z + 0.5f);
            body.transform.localScale = new Vector3(0.78f, 0.78f, 0.78f);
            body.GetComponent<Renderer>().material.color = ComponentColor(component.TypeId);
            body.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ComponentBody, component.Id, Guid.Empty);

            var label = new GameObject("Component label");
            label.transform.SetParent(body.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = component.TypeId == BuiltInPinCatalog.Source ? "SOURCE" :
                component.TypeId == BuiltInPinCatalog.And ? "AND" : "SR";
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.characterSize = 0.22f;
            mesh.fontSize = 48;
            mesh.color = Color.black;
            foreach (var pin in component.BuildPins())
            {
                var pinObject = Primitive("Pin " + pin.PinId.ToString("D"),
                    PrimitiveType.Sphere, generatedRoot);
                pinObject.transform.position = Position(pin.Cell, pin.PointQ);
                pinObject.transform.localScale = Vector3.one * 0.2f;
                pinObject.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ComponentPin, component.Id, pin.PinId);
                pinRenderers.Add(JoinMember.ComponentPin(component.Id, pin.PinId),
                    pinObject.GetComponent<Renderer>());
            }
        }

        private void DrawModule(PlacedOneBitModuleInstance module)
        {
            var cells = module.OccupiedCells();
            foreach (var cell in cells)
            {
                var body = Primitive("Module " + module.InstanceName,
                    PrimitiveType.Cube, generatedRoot);
                body.transform.position = new Vector3(cell.X + 0.5f,
                    cell.Y + 0.5f, cell.Z + 0.5f);
                body.transform.localScale = Vector3.one * 0.82f;
                body.GetComponent<Renderer>().material.color =
                    session.HasModuleVersion(module.VersionId)
                        ? new Color(0.34f, 0.58f, 0.72f)
                        : new Color(0.82f, 0.24f, 0.24f);
                body.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ModuleBody, module.Id, Guid.Empty);
            }
            if (cells.Count > 0)
            {
                var label = new GameObject("Module label " + module.InstanceName);
                label.transform.SetParent(generatedRoot, false);
                label.transform.position = new Vector3(cells[0].X + 0.5f,
                    cells[0].Y + 1.05f, cells[0].Z + 0.5f);
                var mesh = label.AddComponent<TextMesh>();
                mesh.text = session.HasModuleVersion(module.VersionId)
                    ? module.InstanceName : module.InstanceName + " MISSING";
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.characterSize = 0.22f;
                mesh.fontSize = 48;
                mesh.color = Color.black;
            }
            foreach (var port in module.BuildPortBits())
            {
                var marker = Primitive("Module port " + port.PortId.ToString("D"),
                    PrimitiveType.Sphere, generatedRoot);
                marker.transform.position = Position(port.Cell, port.PointQ);
                marker.transform.localScale = Vector3.one * 0.22f;
                marker.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ModulePort, module.Id, port.PortId);
                pinRenderers.Add(JoinMember.ModulePortBit(module.Id,
                    port.PortId, port.BitIndex), marker.GetComponent<Renderer>());
            }
        }

        private void DrawRoute(ConnectorRoute route)
        {
            var root = new GameObject("Connector " + route.Id.ToString("D"));
            root.transform.SetParent(generatedRoot, false);
            var renderers = new List<Renderer>();
            routeBodies.Add(route.Id, renderers);
            routeFirstMembers.Add(route.Id,
                JoinMember.ConnectorNode(route.Id, route.Nodes[0].Id));
            var nodes = new Dictionary<Guid, RouteNode>();
            foreach (var node in route.Nodes)
            {
                nodes.Add(node.Id, node);
                var marker = Primitive("Node " + node.Id.ToString("D"),
                    PrimitiveType.Sphere, root.transform);
                marker.transform.position = RoutePosition(node);
                marker.transform.localScale = Vector3.one * 0.16f;
                marker.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ConnectorNode, route.Id, node.Id);
                renderers.Add(marker.GetComponent<Renderer>());
            }
            foreach (var span in route.Spans)
                DrawSpan(root.transform, route.Id, span,
                    nodes[span.FromNodeId], nodes[span.ToNodeId], renderers);
            var cap = Primitive("Identity cap", PrimitiveType.Sphere, root.transform);
            cap.transform.position = RoutePosition(route.Nodes[0]) +
                Vector3.up * 0.15f;
            cap.transform.localScale = Vector3.one * 0.09f;
            cap.GetComponent<Renderer>().material.color = IdentityColor(route);
            cap.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ConnectorNode, route.Id, route.Nodes[0].Id);
        }

        private static void DrawSpan(Transform root, Guid connectorId, RouteSpan span,
            RouteNode fromNode, RouteNode toNode, List<Renderer> renderers)
        {
            var from = RoutePosition(fromNode);
            var to = RoutePosition(toNode);
            if ((to - from).sqrMagnitude < 0.000001f)
            {
                var bridge = Primitive("Face bridge " + span.Id.ToString("D"),
                    PrimitiveType.Sphere, root);
                bridge.transform.position = from;
                bridge.transform.localScale = Vector3.one * 0.26f;
                bridge.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ConnectorSpan, connectorId, span.Id);
                renderers.Add(bridge.GetComponent<Renderer>());
                return;
            }
            if (fromNode.Cell.Equals(toNode.Cell) &&
                (fromNode.PointQ.IsCenter && toNode.PointQ.IsFacePoint ||
                 toNode.PointQ.IsCenter && fromNode.PointQ.IsFacePoint))
            {
                var center = fromNode.PointQ.IsCenter ? from : to;
                var face = fromNode.PointQ.IsFacePoint ? from : to;
                var facePoint = fromNode.PointQ.IsFacePoint
                    ? fromNode.PointQ : toNode.PointQ;
                DrawCenterToFace(root, connectorId, span.Id,
                    center, face, facePoint, renderers);
                return;
            }
            var xBend = new Vector3(to.x, from.y, from.z);
            var yBend = new Vector3(to.x, to.y, from.z);
            DrawCylinder(root, connectorId, span.Id, from, xBend, renderers);
            DrawCylinder(root, connectorId, span.Id, xBend, yBend, renderers);
            DrawCylinder(root, connectorId, span.Id, yBend, to, renderers);
        }

        private static void DrawCenterToFace(Transform root, Guid connectorId,
            Guid spanId, Vector3 center, Vector3 face, QuarterPoint facePoint,
            List<Renderer> renderers)
        {
            Vector3 first;
            Vector3 second;
            if (facePoint.X == 0 || facePoint.X == 4)
            {
                first = new Vector3(face.x, center.y, center.z);
                second = new Vector3(face.x, face.y, center.z);
            }
            else if (facePoint.Y == 0 || facePoint.Y == 4)
            {
                first = new Vector3(center.x, face.y, center.z);
                second = new Vector3(face.x, face.y, center.z);
            }
            else
            {
                first = new Vector3(center.x, center.y, face.z);
                second = new Vector3(face.x, center.y, face.z);
            }
            DrawCylinder(root, connectorId, spanId, center, first, renderers);
            DrawCylinder(root, connectorId, spanId, first, second, renderers);
            DrawCylinder(root, connectorId, spanId, second, face, renderers);
        }

        private void DrawJunctions()
        {
            foreach (var junction in OneBitVisualTopology.Junctions(
                session.Design.Topology))
            {
                var marker = Primitive("Junction " + junction.NodeId.ToString("D"),
                    PrimitiveType.Sphere, generatedRoot);
                marker.transform.position = RoutePosition(junction.Cell,
                    junction.PointQ, junction.Channel);
                marker.transform.localScale = Vector3.one * 0.34f;
                marker.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ConnectorNode, junction.ConnectorId,
                    junction.NodeId);
                routeBodies[junction.ConnectorId].Add(marker.GetComponent<Renderer>());
            }
        }

        private static void DrawCylinder(Transform root, Guid connectorId,
            Guid spanId, Vector3 from, Vector3 to, List<Renderer> renderers)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            var cylinder = Primitive("Span " + spanId.ToString("D"),
                PrimitiveType.Cylinder, root);
            cylinder.transform.position = (from + to) * 0.5f;
            cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            cylinder.transform.localScale = new Vector3(0.12f, delta.magnitude * 0.5f, 0.12f);
            cylinder.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ConnectorSpan, connectorId, spanId);
            renderers.Add(cylinder.GetComponent<Renderer>());
        }

        private void RefreshSignals()
        {
            foreach (var pair in routeBodies)
            {
                var value = session.Circuit.Net(
                    session.Built.NetIndex(routeFirstMembers[pair.Key])).Value;
                var color = SignalColor(value);
                foreach (var renderer in pair.Value) SetSignalColor(renderer, color);
            }
            foreach (var pair in pinRenderers)
            {
                var value = session.Circuit.Net(session.Built.NetIndex(pair.Key)).Value;
                SetSignalColor(pair.Value, SignalColor(value));
            }
        }

        private void SetSignalColor(Renderer renderer, Color color)
        {
            signalProperties.Clear();
            signalProperties.SetColor("_Color", color);
            signalProperties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(signalProperties);
        }

        private static Color SignalColor(LogicBit value)
        {
            switch (value)
            {
                case LogicBit.Zero: return new Color(0.2f, 0.4f, 0.7f);
                case LogicBit.One: return new Color(0.1f, 0.95f, 0.2f);
                case LogicBit.Z: return Color.gray;
                default: return Color.Lerp(new Color(0.3f, 0f, 0f),
                    Color.red, Mathf.PingPong(Time.unscaledTime * 2f, 1f));
            }
        }

        private static Color ComponentColor(string typeId) =>
            typeId == BuiltInPinCatalog.Source ? new Color(0.65f, 0.65f, 0.8f) :
            typeId == BuiltInPinCatalog.And ? new Color(0.85f, 0.7f, 0.3f) :
            new Color(0.72f, 0.55f, 0.8f);

        private static Color IdentityColor(ConnectorRoute route)
        {
            if (string.IsNullOrEmpty(route.IdentityColor)) return new Color(0.8f, 0.8f, 0.75f);
            return ColorUtility.TryParseHtmlString(route.IdentityColor, out var color)
                ? color : new Color(0.8f, 0.8f, 0.75f);
        }

        private static Vector3 Position(GridCell cell, QuarterPoint point) =>
            new Vector3(cell.X + point.X * 0.25f,
                cell.Y + point.Y * 0.25f,
                cell.Z + point.Z * 0.25f);

        private static Vector3 RoutePosition(RouteNode node) =>
            RoutePosition(node.Cell, node.PointQ, node.Channel);

        private static Vector3 RoutePosition(GridCell cell, QuarterPoint point,
            int channel) => Position(cell, point) +
            Vector3.up * ((channel - 1.5f) * 0.06f);

        private static GameObject Primitive(string name, PrimitiveType type,
            Transform parent)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            return item;
        }
    }
}
