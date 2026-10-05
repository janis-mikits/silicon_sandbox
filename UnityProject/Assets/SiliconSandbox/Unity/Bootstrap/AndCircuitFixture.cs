using System.Collections.Generic;
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

        public OneBitAndCircuit Circuit { get; private set; }

        public void Build()
        {
            if (built) return;
            built = true;
            var authoredFixture = AndFixtureDesign.Create();
            Circuit = new OneBitAndCircuit(AndFixtureGraphBuilder.Build(authoredFixture));

            CreateBody("Fixture Source A", new Vector3(10.5f, 1.5f, 14.5f), new Color(0.65f, 0.65f, 0.8f));
            CreateBody("Fixture Source B", new Vector3(10.5f, 1.5f, 17.5f), new Color(0.65f, 0.65f, 0.8f));
            CreateBody(GateObjectName, new Vector3(16.5f, 1.5f, 15.5f), new Color(0.85f, 0.7f, 0.3f));

            MakeRoute(AConnectorName, aRenderers,
                new Vector3(11f, 1.25f, 14.25f), new Vector3(14f, 1.25f, 14.25f),
                new Vector3(14f, 1.25f, 15.25f), new Vector3(16f, 1.25f, 15.25f));
            MakeRoute(BConnectorName, bRenderers,
                new Vector3(11f, 1.25f, 17.25f), new Vector3(13f, 1.25f, 17.25f),
                new Vector3(13f, 1.25f, 15.25f), new Vector3(13f, 1.75f, 15.25f),
                new Vector3(16f, 1.75f, 15.25f));
            MakeRoute(YConnectorName, yRenderers,
                new Vector3(17f, 1.25f, 15.25f), new Vector3(20f, 1.25f, 15.25f));

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
            Circuit.ConfigureA(a, true);
            Circuit.ConfigureB(b, true);
            Circuit.AdvanceToSettled();
            RefreshPresentation();
        }

        private void Update()
        {
            if (!built) return;
            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetInputs(Next(Circuit.SourceA.Drive), Circuit.SourceB.Drive);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                SetInputs(Circuit.SourceA.Drive, Next(Circuit.SourceB.Drive));
            if (Circuit.A.Value == LogicBit.X || Circuit.B.Value == LogicBit.X || Circuit.Y.Value == LogicBit.X)
                RefreshPresentation();
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
            aLabel.text = "A = " + Circuit.A.Value.ToSymbol();
            bLabel.text = "B = " + Circuit.B.Value.ToSymbol();
            yLabel.text = "Y = " + Circuit.Y.Value.ToSymbol();
            SetSignalColor(aRenderers, Circuit.A.Value);
            SetSignalColor(bRenderers, Circuit.B.Value);
            SetSignalColor(yRenderers, Circuit.Y.Value);
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

        private static void MakeRoute(string name, List<Renderer> renderers, params Vector3[] points)
        {
            var root = new GameObject(name);
            var identityCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            identityCap.name = name + " Identity Cap";
            identityCap.transform.SetParent(root.transform);
            identityCap.transform.position = points[0];
            identityCap.transform.localScale = Vector3.one * 0.17f;
            identityCap.GetComponent<Renderer>().material.color = new Color(0.8f, 0.8f, 0.75f);
            for (var i = 1; i < points.Length; i++)
            {
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                segment.name = name + " Segment " + i;
                segment.transform.SetParent(root.transform);
                var delta = points[i] - points[i - 1];
                segment.transform.position = (points[i] + points[i - 1]) / 2f;
                segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
                segment.transform.localScale = new Vector3(0.25f, delta.magnitude / 2f, 0.25f);
                renderers.Add(segment.GetComponent<Renderer>());
            }
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
