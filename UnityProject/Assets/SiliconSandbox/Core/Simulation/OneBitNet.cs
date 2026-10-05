using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    public sealed class OneBitNet
    {
        private readonly Dictionary<RuntimeDriverKey, LogicBit> drivers =
            new Dictionary<RuntimeDriverKey, LogicBit>();

        public Guid ConnectorId { get; }
        public ResolvedBit Resolution { get; private set; } = new ResolvedBit(LogicBit.Z, ResolutionCause.Undriven);

        public OneBitNet(Guid connectorId)
        {
            if (connectorId == Guid.Empty) throw new ArgumentException("Connector ID is empty.");
            ConnectorId = connectorId;
        }

        public void SetDriver(Guid driverId, LogicBit value)
        {
            if (driverId == Guid.Empty) throw new ArgumentException("Driver ID is empty.");
            SetDriver(new RuntimeDriverKey(RuntimeObjectKey.World(driverId), Guid.Empty), value);
        }

        public void SetDriver(RuntimeDriverKey driver, LogicBit value)
        {
            if (driver.Object.LocalObjectId == Guid.Empty)
                throw new ArgumentException("Driver identity is empty.");
            drivers[driver] = value;
            Recompute();
        }

        public void RemoveDriver(Guid driverId)
        {
            drivers.Remove(new RuntimeDriverKey(RuntimeObjectKey.World(driverId), Guid.Empty));
            Recompute();
        }

        public void RemoveDriver(RuntimeDriverKey driver)
        {
            if (driver.Object.LocalObjectId == Guid.Empty)
                throw new ArgumentException("Driver identity is empty.");
            drivers.Remove(driver);
            Recompute();
        }

        private void Recompute()
        {
            var values = new LogicBit[drivers.Count];
            drivers.Values.CopyTo(values, 0);
            Resolution = OneBitLogic.Resolve(values);
        }
    }
}
