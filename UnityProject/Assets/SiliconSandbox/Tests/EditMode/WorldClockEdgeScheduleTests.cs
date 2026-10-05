using System;
using System.Numerics;
using NUnit.Framework;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class WorldClockEdgeScheduleTests
    {
        [Test]
        public void TenHertzAlternatesAtExactFiftyMillisecondBoundaries()
        {
            var schedule = new WorldClockEdgeSchedule("10", SimulationTime.Zero);
            Assert.That(schedule.EdgeTime(1), Is.EqualTo(new SimulationTime(0, 50_000_000_000)));
            Assert.That(schedule.EdgeTime(2), Is.EqualTo(new SimulationTime(0, 100_000_000_000)));
            Assert.That(schedule.EdgeTime(3), Is.EqualTo(new SimulationTime(0, 150_000_000_000)));
            Assert.That(WorldClockEdgeSchedule.LevelAfterEdge(1), Is.True);
            Assert.That(WorldClockEdgeSchedule.LevelAfterEdge(2), Is.False);
        }

        [Test]
        public void ThreeHertzUsesAbsoluteRationalTimesWithoutRoundedIntervalDrift()
        {
            var schedule = new WorldClockEdgeSchedule("3", SimulationTime.Zero);
            Assert.That(schedule.EdgeTime(1), Is.EqualTo(new SimulationTime(0, 166_666_666_667)));
            Assert.That(schedule.EdgeTime(2), Is.EqualTo(new SimulationTime(0, 333_333_333_333)));
            Assert.That(schedule.EdgeTime(3), Is.EqualTo(new SimulationTime(0, 500_000_000_000)));
            Assert.That(schedule.EdgeTime(6), Is.EqualTo(new SimulationTime(1, 0)));
            Assert.That(schedule.EdgeTime(6000), Is.EqualTo(new SimulationTime(1000, 0)));
        }

        [Test]
        public void ExactHalfPicosecondTiesRoundToNearestEven()
        {
            var schedule = new WorldClockEdgeSchedule("32.768", SimulationTime.Zero);
            Assert.That(schedule.EdgeTime(1), Is.EqualTo(new SimulationTime(0, 15_258_789_062)));
            Assert.That(schedule.EdgeTime(2), Is.EqualTo(new SimulationTime(0, 30_517_578_125)));
            Assert.That(schedule.EdgeTime(3), Is.EqualTo(new SimulationTime(0, 45_776_367_188)));
        }

        [Test]
        public void ExtendedPrecisionRepresentsFutureOneGigahertzHalfPeriod()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldClockEdgeSchedule("1000000000", SimulationTime.Zero));
            var precisionCheck = new WorldClockEdgeSchedule("1000000000",
                SimulationTime.Zero, false);
            Assert.That(precisionCheck.EdgeTime(1), Is.EqualTo(new SimulationTime(0, 500)));
            Assert.That(precisionCheck.EdgeTime(2), Is.EqualTo(new SimulationTime(0, 1000)));
            Assert.Throws<ArgumentException>(() =>
                new WorldClockEdgeSchedule("1000000000000", SimulationTime.Zero, false),
                "At 1 THz the first 0.5 ps edge would collapse onto time zero.");
        }

        [Test]
        public void RangeSyntaxAndTimestampOverflowFailWithoutWrapping()
        {
            new WorldClockEdgeSchedule("0.1", SimulationTime.Zero);
            new WorldClockEdgeSchedule("100", SimulationTime.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldClockEdgeSchedule("0.09", SimulationTime.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldClockEdgeSchedule("100.1", SimulationTime.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldClockEdgeSchedule("0", SimulationTime.Zero));
            Assert.Throws<FormatException>(() =>
                new WorldClockEdgeSchedule("1e1", SimulationTime.Zero));
            Assert.That(new SimulationTime(0, 999_999_999_999).AddPicoseconds(2),
                Is.EqualTo(new SimulationTime(1, 1)));
            Assert.Throws<OverflowException>(() =>
                new SimulationTime(ulong.MaxValue, 999_999_999_999).AddPicoseconds(BigInteger.One));
            Assert.Throws<OverflowException>(() =>
                new WorldClockEdgeSchedule("10",
                    new SimulationTime(ulong.MaxValue, 999_999_999_999)));
        }
    }
}
