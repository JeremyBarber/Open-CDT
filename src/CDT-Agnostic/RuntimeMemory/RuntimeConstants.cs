namespace CDT_Agnostic.RuntimeMemory;

/// <summary>
/// Hardware and memory-representation constants shared across unmanaged arena buffers.
/// </summary>
public static class RuntimeConstants
{
    /// <summary>
    /// The CPU L1 data cache line size in bytes (64 bytes on x86-64 and ARM64).
    /// Used to align hot lookup tables and size unrolled adjacency pages so they never split across cache lines.
    /// </summary>
    public const int CacheLineBytes = 64;

    /// <summary>
    /// Sentinel 32-bit integer value (<c>-1</c>, or <c>0xFFFFFFFF</c> in two's complement)
    /// indicating an uninitialized, unlinked, or invalid arena slot index.
    /// </summary>
    public const int InvalidData = -1;

    /// <summary>
    /// Single-byte fill pattern (<c>0xFF</c>) used with SIMD block initialization so that every
    /// 4-byte <see cref="int"/> lane in an unmanaged buffer is initialized to <see cref="InvalidData"/> (<c>-1</c>).
    /// </summary>
    public const byte InvalidDataByte = 0xFF;
}