using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitPackageDraftTests
    {
        [Test]
        public void SrDraftCanExposeOnlyApprovedFourDemonstrationPorts()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var original = world.Components[0];
            var draft = new OneBitPackageDraft(world,
                new CellRegion(original.AnchorCell, original.AnchorCell), "SR demo");
            Assert.That(draft.Ports.Count, Is.EqualTo(5));
            var qbar = -1;
            for (var i = 0; i < draft.Ports.Count; i++)
                if (draft.Ports[i].Name == "Q_bar") qbar = i;
            Assert.That(qbar, Is.GreaterThanOrEqualTo(0));
            draft.RemovePort(qbar);
            var version = draft.BuildCandidate(Guid.NewGuid());
            Assert.That(version.Ports.Count, Is.EqualTo(4));
            Assert.That(version.Ports[0].Name, Is.EqualTo("S"));
            Assert.That(version.Ports[1].Name, Is.EqualTo("R"));
            Assert.That(version.Ports[2].Name, Is.EqualTo("CLK"));
            Assert.That(version.Ports[3].Name, Is.EqualTo("Q"));
            Assert.That(version.Ports[0].Direction,
                Is.EqualTo(OneBitPortDirection.Input));
            Assert.That(version.Ports[3].Direction,
                Is.EqualTo(OneBitPortDirection.Output));
            Assert.That(version.Components[0].Id, Is.Not.EqualTo(original.Id));
            Assert.That(version.Components[0].PinIds["Q"],
                Is.Not.EqualTo(original.PinIds["Q"]));
            Assert.That(world.Components[0], Is.SameAs(original));
            Assert.That(world.Modules.Count, Is.EqualTo(0));
        }

        [Test]
        public void DuplicatePortNamesRejectCandidateWithoutChangingSourceWorld()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.And,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var draft = new OneBitPackageDraft(world,
                new CellRegion(new GridCell(2, 1, 2),
                    new GridCell(2, 1, 2)), "AND demo");
            var second = draft.Ports[1];
            draft.ReplacePort(1, new OneBitPortChoice(draft.Ports[0].Name,
                second.Direction, second.LocalCell, second.PointQ,
                second.BitZeroTarget));
            Assert.Throws<ArgumentException>(() =>
                draft.BuildCandidate(Guid.NewGuid()));
            Assert.That(world.Components.Count, Is.EqualTo(1));
            Assert.That(world.Topology.Joins.Count, Is.EqualTo(0));
        }

        [Test]
        public void MultiCellDraftOffersExactExteriorPortsOnItsSavedFootprint()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(8, 8, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            world = OneBitWorldEdits.PlaceComponent(world, BuiltInPinCatalog.And,
                new GridCell(3, 1, 2), GridOrientation.Default);
            var draft = new OneBitPackageDraft(world,
                new CellRegion(new GridCell(2, 1, 2),
                    new GridCell(3, 1, 2)), "two cells");
            var version = draft.BuildCandidate(Guid.NewGuid());
            Assert.That(version.SizeCells, Is.EqualTo(new GridCell(2, 1, 1)));
            Assert.That(version.Components.Count, Is.EqualTo(2));
            foreach (var port in version.Ports)
                Assert.That(port.LocalCell.X,
                    Is.EqualTo(port.Direction == OneBitPortDirection.Output
                        ? 1 : 0));
        }
    }
}
