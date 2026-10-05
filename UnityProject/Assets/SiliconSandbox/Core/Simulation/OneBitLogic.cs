using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    public enum ResolutionCause { Undriven, Known, ConflictingDrivers, UnknownDriver }

    public readonly struct ResolvedBit
    {
        public LogicBit Value { get; }
        public ResolutionCause Cause { get; }

        public ResolvedBit(LogicBit value, ResolutionCause cause)
        {
            Value = value;
            Cause = cause;
        }
    }

    public static class OneBitLogic
    {
        public static LogicBit And(LogicBit a, LogicBit b)
        {
            Validate(a);
            Validate(b);
            if (a == LogicBit.Zero || b == LogicBit.Zero) return LogicBit.Zero;
            if (a == LogicBit.One && b == LogicBit.One) return LogicBit.One;
            return LogicBit.X;
        }

        public static ResolvedBit Resolve(IReadOnlyList<LogicBit> drivers)
        {
            if (drivers == null) throw new ArgumentNullException(nameof(drivers));
            var hasZero = false;
            var hasOne = false;
            var hasX = false;
            for (var i = 0; i < drivers.Count; i++)
            {
                switch (drivers[i])
                {
                    case LogicBit.Zero: hasZero = true; break;
                    case LogicBit.One: hasOne = true; break;
                    case LogicBit.X: hasX = true; break;
                    case LogicBit.Z: break;
                    default: throw new ArgumentOutOfRangeException(nameof(drivers));
                }
            }
            if (hasX) return new ResolvedBit(LogicBit.X, ResolutionCause.UnknownDriver);
            if (hasZero && hasOne) return new ResolvedBit(LogicBit.X, ResolutionCause.ConflictingDrivers);
            if (hasZero) return new ResolvedBit(LogicBit.Zero, ResolutionCause.Known);
            if (hasOne) return new ResolvedBit(LogicBit.One, ResolutionCause.Known);
            return new ResolvedBit(LogicBit.Z, ResolutionCause.Undriven);
        }

        private static void Validate(LogicBit value)
        {
            if (value > LogicBit.Z) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
