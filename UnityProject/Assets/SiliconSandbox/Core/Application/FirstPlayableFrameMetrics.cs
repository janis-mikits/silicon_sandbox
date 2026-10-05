using System;
using System.Collections.Generic;

namespace SiliconSandbox.Application
{
    // Frame-time measurements are kept separate from simulation throughput.
    // Inputs are elapsed real seconds for completed rendered frames.
    public sealed class FirstPlayableFrameMetrics
    {
        public int FrameCount { get; }
        public double DurationSeconds { get; }
        public double AverageFramesPerSecond => FrameCount / DurationSeconds;
        public double MedianMilliseconds { get; }
        public double P95Milliseconds { get; }
        public double P99Milliseconds { get; }
        public double MaxMilliseconds { get; }

        public FirstPlayableFrameMetrics(IEnumerable<double> frameSeconds,
            double measuredDurationSeconds)
        {
            if (frameSeconds == null || double.IsNaN(measuredDurationSeconds) ||
                double.IsInfinity(measuredDurationSeconds) ||
                measuredDurationSeconds <= 0d)
                throw new ArgumentException("Measured duration must be positive and finite.");
            var sorted = new List<double>();
            foreach (var seconds in frameSeconds)
            {
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) ||
                    seconds <= 0d)
                    throw new ArgumentException("Frame duration must be positive and finite.");
                sorted.Add(seconds * 1000d);
            }
            if (sorted.Count == 0)
                throw new ArgumentException("At least one rendered frame is required.");
            sorted.Sort();
            FrameCount = sorted.Count;
            DurationSeconds = measuredDurationSeconds;
            MedianMilliseconds = NearestRank(sorted, 0.5d);
            P95Milliseconds = NearestRank(sorted, 0.95d);
            P99Milliseconds = NearestRank(sorted, 0.99d);
            MaxMilliseconds = sorted[sorted.Count - 1];
        }

        private static double NearestRank(IReadOnlyList<double> sorted,
            double percentile) =>
            sorted[(int)Math.Ceiling(sorted.Count * percentile) - 1];
    }
}
