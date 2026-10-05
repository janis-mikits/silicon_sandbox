using System;
using System.Numerics;

namespace SiliconSandbox.Simulation
{
    public readonly struct SimulationTime : IComparable<SimulationTime>, IEquatable<SimulationTime>
    {
        public const long PicosecondsPerSecond = 1_000_000_000_000;
        public static readonly SimulationTime Zero = new SimulationTime(0, 0);

        public ulong WholeSeconds { get; }
        public long PicosecondsWithinSecond { get; }

        public SimulationTime(ulong wholeSeconds, long picosecondsWithinSecond)
        {
            if (picosecondsWithinSecond < 0 || picosecondsWithinSecond >= PicosecondsPerSecond)
                throw new ArgumentOutOfRangeException(nameof(picosecondsWithinSecond));
            WholeSeconds = wholeSeconds;
            PicosecondsWithinSecond = picosecondsWithinSecond;
        }

        public SimulationTime AddPicoseconds(BigInteger nonnegativePicoseconds)
        {
            if (nonnegativePicoseconds.Sign < 0)
                throw new ArgumentOutOfRangeException(nameof(nonnegativePicoseconds));
            var total = (BigInteger)PicosecondsWithinSecond + nonnegativePicoseconds;
            var seconds = (BigInteger)WholeSeconds + total / PicosecondsPerSecond;
            if (seconds > ulong.MaxValue)
                throw new OverflowException("Simulation time limit reached.");
            return new SimulationTime((ulong)seconds, (long)(total % PicosecondsPerSecond));
        }

        public BigInteger PicosecondsUntil(SimulationTime later)
        {
            if (later.CompareTo(this) < 0)
                throw new ArgumentOutOfRangeException(nameof(later));
            return ((BigInteger)later.WholeSeconds - WholeSeconds) *
                PicosecondsPerSecond + later.PicosecondsWithinSecond -
                PicosecondsWithinSecond;
        }

        public int CompareTo(SimulationTime other)
        {
            var seconds = WholeSeconds.CompareTo(other.WholeSeconds);
            return seconds != 0 ? seconds : PicosecondsWithinSecond.CompareTo(other.PicosecondsWithinSecond);
        }

        public bool Equals(SimulationTime other) => WholeSeconds == other.WholeSeconds &&
            PicosecondsWithinSecond == other.PicosecondsWithinSecond;
        public override bool Equals(object obj) => obj is SimulationTime other && Equals(other);
        public override int GetHashCode() => WholeSeconds.GetHashCode() * 397 ^
            PicosecondsWithinSecond.GetHashCode();
        public override string ToString() => WholeSeconds + "." +
            PicosecondsWithinSecond.ToString("D12") + " s";
    }
}
