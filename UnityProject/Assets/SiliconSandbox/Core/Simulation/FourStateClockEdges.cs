using System;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    public static class FourStateClockEdges
    {
        // Accepted positive edges: 0->1/X/Z and X/Z->1.
        public static bool IsPositiveEdge(LogicBit previous, LogicBit current)
        {
            if (previous > LogicBit.Z || current > LogicBit.Z)
                throw new ArgumentOutOfRangeException();
            return previous == LogicBit.Zero && current != LogicBit.Zero ||
                (previous == LogicBit.X || previous == LogicBit.Z) && current == LogicBit.One;
        }
    }
}
