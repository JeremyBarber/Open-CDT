using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic.Config;

using Xunit;

namespace CDT_2plus1_Tests.Engine.State;

public sealed class UniverseTests
{
    [Fact]
    public void Universe_Build_BootstrapsIcosahedralSeedCorrectly()
    {
        var config = new PhysicsParameters { TargetVolume = 10000 };
        var constraints = new Constraints(config);

        using var universe = Universe.Build(
            in constraints,
            subdivisions: 1,
            timeslices: 3,
            rngSeed: 42,
            rngSequence: 42);

        var inspector = universe.GetInspector();

        Assert.True(inspector.Adjacency.TotalSimplices > 0, "Universe failed to bootstrap the initial geometry.");
        Assert.True(inspector.Adjacency.TotalVertices > 0, "Universe failed to bootstrap the initial vertices.");
        Assert.Equal(inspector.N31 + inspector.N22 + inspector.N13, inspector.Adjacency.TotalSimplices);
    }

    [Fact]
    public void Universe_Dispose_PreventsFurtherAccess()
    {
        var constraints = new Constraints(new PhysicsParameters { TargetVolume = 1000 });
        var universe = Universe.Build(in constraints, 1, 3, 1, 1);

        universe.Dispose();

        using var stream = new MemoryStream();
        Assert.Throws<ObjectDisposedException>(() => universe.GetMutator());
        Assert.Throws<ObjectDisposedException>(() => universe.GetInspector());
        Assert.Throws<ObjectDisposedException>(() => universe.GetRandom());
        Assert.Throws<ObjectDisposedException>(() => universe.GetPhysics());
        Assert.Throws<ObjectDisposedException>(() => universe.WriteCheckpoint(stream));

        var exception = Record.Exception(() => universe.Dispose());
        Assert.Null(exception);
    }
}