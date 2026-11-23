using CDT_2plus1.Engine.Topology.Pachner;
using CDT_Agnostic.Measurements.Traces;
using CDT_Agnostic.Physics;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

using M23 = CDT_2plus1.Engine.Topology.Pachner.Move23.Move23;
using M23Ctx = CDT_2plus1.Engine.Topology.Pachner.Move23.Propose.Context;
using M26 = CDT_2plus1.Engine.Topology.Pachner.Move26.Move26;
using M26Ctx = CDT_2plus1.Engine.Topology.Pachner.Move26.Propose.Context;
using M32 = CDT_2plus1.Engine.Topology.Pachner.Move32.Move32;
using M32Ctx = CDT_2plus1.Engine.Topology.Pachner.Move32.Propose.Context;
using M44 = CDT_2plus1.Engine.Topology.Pachner.Move44.Move44;
using M44Ctx = CDT_2plus1.Engine.Topology.Pachner.Move44.Propose.Context;
using M62 = CDT_2plus1.Engine.Topology.Pachner.Move62.Move62;
using M62Ctx = CDT_2plus1.Engine.Topology.Pachner.Move62.Propose.Context;

namespace CDT_2plus1.Engine;

/// <summary>
/// The primary entry point for the unmanaged 2+1D Monte Carlo engine.
/// Acts as a strict air-gap boundary: no managed objects, garbage collection,
/// or heap allocations occur inside this loop.
/// </summary>
internal static unsafe class SimulationEngine
{
    /// <summary>
    /// Executes a continuous block of Monte Carlo sweeps.
    /// Exported as an unmanaged C-ABI function to bypass the .NET interop marshaller
    /// when invoked via function pointer from <see cref="SimulationHarness"/>.
    /// </summary>
    [UnmanagedCallersOnly]
    internal static void ExecuteSweepBurst(
        RandomAccessor random,
        MutatingAccessor mutator,
        PhysicsAccessor physics,
        Volume.Unmanaged* trace,
        int sweepsToRun)
    {
        for (var sweep = 0; sweep < sweepsToRun; sweep++)
        {
            // One Monte Carlo "Sweep" is defined as N attempted moves,
            // where N is the current fluctuating volume of the manifold.
            for (var proposal = 0; proposal < mutator.Inspect.Adjacency.TotalSimplices; proposal++)
            {
                var proposedMove = (PachnerMove)random.GetRandom((int)PachnerMove._Count);

                // Monomorphization: Switching on the enum and routing to struct-constrained generics
                // forces RyuJIT to emit 5 distinct, inline-specialized assembly pipelines with zero virtual dispatch.
                switch (proposedMove)
                {
                    case PachnerMove.Move23:
                        Propose<M23, M23Ctx>(random, mutator, physics);
                        break;

                    case PachnerMove.Move32:
                        Propose<M32, M32Ctx>(random, mutator, physics);
                        break;

                    case PachnerMove.Move26:
                        Propose<M26, M26Ctx>(random, mutator, physics);
                        break;

                    case PachnerMove.Move62:
                        Propose<M62, M62Ctx>(random, mutator, physics);
                        break;

                    case PachnerMove.Move44:
                        Propose<M44, M44Ctx>(random, mutator, physics);
                        break;
                }
            }

            if (trace != null)
            {
                trace->TotalVertices[sweep] = mutator.Inspect.Adjacency.TotalVertices;
                trace->TotalSimplices[sweep] = mutator.Inspect.Adjacency.TotalSimplices;
            }
        }
    }

    /// <summary>
    /// Generic helper trampoline that binds the 2+1D accessor types into <see cref="ProposalPipeline"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Propose<TMove, TContext>(
        RandomAccessor random,
        MutatingAccessor mutator,
        PhysicsAccessor physics)
        where TMove : struct, IPachnerMove<TContext, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>
        where TContext : struct =>
        ProposalPipeline.Propose<TMove, TContext, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>(
            random, mutator, physics);
}