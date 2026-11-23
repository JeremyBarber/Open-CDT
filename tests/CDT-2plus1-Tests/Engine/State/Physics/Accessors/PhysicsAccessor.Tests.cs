using CDT_2plus1.Engine.State.Physics;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.Config;

using System.Runtime.CompilerServices;
using Xunit;

using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1_Tests.Engine.State.Physics.Accessors;

public sealed unsafe class PhysicsAccessorTests
{
    [Fact]
    public void PhysicsAccessor_RoutesToExactMemoryAddresses_WithoutCopying()
    {
        var config = new PhysicsParameters
        {
            K0 = 1.0,
            Kd = 1.0,
            Delta = 0.5,
            Epsilon = 0.02,
            TargetVolume = 1000
        };

        var c = new Constraints(config);
        var d = Create(in c);

        try
        {
            var raw = (DynamicsOverlay*)d;
            var accessor = new PhysicsAccessor(d);

            // Prove that every property returns a direct by-reference alias to the unmanaged field
            // with zero hidden stack defensive copies.
            var raw23Address = Unsafe.AsPointer(ref raw->Move23Weights);
            var acc23Address = Unsafe.AsPointer(ref Unsafe.AsRef(in accessor.Move23));
            Assert.True(raw23Address == acc23Address, "Move23 triggered a defensive memory copy!");

            var raw32Address = Unsafe.AsPointer(ref raw->Move32Weights);
            var acc32Address = Unsafe.AsPointer(ref Unsafe.AsRef(in accessor.Move32));
            Assert.True(raw32Address == acc32Address, "Move32 triggered a defensive memory copy!");

            var raw26Address = Unsafe.AsPointer(ref raw->Move26Weights);
            var acc26Address = Unsafe.AsPointer(ref Unsafe.AsRef(in accessor.Move26));
            Assert.True(raw26Address == acc26Address, "Move26 triggered a defensive memory copy!");

            var raw62Address = Unsafe.AsPointer(ref raw->Move62Weights);
            var acc62Address = Unsafe.AsPointer(ref Unsafe.AsRef(in accessor.Move62));
            Assert.True(raw62Address == acc62Address, "Move62 triggered a defensive memory copy!");
        }
        finally
        {
            Destroy(ref d);
        }
    }
}