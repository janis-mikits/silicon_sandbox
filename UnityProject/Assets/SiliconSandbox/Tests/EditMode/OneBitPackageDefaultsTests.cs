using System;
using NUnit.Framework;
using SiliconSandbox.Application;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitPackageDefaultsTests
    {
        [Test]
        public void OneCellSrSuggestsThreeInputsOppositeTwoOutputsWithExactTargets()
        {
            var world = OneBitWorldEdits.PlaceComponent(OneBitWorldDesign.Empty(
                new WorldBounds(6, 6, 4)), BuiltInPinCatalog.SrFlipFlop,
                new GridCell(2, 1, 2), GridOrientation.Default);
            var snapshot = OneBitModuleSnapshotBuilder.Preview(world,
                new CellRegion(new GridCell(2, 1, 2), new GridCell(2, 1, 2)));
            var choices = OneBitPackageDefaults.ForSingleCell(snapshot);
            var sr = snapshot.Components[0];

            Assert.That(choices.Count, Is.EqualTo(5));
            Assert.That(choices[0].Name, Is.EqualTo("S"));
            Assert.That(choices[1].Name, Is.EqualTo("R"));
            Assert.That(choices[2].Name, Is.EqualTo("CLK"));
            Assert.That(choices[3].Name, Is.EqualTo("Q"));
            Assert.That(choices[4].Name, Is.EqualTo("Q_bar"));
            for (var i = 0; i < 3; i++)
            {
                Assert.That(choices[i].Direction,
                    Is.EqualTo(OneBitPortDirection.Input));
                Assert.That(choices[i].PointQ.X, Is.EqualTo(0));
            }
            for (var i = 3; i < 5; i++)
            {
                Assert.That(choices[i].Direction,
                    Is.EqualTo(OneBitPortDirection.Output));
                Assert.That(choices[i].PointQ.X, Is.EqualTo(4));
            }
            Assert.That(choices[2].BitZeroTarget,
                Is.EqualTo(JoinMember.ComponentPin(sr.Id, sr.PinIds["CLK"])));
            Assert.That(world.Components[0].Id, Is.Not.EqualTo(sr.Id));
        }
    }
}
