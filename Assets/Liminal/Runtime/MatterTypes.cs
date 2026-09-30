using System.Runtime.InteropServices;
using UnityEngine;

namespace Liminal
{
    public enum MatterPhase { Form = 0, Scatter = 1, Transfer = 2, Settled = 3, Dolphin = 4, Recall = 5 }

    [StructLayout(LayoutKind.Sequential)]
    public struct MatterSeed
    {
        public Vector4 form; // Local xyz and particle radius.
        public Vector4 destination; // Permanent world-space resting form.
        public Vector4 color;
        public Vector4 traits; // Group, kind (dust/jelly/fish/whale), seed, tail weight.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MatterGroup
    {
        public Matrix4x4 localToWorld;
        public Vector4 state; // Phase, phase age, energy, reserved.
        public Vector4 impulse; // World-space origin and strength.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MatterParticle
    {
        public Vector4 positionAge;
        public Vector4 velocityEnergy;
        public Vector4 colorSize;
        public Vector4 identityState; // Immutable ID, immutable group, phase, influence memory.
    }
}
