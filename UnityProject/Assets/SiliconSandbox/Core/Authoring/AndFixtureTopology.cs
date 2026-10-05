using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    // The development AND scene now has literal V1 route nodes, spans and joins.
    // New world edits will use the same record shapes instead of fixture pin lists.
    public static class AndFixtureTopology
    {
        public static OneBitAuthoredTopology Create(AndFixtureDesign design)
        {
            if (design == null) throw new ArgumentNullException(nameof(design));
            if (design.Connectors.Count != 3) throw new ArgumentException("AND fixture needs three routes.");
            var pins = new[]
            {
                Pin(design.SourceA.Pin("OUT"), 10, 1, 14, 4, 1, 1),
                Pin(design.SourceB.Pin("OUT"), 10, 1, 17, 4, 1, 1),
                Pin(design.Gate.Pin("A"), 16, 1, 15, 0, 1, 1),
                Pin(design.Gate.Pin("B"), 16, 1, 15, 0, 3, 1),
                Pin(design.Gate.Pin("Y"), 16, 1, 15, 4, 1, 1)
            };

            var a = Route(design.Connectors[0].Id, 0, new[]
            {
                P(11, 1, 14, 0, 1, 1), P(11, 1, 14, 4, 1, 1),
                P(12, 1, 14, 0, 1, 1), P(12, 1, 14, 4, 1, 1),
                P(13, 1, 14, 0, 1, 1), P(13, 1, 14, 4, 1, 1),
                P(14, 1, 14, 0, 1, 1), P(14, 1, 14, 1, 1, 4),
                P(14, 1, 15, 1, 1, 0), P(14, 1, 15, 4, 1, 1),
                P(15, 1, 15, 0, 1, 1), P(15, 1, 15, 4, 1, 1)
            });
            var b = Route(design.Connectors[1].Id, 1, new[]
            {
                P(11, 1, 17, 0, 1, 1), P(11, 1, 17, 4, 1, 1),
                P(12, 1, 17, 0, 1, 1), P(12, 1, 17, 4, 1, 1),
                P(13, 1, 17, 0, 1, 1), P(13, 1, 17, 1, 1, 0),
                P(13, 1, 16, 1, 1, 4), P(13, 1, 16, 1, 1, 0),
                P(13, 1, 15, 1, 1, 4), P(13, 1, 15, 4, 3, 1),
                P(14, 1, 15, 0, 3, 1), P(14, 1, 15, 4, 3, 1),
                P(15, 1, 15, 0, 3, 1), P(15, 1, 15, 4, 3, 1)
            });
            var y = Route(design.Connectors[2].Id, 2, new[]
            {
                P(17, 1, 15, 0, 1, 1), P(17, 1, 15, 4, 1, 1),
                P(18, 1, 15, 0, 1, 1), P(18, 1, 15, 4, 1, 1),
                P(19, 1, 15, 0, 1, 1), P(19, 1, 15, 4, 1, 1)
            });
            var joins = new List<ElectricalJoin>();
            Attach(design.Connectors[0], a, joins);
            Attach(design.Connectors[1], b, joins);
            Attach(design.Connectors[2], y, joins);
            return new OneBitAuthoredTopology(pins, new[] { a, b, y }, joins);
        }

        private static void Attach(FixtureConnector source, ConnectorRoute route,
            List<ElectricalJoin> joins)
        {
            for (var i = 0; i < source.AttachedPins.Count; i++)
            {
                var pin = source.AttachedPins[i];
                var node = route.Nodes[i == 0 ? 0 : route.Nodes.Count - 1];
                joins.Add(new ElectricalJoin(Guid.NewGuid(), new[]
                {
                    JoinMember.ConnectorNode(route.Id, node.Id),
                    JoinMember.ComponentPin(pin.ObjectId, pin.PinId)
                }));
            }
        }

        private static AuthoredPin Pin(FixturePinRef pin, int x, int y, int z,
            int qx, int qy, int qz) =>
            new AuthoredPin(pin.ObjectId, pin.PinId, new GridCell(x, y, z),
                new QuarterPoint(qx, qy, qz));

        private static (GridCell cell, QuarterPoint point) P(int x, int y, int z,
            int qx, int qy, int qz) =>
            (new GridCell(x, y, z), new QuarterPoint(qx, qy, qz));

        private static ConnectorRoute Route(Guid id, int channel,
            (GridCell cell, QuarterPoint point)[] points)
        {
            var nodes = new List<RouteNode>();
            var spans = new List<RouteSpan>();
            foreach (var point in points)
            {
                var node = new RouteNode(Guid.NewGuid(), point.cell, channel, point.point);
                if (nodes.Count > 0)
                    spans.Add(new RouteSpan(Guid.NewGuid(), nodes[nodes.Count - 1].Id, node.Id));
                nodes.Add(node);
            }
            return new ConnectorRoute(id, "wire", 1, nodes, spans);
        }
    }
}
