using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    public sealed class ConstantLogicSource
    {
        public LogicBit ConfiguredOnValue { get; private set; }
        public bool InitialOn { get; private set; }
        public bool IsOn { get; private set; }
        public LogicBit Drive => IsOn ? ConfiguredOnValue : LogicBit.Zero;

        public ConstantLogicSource(LogicBit onValue = LogicBit.One, bool initialOn = false)
        {
            Configure(onValue, initialOn);
        }

        public void Configure(LogicBit onValue, bool initialOn)
        {
            if (onValue > LogicBit.Z) throw new System.ArgumentOutOfRangeException(nameof(onValue));
            ConfiguredOnValue = onValue;
            InitialOn = initialOn;
            IsOn = initialOn;
        }

        public void SetOn(bool on) => IsOn = on;
        public void Reset() => IsOn = InitialOn;
    }
}
