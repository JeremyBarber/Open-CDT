using CDT_Agnostic.Accessors;

using System.Runtime.CompilerServices;

namespace CDT_Agnostic.Physics;

/// <summary>
/// Stateless, zero-allocation execution pipeline for proposing, evaluating, and performing
/// a single Markov Chain Monte Carlo (MCMC) Pachner move.
/// </summary>
public static class ProposalPipeline
{
    /// <summary>
    /// Executes the three-stage Metropolis-Hastings proposal pipeline for <typeparamref name="TMove"/>:
    /// (1) topological site selection and validation, (2) thermodynamic acceptance evaluation,
    /// and (3) in-place manifold mutation.
    /// </summary>
    /// <typeparam name="TMove">The concrete <see cref="IPachnerMove{TContext, TRandom, TInspector, TMutator, TPhysics}"/> struct.</typeparam>
    /// <typeparam name="TContext">The stack-allocated local site context struct.</typeparam>
    /// <typeparam name="TRandom">The concrete unmanaged <see cref="IRandomAccessor"/> implementation.</typeparam>
    /// <typeparam name="TInspector">The concrete unmanaged <see cref="IInspectingAccessor"/> implementation.</typeparam>
    /// <typeparam name="TMutator">The concrete unmanaged <see cref="IMutatingAccessor{TInspector}"/> implementation.</typeparam>
    /// <typeparam name="TPhysics">The concrete unmanaged physics weight accessor.</typeparam>
    /// <param name="random">The zero-allocation random number generator lens.</param>
    /// <param name="mutator">The write-enabled manifold mutator lens (exposing its read-only <see cref="IMutatingAccessor{TInspector}.Inspect"/> lens).</param>
    /// <param name="physics">The precomputed thermodynamic weight lens.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Propose<TMove, TContext, TRandom, TInspector, TMutator, TPhysics>(
        TRandom random,
        TMutator mutator,
        TPhysics physics)
        where TMove : struct, IPachnerMove<TContext, TRandom, TInspector, TMutator, TPhysics>
        where TContext : struct
        where TRandom : unmanaged, IRandomAccessor
        where TInspector : unmanaged, IInspectingAccessor
        where TMutator : unmanaged, IMutatingAccessor<TInspector>
        where TPhysics : unmanaged
    {
        var inspector = mutator.Inspect;

        // 1. Target & Validate
        if (!TMove.TrySelectValidSiteForMove(random, inspector, out var context))
            return;

        // 2. Physics & Thermodynamics
        if (!TMove.IsMoveEnergeticallyFavourable(random, inspector, physics))
            return;

        // 3. Execution
        TMove.PerformMove(mutator, in context);
    }
}