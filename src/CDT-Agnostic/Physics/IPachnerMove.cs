using CDT_Agnostic.Accessors;

namespace CDT_Agnostic.Physics;

/// <summary>
/// Defines the generic, monomorphized contract for a topological Pachner move.
/// Implemented via <c>static abstract</c> interface members so the dimension-agnostic
/// <see cref="ProposalPipeline"/> can execute dimension-specific topological moves with zero virtual dispatch overhead.
/// </summary>
/// <typeparam name="TContext">The stack-allocated struct holding the validated local site geometry for the move.</typeparam>
/// <typeparam name="TRandom">The concrete unmanaged <see cref="IRandomAccessor"/> implementation.</typeparam>
/// <typeparam name="TInspector">The concrete unmanaged <see cref="IInspectingAccessor"/> implementation.</typeparam>
/// <typeparam name="TMutator">The concrete unmanaged <see cref="IMutatingAccessor{TInspector}"/> implementation.</typeparam>
/// <typeparam name="TPhysics">The concrete unmanaged physics weight accessor for the active dimension.</typeparam>
public interface IPachnerMove<TContext, TRandom, TInspector, TMutator, TPhysics>
    where TContext : struct
    where TRandom : unmanaged, IRandomAccessor
    where TInspector : unmanaged, IInspectingAccessor
    where TMutator : unmanaged, IMutatingAccessor<TInspector>
    where TPhysics : unmanaged
{
    /// <summary>
    /// Attempts to randomly select a topologically and causally valid site for the move.
    /// Fails fast without mutating state if the local geometry violates manifold or foliation constraints.
    /// </summary>
    /// <param name="random">The zero-allocation random number generator lens.</param>
    /// <param name="inspector">The read-only lens into the unmanaged manifold arena.</param>
    /// <param name="context">When this method returns <see langword="true"/>, contains the validated local site geometry required to execute the move.</param>
    /// <returns><see langword="true"/> if a valid site was found and <paramref name="context"/> was populated; otherwise, <see langword="false"/>.</returns>
    static abstract bool TrySelectValidSiteForMove(
        TRandom random,
        TInspector inspector,
        out TContext context);

    /// <summary>
    /// Evaluates the Metropolis-Hastings acceptance criterion for the proposed topological transition
    /// using global manifold invariants and precomputed <see cref="MoveWeights"/> lookup tables.
    /// </summary>
    /// <param name="random">The zero-allocation random number generator lens.</param>
    /// <param name="inspector">The read-only lens into the unmanaged manifold arena.</param>
    /// <param name="physics">The precomputed thermodynamic weights for the active dimension.</param>
    /// <returns><see langword="true"/> if the move is thermodynamically accepted; otherwise, <see langword="false"/>.</returns>
    static abstract bool IsMoveEnergeticallyFavourable(
        TRandom random,
        TInspector inspector,
        TPhysics physics);

    /// <summary>
    /// Mutates the unmanaged manifold in place to execute the accepted Pachner move.
    /// Guaranteed to be topologically safe when <see cref="TrySelectValidSiteForMove"/> has returned <see langword="true"/>.
    /// </summary>
    /// <param name="mutator">The write-enabled lens into the unmanaged manifold arena.</param>
    /// <param name="context">Read-only reference to the validated local site geometry.</param>
    static abstract void PerformMove(
        TMutator mutator,
        in TContext context);
}