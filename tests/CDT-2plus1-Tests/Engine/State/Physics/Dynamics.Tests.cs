using CDT_2plus1.Engine.State.Physics;
using CDT_2plus1_Tests.TestHarnesses.StructOverlays;
using CDT_Agnostic.Config;

using Xunit;

namespace CDT_2plus1_Tests.Engine.State.Physics;

public sealed unsafe class DynamicsTests
{
    [Fact]
    public void Dynamics_Create_AlignsRadiusToL1CacheBoundary()
    {
        var constraints = new Constraints(new PhysicsParameters
        {
            Epsilon = 0.0005,
            TargetVolume = 10000
        });

        // For ε = 0.0005, σ = 1 / √(2ε) ≈ 31.62 -> ⌈5σ⌉ = 159 -> padded to 160.
        var m = Dynamics.Create(in constraints);
        try
        {
            var raw = (DynamicsOverlay*)m;

            Assert.Equal(160, raw->Move23Weights.VolumeRadius);
            Assert.Equal(160, raw->Move26Weights.VolumeRadius);
        }
        finally
        {
            Dynamics.Destroy(ref m);
        }
    }

    [Fact]
    public void Dynamics_Create_HydratesTablesFromConstraints()
    {
        var lowK = new Constraints(new PhysicsParameters { K0 = 1.0, Kd = 1.0, Delta = 0.5, Epsilon = 0.02, TargetVolume = 10000 });
        var highK = new Constraints(new PhysicsParameters { K0 = 1.0, Kd = 5.0, Delta = 0.5, Epsilon = 0.02, TargetVolume = 10000 });

        var mLow = Dynamics.Create(in lowK);
        var mHigh = Dynamics.Create(in highK);
        try
        {
            var rawLow = (DynamicsOverlay*)mLow;
            var rawHigh = (DynamicsOverlay*)mHigh;

            var centerIndex = rawLow->Move26Weights.VolumeRadius;
            Assert.Equal(32, centerIndex);
            Assert.True(rawHigh->Move26Weights.ExpDeltaS[centerIndex] < rawLow->Move26Weights.ExpDeltaS[centerIndex]);
        }
        finally
        {
            Dynamics.Destroy(ref mLow);
            Dynamics.Destroy(ref mHigh);
        }
    }

    [Fact]
    public void Dynamics_Destroy_IsIdempotentAndNullsPointer()
    {
        var constraints = new Constraints(new PhysicsParameters { TargetVolume = 100 });
        var m = Dynamics.Create(in constraints);

        Dynamics.Destroy(ref m);
        Assert.Null(m);

        var exception = Record.Exception(() => Dynamics.Destroy(ref m));
        Assert.Null(exception);
    }
}