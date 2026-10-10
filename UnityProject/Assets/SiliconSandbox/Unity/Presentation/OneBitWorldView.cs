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
        public const int RenderRegionSizeCells = 16;
        private readonly Dictionary<Vector2Int, Transform> regionRoots =
            new Dictionary<Vector2Int, Transform>();
        private readonly Dictionary<Guid, List<Renderer>> routeBodies =
            new Dictionary<Guid, List<Renderer>>();
        private readonly Dictionary<Guid, JoinMember> routeFirstMembers =
            new Dictionary<Guid, JoinMember>();
        private readonly Dictionary<JoinMember, Renderer> pinRenderers =
            new Dictionary<JoinMember, Renderer>();
        private readonly Dictionary<Guid, MeshFilter> sourceValueFilters =
            new Dictionary<Guid, MeshFilter>();
        private readonly Dictionary<Guid, PlacedOneBitComponent> drawnComponents =
            new Dictionary<Guid, PlacedOneBitComponent>();
        private readonly Dictionary<Guid, PlacedOneBitModuleInstance> drawnModules =
            new Dictionary<Guid, PlacedOneBitModuleInstance>();
        private readonly Dictionary<Guid, bool> drawnModuleAvailability =
            new Dictionary<Guid, bool>();
        private readonly Dictionary<Guid, ConnectorRoute> drawnRoutes =
            new Dictionary<Guid, ConnectorRoute>();
        private readonly Dictionary<Guid, GameObject> componentRoots =
            new Dictionary<Guid, GameObject>();
        private readonly Dictionary<Guid, GameObject> moduleRoots =
            new Dictionary<Guid, GameObject>();
        private readonly Dictionary<Guid, GameObject> routeRoots =
            new Dictionary<Guid, GameObject>();
        private readonly Dictionary<JoinMember, JunctionGraphic> junctions =
            new Dictionary<JoinMember, JunctionGraphic>();
        private readonly Dictionary<int, List<Renderer>> signalRenderersByNet =
            new Dictionary<int, List<Renderer>>();
        private readonly Dictionary<int, LogicBit> shownSignalValues =
            new Dictionary<int, LogicBit>();
        private readonly Dictionary<Guid, Vector3> attachedNodePositions = new Dictionary<Guid, Vector3>();
        private readonly HashSet<Guid> changedAttachmentRoutes = new HashSet<Guid>();
        private MaterialPropertyBlock signalProperties;
        private Transform generatedRoot;
        private ulong shownRevision = ulong.MaxValue;
        private OneBitWorldSession session;
        private OneBitVisualArt art;

        private sealed class JunctionGraphic
        {
            public OneBitVisibleJunction Model;
            public GameObject Object;
            public Renderer Renderer;
        }

        public OneBitWorldSession Session => session;

        private void Awake()
        {
            signalProperties = new MaterialPropertyBlock();
            art = Resources.Load<OneBitVisualArt>("SiliconSandboxVisualArt");
            if (art == null || !art.IsComplete)
                throw new InvalidOperationException("First-playable visual art is missing.");
        }

        public void Attach(OneBitWorldSession activeSession)
        {
            session = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
            if (generatedRoot != null) Retire(generatedRoot.gameObject);
            generatedRoot = null;
            regionRoots.Clear();
            drawnComponents.Clear();
            drawnModules.Clear();
            drawnModuleAvailability.Clear();
            drawnRoutes.Clear();
            componentRoots.Clear();
            moduleRoots.Clear();
            routeRoots.Clear();
            junctions.Clear();
            routeBodies.Clear();
            routeFirstMembers.Clear();
            pinRenderers.Clear();
            sourceValueFilters.Clear();
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
            if (generatedRoot == null)
            {
                var root = new GameObject("Authored world graphics");
                root.transform.SetParent(transform, false);
                generatedRoot = root.transform;
            }
            ReconcileComponents();
            ReconcileModules();
            RefreshAttachmentPositions();
            ReconcileRoutes();
            ReconcileJunctions();
            BuildSignalGroups();
            shownRevision = session.Revision;
        }

        // Presentation-only partition: electrical joins and authored IDs stay
        // in the Core design. A route may extend beyond its owner's region.
        private Transform RegionRoot(GridCell cell)
        {
            var key = new Vector2Int(cell.X / RenderRegionSizeCells,
                cell.Z / RenderRegionSizeCells);
            if (regionRoots.TryGetValue(key, out var existing))
                return existing;
            var root = new GameObject("Render region " + key.x + "," + key.y);
            root.transform.SetParent(generatedRoot, false);
            regionRoots.Add(key, root.transform);
            return root.transform;
        }

        private void ReconcileComponents()
        {
            var next = new Dictionary<Guid, PlacedOneBitComponent>();
            foreach (var item in session.Design.Components) next.Add(item.Id, item);
            foreach (var pair in drawnComponents)
                if (!next.TryGetValue(pair.Key, out var current) ||
                    !ReferenceEquals(pair.Value, current))
                {
                    foreach (var pin in pair.Value.BuildPins())
                        pinRenderers.Remove(JoinMember.ComponentPin(
                            pair.Key, pin.PinId));
                    sourceValueFilters.Remove(pair.Key);
                    Retire(componentRoots[pair.Key]);
                    componentRoots.Remove(pair.Key);
                }
            foreach (var pair in next)
                if (!drawnComponents.TryGetValue(pair.Key, out var old) ||
                    !ReferenceEquals(old, pair.Value))
                {
                    var root = new GameObject("Component graphics " +
                        pair.Key.ToString("D"));
                    root.transform.SetParent(RegionRoot(pair.Value.AnchorCell), false);
                    componentRoots.Add(pair.Key, root);
                    DrawComponent(pair.Value, root.transform);
                }
            drawnComponents.Clear();
            foreach (var pair in next) drawnComponents.Add(pair.Key, pair.Value);
        }

        private void ReconcileModules()
        {
            var next = new Dictionary<Guid, PlacedOneBitModuleInstance>();
            foreach (var item in session.Design.Modules) next.Add(item.Id, item);
            foreach (var pair in drawnModules)
                if (!next.TryGetValue(pair.Key, out var current) ||
                    !ReferenceEquals(pair.Value, current) ||
                    drawnModuleAvailability[pair.Key] !=
                        session.HasModuleVersion(pair.Value.VersionId))
                {
                    foreach (var port in pair.Value.BuildPortBits())
                        pinRenderers.Remove(JoinMember.ModulePortBit(
                            pair.Key, port.PortId, port.BitIndex));
                    Retire(moduleRoots[pair.Key]);
                    moduleRoots.Remove(pair.Key);
                }
            foreach (var pair in next)
                if (!drawnModules.TryGetValue(pair.Key, out var old) ||
                    !ReferenceEquals(old, pair.Value) ||
                    !moduleRoots.ContainsKey(pair.Key))
                {
                    var root = new GameObject("Module graphics " +
                        pair.Key.ToString("D"));
                    root.transform.SetParent(RegionRoot(pair.Value.AnchorCell), false);
                    moduleRoots.Add(pair.Key, root);
                    DrawModule(pair.Value, root.transform);
                }
            drawnModules.Clear();
            drawnModuleAvailability.Clear();
            foreach (var pair in next)
            {
                drawnModules.Add(pair.Key, pair.Value);
                drawnModuleAvailability.Add(pair.Key,
                    session.HasModuleVersion(pair.Value.VersionId));
            }
        }

        private void ReconcileRoutes()
        {
            var next = new Dictionary<Guid, ConnectorRoute>();
            foreach (var item in session.Design.Topology.Connectors)
                next.Add(item.Id, item);
            foreach (var pair in drawnRoutes)
                if (!next.TryGetValue(pair.Key, out var current) ||
                    !ReferenceEquals(pair.Value, current) || changedAttachmentRoutes.Contains(pair.Key))
                {
                    Retire(routeRoots[pair.Key]);
                    routeRoots.Remove(pair.Key);
                    routeBodies.Remove(pair.Key);
                    routeFirstMembers.Remove(pair.Key);
                }
            foreach (var pair in next)
                if (!drawnRoutes.TryGetValue(pair.Key, out var old) ||
                    !ReferenceEquals(old, pair.Value) || changedAttachmentRoutes.Contains(pair.Key))
                    DrawRoute(pair.Value);
            drawnRoutes.Clear();
            foreach (var pair in next) drawnRoutes.Add(pair.Key, pair.Value);
        }

        private void ReconcileJunctions()
        {
            var next = new Dictionary<JoinMember, OneBitVisibleJunction>();
            foreach (var item in OneBitVisualTopology.Junctions(
                session.Design.Topology))
                next.Add(JoinMember.ConnectorNode(item.ConnectorId,
                    item.NodeId), item);
            var remove = new List<JoinMember>();
            foreach (var pair in junctions)
                if (!next.TryGetValue(pair.Key, out var current) ||
                    !SameJunction(pair.Value.Model, current) ||
                    changedAttachmentRoutes.Contains(pair.Value.Model.ConnectorId))
                {
                    if (routeBodies.TryGetValue(pair.Value.Model.ConnectorId,
                            out var bodies))
                        bodies.Remove(pair.Value.Renderer);
                    Retire(pair.Value.Object);
                    remove.Add(pair.Key);
                }
            foreach (var key in remove) junctions.Remove(key);
            foreach (var pair in next)
                if (!junctions.ContainsKey(pair.Key))
                {
                    var item = pair.Value;
                    var marker = Primitive("Junction " + item.NodeId.ToString("D"),
                        PrimitiveType.Sphere, RegionRoot(item.Cell));
                    marker.transform.position = attachedNodePositions.TryGetValue(item.NodeId, out var joinedPosition)
                        ? joinedPosition : RoutePosition(item.Cell, item.PointQ, item.Channel);
                    var core = WireMeshGeometry.JunctionCore(art.WireVariants[item.DirectionMask]);
                    marker.GetComponent<MeshFilter>().sharedMesh = core;
                    marker.AddComponent<OwnedWireMesh>().Mesh = core;
                    marker.GetComponent<Renderer>().sharedMaterial = art.TintMaterial;
                    marker.GetComponent<SphereCollider>().radius = 0.085f;
                    marker.AddComponent<WorldSelectablePart>().Initialize(
                        WorldPartKind.ConnectorNode, item.ConnectorId,
                        item.NodeId);
                    var renderer = marker.GetComponent<Renderer>();
                    routeBodies[item.ConnectorId].Add(renderer);
                    junctions.Add(pair.Key, new JunctionGraphic
                    {
                        Model = item, Object = marker, Renderer = renderer
                    });
                }
            foreach (var pair in junctions)
            {
                var bodies = routeBodies[pair.Value.Model.ConnectorId];
                if (!bodies.Contains(pair.Value.Renderer))
                    bodies.Add(pair.Value.Renderer);
            }
        }

        private static bool SameJunction(OneBitVisibleJunction a,
            OneBitVisibleJunction b) =>
            a.Cell.Equals(b.Cell) && a.PointQ.Equals(b.PointQ) &&
            a.Channel == b.Channel && a.DirectionMask == b.DirectionMask;

        private static void Retire(GameObject item)
        {
            item.SetActive(false);
            if (UnityEngine.Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        private void BuildSignalGroups()
        {
            signalRenderersByNet.Clear();
            shownSignalValues.Clear();
            foreach (var pair in routeBodies)
            {
                var net = session.Built.NetIndex(routeFirstMembers[pair.Key]);
                if (!signalRenderersByNet.TryGetValue(net, out var group))
                    signalRenderersByNet.Add(net,
                        group = new List<Renderer>());
                group.AddRange(pair.Value);
            }
            foreach (var pair in pinRenderers)
            {
                var net = session.Built.NetIndex(pair.Key);
                if (!signalRenderersByNet.TryGetValue(net, out var group))
                    signalRenderersByNet.Add(net,
                        group = new List<Renderer>());
                group.Add(pair.Value);
            }
        }

        private void DrawComponent(PlacedOneBitComponent component,
            Transform root)
        {
            var body = Primitive("Component " + component.Id.ToString("D"),
                PrimitiveType.Cube, root);
            body.transform.position = new Vector3(component.AnchorCell.X + 0.5f,
                component.AnchorCell.Y + 0.5f, component.AnchorCell.Z + 0.5f);
            body.transform.rotation = OrientationRotation(component.Orientation);
            body.GetComponent<MeshFilter>().sharedMesh =
                component.TypeId == BuiltInPinCatalog.Source ? art.SourceBody :
                component.TypeId == BuiltInPinCatalog.And ? art.AndBody :
                art.SrBody;
            body.GetComponent<Renderer>().sharedMaterial = art.AtlasMaterial;
            body.GetComponent<BoxCollider>().size = Vector3.one * 0.78f;
            body.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ComponentBody, component.Id, Guid.Empty);

            if (component.TypeId == BuiltInPinCatalog.Source)
            {
                var value = new GameObject("Source value " + component.Id.ToString("D"));
                value.transform.SetParent(body.transform, false);
                value.AddComponent<MeshFilter>().sharedMesh = art.SourceZero;
                value.AddComponent<MeshRenderer>().sharedMaterial = art.AtlasMaterial;
                sourceValueFilters.Add(component.Id, value.GetComponent<MeshFilter>());
            }
            foreach (var pin in component.BuildPins())
            {
                var pinObject = Primitive("Pin " + pin.PinId.ToString("D"),
                    PrimitiveType.Cylinder, root);
                pinObject.transform.position = Position(pin.Cell, pin.PointQ);
                pinObject.transform.rotation = Quaternion.FromToRotation(
                    Vector3.up, FaceNormal(pin.PointQ));
                SetPinShape(pinObject);
                pinObject.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ComponentPin, component.Id, pin.PinId);
                pinRenderers.Add(JoinMember.ComponentPin(component.Id, pin.PinId),
                    pinObject.GetComponent<Renderer>());
            }
        }

        private void DrawModule(PlacedOneBitModuleInstance module,
            Transform root)
        {
            var cells = module.OccupiedCells();
            foreach (var cell in cells)
            {
                var body = Primitive("Module " + module.InstanceName,
                    PrimitiveType.Cube, root);
                body.transform.position = new Vector3(cell.X + 0.5f,
                    cell.Y + 0.5f, cell.Z + 0.5f);
                body.transform.rotation = OrientationRotation(module.Orientation);
                body.GetComponent<MeshFilter>().sharedMesh = art.ModuleBody;
                body.GetComponent<Renderer>().sharedMaterial = art.AtlasMaterial;
                body.GetComponent<BoxCollider>().size = Vector3.one * 0.82f;
                if (!session.HasModuleVersion(module.VersionId))
                {
                    var missing = new MaterialPropertyBlock();
                    missing.SetColor("_Color", new Color(0.82f, 0.24f, 0.24f));
                    body.GetComponent<Renderer>().SetPropertyBlock(missing);
                }
                body.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ModuleBody, module.Id, Guid.Empty);
                if (cell.Equals(cells[0]))
                {
                    var label = new GameObject("Module surface name " +
                        module.InstanceName);
                    label.transform.SetParent(body.transform, false);
                    label.transform.localPosition = new Vector3(0f, 0.4106f, 0f);
                    label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    var text = label.AddComponent<TextMesh>();
                    text.text = session.HasModuleVersion(module.VersionId)
                        ? module.InstanceName : module.InstanceName + " MISSING";
                    text.anchor = TextAnchor.MiddleCenter;
                    text.alignment = TextAlignment.Center;
                    text.fontSize = 48;
                    text.characterSize = Mathf.Min(0.14f,
                        0.7f / Mathf.Max(1, text.text.Length));
                    text.color = session.HasModuleVersion(module.VersionId)
                        ? Color.white : new Color(1f, 0.25f, 0.25f);
                }
            }
            foreach (var port in module.BuildPortBits())
            {
                var marker = Primitive("Module port " + port.PortId.ToString("D"),
                    PrimitiveType.Cylinder, root);
                marker.transform.position = Position(port.Cell, port.PointQ);
                marker.transform.rotation = Quaternion.FromToRotation(
                    Vector3.up, FaceNormal(port.PointQ));
                SetPinShape(marker);
                marker.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ModulePort, module.Id, port.PortId);
                pinRenderers.Add(JoinMember.ModulePortBit(module.Id,
                    port.PortId, port.BitIndex), marker.GetComponent<Renderer>());
            }
        }

        private void DrawRoute(ConnectorRoute route)
        {
            var root = new GameObject("Connector " + route.Id.ToString("D"));
            root.transform.SetParent(RegionRoot(route.Nodes[0].Cell), false);
            routeRoots.Add(route.Id, root);
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
                marker.transform.localScale = Vector3.one * 0.08f;
                marker.AddComponent<WorldSelectablePart>().Initialize(
                    WorldPartKind.ConnectorNode, route.Id, node.Id);
                renderers.Add(marker.GetComponent<Renderer>());
            }
            foreach (var span in route.Spans)
                if (route.GeometryVersion == 2)
                    DrawExactSpan(root.transform, route, span, nodes, renderers);
                else DrawSpan(root.transform, route.Id, span,
                    nodes[span.FromNodeId], nodes[span.ToNodeId], renderers);
            var cap = Primitive("Identity ring", PrimitiveType.Cylinder, root.transform);
            var first = RoutePosition(route.Nodes[0]);
            var along = Vector3.up;
            foreach (var node in route.Nodes)
            {
                var candidate = RoutePosition(node) - first;
                if (candidate.sqrMagnitude < 0.000001f) continue;
                if (route.Nodes[0].PointQ.IsFacePoint)
                {
                    var normal = FaceNormal(route.Nodes[0].PointQ);
                    along = Vector3.Dot(candidate, normal) >= 0f ? normal : -normal;
                }
                else if (node.PointQ.IsFacePoint && node.Cell.Equals(route.Nodes[0].Cell))
                    along = FaceNormal(node.PointQ);
                else along = candidate.normalized;
                break;
            }
            cap.transform.position = first + along * 0.14f;
            cap.transform.rotation = Quaternion.FromToRotation(Vector3.up, along);
            cap.GetComponent<MeshFilter>().sharedMesh = art.IdentityRing;
            cap.GetComponent<Renderer>().sharedMaterial = art.TintMaterial;
            var ringCollider = cap.GetComponent<CapsuleCollider>();
            ringCollider.radius = 0.064f;
            ringCollider.height = 0.128f;
            var identity = new MaterialPropertyBlock();
            identity.SetColor("_Color", IdentityColor(route));
            identity.SetColor("_BaseColor", IdentityColor(route));
            cap.GetComponent<Renderer>().SetPropertyBlock(identity);
            cap.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ConnectorNode, route.Id, route.Nodes[0].Id);
        }

        private void DrawExactSpan(Transform root, ConnectorRoute route, RouteSpan span,
            Dictionary<Guid, RouteNode> nodes, List<Renderer> renderers)
        {
            var from=RoutePosition(nodes[span.FromNodeId]);
            var to=RoutePosition(nodes[span.ToNodeId]);
            if((to-from).sqrMagnitude<0.000001f)
            { DrawSpan(root,route.Id,span,nodes[span.FromNodeId],nodes[span.ToNodeId],renderers); return; }
            var heading=(to-from).normalized;
            var incoming=Continuation(span.FromNodeId,span.Id,-heading);
            var outgoing=Continuation(span.ToNodeId,span.Id,heading);
            var item=new GameObject("Exact span " + span.Id.ToString("D"));
            item.transform.SetParent(root,false);item.transform.position=from;
            var mesh=WireMeshGeometry.Path(new[]{Vector3.zero,to-from},-incoming,outgoing);
            item.AddComponent<MeshFilter>().sharedMesh=mesh;
            item.AddComponent<OwnedWireMesh>().Mesh=mesh;
            var renderer=item.AddComponent<MeshRenderer>();renderer.sharedMaterial=art.TintMaterial;
            item.AddComponent<MeshCollider>().sharedMesh=mesh;
            item.AddComponent<WorldSelectablePart>().Initialize(WorldPartKind.ConnectorSpan,route.Id,span.Id);
            renderers.Add(renderer);

            Vector3 Continuation(Guid nodeId,Guid excluded,Vector3 fallback)
            {
                var visited=new HashSet<Guid>();var pending=new Queue<Guid>();pending.Enqueue(nodeId);
                Vector3? result=null;
                while(pending.Count>0)
                {
                    var id=pending.Dequeue();if(!visited.Add(id))continue;
                    foreach(var edge in route.Spans)
                    {
                        if(edge.Id==excluded)continue;
                        var other=edge.FromNodeId==id ? edge.ToNodeId : edge.ToNodeId==id ? edge.FromNodeId : Guid.Empty;
                        if(other==Guid.Empty || visited.Contains(other))continue;
                        var delta=RoutePosition(nodes[other])-RoutePosition(nodes[id]);
                        if(delta.sqrMagnitude<0.000001f){pending.Enqueue(other);continue;}
                        if(result.HasValue)return fallback; // Branch uses the junction core.
                        result=delta.normalized;
                    }
                }
                return result??fallback;
            }
        }

        private void DrawSpan(Transform root, Guid connectorId, RouteSpan span,
            RouteNode fromNode, RouteNode toNode, List<Renderer> renderers)
        {
            var from = RoutePosition(fromNode);
            var to = RoutePosition(toNode);
            if ((to - from).sqrMagnitude < 0.000001f)
            {
                var bridge = Primitive("Face bridge " + span.Id.ToString("D"),
                    PrimitiveType.Sphere, root);
                bridge.transform.position = from;
                bridge.transform.localScale = Vector3.one * 0.125f;
                // Coincident authored face nodes need a hit target, not an enlarged visible bead.
                bridge.GetComponent<Renderer>().enabled = false;
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
            DrawPath(root, connectorId, span.Id,
                new[] { from, xBend, yBend, to }, renderers);
        }

        private void DrawCenterToFace(Transform root, Guid connectorId,
            Guid spanId, Vector3 center, Vector3 face, QuarterPoint facePoint,
            List<Renderer> renderers)
        {
            var normal = FaceNormal(facePoint);
            // Leave the junction in its authored face direction, then align
            // the quadrant before the final coaxial approach into the pin.
            var first = center + normal * 0.15625f;
            var lead = Mathf.Min(0.125f, Vector3.Dot(face - center, normal) * 0.25f);
            var second = face - normal * lead;
            DrawPath(root, connectorId, spanId,
                new[] { center, first, second, face }, renderers);
        }

        private void DrawPath(Transform root, Guid connectorId, Guid spanId,
            Vector3[] candidatePoints, List<Renderer> renderers)
        {
            var points = new List<Vector3>(candidatePoints.Length);
            foreach (var point in candidatePoints)
                if (points.Count == 0 ||
                    (point - points[points.Count - 1]).sqrMagnitude > 0.000001f)
                    points.Add(point);
            if (points.Count < 2) return;
            // Collapse collinear points to keep straight authored routes straight.
            for (var i = points.Count - 2; i > 0; i--)
                if (Vector3.Dot((points[i] - points[i - 1]).normalized,
                    (points[i + 1] - points[i]).normalized) > 0.9999f)
                    points.RemoveAt(i);
            if (points.Count == 2)
            {
                DrawCylinder(root, connectorId, spanId, points[0], points[1], renderers);
                return;
            }
            var item = new GameObject("Continuous span " + spanId.ToString("D"));
            item.transform.SetParent(root, false);
            var origin = points[0]; item.transform.position = origin;
            var local = new List<Vector3>();
            foreach (var point in points) local.Add(point - origin);
            var mesh = WireMeshGeometry.Path(local);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<OwnedWireMesh>().Mesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = art.TintMaterial;
            item.AddComponent<MeshCollider>().sharedMesh = mesh;
            item.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ConnectorSpan, connectorId, spanId);
            renderers.Add(renderer);
        }

        private void DrawCylinder(Transform root, Guid connectorId,
            Guid spanId, Vector3 from, Vector3 to, List<Renderer> renderers)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            var cylinder = Primitive("Span " + spanId.ToString("D"),
                PrimitiveType.Cylinder, root);
            cylinder.transform.position = (from + to) * 0.5f;
            cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            cylinder.transform.localScale = new Vector3(1f, delta.magnitude, 1f);
            cylinder.GetComponent<MeshFilter>().sharedMesh = art.WireStraight;
            cylinder.GetComponent<Renderer>().sharedMaterial = art.TintMaterial;
            var collider = cylinder.GetComponent<CapsuleCollider>();
            collider.radius = 0.0625f;
            collider.height = 1f;
            cylinder.AddComponent<WorldSelectablePart>().Initialize(
                WorldPartKind.ConnectorSpan, connectorId, spanId);
            renderers.Add(cylinder.GetComponent<Renderer>());
        }

        private void RefreshSignals()
        {
            // The marking belongs to the source, not the resolved net. Two
            // opposing drivers show their own values while their wire shows X.
            foreach (var pair in sourceValueFilters)
            {
                var wanted = art.SourceValue(session.Circuit.Source(pair.Key).Drive);
                if (pair.Value.sharedMesh != wanted) pair.Value.sharedMesh = wanted;
            }
            foreach (var pair in signalRenderersByNet)
            {
                var value = session.Circuit.Net(pair.Key).Value;
                if (value != LogicBit.X &&
                    shownSignalValues.TryGetValue(pair.Key, out var shown) &&
                    shown == value)
                    continue;
                shownSignalValues[pair.Key] = value;
                var color = SignalColor(value);
                foreach (var renderer in pair.Value)
                    if (value != LogicBit.X || renderer.isVisible)
                        SetSignalColor(renderer, color);
            }
        }

        private void SetSignalColor(Renderer renderer, Color color)
        {
            var selectable = renderer.GetComponent<WorldSelectablePart>();
            if (selectable != null)
            {
                selectable.SetSignalColor(color);
                return;
            }
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

        private static Color IdentityColor(ConnectorRoute route)
        {
            if (string.IsNullOrEmpty(route.IdentityColor)) return new Color(0.8f, 0.8f, 0.75f);
            return ColorUtility.TryParseHtmlString(route.IdentityColor, out var color)
                ? color : new Color(0.8f, 0.8f, 0.75f);
        }

        private void SetPinShape(GameObject pin)
        {
            pin.GetComponent<MeshFilter>().sharedMesh = art.Pin;
            pin.GetComponent<Renderer>().sharedMaterial = art.TintMaterial;
            var collider = pin.GetComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, -0.03875f, 0f);
            collider.radius = 0.06875f;
            collider.height = 0.2025f;
        }

        private static Quaternion OrientationRotation(GridOrientation orientation) =>
            Quaternion.LookRotation(Direction(orientation.Forward),
                Direction(orientation.Up));

        private static Vector3 Direction(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North: return Vector3.forward;
                case GridDirection.South: return Vector3.back;
                case GridDirection.East: return Vector3.right;
                case GridDirection.West: return Vector3.left;
                case GridDirection.Up: return Vector3.up;
                case GridDirection.Down: return Vector3.down;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static Vector3 FaceNormal(QuarterPoint point)
        {
            if (point.X == 0) return Vector3.left;
            if (point.X == 4) return Vector3.right;
            if (point.Y == 0) return Vector3.down;
            if (point.Y == 4) return Vector3.up;
            if (point.Z == 0) return Vector3.back;
            if (point.Z == 4) return Vector3.forward;
            throw new ArgumentException("Pin has no exterior face.", nameof(point));
        }

        private static Vector3 Position(GridCell cell, QuarterPoint point) =>
            new Vector3(cell.X + point.X * 0.25f,
                cell.Y + point.Y * 0.25f,
                cell.Z + point.Z * 0.25f);

        private void RefreshAttachmentPositions()
        {
            var next = new Dictionary<Guid, Vector3>();
            var visualNodes = new Dictionary<Guid, Vector3>();
            foreach (var route in session.Design.Topology.Connectors)
                foreach (var node in route.Nodes)
                {
                    visualNodes[node.Id] = WireMeshGeometry.NodePosition(node.Cell,node.PointQ,node.Channel,route.GeometryVersion);
                    if(route.GeometryVersion == 2)next[node.Id]=visualNodes[node.Id];
                }
            foreach (var join in session.Design.Topology.Joins)
            {
                Renderer pin = null;
                foreach (var member in join.Members)
                    if (pinRenderers.TryGetValue(member, out pin)) break;
                Vector3? position = pin == null ? (Vector3?)null : pin.transform.position;
                if (!position.HasValue)
                    foreach (var member in join.Members)
                        if (member.Kind == JoinTargetKind.ConnectorNode && next.TryGetValue(member.PartId,out var exact))
                        { position=exact; break; }
                if (!position.HasValue)
                    foreach (var member in join.Members)
                        if (member.Kind == JoinTargetKind.ConnectorNode)
                        { position = visualNodes[member.PartId]; break; }
                if (!position.HasValue) continue;
                foreach (var member in join.Members)
                    if (member.Kind == JoinTargetKind.ConnectorNode)
                        next[member.PartId] = position.Value;
            }
            // Both sides of an authored zero-length face bridge share the
            // attachment position; never propagate to an unrelated crossing.
            bool changed;
            do
            {
                changed = false;
                foreach (var route in session.Design.Topology.Connectors)
                {
                    var nodes = new Dictionary<Guid, RouteNode>();
                    foreach (var node in route.Nodes) nodes[node.Id] = node;
                    foreach (var span in route.Spans)
                    {
                        var a = nodes[span.FromNodeId]; var b = nodes[span.ToNodeId];
                        if (Position(a.Cell, a.PointQ) != Position(b.Cell, b.PointQ)) continue;
                        if (next.TryGetValue(a.Id, out var ap) && !next.ContainsKey(b.Id))
                        { next[b.Id] = ap; changed = true; }
                        else if (next.TryGetValue(b.Id, out var bp) && !next.ContainsKey(a.Id))
                        { next[a.Id] = bp; changed = true; }
                    }
                }
            } while (changed);
            changedAttachmentRoutes.Clear();
            foreach (var route in session.Design.Topology.Connectors)
                foreach (var node in route.Nodes)
                {
                    var before = attachedNodePositions.TryGetValue(node.Id, out var a);
                    var after = next.TryGetValue(node.Id, out var b);
                    if (before != after || before && a != b)
                        changedAttachmentRoutes.Add(route.Id);
                }
            attachedNodePositions.Clear();
            foreach (var pair in next) attachedNodePositions.Add(pair.Key, pair.Value);
        }

        private Vector3 RoutePosition(RouteNode node) =>
            attachedNodePositions.TryGetValue(node.Id, out var exact) ? exact :
            RoutePosition(node.Cell, node.PointQ, node.Channel);

        private static Vector3 RoutePosition(GridCell cell, QuarterPoint point,
            int channel) => WireMeshGeometry.NodePosition(cell, point, channel);

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
