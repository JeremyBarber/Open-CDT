using CDT_Agnostic.RuntimeMemory.Identifiers;

namespace CDT_Agnostic.Accessors;

/// <summary>
/// Defines a zero-allocation, high-performance random number generator interface.
/// Dedicated to drawing random topological entities and raw entropy in strictly O(1) time.
/// </summary>
public interface IRandomAccessor
{
    /// <summary>
    /// Generates raw 32-bit entropy.
    /// </summary>
    /// <returns>A uniformly distributed 32-bit unsigned integer.</returns>
    uint GetRandom();

    /// <summary>
    /// Generates a random integer strictly in the range [0, bound).
    /// </summary>
    /// <param name="bound">The exclusive upper bound.</param>
    /// <returns>A random integer between 0 (inclusive) and bound (exclusive).</returns>
    int GetRandom(int bound);

    /// <summary>
    /// Generates a random integer strictly in the range [0, 3].
    /// </summary>
    /// <returns>A random integer of 0, 1, 2, or 3.</returns>
    uint GetRandomBoundedAtFour();

    /// <summary>
    /// Draws a random Simplex ID from the currently active manifold space in strictly O(1) time.
    /// </summary>
    /// <returns>A valid, active SimplexId.</returns>
    SimplexId SelectRandomActiveSimplex();

    /// <summary>
    /// Draws a random Vertex ID from the currently active manifold space in strictly O(1) time.
    /// </summary>
    /// <returns>A valid, active VertexId.</returns>
    VertexId SelectRandomVertex();
}