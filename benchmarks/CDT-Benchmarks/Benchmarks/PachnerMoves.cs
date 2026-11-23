using CDT_2plus1.Engine;
using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements.Traces;
using CDT_Agnostic.Physics;

using BenchmarkDotNet.Attributes;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

using M23 = CDT_2plus1.Engine.Topology.Pachner.Move23.Move23;
using M23Ctx = CDT_2plus1.Engine.Topology.Pachner.Move23.Propose.Context;
using M26 = CDT_2plus1.Engine.Topology.Pachner.Move26.Move26;
using M26Ctx = CDT_2plus1.Engine.Topology.Pachner.Move26.Propose.Context;
using M32 = CDT_2plus1.Engine.Topology.Pachner.Move32.Move32;
using M32Ctx = CDT_2plus1.Engine.Topology.Pachner.Move32.Propose.Context;
using M44 = CDT_2plus1.Engine.Topology.Pachner.Move44.Move44;
using M44Ctx = CDT_2plus1.Engine.Topology.Pachner.Move44.Propose.Context;
using M62 = CDT_2plus1.Engine.Topology.Pachner.Move62.Move62;
using M62Ctx = CDT_2plus1.Engine.Topology.Pachner.Move62.Propose.Context;

namespace CDT_Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks the five 2+1D ergodic Pachner moves (<c>Move23</c>, <c>Move32</c>, <c>Move26</c>, <c>Move62</c>, <c>Move44</c>)
/// on a thermalized De Sitter manifold (<c>N_3 ≈ 16,000</c>).
/// Decorated with <see cref="DisassemblyDiagnoserAttribute"/> to verify RyuJIT generic monomorphization
/// and L1 instruction cache (L1i) native code size.
/// </summary>
[DisassemblyDiagnoser(maxDepth: 3, exportDiff: false)]
public unsafe class PachnerMoves
{
    private static readonly delegate* unmanaged<RandomAccessor, MutatingAccessor, PhysicsAccessor, Volume.Unmanaged*, int, void> _executeSweepChunk =
        &SimulationEngine.ExecuteSweepBurst;

    private Universe _universe = null!;

    [GlobalSetup]
    public void Setup()
    {
        var physics = new PhysicsParameters
        {
            K0 = 1.5,
            Kd = 1.385,
            Delta = 0.5,
            Epsilon = 0.02,
            TargetVolume = 16_000
        };

        var constraints = new Constraints(physics);

        _universe = Universe.Build(
            in constraints,
            subdivisions: 2,
            timeslices: 16,
            rngSeed: 42UL,
            rngSequence: 1UL);

        // Thermalize to TargetVolume (~16,000 simplices) via the C-ABI function pointer
        _executeSweepChunk(
            _universe.GetRandom(),
            _universe.GetMutator(),
            _universe.GetPhysics(),
            null,
            1_500);
    }

    [GlobalCleanup]
    public void Cleanup() => _universe.Dispose();

    // =========================================================================
    // 3A. PER-MOVE TOPOLOGICAL VALIDATION + LUT ACCEPTANCE (STATIONARY MANIFOLD)
    // =========================================================================

    [Benchmark(Description = "Move23: Select + Validate + LUT Metropolis")]
    public bool ValidateAndEvaluate_Move23()
    {
        var random = _universe.GetRandom();
        var inspector = _universe.GetInspector();
        var physics = _universe.GetPhysics();

        return M23.TrySelectValidSiteForMove(random, inspector, out var _) &&
               M23.IsMoveEnergeticallyFavourable(random, inspector, physics);
    }

    [Benchmark(Description = "Move32: Select + Validate + LUT Metropolis")]
    public bool ValidateAndEvaluate_Move32()
    {
        var random = _universe.GetRandom();
        var inspector = _universe.GetInspector();
        var physics = _universe.GetPhysics();

        return M32.TrySelectValidSiteForMove(random, inspector, out var _) &&
               M32.IsMoveEnergeticallyFavourable(random, inspector, physics);
    }

    [Benchmark(Description = "Move26: Select + Validate + LUT Metropolis")]
    public bool ValidateAndEvaluate_Move26()
    {
        var random = _universe.GetRandom();
        var inspector = _universe.GetInspector();
        var physics = _universe.GetPhysics();

        return M26.TrySelectValidSiteForMove(random, inspector, out var _) &&
               M26.IsMoveEnergeticallyFavourable(random, inspector, physics);
    }

    [Benchmark(Description = "Move62: Select + Validate + LUT Metropolis")]
    public bool ValidateAndEvaluate_Move62()
    {
        var random = _universe.GetRandom();
        var inspector = _universe.GetInspector();
        var physics = _universe.GetPhysics();

        return M62.TrySelectValidSiteForMove(random, inspector, out var _) &&
               M62.IsMoveEnergeticallyFavourable(random, inspector, physics);
    }

    [Benchmark(Description = "Move44: Select + Validate + LUT Metropolis")]
    public bool ValidateAndEvaluate_Move44()
    {
        var random = _universe.GetRandom();
        var inspector = _universe.GetInspector();
        var physics = _universe.GetPhysics();

        return M44.TrySelectValidSiteForMove(random, inspector, out var _) &&
               M44.IsMoveEnergeticallyFavourable(random, inspector, physics);
    }

    // =========================================================================
    // 3B. FULL MONOMORPHIZED PROPOSAL PIPELINE (WITH IN-PLACE MUTATION)
    // =========================================================================

    [Benchmark(Description = "Full Pipeline: Move44 (Volume-Preserving)")]
    public void Propose_Move44()
    {
        ProposalPipeline.Propose<M44, M44Ctx, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>(
            _universe.GetRandom(),
            _universe.GetMutator(),
            _universe.GetPhysics());
    }

    [Benchmark(Description = "Full Pipeline: Move23 + Move32 Conjugate Pair", OperationsPerInvoke = 2)]
    public void Propose_Move23_And_Move32()
    {
        var random = _universe.GetRandom();
        var mutator = _universe.GetMutator();
        var physics = _universe.GetPhysics();

        ProposalPipeline.Propose<M23, M23Ctx, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>(random, mutator, physics);
        ProposalPipeline.Propose<M32, M32Ctx, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>(random, mutator, physics);
    }

    [Benchmark(Description = "Full Pipeline: Move26 + Move62 Conjugate Pair", OperationsPerInvoke = 2)]
    public void Propose_Move26_And_Move62()
    {
        var random = _universe.GetRandom();
        var mutator = _universe.GetMutator();
        var physics = _universe.GetPhysics();

        ProposalPipeline.Propose<M26, M26Ctx, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>(random, mutator, physics);
        ProposalPipeline.Propose<M62, M62Ctx, RandomAccessor, InspectingAccessor, MutatingAccessor, PhysicsAccessor>(random, mutator, physics);
    }
}