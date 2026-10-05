using System;
using UnityEngine;

namespace SiliconSandbox.Presentation
{
    public enum WorldPartKind
    {
        ComponentBody,
        ComponentPin,
        ConnectorNode,
        ConnectorSpan
    }

    // A picking proxy has authored IDs only. Unity instance IDs never become
    // design, save, or electrical identities.
    public sealed class WorldSelectablePart : MonoBehaviour
    {
        public WorldPartKind Kind { get; private set; }
        public Guid OwnerId { get; private set; }
        public Guid PartId { get; private set; }

        public void Initialize(WorldPartKind kind, Guid ownerId, Guid partId)
        {
            Kind = kind;
            OwnerId = ownerId;
            PartId = partId;
        }
    }
}
