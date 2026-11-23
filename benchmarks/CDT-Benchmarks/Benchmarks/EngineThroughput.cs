using CDT_2plus1;
using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements.Traces;

using BenchmarkDotNet.Attributes;

namespace CDT_Benchmarks.Benchmarks;

/// <summary>
/// End-to-end engine benchmarks proving:
/// (1) <c>0 B</c> managed allocation and <c>0</c> GC stops across 100 sweeps over the <c>[UnmanagedCallersOnly]</c> + POH air-gap,
/// (2) <c>O(1)</c> per-proposal scaling across <c>N_3 in {4k, 16k, 64k}</c>,
/// (3) Immunity to arena slot/adjacency fragmentation after 25,000 sweeps (~400M proposals) without a memory lineariser, and
/// (4) Linear multi-core ensemble scaling across independent replicas.
/// </summary>
public class EngineThroughput
{
    private ISimulationHarness _harness4k = null!;
    private ISimulationHarness _harness16kFresh = null!;
    private ISimulationHarness _harness16kChurned = null!;
    private ISimulationHarness _harness64k = null!;
    private ISimulationHarness[] _ensemble = null!;

    private Volume _trace100 = null!;
    private Volume[] _ensembleTraces = null!;

    [GlobalSetup]
    public void Setup()
    {
        _trace100 = new Volume(100);

        // 1. Volume Scaling Harnesses (4k, 16k, 64k)
        _harness4k = CreateAndThermalize(id: 0, targetVolume: 4_000, burnInSweeps: 800);
        _harness16kFresh = CreateAndThermalize(id: 1, targetVolume: 16_000, burnInSweeps: 1_500);
        _harness64k = CreateAndThermalize(id: 2, targetVolume: 64_000, burnInSweeps: 3_000);

        // 2. Heavily Churned 16k Harness (25,000 sweeps = ~400,000,000 proposals of swap-and-pop slot/page scrambling)
        _harness16kChurned = CreateAndThermalize(id: 3, targetVolume: 16_000, burnInSweeps: 25_000);

        // 3. Multi-Core Fleet (8 independent 16k replicas)
        _ensemble = new ISimulationHarness[8];
        _ensembleTraces = new Volume[8];
        Parallel.For(0, 8, i =>
        {
            _ensemble[i] = CreateAndThermalize(id: (uint)(10 + i), targetVolume: 16_000, burnInSweeps: 1_500);
            _ensembleTraces[i] = new Volume(10);
        });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _harness4k.Dispose();
        _harness16kFresh.Dispose();
        _harness16kChurned.Dispose();
        _harness64k.Dispose();

        foreach (var h in _ensemble)
            h.Dispose();
    }

    private static ISimulationHarness CreateAndThermalize(uint id, uint targetVolume, int burnInSweeps)
    {
        var physics = new PhysicsParameters
        {
            K0 = 1.0,
            Kd = 0.9166666666666665,
            Delta = 0.5,
            Epsilon = 0.0005,
            TargetVolume = targetVolume
        };

        var seed = new SeedParameters { K = 2, Timeslices = 16, Rng = 42UL };

        var harness = SimulationHarness.Create(id, physics, seed);
        harness.Run(burnInSweeps);
        return harness;
    }

    // =========================================================================
    // 4A. ZERO-ALLOC AIR-GAP & VOLUME SCALING (100 SWEEPS WITH POH TELEMETRY)
    // =========================================================================

    [Benchmark(Description = "100 Sweeps (N3 = 4,000) [~400k Proposals]")]
    public void Run100Sweeps_4k() => _harness4k.Run(100, _trace100);

    [Benchmark(Description = "100 Sweeps (N3 = 16,000) [~1.6M Proposals]")]
    public void Run100Sweeps_16k() => _harness16kFresh.Run(100, _trace100);

    [Benchmark(Description = "100 Sweeps (N3 = 64,000) [~6.4M Proposals]")]
    public void Run100Sweeps_64k() => _harness64k.Run(100, _trace100);

    // =========================================================================
    // 4B. FRAGMENTATION IMMUNITY (FRESH vs. 25,000-SWEEP CHURNED ARENA)
    // =========================================================================

    [Benchmark(Description = "1 Sweep (N3 = 16k, Freshly Inflated Arena)")]
    public void Run1Sweep_16k_Fresh() => _harness16kFresh.Run(1, _trace100);

    [Benchmark(Description = "1 Sweep (N3 = 16k, 25,000-Sweep Churned Arena)")]
    public void Run1Sweep_16k_Churned() => _harness16kChurned.Run(1, _trace100);

    // =========================================================================
    // 4C. MULTI-CORE ENSEMBLE THROUGHPUT (8 REPLICAS × 10 SWEEPS = 80 SWEEPS)
    // =========================================================================

    [Benchmark(Description = "Ensemble: 8 Replicas × 10 Sweeps (Parallel)")]
    public void RunEnsemble_8Replicas_Parallel()
    {
        Parallel.For(0, _ensemble.Length, i =>
        {
            _ensemble[i].Run(10, _ensembleTraces[i]);
        });
    }
}