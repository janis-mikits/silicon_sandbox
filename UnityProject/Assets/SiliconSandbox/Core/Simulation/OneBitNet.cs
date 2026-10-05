using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Simulation
{
    public sealed class OneBitNet
    {
        private readonly Dictionary<Guid, LogicBit> drivers = new Dictionary<Guid, LogicBit>();

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
            drivers[driverId] = value;
            Recompute();
        }

        public void RemoveDriver(Guid driverId)
        {
            drivers.Remove(driverId);
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
