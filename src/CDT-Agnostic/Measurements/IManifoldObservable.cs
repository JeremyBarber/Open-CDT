using CDT_Agnostic.Accessors;

namespace CDT_Agnostic.Measurements;

/// <summary>
/// Defines a dimension-agnostic physical observable evaluated on a paused CDT manifold between Monte Carlo sweep bursts.
/// Implementations are strictly read-only with respect to manifold topology and return an immutable snapshot of <typeparamref name="TResult"/> per call.
/// </summary>
/// <typeparam name="TResult">The measurement snapshot type returned by the observable.</typeparam>
public interface IManifoldObservable<out TResult>
{
    /// <summary>
    /// Evaluates the observable across the current manifold state using zero-allocation read-only accessors.
    /// </summary>
    /// <typeparam name="TAdjacency">The concrete unmanaged <see cref="IAdjacencyAccessor"/> implementation.</typeparam>
    /// <typeparam name="TInspector">The concrete unmanaged <see cref="IInspectingAccessor{TAdjacency}"/> implementation.</typeparam>
    /// <typeparam name="TRandom">The concrete unmanaged <see cref="IRandomAccessor"/> implementation.</typeparam>
    /// <param name="inspector">Read-only geometric and adjacency lens into the manifold.</param>
    /// <param name="random">Read-only random number generator lens for stochastic observables.</param>
    /// <returns>The computed measurement snapshot.</returns>
    TResult Measure<TAdjacency, TInspector, TRandom>(TInspector inspector, TRandom random)
        where TAdjacency : unmanaged, IAdjacencyAccessor
        where TInspector : unmanaged, IInspectingAccessor<TAdjacency>
        where TRandom : unmanaged, IRandomAccessor;
}