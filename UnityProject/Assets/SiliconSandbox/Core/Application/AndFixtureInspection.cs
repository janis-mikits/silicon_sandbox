using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Application
{
    public readonly struct InspectedDriver
    {
        public JoinMember Pin { get; }
        public LogicBit Value { get; }

        public InspectedDriver(JoinMember pin, LogicBit value) { Pin = pin; Value = value; }
    }

    public sealed class OneBitInspection
    {
        public int Width => 1;
        public LogicBit Value { get; }
        public ResolutionCause ResolutionCause { get; }
        public string Tag { get; }
        public IReadOnlyList<JoinMember> ConnectedPins { get; }
        public IReadOnlyList<Guid> ConnectorIds { get; }
        public IReadOnlyList<InspectedDriver> ActiveDrivers { get; }
        public string Explanation { get; }

        internal OneBitInspection(ResolvedBit resolved, string tag, List<JoinMember> pins,
            IReadOnlyList<Guid> connectorIds, List<InspectedDriver> drivers, string explanation)
        {
            Value = resolved.Value;
            ResolutionCause = resolved.Cause;
            Tag = tag;
            ConnectedPins = pins.AsReadOnly();
            ConnectorIds = connectorIds;
            ActiveDrivers = drivers.AsReadOnly();
            Explanation = explanation;
        }
    }

    // Read-only fixture bridge. Inspection follows the authored graph's membership,
    // then overlays settled simulator values; renderer geometry supplies no links.
    public sealed class AndFixtureInspection
    {
        private readonly AndFixtureDesign design;
        private readonly OneBitAndCircuit circuit;
        private readonly OneBitTopologyGraph graph;

        public AndFixtureInspection(AndFixtureDesign design, OneBitAndCircuit circuit)
        {
            this.design = design ?? throw new ArgumentNullException(nameof(design));
            this.circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
            graph = OneBitTopologyGraphBuilder.Build(design.Topology);
        }

        public OneBitInspection InspectConnector(Guid connectorId)
        {
            foreach (var route in design.Topology.Connectors)
                if (route.Id == connectorId)
                    return Inspect(graph.NetFor(JoinMember.ConnectorNode(route.Id, route.Nodes[0].Id)));
            throw new KeyNotFoundException("Connector is not in the authored design.");
        }

        public OneBitInspection InspectPin(Guid objectId, Guid pinId) =>
            Inspect(graph.NetFor(JoinMember.ComponentPin(objectId, pinId)));

        private OneBitInspection Inspect(DerivedOneBitNet net)
        {
            var pins = new List<JoinMember>();
            foreach (var member in net.Members)
                if (member.Kind == JoinTargetKind.ComponentPin) pins.Add(member);
            var drivers = new List<InspectedDriver>();
            AddDriver(design.SourceA.Pin("OUT"), circuit.SourceA.Drive);
            AddDriver(design.SourceB.Pin("OUT"), circuit.SourceB.Drive);
            AddDriver(design.Gate.Pin("Y"), circuit.Y.Value);

            ResolvedBit resolved;
            string explanation;
            if (ContainsConnector(net, circuit.ANetConnectorId))
            {
                resolved = circuit.A;
                explanation = ExplainResolution(resolved);
            }
            else if (ContainsConnector(net, circuit.BNetConnectorId))
            {
                resolved = circuit.B;
                explanation = ExplainResolution(resolved);
            }
            else if (ContainsConnector(net, circuit.YNetConnectorId))
            {
                resolved = circuit.Y;
                explanation = circuit.Y.Value == LogicBit.X &&
                    circuit.A.Value != LogicBit.Zero && circuit.B.Value != LogicBit.Zero
                    ? "AND output is uncertain because " + UncertainInputs() + "."
                    : ExplainResolution(resolved);
            }
            else
                throw new ArgumentException("Fixture net has no known simulated connector.");
            return new OneBitInspection(resolved, net.Tag, pins, net.ConnectorIds,
                drivers, explanation);

            void AddDriver(FixturePinRef pin, LogicBit value)
            {
                var reference = JoinMember.ComponentPin(pin.ObjectId, pin.PinId);
                if (value != LogicBit.Z && pins.Contains(reference))
                    drivers.Add(new InspectedDriver(reference, value));
            }
        }

        private string UncertainInputs()
        {
            var uncertain = new List<string>();
            if (circuit.A.Value == LogicBit.X || circuit.A.Value == LogicBit.Z)
                uncertain.Add("input A is " + circuit.A.Value.ToSymbol());
            if (circuit.B.Value == LogicBit.X || circuit.B.Value == LogicBit.Z)
                uncertain.Add("input B is " + circuit.B.Value.ToSymbol());
            return string.Join(" and ", uncertain);
        }

        private static string ExplainResolution(ResolvedBit resolved)
        {
            switch (resolved.Cause)
            {
                case ResolutionCause.Undriven: return "No active driver.";
                case ResolutionCause.ConflictingDrivers: return "Conflicting active drivers.";
                case ResolutionCause.UnknownDriver: return "An active driver is uncertain.";
                default: return "Known driven value.";
            }
        }

        private static bool ContainsConnector(DerivedOneBitNet net, Guid id)
        {
            foreach (var candidate in net.ConnectorIds)
                if (candidate == id) return true;
            return false;
        }
    }
}
