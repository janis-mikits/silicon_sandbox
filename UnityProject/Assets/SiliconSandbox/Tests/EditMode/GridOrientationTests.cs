using System;
using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class GridOrientationTests
    {
        [Test]
        public void DefaultAndYawUseAcceptedXAxisEastYAxisUpZAxisNorth()
        {
            var identity = GridOrientation.Default;
            Assert.That(identity.TransformCellOffset(new GridCell(1, 2, 3)),
                Is.EqualTo(new GridCell(1, 2, 3)));
            Assert.That(identity.TransformPoint(new QuarterPoint(4, 1, 1)),
                Is.EqualTo(new QuarterPoint(4, 1, 1)));
            var eastFacing = identity.ClockwiseYaw();
            Assert.That(eastFacing.Forward, Is.EqualTo(GridDirection.East));
            Assert.That(eastFacing.Up, Is.EqualTo(GridDirection.Up));
            Assert.That(eastFacing.TransformCellOffset(new GridCell(1, 2, 3)),
                Is.EqualTo(new GridCell(3, 2, -1)));
            Assert.That(eastFacing.TransformPoint(new QuarterPoint(4, 1, 1)),
                Is.EqualTo(new QuarterPoint(1, 1, 0)));
            Assert.That(eastFacing.CounterclockwiseYaw(), Is.EqualTo(identity));
            var fullTurn = identity;
            for (var i = 0; i < 4; i++) fullTurn = fullTurn.ClockwiseYaw();
            Assert.That(fullTurn, Is.EqualTo(identity));
        }

        [Test]
        public void AllTwentyFourOrientationsPreserveExactFaceQuadrants()
        {
            var orientations = new HashSet<GridOrientation>();
            var faces = new[]
            {
                GridDirection.North, GridDirection.South, GridDirection.East,
                GridDirection.West, GridDirection.Up, GridDirection.Down
            };
            foreach (var forward in faces)
                foreach (var up in faces)
                {
                    try
                    {
                        var orientation = new GridOrientation(forward, up);
                        Assert.That(orientations.Add(orientation), Is.True);
                        foreach (var local in new[]
                        {
                            new QuarterPoint(0, 1, 1), new QuarterPoint(4, 3, 3),
                            new QuarterPoint(1, 0, 3), new QuarterPoint(3, 4, 1),
                            new QuarterPoint(1, 1, 0), new QuarterPoint(3, 3, 4)
                        })
                            Assert.That(orientation.TransformPoint(local).IsFacePoint, Is.True);
                    }
                    catch (ArgumentException)
                    {
                        // Parallel/opposite pairs are invalid by the schema.
                    }
                }
            Assert.That(orientations.Count, Is.EqualTo(24));
        }

        [Test]
        public void SavedFaceNamesRoundTripAndParallelPairsAreRejected()
        {
            foreach (GridDirection face in Enum.GetValues(typeof(GridDirection)))
                Assert.That(GridOrientation.ParseFace(GridOrientation.FaceName(face)),
                    Is.EqualTo(face));
            Assert.Throws<ArgumentException>(() =>
                new GridOrientation(GridDirection.North, GridDirection.South));
            Assert.Throws<ArgumentException>(() =>
                new GridOrientation(GridDirection.Up, GridDirection.Up));
            Assert.Throws<ArgumentException>(() => GridOrientation.ParseFace("NORTH"));
        }
    }
}
