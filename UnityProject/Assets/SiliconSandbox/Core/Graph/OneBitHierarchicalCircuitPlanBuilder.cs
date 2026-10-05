using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Graph
{
    // Composes rebuildable net indexes across exact version/port mappings.
    // Authored IDs and instance paths remain the runtime state identities.
    public static class OneBitHierarchicalCircuitPlanBuilder
    {
        public static BuiltOneBitCircuitPlan Build(OneBitWorldDesign world,
            IReadOnlyDictionary<Guid, OneBitModuleVersion> versions)
        {
            if (world == null || versions == null) throw new ArgumentNullException();
            var worldComponents = new List<OneBitComponent>();
            foreach (var component in world.Components)
                worldComponents.Add(component.RuntimeDescriptor());
            var worldBuilt = OneBitCircuitPlanBuilder.Build(world.Topology,
                worldComponents);
            var moduleBuilt = new Dictionary<Guid, BuiltOneBitCircuitPlan>();
            foreach (var instance in world.Modules)
            {
                if (!versions.TryGetValue(instance.VersionId, out var version) ||
                    version.FamilyId != instance.FamilyId)
                    throw new ArgumentException("Exact placed module version is unavailable.");
                CheckInterface(instance, version);
                if (version.Topology.ModulePorts.Count != 0)
                    throw new NotSupportedException(
                        "Nested module execution is not available in this one-bit stage.");
                if (moduleBuilt.ContainsKey(version.VersionId)) continue;
                var descriptors = new List<OneBitComponent>();
                foreach (var component in version.Components)
                    descriptors.Add(component.RuntimeDescriptor());
                moduleBuilt.Add(version.VersionId,
                    OneBitCircuitPlanBuilder.Build(version.Topology, descriptors));
            }

            var offsetByInstance = new Dictionary<Guid, int>();
            var total = worldBuilt.Plan.NetCount;
            foreach (var instance in world.Modules)
            {
                offsetByInstance.Add(instance.InstanceId, total);
                total = checked(total + moduleBuilt[instance.VersionId].Plan.NetCount);
            }
            var union = new NetUnion(total);
            var chosenClock = worldBuilt.Plan.WorldClock;
            var chosenClockRaw = chosenClock.HasValue ?
                chosenClock.Value.OutputNet : -1;
            foreach (var instance in world.Modules)
            {
                var version = versions[instance.VersionId];
                var built = moduleBuilt[instance.VersionId];
                var offset = offsetByInstance[instance.InstanceId];
                foreach (var port in version.Ports)
                    union.Join(worldBuilt.NetIndex(JoinMember.ModulePortBit(
                        instance.Id, port.Id, 0)),
                        checked(offset + built.NetIndex(port.BitZeroTarget)));
                if (!built.Plan.WorldClock.HasValue) continue;
                var internalClock = checked(offset +
                    built.Plan.WorldClock.Value.OutputNet);
                if (chosenClockRaw >= 0) union.Join(chosenClockRaw, internalClock);
                else
                {
                    chosenClockRaw = internalClock;
                    chosenClock = built.Plan.WorldClock;
                }
            }
            var dense = new Dictionary<int, int>();
            for (var raw = 0; raw < total; raw++)
            {
                var root = union.Find(raw);
                if (!dense.ContainsKey(root)) dense.Add(root, dense.Count);
            }
            int Net(int raw) => dense[union.Find(raw)];

            var sources = new List<SourceBinding>();
            var gates = new List<AndBinding>();
            var storage = new List<SrBinding>();
            AddBindings(worldBuilt.Plan, 0, null);
            foreach (var instance in world.Modules)
                AddBindings(moduleBuilt[instance.VersionId].Plan,
                    offsetByInstance[instance.InstanceId], instance);
            WorldClockBinding? clockBinding = null;
            if (chosenClock.HasValue)
                clockBinding = new WorldClockBinding(chosenClock.Value.ConnectorId,
                    chosenClock.Value.AnchorNodeId, Net(chosenClockRaw));

            var plan = new OneBitCircuitPlan(dense.Count, sources, gates, storage,
                clockBinding);
            var worldIndexes = new Dictionary<JoinMember, int>();
            foreach (var net in worldBuilt.Graph.Nets)
                foreach (var member in net.Members)
                    worldIndexes.Add(member, Net(worldBuilt.NetIndex(member)));
            var internalIndexes = new Dictionary<(Guid, JoinMember), int>();
            foreach (var instance in world.Modules)
            {
                var built = moduleBuilt[instance.VersionId];
                var offset = offsetByInstance[instance.InstanceId];
                foreach (var net in built.Graph.Nets)
                    foreach (var member in net.Members)
                        internalIndexes.Add((instance.InstanceId, member),
                            Net(checked(offset + built.NetIndex(member))));
            }
            return new BuiltOneBitCircuitPlan(worldBuilt.Graph, plan,
                worldIndexes, internalIndexes);

            void AddBindings(OneBitCircuitPlan local, int offset,
                PlacedOneBitModuleInstance instance)
            {
                RuntimeObjectKey Key(Guid localId) => instance == null
                    ? RuntimeObjectKey.World(localId)
                    : RuntimeObjectKey.Module(instance.VersionId, localId,
                        new[] { instance.InstanceId });
                foreach (var source in local.Sources)
                    sources.Add(new SourceBinding(Key(source.ObjectId),
                        Net(checked(offset + source.OutputNet)), source.OnValue,
                        source.InitialOn));
                foreach (var gate in local.AndGates)
                    gates.Add(new AndBinding(Key(gate.ObjectId),
                        Net(checked(offset + gate.InputA)),
                        Net(checked(offset + gate.InputB)),
                        Net(checked(offset + gate.OutputY))));
                foreach (var sr in local.SrFlipFlops)
                    storage.Add(new SrBinding(Key(sr.ObjectId),
                        Net(checked(offset + sr.S)), Net(checked(offset + sr.R)),
                        Net(checked(offset + sr.Clock)),
                        Net(checked(offset + sr.Q)),
                        Net(checked(offset + sr.QBar)), sr.QDriverId,
                        sr.QBarDriverId, sr.InitialQ));
            }
        }

        private static void CheckInterface(PlacedOneBitModuleInstance instance,
            OneBitModuleVersion version)
        {
            if (!instance.SizeCells.Equals(version.SizeCells) ||
                instance.InterfacePorts.Count != version.Ports.Count)
                throw new ArgumentException("Placed interface disagrees with exact version.");
            var versionPorts = new Dictionary<Guid, OneBitModulePort>();
            foreach (var port in version.Ports) versionPorts.Add(port.Id, port);
            foreach (var snapshot in instance.InterfacePorts)
            {
                if (!versionPorts.TryGetValue(snapshot.Id, out var port) ||
                    snapshot.Name != port.Name ||
                    snapshot.Direction != port.Direction ||
                    !snapshot.LocalCell.Equals(port.LocalCell) ||
                    !snapshot.PointQ.Equals(port.PointQ))
                    throw new ArgumentException("Placed interface snapshot is incompatible.");
            }
        }

        private sealed class NetUnion
        {
            private readonly int[] parent;
            public NetUnion(int count)
            {
                parent = new int[count];
                for (var i = 0; i < count; i++) parent[i] = i;
            }
            public int Find(int item)
            {
                if (parent[item] != item) parent[item] = Find(parent[item]);
                return parent[item];
            }
            public void Join(int first, int second)
            {
                first = Find(first); second = Find(second);
                if (first != second) parent[second] = first;
            }
        }
    }
}
