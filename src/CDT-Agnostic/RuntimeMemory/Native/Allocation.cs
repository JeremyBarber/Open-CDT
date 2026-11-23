using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Native;

/// <summary>
/// Low-level, zero-GC native memory allocation, 64-byte cache-line alignment, and vectorized block-fill utilities.
/// </summary>
public static unsafe class Allocation
{
    private const nuint CacheLineAlignmentMask = (nuint)CacheLineBytes - 1;

    /// <summary>
    /// Allocates a contiguous, zero-initialized array of <paramref name="capacity"/> elements of type <typeparamref name="T"/> on the native heap.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type to allocate.</typeparam>
    /// <param name="capacity">The number of elements to allocate.</param>
    /// <returns>A pointer to the zero-initialized native memory block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Allocate<T>(uint capacity) where T : unmanaged =>
        (T*)NativeMemory.AllocZeroed(capacity, (nuint)sizeof(T));

    /// <summary>
    /// Allocates a contiguous array of <paramref name="capacity"/> elements of type <typeparamref name="T"/>
    /// aligned to a 64-byte L1 cache-line boundary, padded to a multiple of 64 bytes to prevent false sharing,
    /// and zero-initialized.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type to allocate.</typeparam>
    /// <param name="capacity">The number of elements to allocate.</param>
    /// <returns>A 64-byte aligned pointer to the zero-initialized native memory block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* AllocateAligned<T>(uint capacity) where T : unmanaged
    {
        var rawBytes = (nuint)capacity * (nuint)sizeof(T);

        // Pad the allocation size up to the nearest multiple of 64 bytes (1 cache line).
        var paddedBytes = (rawBytes + CacheLineAlignmentMask) & ~CacheLineAlignmentMask;

        var ptr = (T*)NativeMemory.AlignedAlloc(paddedBytes, (nuint)CacheLineBytes);

        // AlignedAlloc does not zero-initialize memory; clear the full 64-bit nuint byte count via SIMD.
        NativeMemory.Clear(ptr, paddedBytes);

        return ptr;
    }

    /// <summary>
    /// Broadcasts a single byte value (defaults to <see cref="InvalidDataByte"/> = <c>0xFF</c>, which sets 32-bit <see cref="int"/>
    /// fields to <c>-1</c> in two's complement) across all bytes of an unmanaged array using hardware SIMD instructions.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the target buffer.</typeparam>
    /// <param name="container">Pointer to the target unmanaged buffer (safe to pass <see langword="null"/>).</param>
    /// <param name="capacity">The number of elements of type <typeparamref name="T"/> to fill.</param>
    /// <param name="fillValue">The byte pattern to replicate across the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill<T>(T* container, uint capacity, byte fillValue = InvalidDataByte) where T : unmanaged
    {
        if (container is not null)
            NativeMemory.Fill(container, (nuint)capacity * (nuint)sizeof(T), fillValue);
    }

    /// <summary>
    /// Safely frees a native memory block allocated via <see cref="Allocate{T}"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the buffer.</typeparam>
    /// <param name="container">Pointer to the native buffer to free (safe to pass <see langword="null"/>).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SafeFree<T>(T* container) where T : unmanaged
    {
        if (container is not null)
            NativeMemory.Free(container);
    }

    /// <summary>
    /// Safely frees a 64-byte aligned native memory block allocated via <see cref="AllocateAligned{T}"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the buffer.</typeparam>
    /// <param name="container">Pointer to the aligned native buffer to free (safe to pass <see langword="null"/>).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SafeFreeAligned<T>(T* container) where T : unmanaged
    {
        if (container is not null)
            NativeMemory.AlignedFree(container);
    }
}