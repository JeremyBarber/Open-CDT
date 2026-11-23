using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;

namespace CDT_2plus1.Engine.Topology.Pachner;

/// <summary>
/// A fixed-size, 24-byte stack-allocated inline array of 6 <see cref="SimplexId"/> elements.
/// Used with <see cref="Unsafe.SkipInit{T}(out T)"/> during Pachner move site validation
/// (<c>Move32</c>, <c>Move44</c>, and <c>Move62</c>) to avoid both zero-initialization
/// and the JIT stack-canary (<c>GS</c> cookie) overhead of <see langword="stackalloc"/>.
/// </summary>
[InlineArray(Capacity)]
internal struct IncidentSimplicesBuffer
{
    /// <summary>
    /// The maximum number of incident simplices queried during local move validation (degree 6 for <c>Move62</c>).
    /// </summary>
    public const int Capacity = 6;

    private SimplexId _element0;
}