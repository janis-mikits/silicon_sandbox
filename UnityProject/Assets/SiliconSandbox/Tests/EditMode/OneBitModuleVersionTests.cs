using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitModuleVersionTests
    {
        [Test]
        public void SrVersionHasExactFourPortMappingsAndNoRuntimeState()
        {
            var source = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(6, 6, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(source,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var copied = snapshot.Components[0];
            var family = Guid.NewGuid();
            var version = OneBitModuleVersionFactory.Create(snapshot, family, "SR",
                new[]
                {
                    Choice("S", OneBitPortDirection.Input, copied, "S",
                        new QuarterPoint(0, 1, 1)),
                    Choice("R", OneBitPortDirection.Input, copied, "R",
                        new QuarterPoint(0, 3, 1)),
                    Choice("CLK", OneBitPortDirection.Input, copied, "CLK",
                        new QuarterPoint(1, 1, 0)),
                    Choice("Q", OneBitPortDirection.Output, copied, "Q",
                        new QuarterPoint(4, 1, 1))
                });

            Assert.That(version.FamilyId, Is.EqualTo(family));
            Assert.That(version.VersionId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(version.Ports.Count, Is.EqualTo(4));
            Assert.That(version.Ports[0].Width, Is.EqualTo(1));
            Assert.That(version.Ports[0].BitZeroTarget,
                Is.EqualTo(JoinMember.ComponentPin(copied.Id, copied.PinIds["S"])));
            Assert.That(version.Ports[3].Direction, Is.EqualTo(OneBitPortDirection.Output));
            Assert.That(version.Ports[3].PointQ,
                Is.EqualTo(new QuarterPoint(4, 1, 1)));
            Assert.That(version.Components[0].Id,
                Is.Not.EqualTo(source.Components[0].Id));
            Assert.That(version.Components[0].SrInitialQ, Is.Null,
                "An unconfigured SR begins X in each future instance.");
            Assert.That(version.ChildVersionIds.Count, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateNamePositionAndUnmappedEndpointRejectBeforeVersionExists()
        {
            var source = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(6, 6, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(source,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var copied = snapshot.Components[0];
            var s = Choice("S", OneBitPortDirection.Input, copied, "S",
                new QuarterPoint(0, 1, 1));
            var duplicateName = Choice("S", OneBitPortDirection.Input, copied, "R",
                new QuarterPoint(0, 3, 1));
            var duplicatePosition = Choice("R", OneBitPortDirection.Input, copied,
                "R", new QuarterPoint(0, 1, 1));
            var unmapped = new OneBitPortChoice("R", OneBitPortDirection.Input,
                new GridCell(0, 0, 0), new QuarterPoint(0, 3, 1),
                JoinMember.ComponentPin(Guid.NewGuid(), Guid.NewGuid()));

            Assert.Throws<ArgumentException>(() => OneBitModuleVersionFactory.Create(
                snapshot, Guid.NewGuid(), "SR", new[] { s, duplicateName }));
            Assert.Throws<ArgumentException>(() => OneBitModuleVersionFactory.Create(
                snapshot, Guid.NewGuid(), "SR", new[] { s, duplicatePosition }));
            Assert.Throws<ArgumentException>(() =>
                OneBitModuleVersionFactory.Create(snapshot, Guid.NewGuid(), "SR",
                    new[] { s, unmapped }));
            Assert.That(source.Components.Count, Is.EqualTo(1));
            Assert.That(source.Topology.Connectors.Count, Is.EqualTo(0));
        }

        private static OneBitPortChoice Choice(string name,
            OneBitPortDirection direction, PlacedOneBitComponent component,
            string pinKey, QuarterPoint point) => new OneBitPortChoice(name,
                direction, new GridCell(0, 0, 0), point,
                JoinMember.ComponentPin(component.Id, component.PinIds[pinKey]));
    }
}
