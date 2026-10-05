using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class ProfessorAndSequenceTests
    {
        [Test]
        public void AuthoredWorldShowsFiveIndependentAndExpectationsAndInspection()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(16, 16, 8)));
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(4, 1, 6), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.Source,
                new GridCell(4, 1, 8), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(6, 1, 6), GridOrientation.Default);
            var sourceA = session.Design.Components[0];
            var sourceB = session.Design.Components[1];
            var gate = session.Design.Components[2];
            session.ConnectPins(Out(sourceA), Pin(gate, "A"));
            session.ConnectPins(Out(sourceB), Pin(gate, "B"));
            session.PlaceWireStub(Pin(gate, "Y"), new GridCell(8, 1, 6));
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(3));
            Assert.That(session.Scheduler.ClockRunning, Is.False);
            session.ToggleSource(sourceA.Id);
            session.ToggleSource(sourceB.Id);

            Check(session, sourceA, sourceB, gate,
                LogicBit.Zero, LogicBit.One, LogicBit.Zero);
            Check(session, sourceA, sourceB, gate,
                LogicBit.One, LogicBit.One, LogicBit.One);
            Check(session, sourceA, sourceB, gate,
                LogicBit.Zero, LogicBit.Z, LogicBit.Zero);
            Check(session, sourceA, sourceB, gate,
                LogicBit.One, LogicBit.Z, LogicBit.X);

            var bRoute = session.Design.Topology.Connectors[1];
            var b = session.Inspector.InspectConnector(bRoute.Id);
            Assert.That(b.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(b.ActiveDrivers.Count, Is.EqualTo(0));
            Assert.That(b.ConnectedPins, Is.EquivalentTo(new[]
            {
                Out(sourceB), Pin(gate, "B")
            }));
            var y = session.Inspector.InspectConnector(
                session.Design.Topology.Connectors[2].Id);
            Assert.That(y.Value, Is.EqualTo(LogicBit.X));
            Assert.That(y.Explanation, Does.Contain("input B is Z"));
            Assert.That(y.Explanation, Does.Not.Contain("Conflicting"));
            Check(session, sourceA, sourceB, gate,
                LogicBit.One, LogicBit.X, LogicBit.X);
            Assert.That(session.Scheduler.ClockRunning, Is.False);
        }

        private static void Check(OneBitWorldSession session,
            PlacedOneBitComponent a, PlacedOneBitComponent b,
            PlacedOneBitComponent gate, LogicBit inputA, LogicBit inputB,
            LogicBit expectedY)
        {
            session.ConfigureSource(a.Id, inputA, false);
            session.ConfigureSource(b.Id, inputB, false);
            Assert.That(session.Inspector.InspectPin(gate.Id,
                gate.PinIds["A"]).Value, Is.EqualTo(inputA));
            Assert.That(session.Inspector.InspectPin(gate.Id,
                gate.PinIds["B"]).Value, Is.EqualTo(inputB));
            Assert.That(session.Inspector.InspectPin(gate.Id,
                gate.PinIds["Y"]).Value, Is.EqualTo(expectedY));
        }

        private static JoinMember Out(PlacedOneBitComponent source) =>
            Pin(source, "OUT");

        private static JoinMember Pin(PlacedOneBitComponent component,
            string key) => JoinMember.ComponentPin(component.Id,
                component.PinIds[key]);
    }
}
