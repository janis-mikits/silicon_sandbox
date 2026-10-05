using System;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    // Edge-sampled one-bit storage. The world scheduler supplies settled S/R and
    // CLK values after combinational propagation, before committing edge updates.
    public sealed class OneBitSrFlipFlop
    {
        private readonly LogicBit initialQ;
        private LogicBit previousClock;

        public LogicBit Q { get; private set; }
        public LogicBit QBar => Q == LogicBit.Zero ? LogicBit.One :
            Q == LogicBit.One ? LogicBit.Zero : LogicBit.X;
        public LogicBit PreviousClock => previousClock;

        public OneBitSrFlipFlop(LogicBit? authoredInitialQ = null)
        {
            if (authoredInitialQ.HasValue && authoredInitialQ.Value > LogicBit.Z)
                throw new ArgumentOutOfRangeException(nameof(authoredInitialQ));
            initialQ = authoredInitialQ ?? LogicBit.X;
            Reset();
        }

        public void Reset()
        {
            Q = initialQ;
            previousClock = LogicBit.Zero;
        }

        internal OneBitSrFlipFlop CopyWithInitialQ(LogicBit? authoredInitialQ)
        {
            var copy = new OneBitSrFlipFlop(authoredInitialQ);
            copy.Q = Q;
            copy.previousClock = previousClock;
            return copy;
        }

        internal void InitializeClockBaseline(LogicBit settledClock)
        {
            if (settledClock > LogicBit.Z)
                throw new ArgumentOutOfRangeException(nameof(settledClock));
            previousClock = settledClock;
        }

        // Returns whether the transition caused an edge sample.
        public bool AdvanceClock(LogicBit currentClock, LogicBit s, LogicBit r)
        {
            var sample = PreviewClock(currentClock, s, r, out var nextQ);
            CommitClock(currentClock, nextQ);
            return sample;
        }

        // Preview all storage components before any output update. The world
        // simulator commits previews together, then propagates their outputs.
        public bool PreviewClock(LogicBit currentClock, LogicBit s, LogicBit r,
            out LogicBit nextQ)
        {
            if (currentClock > LogicBit.Z || s > LogicBit.Z || r > LogicBit.Z)
                throw new ArgumentOutOfRangeException();
            var sample = FourStateClockEdges.IsPositiveEdge(previousClock, currentClock);
            nextQ = sample ? NextState(Q, s, r) : Q;
            return sample;
        }

        public void CommitClock(LogicBit currentClock, LogicBit nextQ)
        {
            if (currentClock > LogicBit.Z || nextQ > LogicBit.Z)
                throw new ArgumentOutOfRangeException();
            previousClock = currentClock;
            Q = nextQ;
        }

        public static LogicBit NextState(LogicBit priorQ, LogicBit s, LogicBit r)
        {
            if (priorQ > LogicBit.Z || s > LogicBit.Z || r > LogicBit.Z)
                throw new ArgumentOutOfRangeException();
            var first = true;
            var result = LogicBit.X;
            for (var set = 0; set <= 1; set++)
            {
                if (s <= LogicBit.One && (int)s != set) continue;
                for (var reset = 0; reset <= 1; reset++)
                {
                    if (r <= LogicBit.One && (int)r != reset) continue;
                    var candidate = KnownNext(priorQ, set, reset);
                    if (first) { result = candidate; first = false; }
                    else if (candidate != result) return LogicBit.X;
                }
            }
            return result;
        }

        private static LogicBit KnownNext(LogicBit priorQ, int set, int reset)
        {
            if (set == 1 && reset == 1) return LogicBit.X;
            if (set == 1) return LogicBit.One;
            if (reset == 1) return LogicBit.Zero;
            return priorQ;
        }
    }
}
