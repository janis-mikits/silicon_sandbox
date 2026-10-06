using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Simulation;
using SiliconSandbox.Persistence;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class FirstPlayableBenchmarkFactoryTests
    {
        [Test]
        public void FrozenDenseDiagnosticHasExactHashAndFortyByTwentyFiveGateGrid()
        {
            const string expectedSha256 =
                "9dfc121962af93d0151c09203b82bb2dc2d4275b72145aeba2d505f7dbef9242";
            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "Benchmarks/first-playable-dense-diagnostic.ssworld");
            var bytes = File.ReadAllBytes(path);
            Assert.That(WorldManifestIntegrity.Sha256Hex(bytes),
                Is.EqualTo(expectedSha256));
            using (var stream = new MemoryStream(bytes))
            {
                var loaded = WorldV1ArchiveCodec.Read(stream);
                var world = loaded.Snapshot.Design;
                Assert.That(world.Components.Count, Is.EqualTo(1000));
                Assert.That(world.Modules, Is.Empty);
                Assert.That(world.Topology.Connectors, Is.Empty);
                Assert.That(loaded.Snapshot.ModuleVersions, Is.Empty);
                var occupied = new HashSet<GridCell>();
                foreach (var gate in world.Components)
                {
                    Assert.That(gate.TypeId, Is.EqualTo(BuiltInPinCatalog.And));
                    Assert.That(gate.AnchorCell.X, Is.InRange(2, 41));
                    Assert.That(gate.AnchorCell.Z, Is.InRange(2, 26));
                    Assert.That(gate.AnchorCell.Y, Is.EqualTo(1));
                    Assert.That(occupied.Add(gate.AnchorCell), Is.True);
                }
                for (var x = 2; x <= 41; x++)
                    for (var z = 2; z <= 26; z++)
                        Assert.That(occupied.Contains(new GridCell(x, 1, z)),
                            Is.True);
            }
        }

        [Test]
        public void FrozenSavedReferenceHasExpectedBytesAndDistribution()
        {
            const string expectedSha256 =
                "016e9cff9773be7cb0b931a7d7492b8a9ec856b79418e445a39ec0acf1c95c3c";
            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath,
                "Benchmarks/first-playable-1000-gates-compact-v1.ssworld");
            var bytes = File.ReadAllBytes(path);
            Assert.That(WorldManifestIntegrity.Sha256Hex(bytes),
                Is.EqualTo(expectedSha256));
            using (var stream = new MemoryStream(bytes))
            {
                var loaded = WorldV1ArchiveCodec.Read(stream);
                var world = loaded.Snapshot.Design;
                var standalone = 0;
                foreach (var component in world.Components)
                    if (component.TypeId == BuiltInPinCatalog.And)
                        standalone++;
                Assert.That(standalone, Is.EqualTo(500));
                Assert.That(world.Modules.Count, Is.EqualTo(10));
                Assert.That(loaded.Snapshot.ModuleVersions.Count, Is.EqualTo(1));
                foreach (var version in loaded.Snapshot.ModuleVersions.Values)
                {
                    var internalGates = 0;
                    foreach (var component in version.Components)
                        if (component.TypeId == BuiltInPinCatalog.And)
                            internalGates++;
                    Assert.That(internalGates, Is.EqualTo(50));
                }
                Assert.That(loaded.UnavailableModuleVersionIds, Is.Empty);
            }
        }

        [Test]
        public void MixedFixtureProcessesEveryTenHertzEdgeForSixtySimulatedSeconds()
        {
            var fixture = FirstPlayableBenchmarkFactory.Create();
            var session = new OneBitWorldSession(fixture.World, "10",
                new Dictionary<Guid, OneBitModuleVersion>
                { [fixture.Version.VersionId] = fixture.Version });
            session.Scheduler.StartClock();
            session.Scheduler.AdvanceUntil(new SimulationTime(60, 0));
            Assert.That(session.Scheduler.ClockEdgesProcessed.ToString(),
                Is.EqualTo("1200"),
                "Ten complete cycles per simulated second have two edges each.");
            Assert.That(session.Scheduler.ClockLevel, Is.EqualTo(LogicBit.Zero));
            Assert.That(session.Scheduler.Now,
                Is.EqualTo(new SimulationTime(60, 0)));
            var gate = fixture.World.Components[0];
            Assert.That(session.Inspector.InspectPin(gate.Id,
                gate.PinIds["Y"]).Value, Is.EqualTo(LogicBit.Zero));
        }

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
