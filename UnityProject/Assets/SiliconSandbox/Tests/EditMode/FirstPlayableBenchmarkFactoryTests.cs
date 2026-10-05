using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class FirstPlayableBenchmarkFactoryTests
    {
        [Test]
        public void ReferenceWorldUsesRepeatableAuthoredIdentities()
        {
            var first = FirstPlayableBenchmarkFactory.Create();
            var second = FirstPlayableBenchmarkFactory.Create();
            Assert.That(first.FirstWorldGateId.ToString("D"),
                Is.EqualTo("696c6953-6f63-426e-8000-000000000001"));
            Assert.That(second.FirstWorldGateId, Is.EqualTo(first.FirstWorldGateId));
            Assert.That(second.Version.VersionId, Is.EqualTo(first.Version.VersionId));
            Assert.That(second.FirstModuleInstanceId,
                Is.EqualTo(first.FirstModuleInstanceId));
            for (var i = 0; i < first.World.Components.Count; i++)
                Assert.That(second.World.Components[i].Id,
                    Is.EqualTo(first.World.Components[i].Id));
            for (var i = 0; i < first.World.Topology.Connectors.Count; i++)
                Assert.That(second.World.Topology.Connectors[i].Id,
                    Is.EqualTo(first.World.Topology.Connectors[i].Id));
        }

        [Test]
        public void FixedMixedReferenceContainsExactlyOneThousandLiveAndOperations()
        {
            var fixture = FirstPlayableBenchmarkFactory.Create();
            var versions = new Dictionary<Guid, OneBitModuleVersion>
            { [fixture.Version.VersionId] = fixture.Version };
            var session = new OneBitWorldSession(fixture.World, "10", versions);

            var individual = 0;
            foreach (var component in fixture.World.Components)
                if (component.TypeId == BuiltInPinCatalog.And) individual++;
            var internalGates = 0;
            foreach (var component in fixture.Version.Components)
                if (component.TypeId == BuiltInPinCatalog.And) internalGates++;
            Assert.That(individual, Is.EqualTo(500));
            Assert.That(fixture.World.Modules.Count, Is.EqualTo(10));
            Assert.That(internalGates, Is.EqualTo(50));
            Assert.That(individual + fixture.World.Modules.Count * internalGates,
                Is.EqualTo(1000));
            Assert.That(session.Built.Plan.AndGates.Count, Is.EqualTo(1000));
            Assert.That(fixture.World.Topology.Connectors.Count,
                Is.EqualTo(1011));
            var segments = 0;
            foreach (var connector in fixture.World.Topology.Connectors)
                segments += connector.Spans.Count;
            Assert.That(segments, Is.EqualTo(1013));

            var firstGate = fixture.World.Components[0];
            var firstPort = fixture.Version.Ports[0];
            var firstModule = fixture.World.Modules[0];
            Assert.That(session.Inspector.InspectPin(firstGate.Id,
                firstGate.PinIds["Y"]).Value, Is.EqualTo(LogicBit.Zero));
            session.Scheduler.StartClock();
            session.Scheduler.AdvanceUntil(session.Scheduler.NextClockEdge.Value);
            Assert.That(session.Inspector.InspectPin(firstGate.Id,
                firstGate.PinIds["Y"]).Value, Is.EqualTo(LogicBit.One),
                "The first AND has its B input held at 1 by a source.");
            Assert.That(session.Inspector.InspectModulePort(firstModule.Id,
                firstPort.Id).Value, Is.EqualTo(LogicBit.X),
                "An unconnected B input makes 1 AND Z uncertain.");
            session.Scheduler.AdvanceUntil(session.Scheduler.NextClockEdge.Value);
            Assert.That(session.Inspector.InspectPin(firstGate.Id,
                firstGate.PinIds["Y"]).Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Inspector.InspectModulePort(firstModule.Id,
                firstPort.Id).Value, Is.EqualTo(LogicBit.Zero));
        }
    }
}
