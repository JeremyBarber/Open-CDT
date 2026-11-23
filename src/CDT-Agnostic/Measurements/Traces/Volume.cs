using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_Agnostic.Measurements.Traces;

/// <summary>
/// Managed owner of Pinned Object Heap (POH) telemetry arrays that record sweep-by-sweep
/// vertex (<c>N_0</c>) and top-dimensional simplex (<c>N_d</c>) counts.
/// Exposes a pre-pinned <see cref="Unmanaged"/> pointer view so the native simulation engine
/// can stream telemetry directly into managed memory across the C-ABI boundary with zero copy or pinning overhead.
/// </summary>
public sealed unsafe class Volume
{
    /// <summary>
    /// Blittable 16-byte pointer header passed across the <c>[UnmanagedCallersOnly]</c> boundary
    /// into the native simulation engine.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Unmanaged
    {
        /// <summary>
        /// Raw pointer to the first element of the pinned <see cref="Volume.TotalVertices"/> array.
        /// </summary>
        public int* TotalVertices;

        /// <summary>
        /// Raw pointer to the first element of the pinned <see cref="Volume.TotalSimplices"/> array.
        /// </summary>
        public int* TotalSimplices;
    }

    // ========================================================================
    // PINNED OBJECT HEAP (POH) STORAGE
    // ========================================================================
    // Allocating both the telemetry buffers and the 1-element Unmanaged header struct
    // on the Pinned Object Heap (pinned: true) guarantees:
    //   1. The GC will never relocate these buffers during a compaction phase, so
    //      UnmanagedPointer remains permanently valid without 'fixed' blocks on each sweep burst.
    //   2. Unlike NativeMemory.Alloc, POH arrays remain tracked by the GC and are reclaimed
    //      automatically when the owning Job completes—requiring no IDisposable boilerplate.
    private readonly Unmanaged[] _pinnedHeader;

    /// <summary>
    /// Permanent raw pointer to the pinned <see cref="Unmanaged"/> header struct.
    /// Passed across the native engine boundary during sweep execution.
    /// </summary>
    public readonly Unmanaged* UnmanagedPointer;

    /// <summary>
    /// Gets the managed view of the pinned vertex counts (<c>N_0</c>) recorded per sweep in <c>[0 .. Length - 1]</c>.
    /// </summary>
    public int[] TotalVertices { get; }

    /// <summary>
    /// Gets the managed view of the pinned simplex counts (<c>N_d</c>) recorded per sweep in <c>[0 .. Length - 1]</c>.
    /// </summary>
    public int[] TotalSimplices { get; }

    /// <summary>
    /// Gets the total sweep capacity of the trace buffers.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Volume"/> class with Pinned Object Heap buffers of the specified length.
    /// </summary>
    /// <param name="length">The number of sweeps the trace buffer can hold (must be at least 1).</param>
    public Volume(int length)
    {
        Length = length;

        TotalVertices = GC.AllocateArray<int>(length, pinned: true);
        TotalSimplices = GC.AllocateArray<int>(length, pinned: true);

        _pinnedHeader = GC.AllocateArray<Unmanaged>(1, pinned: true);
        UnmanagedPointer = (Unmanaged*)Unsafe.AsPointer(ref _pinnedHeader[0]);

        UnmanagedPointer->TotalVertices = (int*)Unsafe.AsPointer(ref TotalVertices[0]);
        UnmanagedPointer->TotalSimplices = (int*)Unsafe.AsPointer(ref TotalSimplices[0]);
    }
}