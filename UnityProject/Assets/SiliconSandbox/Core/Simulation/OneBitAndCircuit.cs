using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    // Zero-time, event-driven fixture. The later world scheduler will own time slots.
    public sealed class OneBitAndCircuit
    {
        private enum Work { SourceA, SourceB, Gate }

        private readonly Queue<Work> pending = new Queue<Work>();
        private readonly AndCircuitWiring wiring;
        private readonly OneBitNet aNet;
        private readonly OneBitNet bNet;
        private readonly OneBitNet yNet;

        public ConstantLogicSource SourceA { get; } = new ConstantLogicSource();
        public ConstantLogicSource SourceB { get; } = new ConstantLogicSource();
        public ResolvedBit A => aNet.Resolution;
        public ResolvedBit B => bNet.Resolution;
        public ResolvedBit Y => yNet.Resolution;
        public Guid ANetConnectorId => aNet.ConnectorId;
        public Guid BNetConnectorId => bNet.ConnectorId;
        public Guid YNetConnectorId => yNet.ConnectorId;

        public OneBitAndCircuit(AndCircuitWiring wiring)
        {
            this.wiring = wiring ?? throw new ArgumentNullException(nameof(wiring));
            aNet = new OneBitNet(wiring.ANetConnectorId);
            bNet = new OneBitNet(wiring.BNetConnectorId);
            yNet = new OneBitNet(wiring.YNetConnectorId);
            pending.Enqueue(Work.SourceA);
            pending.Enqueue(Work.SourceB);
            AdvanceToSettled();
        }

        public void ConfigureA(LogicBit onValue, bool initialOn)
        {
            SourceA.Configure(onValue, initialOn);
            pending.Enqueue(Work.SourceA);
        }

        public void ConfigureB(LogicBit onValue, bool initialOn)
        {
            SourceB.Configure(onValue, initialOn);
            pending.Enqueue(Work.SourceB);
        }

        public void SetAOn(bool on)
        {
            SourceA.SetOn(on);
            pending.Enqueue(Work.SourceA);
        }

        public void SetBOn(bool on)
        {
            SourceB.SetOn(on);
            pending.Enqueue(Work.SourceB);
        }

        public void ResetSources()
        {
            SourceA.Reset();
            SourceB.Reset();
            pending.Enqueue(Work.SourceA);
            pending.Enqueue(Work.SourceB);
        }

        public void AdvanceToSettled()
        {
            while (pending.Count > 0)
            {
                switch (pending.Dequeue())
                {
                    case Work.SourceA:
                        aNet.SetDriver(wiring.SourceAId, SourceA.Drive);
                        pending.Enqueue(Work.Gate);
                        break;
                    case Work.SourceB:
                        bNet.SetDriver(wiring.SourceBId, SourceB.Drive);
                        pending.Enqueue(Work.Gate);
                        break;
                    case Work.Gate:
                        yNet.SetDriver(wiring.GateId, OneBitLogic.And(A.Value, B.Value));
                        break;
                }
            }
        }
    }
}
