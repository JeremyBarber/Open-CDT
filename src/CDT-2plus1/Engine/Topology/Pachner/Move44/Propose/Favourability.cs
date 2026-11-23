using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1.Engine.Topology.Pachner.Move44;

internal readonly partial struct Move44
{
    /// <summary>
    /// Evaluates the full Metropolis-Hastings acceptance probability.
    /// Fuses the dynamic topological Hastings ratio with the precomputed thermodynamic Boltzmann weights.
    /// </summary>
    /// <remarks>
    /// Because the (4,4) move perfectly preserves all spatial and temporal volumes,
    /// the action change $\Delta S$ is identically zero. Because the proposal probability 
    /// is symmetric, the Hastings ratio is exactly 1. 
    /// Therefore, the Metropolis-Hastings acceptance rate is mathematically 100%.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMoveEnergeticallyFavourable(RandomAccessor random, InspectingAccessor inspector, PhysicsAccessor physics) =>
        true;
}