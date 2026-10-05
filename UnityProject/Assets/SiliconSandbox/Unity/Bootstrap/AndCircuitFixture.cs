using System.Collections.Generic;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;
using UnityEngine;

namespace SiliconSandbox.Bootstrap
{
    // Interactive development fixture. Player placement and exact selectable routes follow in slice 2.
    public sealed class AndCircuitFixture : MonoBehaviour
    {
        public const string GateObjectName = "Fixture AND Gate";
        public const string AConnectorName = "Fixture A Connector";
        public const string BConnectorName = "Fixture B Connector";
        public const string YConnectorName = "Fixture Y Connector";

        private readonly List<Renderer> aRenderers = new List<Renderer>();
        private readonly List<Renderer> bRenderers = new List<Renderer>();
        private readonly List<Renderer> yRenderers = new List<Renderer>();
        private TextMesh aLabel;
        private TextMesh bLabel;
        private TextMesh yLabel;
        private bool built;
        private AndFixtureDesign authoredFixture;
        private OneBitAuthoredTopology topology;
        private BuiltOneBitCircuitPlan builtPlan;
        private List<OneBitComponent> components;
        private System.Guid hoveredConnectorId;
        private System.Guid selectedConnectorId;

        public GraphDrivenOneBitCircuit Circuit { get; private set; }
        public OneBitCircuitInspection Inspector { get; private set; }
        public OneBitAuthoredTopology AuthoredTopology => topology;
        public ResolvedBit A => PinValue(authoredFixture.Gate, "A");
        public ResolvedBit B => PinValue(authoredFixture.Gate, "B");
        public ResolvedBit Y => PinValue(authoredFixture.Gate, "Y");

        public void Build()
        {
            if (built) return;
            built = true;
            authoredFixture = AndFixtureDesign.Create();
            topology = authoredFixture.Topology;
            components = new List<OneBitComponent>
            {
                Component(authoredFixture.SourceA, "OUT"),
                Component(authoredFixture.SourceB, "OUT"),
                Component(authoredFixture.Gate, "A", "B", "Y")
            };
            builtPlan = OneBitCircuitPlanBuilder.Build(topology, components);
            Circuit = new GraphDrivenOneBitCircuit(builtPlan.Plan);
            Inspector = new OneBitCircuitInspection(topology, components, builtPlan, Circuit);

            CreateBody("Fixture Source A", new Vector3(10.5f, 1.5f, 14.5f), new Color(0.65f, 0.65f, 0.8f));
            CreateBody("Fixture Source B", new Vector3(10.5f, 1.5f, 17.5f), new Color(0.65f, 0.65f, 0.8f));
            CreateBody(GateObjectName, new Vector3(16.5f, 1.5f, 15.5f), new Color(0.85f, 0.7f, 0.3f));

            MakeRoute(AConnectorName, aRenderers, topology.Connectors[0]);
            MakeRoute(BConnectorName, bRenderers, topology.Connectors[1]);
            MakeRoute(YConnectorName, yRenderers, topology.Connectors[2]);

            aLabel = MakeLabel("Fixture A Label", new Vector3(10f, 2.4f, 14f));
            bLabel = MakeLabel("Fixture B Label", new Vector3(10f, 2.4f, 17f));
            yLabel = MakeLabel("Fixture Y Label", new Vector3(18f, 2.4f, 15.5f));
            var controls = MakeLabel("Fixture Controls", new Vector3(12f, 2.9f, 13f));
            controls.text = "1: cycle A   2: cycle B   Clock stopped";
            controls.characterSize = 0.18f;
            RefreshPresentation();
        }

        // Development-fixture control; changes configured source value and current On state.
        public void SetInputs(LogicBit a, LogicBit b)
        {
            Circuit.ConfigureSource(authoredFixture.SourceA.Id, a, true);
            Circuit.ConfigureSource(authoredFixture.SourceB.Id, b, true);
            Circuit.AdvanceToSettled();
            RefreshPresentation();
        }

        private void Update()
        {
            if (!built) return;
            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetInputs(Next(Circuit.Source(authoredFixture.SourceA.Id).Drive),
                    Circuit.Source(authoredFixture.SourceB.Id).Drive);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                SetInputs(Circuit.Source(authoredFixture.SourceA.Id).Drive,
                    Next(Circuit.Source(authoredFixture.SourceB.Id).Drive));
            var camera = Camera.main;
            hoveredConnectorId = System.Guid.Empty;
            if (camera != null && Physics.Raycast(camera.transform.position,
                    camera.transform.forward, out var hit, 15f))
            {
                var part = hit.collider.GetComponent<RoutePartIdentity>();
                if (part != null) hoveredConnectorId = part.ConnectorId;
            }
            if (Input.GetKeyDown(KeyCode.I) && hoveredConnectorId != System.Guid.Empty)
                selectedConnectorId = hoveredConnectorId;
            if (Input.GetKeyDown(KeyCode.Escape)) selectedConnectorId = System.Guid.Empty;
            if (A.Value == LogicBit.X || B.Value == LogicBit.X || Y.Value == LogicBit.X)
                RefreshPresentation();
        }

        private void OnGUI()
        {
            if (!built || Inspector == null) return;
            if (hoveredConnectorId != System.Guid.Empty)
            {
                var quick = Inspector.InspectConnector(hoveredConnectorId);
                GUI.Box(new Rect(12, 12, 390, 48),
                    "Wire  width " + quick.Width + "  value " + quick.Value.ToSymbol() +
                    "  connections " + quick.ConnectedPins.Count + "  tag " + quick.Tag);
            }
            if (selectedConnectorId == System.Guid.Empty) return;
            var detail = Inspector.InspectConnector(selectedConnectorId);
            var connections = new List<string>();
            foreach (var pin in detail.ConnectedPins)
                connections.Add(PinName(pin));
            GUI.Box(new Rect(12, 68, 500, 148),
                "Inspect one-bit wire: " + detail.Value.ToSymbol() + "\n" +
                "Connections: " + string.Join(", ", connections) + "\n" +
                "Active drivers: " + detail.ActiveDrivers.Count + "\n" +
                detail.Explanation);
        }

        private string PinName(JoinMember pin)
        {
            if (pin.Equals(JoinMember.ComponentPin(authoredFixture.SourceA.Id, authoredFixture.SourceA.Pin("OUT").PinId)))
                return "Source A.OUT";
            if (pin.Equals(JoinMember.ComponentPin(authoredFixture.SourceB.Id, authoredFixture.SourceB.Pin("OUT").PinId)))
                return "Source B.OUT";
            if (pin.Equals(JoinMember.ComponentPin(authoredFixture.Gate.Id, authoredFixture.Gate.Pin("A").PinId)))
                return "AND.A";
            if (pin.Equals(JoinMember.ComponentPin(authoredFixture.Gate.Id, authoredFixture.Gate.Pin("B").PinId)))
                return "AND.B";
            if (pin.Equals(JoinMember.ComponentPin(authoredFixture.Gate.Id, authoredFixture.Gate.Pin("Y").PinId)))
                return "AND.Y";
            return pin.OwnerId.ToString("D") + "." + pin.PartId.ToString("D");
        }

        private static LogicBit Next(LogicBit value)
        {
            switch (value)
            {
                case LogicBit.Zero: return LogicBit.One;
                case LogicBit.One: return LogicBit.X;
                case LogicBit.X: return LogicBit.Z;
                default: return LogicBit.Zero;
            }
        }

        private void RefreshPresentation()
        {
            aLabel.text = "A = " + A.Value.ToSymbol();
            bLabel.text = "B = " + B.Value.ToSymbol();
            yLabel.text = "Y = " + Y.Value.ToSymbol();
            SetSignalColor(aRenderers, A.Value);
            SetSignalColor(bRenderers, B.Value);
            SetSignalColor(yRenderers, Y.Value);
        }

        private ResolvedBit PinValue(FixtureComponent component, string key) =>
            Circuit.Net(builtPlan.NetIndex(JoinMember.ComponentPin(component.Id,
                component.Pin(key).PinId)));

        private static OneBitComponent Component(FixtureComponent fixture, params string[] keys)
        {
            var pins = new Dictionary<string, System.Guid>();
            foreach (var key in keys) pins.Add(key, fixture.Pin(key).PinId);
            return new OneBitComponent(fixture.Id, fixture.TypeId, fixture.TypeVersion, pins);
        }

        private static void SetSignalColor(IEnumerable<Renderer> renderers, LogicBit value)
        {
            Color color;
            switch (value)
            {
                case LogicBit.Zero: color = new Color(0.2f, 0.4f, 0.7f); break;
                case LogicBit.One: color = new Color(0.1f, 0.95f, 0.2f); break;
                case LogicBit.Z: color = Color.gray; break;
                default: color = Color.Lerp(new Color(0.3f, 0f, 0f), Color.red,
                    Mathf.PingPong(Time.unscaledTime * 2f, 1f)); break;
            }
            foreach (var renderer in renderers) renderer.material.color = color;
        }

        private static GameObject CreateBody(string name, Vector3 position, Color color)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = name;
            body.transform.position = position;
            body.GetComponent<Renderer>().material.color = color;
            return body;
        }

        private static void MakeRoute(string name, List<Renderer> renderers, ConnectorRoute route)
        {
            var root = new GameObject(name);
            var identityCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            identityCap.name = name + " Identity Cap";
            identityCap.transform.SetParent(root.transform);
            identityCap.transform.position = Position(route.Nodes[0]);
            identityCap.transform.localScale = Vector3.one * 0.17f;
            identityCap.GetComponent<Renderer>().material.color = new Color(0.8f, 0.8f, 0.75f);
            identityCap.AddComponent<RoutePartIdentity>().InitializeNode(route.Id, route.Nodes[0].Id);
            var nodes = new Dictionary<System.Guid, RouteNode>();
            foreach (var node in route.Nodes)
            {
                nodes.Add(node.Id, node);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = name + " Node " + node.Id.ToString("D");
                marker.transform.SetParent(root.transform);
                marker.transform.position = Position(node);
                marker.transform.localScale = Vector3.one * 0.25f;
                marker.AddComponent<RoutePartIdentity>().InitializeNode(route.Id, node.Id);
                renderers.Add(marker.GetComponent<Renderer>());
            }
            foreach (var span in route.Spans)
            {
                var from = Position(nodes[span.FromNodeId]);
                var to = Position(nodes[span.ToNodeId]);
                var xBend = new Vector3(to.x, from.y, from.z);
                var yBend = new Vector3(to.x, to.y, from.z);
                MakeSegment(name, route.Id, span.Id, root.transform, renderers, from, xBend);
                MakeSegment(name, route.Id, span.Id, root.transform, renderers, xBend, yBend);
                MakeSegment(name, route.Id, span.Id, root.transform, renderers, yBend, to);
            }
        }

        private static Vector3 Position(RouteNode node) =>
            new Vector3(node.Cell.X + node.PointQ.X * 0.25f,
                node.Cell.Y + node.PointQ.Y * 0.25f,
                node.Cell.Z + node.PointQ.Z * 0.25f);

        private static void MakeSegment(string name, System.Guid connectorId, System.Guid spanId,
            Transform parent, List<Renderer> renderers,
            Vector3 from, Vector3 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = name + " Segment " + renderers.Count;
            segment.transform.SetParent(parent);
            segment.transform.position = (from + to) / 2f;
            segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
            segment.transform.localScale = new Vector3(0.25f, delta.magnitude / 2f, 0.25f);
            segment.AddComponent<RoutePartIdentity>().InitializeSpan(connectorId, spanId);
            renderers.Add(segment.GetComponent<Renderer>());
        }

        private static TextMesh MakeLabel(string name, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.position = position;
            if (Camera.main != null)
                obj.transform.rotation = Quaternion.LookRotation(obj.transform.position - Camera.main.transform.position);
            var label = obj.AddComponent<TextMesh>();
            label.characterSize = 0.3f;
            label.fontSize = 48;
            label.color = Color.black;
            return label;
        }
    }
}
