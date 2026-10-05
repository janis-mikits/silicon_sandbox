using System;
using NUnit.Framework;
using SiliconSandbox.Application;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class FirstPlayableFrameMetricsTests
    {
        [Test]
        public void ReportsDistinctAverageAndTailFrameTimes()
        {
            var samples = new double[100];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = (i + 1) / 1000d;
            var measured = new FirstPlayableFrameMetrics(samples, 2d);
            Assert.That(measured.FrameCount, Is.EqualTo(100));
            Assert.That(measured.AverageFramesPerSecond, Is.EqualTo(50d));
            Assert.That(measured.MedianMilliseconds, Is.EqualTo(50d).Within(1e-9));
            Assert.That(measured.P95Milliseconds, Is.EqualTo(95d).Within(1e-9));
            Assert.That(measured.P99Milliseconds, Is.EqualTo(99d).Within(1e-9));
            Assert.That(measured.MaxMilliseconds, Is.EqualTo(100d).Within(1e-9));
        }

        [Test]
        public void EmptyAndInvalidMeasurementsCannotPassAcceptance()
        {
            Assert.Throws<ArgumentException>(() =>
                new FirstPlayableFrameMetrics(Array.Empty<double>(), 60d));
            Assert.Throws<ArgumentException>(() =>
                new FirstPlayableFrameMetrics(new[] { double.NaN }, 60d));
            Assert.Throws<ArgumentException>(() =>
                new FirstPlayableFrameMetrics(new[] { 0.016d }, 0d));
        }
    }
}
