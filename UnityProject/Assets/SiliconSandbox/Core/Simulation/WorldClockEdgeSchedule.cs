using System;
using System.Numerics;

namespace SiliconSandbox.Simulation
{
    // Exact decimal request -> rational half-period. Every edge is rounded from
    // its absolute index; rounded intervals are never accumulated.
    public sealed class WorldClockEdgeSchedule
    {
        private readonly BigInteger frequencyNumerator;
        private readonly BigInteger frequencyDenominator;

        public string RequestedFrequencyHz { get; }
        public SimulationTime Origin { get; }

        public WorldClockEdgeSchedule(string requestedFrequencyHz, SimulationTime origin,
            bool enforceFirstPlayableRange = true)
        {
            RequestedFrequencyHz = requestedFrequencyHz ??
                throw new ArgumentNullException(nameof(requestedFrequencyHz));
            ParseDecimal(requestedFrequencyHz, out frequencyNumerator,
                out frequencyDenominator);
            if (frequencyNumerator.Sign <= 0 ||
                (enforceFirstPlayableRange &&
                 (frequencyNumerator * 10 < frequencyDenominator ||
                  frequencyNumerator > frequencyDenominator * 100)))
                throw new ArgumentOutOfRangeException(nameof(requestedFrequencyHz),
                    "First-playable world clock frequency must be 0.1 through 100 Hz.");
            Origin = origin;
            if (EdgeTime(BigInteger.One).CompareTo(origin) <= 0)
                throw new ArgumentException("Clock edges collapse at 1 ps resolution.");
        }

        public SimulationTime EdgeTime(BigInteger edgeNumber)
        {
            if (edgeNumber < 1) throw new ArgumentOutOfRangeException(nameof(edgeNumber));
            var current = EdgeOffset(edgeNumber);
            if (current <= EdgeOffset(edgeNumber - 1))
                throw new ArgumentException("Clock edges collapse at 1 ps resolution.");
            return Origin.AddPicoseconds(current);
        }

        public static bool LevelAfterEdge(BigInteger edgeNumber)
        {
            if (edgeNumber < 1) throw new ArgumentOutOfRangeException(nameof(edgeNumber));
            return !edgeNumber.IsEven;
        }

        private BigInteger EdgeOffset(BigInteger edgeNumber)
        {
            var numerator = edgeNumber * SimulationTime.PicosecondsPerSecond *
                frequencyDenominator;
            var denominator = 2 * frequencyNumerator;
            var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
            // Exact half-ps ties use nearest-even to avoid a directional bias.
            if (remainder * 2 > denominator ||
                (remainder * 2 == denominator && !quotient.IsEven)) quotient++;
            return quotient;
        }

        private static void ParseDecimal(string text, out BigInteger numerator,
            out BigInteger denominator)
        {
            numerator = BigInteger.Zero;
            denominator = BigInteger.One;
            var decimalSeen = false;
            var digitSeen = false;
            foreach (var character in text)
            {
                if (character == '.' && !decimalSeen)
                { decimalSeen = true; continue; }
                if (character < '0' || character > '9')
                    throw new FormatException("Clock frequency must be an unsigned decimal string.");
                digitSeen = true;
                numerator = numerator * 10 + (character - '0');
                if (decimalSeen) denominator *= 10;
            }
            if (!digitSeen) throw new FormatException("Clock frequency has no digits.");
            var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
            if (divisor > 1) { numerator /= divisor; denominator /= divisor; }
        }
    }
}
