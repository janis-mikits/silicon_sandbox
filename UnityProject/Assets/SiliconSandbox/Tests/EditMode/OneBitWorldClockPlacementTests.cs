using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitWorldClockPlacementTests
    {
        [Test]
        public void ClockLinkDrivesAndInputButCannotAttachToOutput()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), "10");
            session.PlaceComponent(BuiltInPinCatalog.And,
                new GridCell(3, 1, 3), GridOrientation.Default);
            var gate = session.Design.Components[0];
            var input = JoinMember.ComponentPin(gate.Id, gate.PinIds["A"]);
            session.AttachWorldClockPin(gate.Id, gate.PinIds["A"]);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(input)).Value,
                Is.EqualTo(LogicBit.Zero));
            session.Scheduler.StepClockEdge();
            Assert.That(session.Circuit.Net(session.Built.NetIndex(input)).Value,
                Is.EqualTo(LogicBit.One));
            Assert.Throws<ArgumentException>(() => session.AttachWorldClockPin(
                gate.Id, gate.PinIds["Y"]));
        }

        [Test]
        public void TwoVisibleStubsShareWorldClockButKeepPhysicalIdentities()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), "10");
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(5, 1, 2), GridOrientation.Default);
            var first = session.Design.Components[0];
            var second = session.Design.Components[1];
            session.AttachWorldClockPin(first.Id);
            session.AttachWorldClockPin(second.Id);

            var firstClock = JoinMember.ComponentPin(first.Id, first.PinIds["CLK"]);
            var secondClock = JoinMember.ComponentPin(second.Id, second.PinIds["CLK"]);
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(2));
            Assert.That(session.Design.Topology.Connectors[0].Id,
                Is.Not.EqualTo(session.Design.Topology.Connectors[1].Id));
            foreach (var stub in session.Design.Topology.Connectors)
            {
                Assert.That(stub.Kind, Is.EqualTo("netLink"));
                Assert.That(stub.LinkName, Is.EqualTo("@world-clock"));
                Assert.That(stub.Nodes.Count, Is.EqualTo(1));
            }
            Assert.That(session.Built.Graph.Connected(firstClock, secondClock), Is.True);
            Assert.That(session.Circuit.Net(session.Built.NetIndex(firstClock)).Value,
                Is.EqualTo(LogicBit.Zero));
            session.Scheduler.StepClockEdge();
            Assert.That(session.Circuit.Net(session.Built.NetIndex(firstClock)).Value,
                Is.EqualTo(LogicBit.One));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(secondClock)).Value,
                Is.EqualTo(LogicBit.One));
        }

        [Test]
        public void BoundaryClockStubUsesExactPinFaceAndRejectsSecondAttachment()
        {
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(5, 2, 3)));
            session.PlaceComponent(BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 0), GridOrientation.Default);
            var sr = session.Design.Components[0];
            session.AttachWorldClockPin(sr.Id);
            var stub = session.Design.Topology.Connectors[0];
            Assert.That(stub.Nodes[0].Cell, Is.EqualTo(sr.AnchorCell));
            Assert.That(stub.Nodes[0].PointQ, Is.EqualTo(new QuarterPoint(1, 1, 0)));
            var before = session.Design;
            var revision = session.Revision;
            Assert.Throws<ArgumentException>(() => session.AttachWorldClockPin(sr.Id));
            Assert.That(session.Design, Is.SameAs(before));
            Assert.That(session.Revision, Is.EqualTo(revision));
            Assert.That(session.Design.Topology.Connectors.Count, Is.EqualTo(1));
        }
    }
}
