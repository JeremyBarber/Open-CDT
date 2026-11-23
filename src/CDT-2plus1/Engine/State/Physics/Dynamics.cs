using CDT_2plus1.Engine.State.Physics.Actions;
using CDT_2plus1.Engine.Topology.Pachner.Move23;
using CDT_2plus1.Engine.Topology.Pachner.Move26;
using CDT_2plus1.Engine.Topology.Pachner.Move32;
using CDT_2plus1.Engine.Topology.Pachner.Move62;
using CDT_Agnostic.Physics;

using System.Runtime.InteropServices;

using static CDT_Agnostic.RuntimeMemory.Native.Allocation;

namespace CDT_2plus1.Engine.State.Physics;

/// <summary>
/// Manages the unmanaged lifecycle of the 64-byte cache-line-aligned <see cref="MoveWeights"/>
/// Look-Up Tables (LUTs) for all volume-changing 2+1D Pachner moves.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct Dynamics
{
    /// <summary>
    /// Number of Gaussian standard deviations (<c>5σ</c>, where <c>σ = 1 / √(2ε)</c>) precomputed around <c>TargetVolume</c>.
    /// </summary>
    private const double FluctuationSigmaMultiplier = 5.0;

    /// <summary>
    /// Upper bound on precomputed LUT radius (32 KB per move) to guard against tiny <c>ε</c> values.
    /// </summary>
    private const int MaxVolumeRadius = 4096;

    private MoveWeights Move23Weights;
    private MoveWeights Move32Weights;
    private MoveWeights Move26Weights;
    private MoveWeights Move62Weights;

    /// <summary>
    /// Allocates the unmanaged <see cref="Dynamics"/> state and its four 64-byte aligned <see cref="MoveWeights"/> tables.
    /// </summary>
    /// <param name="constraints">The initial physical coupling parameters.</param>
    /// <returns>An unmanaged pointer to the initialized <see cref="Dynamics"/> struct.</returns>
    internal static Dynamics* Create(in Constraints constraints)
    {
        var state = Allocate<Dynamics>(1);

        var rawRadius = 0;
        if (constraints.Epsilon > 0.0)
        {
            var sigma = 1.0 / Math.Sqrt(2.0 * constraints.Epsilon);
            var targetRadius = Math.Ceiling(FluctuationSigmaMultiplier * sigma);
            rawRadius = (int)Math.Min(MaxVolumeRadius, targetRadius);
        }

        state->Move23Weights = MoveWeights.Allocate(rawRadius);
        state->Move32Weights = MoveWeights.Allocate(rawRadius);
        state->Move26Weights = MoveWeights.Allocate(rawRadius);
        state->Move62Weights = MoveWeights.Allocate(rawRadius);

        state->Move23Weights.BuildWeights(
            reggeDelta: Regge.CalculateDeltaAction(in constraints, in Move23.Deltas),
            volumeTarget: constraints.TargetVolume,
            volumeDeltaForMove: Move23.Deltas.DeltaN3,
            epsilon: constraints.Epsilon);

        state->Move32Weights.BuildWeights(
            reggeDelta: Regge.CalculateDeltaAction(in constraints, in Move32.Deltas),
            volumeTarget: constraints.TargetVolume,
            volumeDeltaForMove: Move32.Deltas.DeltaN3,
            epsilon: constraints.Epsilon);

        state->Move26Weights.BuildWeights(
            reggeDelta: Regge.CalculateDeltaAction(in constraints, in Move26.Deltas),
            volumeTarget: constraints.TargetVolume,
            volumeDeltaForMove: Move26.Deltas.DeltaN3,
            epsilon: constraints.Epsilon);

        state->Move62Weights.BuildWeights(
            reggeDelta: Regge.CalculateDeltaAction(in constraints, in Move62.Deltas),
            volumeTarget: constraints.TargetVolume,
            volumeDeltaForMove: Move62.Deltas.DeltaN3,
            epsilon: constraints.Epsilon);

        return state;
    }

    /// <summary>
    /// Disposes all four <see cref="MoveWeights"/> buffers, frees the <see cref="Dynamics"/> struct,
    /// and nulls the caller's pointer reference.
    /// </summary>
    /// <param name="state">Reference to the <see cref="Dynamics"/> pointer to destroy (safe to pass <see langword="null"/>).</param>
    internal static void Destroy(ref Dynamics* state)
    {
        if (state == null)
            return;

        state->Dispose();
        NativeMemory.Free(state);

        state = null;
    }

    private void Dispose()
    {
        Move23Weights.Dispose();
        Move23Weights = default;

        Move32Weights.Dispose();
        Move32Weights = default;

        Move26Weights.Dispose();
        Move26Weights = default;

        Move62Weights.Dispose();
        Move62Weights = default;
    }
}