using NUnit.Framework;
using SiliconSandbox.Contracts;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class OneBitSrFlipFlopTests
    {
        [Test]
        public void AllSixteenOrderedClockTransitionsUseLiteralPositiveEdgeTable()
        {
            var bits = new[] { LogicBit.Zero, LogicBit.One, LogicBit.X, LogicBit.Z };
            var expected = new[]
            {
                new[] { false, true, true, true },
                new[] { false, false, false, false },
                new[] { false, true, false, false },
                new[] { false, true, false, false }
            };
            for (var previous = 0; previous < 4; previous++)
                for (var current = 0; current < 4; current++)
                {
                    var storage = new OneBitSrFlipFlop(LogicBit.Zero);
                    storage.AdvanceClock(bits[previous], LogicBit.Zero, LogicBit.Zero);
                    var sampled = storage.AdvanceClock(bits[current], LogicBit.One, LogicBit.Zero);
                    Assert.That(FourStateClockEdges.IsPositiveEdge(bits[previous], bits[current]),
                        Is.EqualTo(expected[previous][current]),
                        bits[previous] + "->" + bits[current]);
                    Assert.That(sampled, Is.EqualTo(expected[previous][current]));
                    Assert.That(storage.Q,
                        Is.EqualTo(expected[previous][current] ? LogicBit.One : LogicBit.Zero));
                }
        }

        [Test]
        public void KnownSrRowsHoldResetSetAndInvalidateOnlyOnRisingEdge()
        {
            var storage = new OneBitSrFlipFlop();
            Assert.That(storage.Q, Is.EqualTo(LogicBit.X));
            storage.AdvanceClock(LogicBit.One, LogicBit.Zero, LogicBit.Zero);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.X), "00 holds uninitialized X.");
            storage.AdvanceClock(LogicBit.Zero, LogicBit.One, LogicBit.Zero);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.X), "Falling edge does not sample S.");
            storage.AdvanceClock(LogicBit.One, LogicBit.One, LogicBit.Zero);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.One));
            Assert.That(storage.QBar, Is.EqualTo(LogicBit.Zero));
            storage.AdvanceClock(LogicBit.One, LogicBit.Zero, LogicBit.One);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.One), "Held-high clock does not sample R.");
            storage.AdvanceClock(LogicBit.Zero, LogicBit.Zero, LogicBit.One);
            storage.AdvanceClock(LogicBit.One, LogicBit.Zero, LogicBit.One);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.Zero));
            storage.AdvanceClock(LogicBit.Zero, LogicBit.One, LogicBit.One);
            storage.AdvanceClock(LogicBit.One, LogicBit.One, LogicBit.One);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.X));
            Assert.That(storage.QBar, Is.EqualTo(LogicBit.X));
        }

        [Test]
        public void UncertainControlsMergeEveryPermittedKnownRow()
        {
            Assert.That(OneBitSrFlipFlop.NextState(LogicBit.One, LogicBit.X, LogicBit.Zero),
                Is.EqualTo(LogicBit.One), "Hold and set agree at one.");
            Assert.That(OneBitSrFlipFlop.NextState(LogicBit.Zero, LogicBit.X, LogicBit.Zero),
                Is.EqualTo(LogicBit.X), "Hold zero and set one disagree.");
            Assert.That(OneBitSrFlipFlop.NextState(LogicBit.Zero, LogicBit.Zero, LogicBit.Z),
                Is.EqualTo(LogicBit.Zero), "Hold and reset agree at zero.");
            Assert.That(OneBitSrFlipFlop.NextState(LogicBit.One, LogicBit.Zero, LogicBit.X),
                Is.EqualTo(LogicBit.X), "Hold one and reset zero disagree.");
            Assert.That(OneBitSrFlipFlop.NextState(LogicBit.One, LogicBit.Z, LogicBit.One),
                Is.EqualTo(LogicBit.X), "Invalid 11 remains a possible row.");
            Assert.That(OneBitSrFlipFlop.NextState(LogicBit.X, LogicBit.One, LogicBit.Zero),
                Is.EqualTo(LogicBit.One), "Known set recovers from X.");
        }

        [Test]
        public void ResetRestoresAuthoredInitializationAndClockZero()
        {
            var storage = new OneBitSrFlipFlop(LogicBit.Z);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.Z));
            Assert.That(storage.QBar, Is.EqualTo(LogicBit.X));
            storage.AdvanceClock(LogicBit.One, LogicBit.Zero, LogicBit.Zero);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.Z), "Known hold preserves explicit Z.");
            storage.AdvanceClock(LogicBit.Zero, LogicBit.One, LogicBit.Zero);
            storage.AdvanceClock(LogicBit.One, LogicBit.One, LogicBit.Zero);
            Assert.That(storage.Q, Is.EqualTo(LogicBit.One));
            storage.Reset();
            Assert.That(storage.Q, Is.EqualTo(LogicBit.Z));
            Assert.That(storage.PreviousClock, Is.EqualTo(LogicBit.Zero));
        }
    }
}
