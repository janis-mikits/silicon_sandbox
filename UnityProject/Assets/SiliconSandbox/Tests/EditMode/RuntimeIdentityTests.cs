using System;
using NUnit.Framework;
using SiliconSandbox.Contracts;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Tests.EditMode
{
    public sealed class RuntimeIdentityTests
    {
        [Test]
        public void SameBlueprintLocalIdsSeparateByInstancePathAndVersion()
        {
            var version = Guid.NewGuid(); var local = Guid.NewGuid();
            var firstInstance = Guid.NewGuid(); var secondInstance = Guid.NewGuid();
            var first = RuntimeObjectKey.Module(version, local, new[] { firstInstance });
            var second = RuntimeObjectKey.Module(version, local, new[] { secondInstance });
            var nested = RuntimeObjectKey.Module(version, local,
                new[] { firstInstance, secondInstance });
            var otherVersion = RuntimeObjectKey.Module(Guid.NewGuid(), local,
                new[] { firstInstance });
            Assert.That(first.Equals(second), Is.False);
            Assert.That(first.Equals(nested), Is.False);
            Assert.That(first.Equals(otherVersion), Is.False);
            Assert.That(first.Equals(RuntimeObjectKey.Module(version, local,
                new[] { firstInstance })), Is.True);
            Assert.Throws<ArgumentException>(() => RuntimeObjectKey.Module(version,
                local, Array.Empty<Guid>()));
        }

        [Test]
        public void SameLocalSourceInTwoInstancesRemainsTwoIndependentDrivers()
        {
            var version = Guid.NewGuid(); var local = Guid.NewGuid();
            var first = RuntimeObjectKey.Module(version, local, new[] { Guid.NewGuid() });
            var second = RuntimeObjectKey.Module(version, local, new[] { Guid.NewGuid() });
            var plan = new OneBitCircuitPlan(1,
                new[] { new SourceBinding(first, 0), new SourceBinding(second, 0) },
                Array.Empty<AndBinding>());
            var circuit = new GraphDrivenOneBitCircuit(plan);
            Assert.That(circuit.Net(0).Value, Is.EqualTo(LogicBit.Zero));
            circuit.SetSourceOn(second, true);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Source(first).IsOn, Is.False);
            Assert.That(circuit.Source(second).IsOn, Is.True);
            Assert.That(circuit.Net(0).Value, Is.EqualTo(LogicBit.X));
            Assert.That(circuit.Net(0).Cause, Is.EqualTo(ResolutionCause.ConflictingDrivers));
        }

        [Test]
        public void SameLocalSrInTwoInstancesKeepsSeparateQThroughGraphRefresh()
        {
            var version = Guid.NewGuid(); var local = Guid.NewGuid();
            var first = RuntimeObjectKey.Module(version, local, new[] { Guid.NewGuid() });
            var second = RuntimeObjectKey.Module(version, local, new[] { Guid.NewGuid() });
            var firstSet = Guid.NewGuid(); var firstReset = Guid.NewGuid();
            var secondSet = Guid.NewGuid(); var secondReset = Guid.NewGuid();
            var qPin = Guid.NewGuid(); var qBarPin = Guid.NewGuid();
            var clock = new WorldClockBinding(Guid.NewGuid(), Guid.NewGuid(), 4);
            var plan = new OneBitCircuitPlan(9,
                new[]
                {
                    new SourceBinding(firstSet, 0), new SourceBinding(firstReset, 1),
                    new SourceBinding(secondSet, 2), new SourceBinding(secondReset, 3)
                }, Array.Empty<AndBinding>(), new[]
                {
                    new SrBinding(first, 0, 1, 4, 5, 6, qPin, qBarPin),
                    new SrBinding(second, 2, 3, 4, 7, 8, qPin, qBarPin)
                }, clock);
            var circuit = new GraphDrivenOneBitCircuit(plan);
            circuit.SetSourceOn(firstSet, true);
            circuit.SetWorldClockLevel(LogicBit.One);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Storage(first).Q, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Storage(second).Q, Is.EqualTo(LogicBit.X));

            circuit.ReplacePlan(plan);
            Assert.That(circuit.Storage(first).Q, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Storage(second).Q, Is.EqualTo(LogicBit.X));
            circuit.SetWorldClockLevel(LogicBit.Zero);
            circuit.SetSourceOn(firstSet, false);
            circuit.SetSourceOn(secondReset, true);
            circuit.AdvanceToSettled();
            circuit.SetWorldClockLevel(LogicBit.One);
            circuit.AdvanceToSettled();
            Assert.That(circuit.Storage(first).Q, Is.EqualTo(LogicBit.One));
            Assert.That(circuit.Storage(second).Q, Is.EqualTo(LogicBit.Zero));
        }
    }
}
