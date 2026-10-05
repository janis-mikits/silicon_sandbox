using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Application
{
    // Inspection reads the same derived graph and settled execution plan as the
    // world. A connector's rendered location never creates an electrical member.
    public sealed class OneBitCircuitInspection
    {
        private readonly OneBitAuthoredTopology topology;
        private readonly IReadOnlyList<OneBitComponent> components;
        private readonly BuiltOneBitCircuitPlan built;
        private readonly GraphDrivenOneBitCircuit circuit;

        public OneBitCircuitInspection(OneBitAuthoredTopology topology,
            IReadOnlyList<OneBitComponent> components, BuiltOneBitCircuitPlan built,
            GraphDrivenOneBitCircuit circuit)
        {
            this.topology = topology ?? throw new ArgumentNullException(nameof(topology));
            this.components = components ?? throw new ArgumentNullException(nameof(components));
            this.built = built ?? throw new ArgumentNullException(nameof(built));
            this.circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
        }

        public OneBitInspection InspectConnector(Guid connectorId)
        {
            foreach (var route in topology.Connectors)
                if (route.Id == connectorId)
                    return Inspect(JoinMember.ConnectorNode(route.Id, route.Nodes[0].Id));
            throw new KeyNotFoundException("Connector is not in the authored design.");
        }

        public OneBitInspection InspectPin(Guid objectId, Guid pinId) =>
            Inspect(JoinMember.ComponentPin(objectId, pinId));

        public OneBitInspection InspectModulePort(Guid objectId, Guid portId) =>
            Inspect(JoinMember.ModulePortBit(objectId, portId, 0));

        public LogicBit InspectInternalPin(Guid instanceId, Guid componentId,
            Guid pinId) => circuit.Net(built.NetIndex(instanceId,
                JoinMember.ComponentPin(componentId, pinId))).Value;

        private OneBitInspection Inspect(JoinMember member)
        {
            var net = built.Graph.NetFor(member);
            var netIndex = built.NetIndex(member);
            var resolved = circuit.Net(netIndex);
            var pins = new List<JoinMember>();
            foreach (var item in net.Members)
                if (item.Kind == JoinTargetKind.ComponentPin ||
                    item.Kind == JoinTargetKind.ModulePortBit) pins.Add(item);
            var drivers = new List<InspectedDriver>();
            foreach (var component in components)
            {
                if (component.TypeId == BuiltInPinCatalog.Source)
                    AddDriver(component, "OUT", circuit.Source(component.Id).Drive);
                else if (component.TypeId == BuiltInPinCatalog.And)
                {
                    var a = circuit.Net(built.NetIndex(Pin(component, "A"))).Value;
                    var b = circuit.Net(built.NetIndex(Pin(component, "B"))).Value;
                    AddDriver(component, "Y", OneBitLogic.And(a, b));
                }
                else if (component.TypeId == BuiltInPinCatalog.SrFlipFlop)
                {
                    AddDriver(component, "Q", circuit.Storage(component.Id).Q);
                    AddDriver(component, "Q_bar", circuit.Storage(component.Id).QBar);
                }
            }
            foreach (var storage in built.Plan.SrFlipFlops)
            {
                if (storage.RuntimeKey.VersionId == Guid.Empty) continue;
                if (storage.Q == netIndex)
                    drivers.Add(new InspectedDriver(
                        JoinMember.ComponentPin(storage.ObjectId,
                            storage.QDriverId),
                        circuit.Storage(storage.RuntimeKey).Q,
                        storage.RuntimeKey));
                if (storage.QBar == netIndex)
                    drivers.Add(new InspectedDriver(
                        JoinMember.ComponentPin(storage.ObjectId,
                            storage.QBarDriverId),
                        circuit.Storage(storage.RuntimeKey).QBar,
                        storage.RuntimeKey));
            }
            foreach (var missing in built.Plan.MissingModuleOutputs)
                if (missing.OutputNet == netIndex)
                    drivers.Add(new InspectedDriver(JoinMember.ModulePortBit(
                        missing.InstanceObjectId, missing.PortId, 0), LogicBit.X));
            if (built.Plan.WorldClock.HasValue &&
                built.Plan.WorldClock.Value.OutputNet == netIndex)
            {
                var clock = built.Plan.WorldClock.Value;
                drivers.Add(new InspectedDriver(
                    JoinMember.ConnectorNode(clock.ConnectorId, clock.AnchorNodeId),
                    circuit.WorldClockLevel));
            }
            return new OneBitInspection(resolved, net.Tag, pins, net.ConnectorIds,
                drivers, Explain(netIndex, resolved));

            void AddDriver(OneBitComponent component, string key, LogicBit drive)
            {
                var pin = Pin(component, key);
                if (drive != LogicBit.Z && pins.Contains(pin))
                    drivers.Add(new InspectedDriver(pin, drive));
            }
        }

        private string Explain(int netIndex, ResolvedBit resolved)
        {
            foreach (var missing in built.Plan.MissingModuleOutputs)
                if (missing.OutputNet == netIndex)
                    return "Exact module definition is missing; its output drives X.";
            if (resolved.Cause == ResolutionCause.Undriven) return "No active driver.";
            if (resolved.Cause == ResolutionCause.ConflictingDrivers)
                return "Conflicting active drivers.";
            if (resolved.Cause != ResolutionCause.UnknownDriver) return "Known driven value.";
            foreach (var gate in built.Plan.AndGates)
                if (gate.OutputY == netIndex)
                {
                    var a = circuit.Net(gate.InputA).Value;
                    var b = circuit.Net(gate.InputB).Value;
                    if (a != LogicBit.Zero && b != LogicBit.Zero &&
                        (a == LogicBit.X || a == LogicBit.Z || b == LogicBit.X || b == LogicBit.Z))
                    {
                        var uncertain = new List<string>();
                        if (a == LogicBit.X || a == LogicBit.Z)
                            uncertain.Add("input A is " + a.ToSymbol());
                        if (b == LogicBit.X || b == LogicBit.Z)
                            uncertain.Add("input B is " + b.ToSymbol());
                        return "AND output is uncertain because " + string.Join(" and ", uncertain) + ".";
                    }
                }
            return "An active driver is uncertain.";
        }

        private static JoinMember Pin(OneBitComponent component, string key) =>
            JoinMember.ComponentPin(component.Id, component.Pin(key));
    }
}
