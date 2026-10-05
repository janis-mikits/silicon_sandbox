using System;

namespace SiliconSandbox.Simulation
{
    // One timestamp may contain many acyclic worklist evaluations, but only a
    // bounded number of repeated feedback passes. This budget also stops a
    // worklist that never becomes empty (for example, a test-only inverter loop).
    public sealed class DeltaConvergenceGuard
    {
        private readonly long maximumEvaluations;
        private long evaluations;

        public DeltaConvergenceGuard(int maximumDeltaPasses,
            long evaluationsPerPass)
        {
            if (maximumDeltaPasses < 1 || evaluationsPerPass < 1)
                throw new ArgumentOutOfRangeException();
            maximumEvaluations = checked(maximumDeltaPasses *
                evaluationsPerPass);
        }

        public bool TryEvaluate()
        {
            if (evaluations >= maximumEvaluations) return false;
            evaluations++;
            return true;
        }
    }
}
