using System;
using UnityEngine;

namespace SiliconSandbox.Presentation
{
    public enum WorldPartKind
    {
        ComponentBody,
        ComponentPin,
        ModuleBody,
        ModulePort,
        ConnectorNode,
        ConnectorSpan
    }

    // A picking proxy has authored IDs only. Unity instance IDs never become
    // design, save, or electrical identities.
    public sealed class WorldSelectablePart : MonoBehaviour
    {
        private MaterialPropertyBlock feedback;
        private Renderer partRenderer;
        private float invalidUntil;
        private bool wasInvalid;
        private bool hasSignalColor;
        private Color signalColor;

        public WorldPartKind Kind { get; private set; }
        public Guid OwnerId { get; private set; }
        public Guid PartId { get; private set; }

        private void Awake()
        {
            feedback = new MaterialPropertyBlock();
        }

        public void Initialize(WorldPartKind kind, Guid ownerId, Guid partId)
        {
            Kind = kind;
            OwnerId = ownerId;
            PartId = partId;
            partRenderer = GetComponent<Renderer>();
        }

        public void FlashInvalid() => invalidUntil = Time.unscaledTime + 0.18f;

        public void SetSignalColor(Color color)
        {
            signalColor = color;
            hasSignalColor = true;
            if (Time.unscaledTime >= invalidUntil)
                ApplySignalColor();
        }

        private void ApplySignalColor()
        {
            feedback.Clear();
            feedback.SetColor("_Color", signalColor);
            feedback.SetColor("_BaseColor", signalColor);
            partRenderer.SetPropertyBlock(feedback);
        }

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
                if (hasSignalColor) ApplySignalColor();
                else partRenderer.SetPropertyBlock(null);
                wasInvalid = false;
            }
        }
    }
}
