using System;

namespace SiliconSandbox.Contracts
{
    // Runtime indexes from an authored fixture. Connector IDs remain authored identities.
    public sealed class AndCircuitWiring
    {
        public Guid SourceAId { get; }
        public Guid SourceBId { get; }
        public Guid GateId { get; }
        public Guid ANetConnectorId { get; }
        public Guid BNetConnectorId { get; }
        public Guid YNetConnectorId { get; }

        public AndCircuitWiring(Guid sourceAId, Guid sourceBId, Guid gateId,
            Guid aNetConnectorId, Guid bNetConnectorId, Guid yNetConnectorId)
        {
            SourceAId = sourceAId;
            SourceBId = sourceBId;
            GateId = gateId;
            ANetConnectorId = aNetConnectorId;
            BNetConnectorId = bNetConnectorId;
            YNetConnectorId = yNetConnectorId;
        }
    }
}
