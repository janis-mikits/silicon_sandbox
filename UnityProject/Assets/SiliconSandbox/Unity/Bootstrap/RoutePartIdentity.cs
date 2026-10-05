using System;
using UnityEngine;

namespace SiliconSandbox.Bootstrap
{
    // A selectable primitive points back to its authored span, not a derived net.
    public sealed class RoutePartIdentity : MonoBehaviour
    {
        public Guid ConnectorId { get; private set; }
        public Guid SpanId { get; private set; }
        public Guid NodeId { get; private set; }

        public void InitializeSpan(Guid connectorId, Guid spanId)
        {
            ConnectorId = connectorId;
            SpanId = spanId;
        }

        public void InitializeNode(Guid connectorId, Guid nodeId)
        {
            ConnectorId = connectorId;
            NodeId = nodeId;
        }
    }
}
