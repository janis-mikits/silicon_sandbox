using System;

namespace SiliconSandbox.Contracts
{
    public enum LogicBit : byte
    {
        Zero,
        One,
        X,
        Z
    }

    public static class LogicBitText
    {
        public static string ToSymbol(this LogicBit value)
        {
            switch (value)
            {
                case LogicBit.Zero: return "0";
                case LogicBit.One: return "1";
                case LogicBit.X: return "X";
                case LogicBit.Z: return "Z";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
