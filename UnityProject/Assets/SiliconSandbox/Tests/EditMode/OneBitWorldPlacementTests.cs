using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitWorldPlacementTests
    {
        private static readonly WorldBounds Bounds = new WorldBounds(20, 20, 10);

        [Test]
        public void PlaceSourceAndAndGateCreatesStableVersionedPinsAndDisconnectedZInputs()
        {
            var empty = OneBitWorldDesign.Empty(Bounds);
            var withSource = OneBitWorldEdits.PlaceComponent(empty,
                BuiltInPinCatalog.Source, new GridCell(5, 1, 5), GridOrientation.Default,
                LogicBit.Z, true);
            var source = withSource.Components[0];
            Assert.That(empty.Components.Count, Is.EqualTo(0), "Old authored revision is unchanged.");
            Assert.That(source.TypeVersion, Is.EqualTo(1));
            Assert.That(source.SourceOnValue, Is.EqualTo(LogicBit.Z));
            Assert.That(source.SourceInitialOn, Is.True);
            Assert.That(withSource.Topology.Pins[0].PointQ,
                Is.EqualTo(new QuarterPoint(4, 1, 1)));
            Assert.That(withSource.Topology.Pins[0].Cell,
                Is.EqualTo(new GridCell(5, 1, 5)));

            var withGate = OneBitWorldEdits.PlaceComponent(withSource,
                BuiltInPinCatalog.And, new GridCell(8, 1, 5),
                GridOrientation.Default.ClockwiseYaw());
            Assert.That(withGate.Components.Count, Is.EqualTo(2));
            Assert.That(withGate.Topology.Pins.Count, Is.EqualTo(4));
            Assert.That(withGate.Topology.Pins[1].PointQ,
                Is.EqualTo(new QuarterPoint(1, 1, 4)),
                "Yawed local west input A faces world north.");
            var descriptors = new[]
            {
                withGate.Components[0].RuntimeDescriptor(),
                withGate.Components[1].RuntimeDescriptor()
            };
            var built = OneBitCircuitPlanBuilder.Build(withGate.Topology, descriptors);
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            var gate = withGate.Components[1];
            var sourceNet = built.NetIndex(JoinMember.ComponentPin(source.Id,
                source.PinIds["OUT"]));
            var a = built.NetIndex(JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]));
            var b = built.NetIndex(JoinMember.ComponentPin(gate.Id, gate.PinIds["B"]));
            var y = built.NetIndex(JoinMember.ComponentPin(gate.Id, gate.PinIds["Y"]));
            Assert.That(circuit.Source(source.Id).IsOn, Is.True);
            Assert.That(circuit.Net(sourceNet).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.Net(a).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.Net(b).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.Net(y).Value, Is.EqualTo(LogicBit.X));
        }

        [Test]
        public void OccupiedFloorOutsideWorldAndWireCellRejectWithoutPublishing()
        {
            var empty = OneBitWorldDesign.Empty(Bounds);
            var first = OneBitWorldEdits.PlaceComponent(empty,
                BuiltInPinCatalog.Source, new GridCell(2, 1, 2), GridOrientation.Default);
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceComponent(first,
                BuiltInPinCatalog.And, new GridCell(2, 1, 2), GridOrientation.Default));
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceComponent(first,
                BuiltInPinCatalog.And, new GridCell(3, 0, 3), GridOrientation.Default));
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceComponent(first,
                BuiltInPinCatalog.And, new GridCell(20, 1, 3), GridOrientation.Default));
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceComponent(first,
                BuiltInPinCatalog.And, new GridCell(3, 10, 3), GridOrientation.Default));
            Assert.That(first.Components.Count, Is.EqualTo(1));
            Assert.That(first.Topology.Pins.Count, Is.EqualTo(1));

            var wireCell = new GridCell(7, 1, 7);
            var route = new ConnectorRoute(Guid.NewGuid(), "wire", 1,
                new[] { new RouteNode(Guid.NewGuid(), wireCell, 0,
                    new QuarterPoint(2, 2, 2)) }, Array.Empty<RouteSpan>());
            var occupiedByWire = new OneBitWorldDesign(Bounds,
                Array.Empty<PlacedOneBitComponent>(),
                new OneBitAuthoredTopology(Array.Empty<AuthoredPin>(),
                    new[] { route }, Array.Empty<ElectricalJoin>()));
            Assert.Throws<ArgumentException>(() => OneBitWorldEdits.PlaceComponent(
                occupiedByWire, BuiltInPinCatalog.Source, wireCell,
                GridOrientation.Default));
            Assert.That(occupiedByWire.Components.Count, Is.EqualTo(0));
            Assert.That(occupiedByWire.Topology.Connectors.Count, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedOrUnrotatedSavedPinsAreRejected()
        {
            var first = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(Bounds),
                BuiltInPinCatalog.And, new GridCell(4, 1, 4),
                GridOrientation.Default.ClockwiseYaw());
            var component = first.Components[0];
            var pins = new AuthoredPin[first.Topology.Pins.Count];
            for (var i = 0; i < pins.Length; i++) pins[i] = first.Topology.Pins[i];
            pins[0] = new AuthoredPin(component.Id, component.PinIds["A"],
                component.AnchorCell, new QuarterPoint(0, 1, 1));
            Assert.Throws<ArgumentException>(() => new OneBitWorldDesign(Bounds,
                first.Components, new OneBitAuthoredTopology(pins,
                    Array.Empty<ConnectorRoute>(), Array.Empty<ElectricalJoin>())));
            pins[0] = first.Topology.Pins[0];
            pins[1] = first.Topology.Pins[0];
            Assert.Throws<ArgumentException>(() => new OneBitWorldDesign(Bounds,
                first.Components, new OneBitAuthoredTopology(pins,
                    Array.Empty<ConnectorRoute>(), Array.Empty<ElectricalJoin>())));
        }
    }
}
