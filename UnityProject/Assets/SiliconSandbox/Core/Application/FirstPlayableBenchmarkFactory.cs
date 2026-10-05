using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Application
{
    public sealed class FirstPlayableBenchmarkFixture
    {
        public OneBitWorldDesign World { get; }
        public OneBitModuleVersion Version { get; }
        public Guid FirstWorldGateId { get; }
        public Guid FirstModuleInstanceId { get; }

        internal FirstPlayableBenchmarkFixture(OneBitWorldDesign world,
            OneBitModuleVersion version, Guid firstWorldGateId,
            Guid firstModuleInstanceId)
        {
            World = world;
            Version = version;
            FirstWorldGateId = firstWorldGateId;
            FirstModuleInstanceId = firstModuleInstanceId;
        }
    }

    // The authored benchmark source. Once V1 persistence is connected, its
    // generated world is saved once and the resulting file hash is frozen.
    public static class FirstPlayableBenchmarkFactory
    {
        public static FirstPlayableBenchmarkFixture Create()
        {
            var components = new List<PlacedOneBitComponent>();
            var pins = new List<AuthoredPin>();
            var routes = new List<ConnectorRoute>();
            var joins = new List<ElectricalJoin>();
            Guid firstGateId = Guid.Empty;
            PlacedOneBitComponent firstGate = null;
            for (var i = 0; i < 500; i++)
            {
                var gate = Gate(new GridCell(2 + i % 50 * 2, 1,
                    2 + i / 50 * 3));
                if (i == 0) { firstGate = gate; firstGateId = gate.Id; }
                components.Add(gate);
                pins.AddRange(gate.BuildPins());
                AddClockAndOutputStubs(gate, true, routes, joins);
            }
            var source = Source(new GridCell(0, 1, 2));
            components.Add(source);
            pins.AddRange(source.BuildPins());
            AddSourceToGateB(source, firstGate, routes, joins);

            var version = FiftyAndVersion();
            var modules = new List<PlacedOneBitModuleInstance>();
            var worldPorts = new List<AuthoredModulePortBit>();
            Guid firstInstanceId = Guid.Empty;
            for (var i = 0; i < 10; i++)
            {
                var port = version.Ports[0];
                var instance = new PlacedOneBitModuleInstance(Guid.NewGuid(),
                    Guid.NewGuid(), version.FamilyId, version.VersionId,
                    "AND50_" + (i + 1), new GridCell(110, 1, 2 + i * 7),
                    GridOrientation.Default, version.SizeCells,
                    new[] { new OneBitPortInterface(port.Id, port.Name,
                        port.Direction, port.LocalCell, port.PointQ) });
                if (i == 0) firstInstanceId = instance.InstanceId;
                modules.Add(instance);
                var exterior = instance.BuildPortBits()[0];
                worldPorts.Add(exterior);
                AddStub(JoinMember.ModulePortBit(instance.Id, port.Id, 0),
                    exterior.Cell, exterior.PointQ, false, 3, true,
                    routes, joins);
            }
            var topology = new OneBitAuthoredTopology(pins, routes, joins,
                worldPorts);
            var world = new OneBitWorldDesign(new WorldBounds(128, 80, 6),
                components, topology, modules);
            return new FirstPlayableBenchmarkFixture(world, version,
                firstGateId, firstInstanceId);
        }

        private static OneBitModuleVersion FiftyAndVersion()
        {
            var components = new List<PlacedOneBitComponent>();
            var pins = new List<AuthoredPin>();
            var routes = new List<ConnectorRoute>();
            var joins = new List<ElectricalJoin>();
            PlacedOneBitComponent first = null;
            for (var i = 0; i < 50; i++)
            {
                var gate = Gate(new GridCell(i % 10, 0, i / 10));
                if (first == null) first = gate;
                components.Add(gate);
                pins.AddRange(gate.BuildPins());
                AddClockAndOutputStubs(gate, false, routes, joins);
            }
            var topology = new OneBitAuthoredTopology(pins, routes, joins);
            var port = new OneBitModulePort(Guid.NewGuid(), "Y",
                OneBitPortDirection.Output, new GridCell(9, 0, 0),
                new QuarterPoint(4, 1, 1),
                JoinMember.ComponentPin(first.Id, first.PinIds["Y"]));
            return new OneBitModuleVersion(Guid.NewGuid(), Guid.NewGuid(),
                "AND50 benchmark", new GridCell(10, 1, 5), components,
                topology, new[] { port });
        }

        private static PlacedOneBitComponent Gate(GridCell cell) =>
            new PlacedOneBitComponent(Guid.NewGuid(), BuiltInPinCatalog.And,
                1, cell, GridOrientation.Default, new Dictionary<string, Guid>
                {
                    ["A"] = Guid.NewGuid(), ["B"] = Guid.NewGuid(),
                    ["Y"] = Guid.NewGuid()
                });

        private static PlacedOneBitComponent Source(GridCell cell) =>
            new PlacedOneBitComponent(Guid.NewGuid(), BuiltInPinCatalog.Source,
                1, cell, GridOrientation.Default, new Dictionary<string, Guid>
                { ["OUT"] = Guid.NewGuid() },
                SiliconSandbox.Contracts.LogicBit.One, true);

        private static void AddClockAndOutputStubs(
            PlacedOneBitComponent gate, bool visibleSpan,
            List<ConnectorRoute> routes,
            List<ElectricalJoin> joins)
        {
            AddStub(JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]),
                gate.AnchorCell, new QuarterPoint(0, 1, 1), true, 2,
                visibleSpan,
                routes, joins);
            AddStub(JoinMember.ComponentPin(gate.Id, gate.PinIds["Y"]),
                gate.AnchorCell, new QuarterPoint(4, 1, 1), false, 3,
                visibleSpan,
                routes, joins);
        }

        private static void AddStub(JoinMember endpoint, GridCell cell,
            QuarterPoint point, bool clock, int channel,
            bool visibleSpan,
            List<ConnectorRoute> routes, List<ElectricalJoin> joins)
        {
            var node = new RouteNode(Guid.NewGuid(), cell, channel, point);
            var nodes = new List<RouteNode> { node };
            var spans = new List<RouteSpan>();
            if (visibleSpan)
            {
                var step = point.X == 0 ? -1 : 1;
                var end = new RouteNode(Guid.NewGuid(),
                    new GridCell(cell.X + step, cell.Y, cell.Z), channel,
                    new QuarterPoint(point.X == 0 ? 4 : 0,
                        point.Y, point.Z));
                nodes.Add(end);
                spans.Add(new RouteSpan(Guid.NewGuid(), node.Id, end.Id));
            }
            var route = new ConnectorRoute(Guid.NewGuid(),
                clock ? "netLink" : "wire", 1, nodes,
                spans, "", null,
                clock ? "@world-clock" : null,
                clock ? "world" : null,
                clock ? "worldClock" : null);
            routes.Add(route);
            joins.Add(new ElectricalJoin(Guid.NewGuid(), new[]
            {
                endpoint, JoinMember.ConnectorNode(route.Id, node.Id)
            }));
        }

        private static void AddSourceToGateB(
            PlacedOneBitComponent source, PlacedOneBitComponent gate,
            List<ConnectorRoute> routes, List<ElectricalJoin> joins)
        {
            var cell = new GridCell(1, 1, 2);
            var nodes = new[]
            {
                new RouteNode(Guid.NewGuid(), source.AnchorCell, 0,
                    new QuarterPoint(4, 1, 1)),
                new RouteNode(Guid.NewGuid(), cell, 0,
                    new QuarterPoint(0, 1, 1)),
                new RouteNode(Guid.NewGuid(), cell, 0,
                    new QuarterPoint(4, 3, 1)),
                new RouteNode(Guid.NewGuid(), gate.AnchorCell, 0,
                    new QuarterPoint(0, 3, 1))
            };
            var spans = new List<RouteSpan>();
            for (var i = 1; i < nodes.Length; i++)
                spans.Add(new RouteSpan(Guid.NewGuid(), nodes[i - 1].Id,
                    nodes[i].Id));
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                nodes, spans);
            routes.Add(route);
            joins.Add(new ElectricalJoin(Guid.NewGuid(), new[]
            {
                JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]),
                JoinMember.ConnectorNode(route.Id, nodes[0].Id)
            }));
            joins.Add(new ElectricalJoin(Guid.NewGuid(), new[]
            {
                JoinMember.ComponentPin(gate.Id, gate.PinIds["B"]),
                JoinMember.ConnectorNode(route.Id, nodes[3].Id)
            }));
        }
    }
}
