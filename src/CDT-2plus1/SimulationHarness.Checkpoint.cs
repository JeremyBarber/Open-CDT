using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic;
using CDT_Agnostic.Config;

namespace CDT_2plus1;

public sealed partial class SimulationHarness
{
    /// <inheritdoc/>
    public static ISimulationHarness LoadCheckpoint(
        uint harnessId,
        Stream stream,
        PhysicsParameters physics,
        ulong? reseedRng = null) =>
        new SimulationHarness(harnessId, stream, physics, reseedRng);

    private SimulationHarness(
        uint harnessId,
        Stream stream,
        PhysicsParameters physics,
        ulong? reseedRng)
    {
        HarnessId = harnessId;
        var constraints = new Constraints(physics);

        _universe = Universe.LoadCheckpoint(
            stream,
            in constraints,
            reseedRng,
            harnessId);
    }

    /// <inheritdoc/>
    public void SaveCheckpoint(Stream stream) =>
        _universe.WriteCheckpoint(stream);
}