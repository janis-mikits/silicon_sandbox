using SiliconSandbox.Contracts;
using UnityEngine;

namespace SiliconSandbox.Presentation
{
    // Imported presentation assets. Authored identities, pin locations, and
    // electrical topology stay in Core; no FBX hierarchy is a game contract.
    public sealed class OneBitVisualArt : ScriptableObject
    {
        public Mesh SourceBody;
        public Mesh AndBody;
        public Mesh SrBody;
        public Mesh ModuleBody;
        public Mesh SourceZero;
        public Mesh SourceOne;
        public Mesh SourceX;
        public Mesh SourceZ;
        public Mesh Pin;
        public Mesh WireStraight;
        public Mesh WireElbow;
        public Mesh Junction;
        public Mesh IdentityRing;
        public Material AtlasMaterial;
        public Material TintMaterial;

        public bool IsComplete => SourceBody != null && AndBody != null &&
            SrBody != null && ModuleBody != null && SourceZero != null &&
            SourceOne != null && SourceX != null && SourceZ != null &&
            Pin != null && WireStraight != null && WireElbow != null &&
            Junction != null && IdentityRing != null &&
            AtlasMaterial != null && TintMaterial != null;

        public Mesh SourceValue(LogicBit value)
        {
            switch (value)
            {
                case LogicBit.Zero: return SourceZero;
                case LogicBit.One: return SourceOne;
                case LogicBit.Z: return SourceZ;
                default: return SourceX;
            }
        }
    }
}
