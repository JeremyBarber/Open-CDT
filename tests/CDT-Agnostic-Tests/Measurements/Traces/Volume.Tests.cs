using CDT_Agnostic.Measurements.Traces;

using Xunit;

namespace CDT_Agnostic_Tests.Measurements.Traces;

public sealed unsafe class VolumeTests
{
    [Fact]
    public void UnmanagedPointer_WritesDirectlyIntoManagedPinnedArrays()
    {
        // 1. ARRANGE
        const int length = 8;
        var trace = new Volume(length);

        Assert.Equal(length, trace.Length);
        Assert.Equal(length, trace.TotalVertices.Length);
        Assert.Equal(length, trace.TotalSimplices.Length);
        Assert.NotEqual(nint.Zero, (nint)trace.UnmanagedPointer);
        Assert.NotEqual(nint.Zero, (nint)trace.UnmanagedPointer->TotalVertices);
        Assert.NotEqual(nint.Zero, (nint)trace.UnmanagedPointer->TotalSimplices);

        // 2. ACT: Simulate the unmanaged C-ABI engine writing telemetry across a sweep burst
        var nativeView = trace.UnmanagedPointer;
        for (var i = 0; i < length; i++)
        {
            nativeView->TotalVertices[i] = 100 + i;
            nativeView->TotalSimplices[i] = 400 + (i * 10);
        }

        // 3. ASSERT: The managed arrays reflect the exact values with zero copying
        for (var i = 0; i < length; i++)
        {
            Assert.Equal(100 + i, trace.TotalVertices[i]);
            Assert.Equal(400 + (i * 10), trace.TotalSimplices[i]);
        }
    }
}