using System.Collections.Generic;
using NUnit.Framework;
using SiliconSandbox.Contracts;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitLogicTests
    {
        private static readonly LogicBit[] Values = { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.Z };
        private static readonly LogicBit[,] AndExpected =
        {
            { LogicBit.Zero, LogicBit.Zero, LogicBit.Zero, LogicBit.Zero },
            { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.X },
            { LogicBit.Zero, LogicBit.X, LogicBit.X, LogicBit.X },
            { LogicBit.Zero, LogicBit.X, LogicBit.X, LogicBit.X }
        };
        private static readonly LogicBit[,] ResolutionExpected =
        {
            { LogicBit.Zero, LogicBit.X, LogicBit.X, LogicBit.Zero },
            { LogicBit.X, LogicBit.One, LogicBit.X, LogicBit.One },
            { LogicBit.X, LogicBit.X, LogicBit.X, LogicBit.X },
            { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.Z }
        };

        public static IEnumerable<TestCaseData> OrderedPairs()
        {
            for (var a = 0; a < 4; a++)
                for (var b = 0; b < 4; b++)
                    yield return new TestCaseData(a, b).SetName("A" + Values[a] + "_B" + Values[b]);
        }

        [TestCaseSource(nameof(OrderedPairs))]
        public void AndMatchesLiteralSixteenEntryTable(int a, int b)
        {
            Assert.That(OneBitLogic.And(Values[a], Values[b]), Is.EqualTo(AndExpected[a, b]));
        }

        [TestCaseSource(nameof(OrderedPairs))]
        public void TwoDriverResolutionMatchesSeparateLiteralTable(int a, int b)
        {
            var result = OneBitLogic.Resolve(new[] { Values[a], Values[b] });
            Assert.That(result.Value, Is.EqualTo(ResolutionExpected[a, b]));
        }

        [Test]
        public void EmptyAndReleasedNetAreUndrivenZ()
        {
            Assert.That(OneBitLogic.Resolve(new LogicBit[0]).Value, Is.EqualTo(LogicBit.Z));
            Assert.That(OneBitLogic.Resolve(new[] { LogicBit.Z, LogicBit.Z }).Cause,
                Is.EqualTo(ResolutionCause.Undriven));
            Assert.That(OneBitLogic.Resolve(new[] { LogicBit.Zero, LogicBit.Z, LogicBit.Zero }).Value,
                Is.EqualTo(LogicBit.Zero));
        }

        [Test]
        public void ConflictAndUnknownDriverHaveDifferentCauses()
        {
            var conflict = OneBitLogic.Resolve(new[] { LogicBit.Zero, LogicBit.One, LogicBit.Z });
            var unknown = OneBitLogic.Resolve(new[] { LogicBit.Zero, LogicBit.X });
            Assert.That(conflict.Value, Is.EqualTo(LogicBit.X));
            Assert.That(conflict.Cause, Is.EqualTo(ResolutionCause.ConflictingDrivers));
            Assert.That(unknown.Value, Is.EqualTo(LogicBit.X));
            Assert.That(unknown.Cause, Is.EqualTo(ResolutionCause.UnknownDriver));
        }

        [Test]
        public void RemovingLastDriverReturnsNetToZ()
        {
            var net = new OneBitNet(System.Guid.Parse("e4dbdc2e-344c-4ce4-83a7-2e335de1b18c"));
            var driver = System.Guid.Parse("93ba5ca8-a903-408b-a1be-4caad96c5550");
            net.SetDriver(driver, LogicBit.One);
            Assert.That(net.Resolution.Value, Is.EqualTo(LogicBit.One));
            net.RemoveDriver(driver);
            Assert.That(net.Resolution.Value, Is.EqualTo(LogicBit.Z));
            Assert.That(net.Resolution.Cause, Is.EqualTo(ResolutionCause.Undriven));
        }
    }
}
