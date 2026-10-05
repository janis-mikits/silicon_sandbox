using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitHierarchicalCircuitTests
    {
        [Test]
        public void MissingExactVersionRetainsTwoPlacementsAndDrivesOnlyOutputsX()
        {
            var version = SrVersion();
            var world = OneBitWorldDesign.Empty(new WorldBounds(12, 12, 5));
            world = OneBitWorldEdits.PlaceModule(world, version, "A",
                new GridCell(3, 1, 3), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceModule(world, version, "B",
                new GridCell(8, 1, 3), GridOrientation.Default);
            var session = new OneBitWorldSession(world, "10");
            var a = world.Modules[0];
            var b = world.Modules[1];
            var s = version.Ports[0].Id;
            var q = version.Ports[3].Id;

            Assert.That(session.HasModuleVersion(version.VersionId), Is.False);
            Assert.That(session.Design.Modules.Count, Is.EqualTo(2));
            Assert.That(a.InstanceId, Is.Not.EqualTo(b.InstanceId));
            Assert.That(session.Built.Plan.MissingModuleOutputs.Count,
                Is.EqualTo(2));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(
                ModulePort(a, s))).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(
                ModulePort(a, q))).Value, Is.EqualTo(LogicBit.X));
            Assert.That(session.Circuit.Net(session.Built.NetIndex(
                ModulePort(b, q))).Value, Is.EqualTo(LogicBit.X));
            Assert.That(session.Inspector.InspectModulePort(a.Id, q).Explanation,
                Does.Contain("missing"));
        }

        [Test]
        public void TwoFixedSrInstancesFollowIndependentThreeEdgeExpectedSequence()
        {
            var version = SrVersion();
            var session = new OneBitWorldSession(OneBitWorldDesign.Empty(
                new WorldBounds(22, 12, 5)), "10");
            session.PlaceModule(version, "A", new GridCell(6, 1, 5),
                GridOrientation.Default);
            session.PlaceModule(version, "B", new GridCell(16, 1, 5),
                GridOrientation.Default);
            var a = session.Design.Modules[0];
            var b = session.Design.Modules[1];
            var sPort = version.Ports[0].Id;
            var rPort = version.Ports[1].Id;
            var clkPort = version.Ports[2].Id;
            var qPort = version.Ports[3].Id;
            var locations = new[]
            {
                new GridCell(4,1,5), new GridCell(4,1,7),
                new GridCell(14,1,5), new GridCell(14,1,7)
            };
            foreach (var cell in locations)
                session.PlaceComponent(BuiltInPinCatalog.Source, cell,
                    GridOrientation.Default, LogicBit.One, false);
            var aS = session.Design.Components[0];
            var aR = session.Design.Components[1];
            var bS = session.Design.Components[2];
            var bR = session.Design.Components[3];
            session.ConnectPins(SourceOut(aS), ModulePort(a, sPort));
            session.ConnectPins(SourceOut(aR), ModulePort(a, rPort));
            session.ConnectPins(SourceOut(bS), ModulePort(b, sPort));
            session.ConnectPins(SourceOut(bR), ModulePort(b, rPort));
            session.AttachWorldClockPort(a.Id, clkPort);
            session.AttachWorldClockPort(b.Id, clkPort);

            var localSr = version.Components[0];
            var aKey = RuntimeObjectKey.Module(version.VersionId, localSr.Id,
                new[] { a.InstanceId });
            var bKey = RuntimeObjectKey.Module(version.VersionId, localSr.Id,
                new[] { b.InstanceId });
            Assert.That(session.Circuit.Storage(aKey).Q, Is.EqualTo(LogicBit.X));
            Assert.That(session.Circuit.Storage(bKey).Q, Is.EqualTo(LogicBit.X));

            session.ToggleSource(aS.Id); // A: S=1,R=0; B: S=0,R=0.
            session.Scheduler.StepClockEdge();
            AssertState(LogicBit.One, LogicBit.X);

            session.ToggleSource(aS.Id);
            session.ToggleSource(bR.Id); // A: hold; B: reset.
            session.Scheduler.StepClockEdge(); // Falling.
            session.Scheduler.StepClockEdge(); // Second rising.
            AssertState(LogicBit.One, LogicBit.Zero);

            session.ToggleSource(aR.Id);
            session.ToggleSource(bR.Id); // A: reset; B: hold.
            session.Scheduler.StepClockEdge(); // Falling.
            session.Scheduler.StepClockEdge(); // Third rising.
            AssertState(LogicBit.Zero, LogicBit.Zero);
            var inspectedA = session.Inspector.InspectModulePort(a.Id, qPort);
            var inspectedB = session.Inspector.InspectModulePort(b.Id, qPort);
            Assert.That(inspectedA.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(inspectedB.Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(inspectedA.ActiveDrivers.Count, Is.EqualTo(1));
            Assert.That(inspectedA.ActiveDrivers[0].RuntimeKey,
                Is.EqualTo(aKey));
            Assert.That(inspectedB.ActiveDrivers.Count, Is.EqualTo(1));
            Assert.That(inspectedB.ActiveDrivers[0].RuntimeKey,
                Is.EqualTo(bKey));

            void AssertState(LogicBit expectedA, LogicBit expectedB)
            {
                Assert.That(session.Circuit.Storage(aKey).Q, Is.EqualTo(expectedA));
                Assert.That(session.Circuit.Storage(bKey).Q, Is.EqualTo(expectedB));
                Assert.That(session.Circuit.Net(session.Built.NetIndex(
                    ModulePort(a, qPort))).Value, Is.EqualTo(expectedA));
                Assert.That(session.Circuit.Net(session.Built.NetIndex(
                    ModulePort(b, qPort))).Value, Is.EqualTo(expectedB));
            }
        }

        [Test]
        public void ExteriorPortMapsToOnlyItsOwningInstanceInternalNet()
        {
            var version = SrVersion();
            var world = OneBitWorldDesign.Empty(new WorldBounds(16, 12, 5));
            world = OneBitWorldEdits.PlaceModule(world, version, "A",
                new GridCell(5, 1, 3), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceModule(world, version, "B",
                new GridCell(11, 1, 3), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceComponent(world,
                BuiltInPinCatalog.Source, new GridCell(2, 1, 3),
                GridOrientation.Default, LogicBit.One, true);
            var a = world.Modules[0];
            var b = world.Modules[1];
            var source = world.Components[0];
            var sourcePin = JoinMember.ComponentPin(source.Id,
                source.PinIds["OUT"]);
            var aPort = JoinMember.ModulePortBit(a.Id, version.Ports[0].Id, 0);
            var proposal = OneBitPinRoutePlanner.Plan(world, sourcePin, aPort);
            world = OneBitWorldEdits.PlaceConnector(world, proposal.Route,
                proposal.Joins);

            var built = OneBitHierarchicalCircuitPlanBuilder.Build(world,
                new Dictionary<Guid, OneBitModuleVersion>
                { [version.VersionId] = version });
            var circuit = new GraphDrivenOneBitCircuit(built.Plan);
            var internalS = JoinMember.ComponentPin(version.Components[0].Id,
                version.Components[0].PinIds["S"]);
            var aS = built.NetIndex(a.InstanceId, internalS);
            var bS = built.NetIndex(b.InstanceId, internalS);
            var aKey = RuntimeObjectKey.Module(version.VersionId,
                version.Components[0].Id, new[] { a.InstanceId });
            var bKey = RuntimeObjectKey.Module(version.VersionId,
                version.Components[0].Id, new[] { b.InstanceId });

            Assert.That(aS, Is.EqualTo(built.NetIndex(aPort)));
            Assert.That(aS, Is.Not.EqualTo(bS));
            Assert.That(circuit.Net(aS).Value, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Net(bS).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(circuit.Storage(aKey).Q, Is.EqualTo(LogicBit.X));
            Assert.That(circuit.Storage(bKey).Q, Is.EqualTo(LogicBit.X));
            circuit.SetSourceOn(source.Id, false);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Net(aS).Value, Is.EqualTo(LogicBit.Zero));
            Assert.That(circuit.Net(bS).Value, Is.EqualTo(LogicBit.Z));
        }

        private static OneBitModuleVersion SrVersion()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var sr = snapshot.Components[0];
            return OneBitModuleVersionFactory.Create(snapshot, Guid.NewGuid(), "SR",
                new[]
                {
                    Port("S", OneBitPortDirection.Input, sr,
                        new QuarterPoint(0, 1, 1)),
                    Port("R", OneBitPortDirection.Input, sr,
                        new QuarterPoint(0, 3, 1)),
                    Port("CLK", OneBitPortDirection.Input, sr,
                        new QuarterPoint(1, 1, 0)),
                    Port("Q", OneBitPortDirection.Output, sr,
                        new QuarterPoint(4, 1, 1))
                });
        }

        private static OneBitPortChoice Port(string name,
            OneBitPortDirection direction, PlacedOneBitComponent sr,
            QuarterPoint point) => new OneBitPortChoice(name, direction,
                new GridCell(0, 0, 0), point,
                JoinMember.ComponentPin(sr.Id, sr.PinIds[name]));

        private static JoinMember SourceOut(PlacedOneBitComponent source) =>
            JoinMember.ComponentPin(source.Id, source.PinIds["OUT"]);

        private static JoinMember ModulePort(PlacedOneBitModuleInstance module,
            Guid portId) => JoinMember.ModulePortBit(module.Id, portId, 0);
    }
}
