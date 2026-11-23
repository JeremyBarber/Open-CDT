using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic.Physics;

using System.Runtime.InteropServices;
using Xunit;

namespace CDT_2plus1_Tests.TestHarnesses.StructOverlays;

/// <summary>
/// Testing-only memory overlay that mirrors the exact sequential field layout of <see cref="Dynamics"/>.
/// Allows white-box inspection in tests while keeping production <see cref="Dynamics"/> fields strictly <see langword="private"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DynamicsOverlay
{
    public MoveWeightsOverlay Move23Weights;
    public MoveWeightsOverlay Move32Weights;
    public MoveWeightsOverlay Move26Weights;
    public MoveWeightsOverlay Move62Weights;

    static DynamicsOverlay()
    {
        Assert.Equal(sizeof(MoveWeights), sizeof(MoveWeightsOverlay));
        Assert.Equal(sizeof(Dynamics), sizeof(DynamicsOverlay));
    }
}