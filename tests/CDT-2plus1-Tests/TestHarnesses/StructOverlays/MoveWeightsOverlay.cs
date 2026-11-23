using System.Runtime.InteropServices;

namespace CDT_2plus1_Tests.TestHarnesses.StructOverlays;

/// <summary>
/// Testing-only memory overlay that mirrors the exact sequential layout of <see cref="MoveWeights"/>
/// to expose the private <c>_expDeltaS</c> lookup table pointer for white-box assertions.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct MoveWeightsOverlay
{
    public int VolumeTarget;
    public int VolumeRadius;
    public double ReggeDelta;
    public double BaseVolumeS;
    public double DriftFactor;
    public uint* ExpDeltaS;
}
