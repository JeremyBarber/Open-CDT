using CDT_2plus1.Engine.State.Physics;
using CDT_2plus1.Engine.State.Physics.Actions;
using CDT_2plus1.Engine.Topology.Pachner.Move23;
using CDT_2plus1.Engine.Topology.Pachner.Move26;
using CDT_2plus1.Engine.Topology.Pachner.Move32;
using CDT_2plus1.Engine.Topology.Pachner.Move62;
using CDT_Agnostic.Config;

using Xunit;

namespace CDT_2plus1_Tests.Engine.State.Physics.Actions;

public sealed class ReggeTests
{
    private readonly Constraints _testConstraints = new(new PhysicsParameters
    {
        K0 = 1.0,
        Kd = 2.0,
        Delta = 0.5,
        Epsilon = 0.0,
        TargetVolume = 1000
    });

    [Fact]
    public void Regge_CalculateDeltaAction_ForMove23AndMove32_MatchesTheoreticalMathAndAntisymmetry()
    {
        // Move (2,3): ΔN0 = 0, ΔN31 = 0, ΔN22 = +1, ΔN13 = 0 => ΔN3 = +1
        // ΔS_23 = -K0(0) + K3(1) + Delta(0) = +2.0
        var deltaS23 = Regge.CalculateDeltaAction(in _testConstraints, in Move23.Deltas);
        var deltaS32 = Regge.CalculateDeltaAction(in _testConstraints, in Move32.Deltas);

        Assert.Equal(2.0, deltaS23);
        Assert.Equal(-2.0, deltaS32);
    }

    [Fact]
    public void Regge_CalculateDeltaAction_ForMove26AndMove62_MatchesTheoreticalMathAndAntisymmetry()
    {
        // Move (2,6): ΔN0 = +1, ΔN31 = +2, ΔN22 = 0, ΔN13 = +2 => ΔN3 = +4
        // ΔS_26 = -1.0(1) + 2.0(4) + 0.5(4) = -1.0 + 8.0 + 2.0 = +9.0
        var deltaS26 = Regge.CalculateDeltaAction(in _testConstraints, in Move26.Deltas);
        var deltaS62 = Regge.CalculateDeltaAction(in _testConstraints, in Move62.Deltas);

        Assert.Equal(9.0, deltaS26);
        Assert.Equal(-9.0, deltaS62);
    }

    [Fact]
    public void Regge_CalculateDeltaAction_NegativeK0_EvaluatesCorrectly()
    {
        var stringPhaseConstraints = new Constraints(new PhysicsParameters
        {
            K0 = -2.0,
            Kd = 1.0,
            Delta = 0.0
        });

        // Move (2,6): ΔS = -(-2.0 * 1) + 1.0(4) + 0 = 2.0 + 4.0 = 6.0
        var deltaS = Regge.CalculateDeltaAction(in stringPhaseConstraints, in Move26.Deltas);

        Assert.Equal(6.0, deltaS);
    }
}