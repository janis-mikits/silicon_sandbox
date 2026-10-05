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
        private readonly MaterialPropertyBlock feedback = new MaterialPropertyBlock();
        private Renderer partRenderer;
        private float invalidUntil;
        private bool wasInvalid;

        public WorldPartKind Kind { get; private set; }
        public Guid OwnerId { get; private set; }
        public Guid PartId { get; private set; }

        public void Initialize(WorldPartKind kind, Guid ownerId, Guid partId)
        {
            Kind = kind;
            OwnerId = ownerId;
            PartId = partId;
            partRenderer = GetComponent<Renderer>();
        }

        public void FlashInvalid() => invalidUntil = Time.unscaledTime + 0.18f;

        private void LateUpdate()
        {
            if (partRenderer == null) return;
            if (Time.unscaledTime < invalidUntil)
            {
                feedback.Clear();
                feedback.SetColor("_Color", Color.red);
                feedback.SetColor("_BaseColor", Color.red);
                partRenderer.SetPropertyBlock(feedback);
                wasInvalid = true;
            }
            else if (wasInvalid)
            {
                partRenderer.SetPropertyBlock(null);
                wasInvalid = false;
            }
        }
    }
}
