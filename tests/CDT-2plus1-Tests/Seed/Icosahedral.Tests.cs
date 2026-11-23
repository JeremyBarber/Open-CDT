using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_2plus1.Seed;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_Agnostic.Config;

using Xunit;

namespace CDT_2plus1_Tests.Seed;

public sealed class IcosahedralSeedTests
{
    private static Constraints GetDefaultConstraints() =>
        new(new PhysicsParameters
        {
            TargetVolume = 10000,
            K0 = 1.0,
            Kd = 1.0,
            Delta = 0.5,
            Epsilon = 0.002
        });

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 6)]
    [InlineData(3, 8)]
    [InlineData(4, 10)]
    public void Seed_ProducesValidManifold(uint subdivisions, uint timeslices)
    {
        var constraints = GetDefaultConstraints();

        (var expectedVertices, var initialSimplices) = Icosahedral.CalculateIcosahedralSize(subdivisions, timeslices);

        using var universe = Universe.Build(
            in constraints,
            subdivisions: subdivisions,
            timeslices: timeslices,
            rngSeed: 100UL * subdivisions,
            rngSequence: 0UL);

        var inspector = universe.GetInspector();

        Assert.Equal((int)expectedVertices, inspector.Adjacency.TotalVertices);
        Assert.Equal((int)initialSimplices, inspector.Adjacency.TotalSimplices);

        ManifoldValidation.AssertIntegrity(inspector, timeslices);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 6)]
    [InlineData(3, 8)]
    [InlineData(4, 10)]
    public void Seed_ContainsCorrectSimplexTypeRatios(uint subdivisions, uint timeslices)
    {
        var constraints = GetDefaultConstraints();

        using var universe = Universe.Build(
            in constraints,
            subdivisions: subdivisions,
            timeslices: timeslices,
            rngSeed: 100UL * subdivisions,
            rngSequence: 0UL);

        var inspector = universe.GetInspector();

        Assert.True(inspector.Adjacency.TotalSimplices > 0, "Manifold contains no simplices.");
        Assert.Equal(inspector.N31, inspector.N13);
        Assert.Equal(inspector.N31, inspector.N22);
        Assert.Equal(inspector.Adjacency.TotalSimplices, inspector.N31 + inspector.N22 + inspector.N13);
    }

    [Theory]
    [InlineData(0u, 4u)]
    [InlineData(1u, 0u)]
    [InlineData(1u, 2u)]
    public void CalculateIcosahedralSize_WithInvalidArguments_ThrowsArgumentOutOfRangeException(
        uint subdivisions,
        uint timeslices)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Icosahedral.CalculateIcosahedralSize(subdivisions, timeslices));
    }
}