using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic.Config;

using Xunit;

namespace CDT_2plus1_Tests.Engine.State;

public sealed class UniverseCheckpointTests
{
    private static Constraints CreateTestConstraints() =>
            new(new PhysicsParameters
            {
                K0 = 5.05,
                Kd = 1.369,
                Delta = 0.69,
                Epsilon = 0.0005,
                TargetVolume = 2000
            });

    [Fact]
    public void WriteAndLoadCheckpoint_WhenSavedCapacityExceedsConfiguredCapacity_ScalesUpWithoutThrowing()
    {
        var constraints = CreateTestConstraints();

        using var source = Universe.Build(
            in constraints,
            subdivisions: 1,
            timeslices: 4,
            rngSeed: 123,
            rngSequence: 1);

        using var stream = new MemoryStream();
        source.WriteCheckpoint(stream);

        // Pass a smaller configuredMaxSimplices (500 < 4000) and a reseedRng override
        stream.Position = 0;
        using var restored = Universe.LoadCheckpoint(
            stream,
            in constraints,
            reseedRng: 999UL,
            rngSequence: 1);

        Assert.Equal(
            source.GetInspector().Adjacency.TotalSimplices,
            restored.GetInspector().Adjacency.TotalSimplices);
        Assert.Equal(
            source.GetInspector().Adjacency.TotalVertices,
            restored.GetInspector().Adjacency.TotalVertices);
    }

    [Fact]
    public void WriteAndLoadCheckpoint_WithInvalidStreams_ThrowsExpectedException()
    {
        var constraints = CreateTestConstraints();
        using var universe = Universe.Build(in constraints, 1, 3, 42, 0);

        Assert.Throws<ArgumentNullException>(() => universe.WriteCheckpoint(null!));
        Assert.Throws<ArgumentNullException>(() => Universe.LoadCheckpoint(null!, in constraints));

        using var nonWritable = new NonWritableMemoryStream();
        Assert.Throws<ArgumentException>(() => universe.WriteCheckpoint(nonWritable));

        using var nonReadable = new NonReadableMemoryStream();
        Assert.Throws<ArgumentException>(() => Universe.LoadCheckpoint(nonReadable, in constraints));
    }

    private sealed class NonWritableMemoryStream : MemoryStream
    {
        public override bool CanWrite => false;
    }

    private sealed class NonReadableMemoryStream : MemoryStream
    {
        public override bool CanRead => false;
    }
}