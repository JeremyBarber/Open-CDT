using System.Runtime.CompilerServices;

namespace CDT_Agnostic.RuntimeMemory.Native;

/// <summary>
/// Guarded, zero-allocation binary blitters between unmanaged arena buffers and <see cref="Stream"/> instances.
/// </summary>
public static unsafe class ArenaBinaryIO
{
    /// <summary>
    /// Writes a single unmanaged value of type <typeparamref name="T"/> as raw bytes to <paramref name="stream"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteValue<T>(Stream stream, in T value) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);

        fixed (T* ptr = &value)
        {
            stream.Write(new ReadOnlySpan<byte>(ptr, sizeof(T)));
        }
    }

    /// <summary>
    /// Reads a single unmanaged value of type <typeparamref name="T"/> as raw bytes from <paramref name="stream"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadValue<T>(Stream stream) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);

        Unsafe.SkipInit(out T value);
        stream.ReadExactly(new Span<byte>(&value, sizeof(T)));
        return value;
    }

    /// <summary>
    /// Blits <paramref name="count"/> contiguous unmanaged elements from <paramref name="buffer"/> directly to <paramref name="stream"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBlit<T>(Stream stream, T* buffer, uint count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (buffer == null)
            throw new InvalidOperationException("Cannot blit from a null unmanaged buffer.");

        var byteLength = checked((int)((ulong)count * (ulong)sizeof(T)));
        stream.Write(new ReadOnlySpan<byte>(buffer, byteLength));
    }

    /// <summary>
    /// Blits <paramref name="count"/> contiguous unmanaged elements from <paramref name="stream"/> directly into <paramref name="buffer"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBlit<T>(Stream stream, T* buffer, uint count) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (buffer == null)
            throw new InvalidOperationException("Cannot blit into a null unmanaged buffer.");

        var byteLength = checked((int)((ulong)count * (ulong)sizeof(T)));
        stream.ReadExactly(new Span<byte>(buffer, byteLength));
    }
}