using CDT_2plus1.Engine.Topology.Pachner;

using Xunit;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner;

public sealed class PachnerDeltaTests
{
    [Fact]
    public void PachnerDelta_PrecomputesAllFieldsAndTotalVolumeCorrectly()
    {
        var delta = new PachnerDelta(dN0: 1, dN31: 2, dN22: 0, dN13: 2);

        Assert.Equal(1, delta.DeltaN0);
        Assert.Equal(2, delta.DeltaN31);
        Assert.Equal(0, delta.DeltaN22);
        Assert.Equal(2, delta.DeltaN13);
        Assert.Equal(4, delta.DeltaN3);
    }
}