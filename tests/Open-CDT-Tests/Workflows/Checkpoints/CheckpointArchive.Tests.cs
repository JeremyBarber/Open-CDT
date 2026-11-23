using CDT_2plus1;
using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Checkpoints;

using System.Buffers.Binary;
using Xunit;

namespace Open_CDT_Tests.Workflows.Checkpoints;

public sealed class CheckpointArchiveTests : IDisposable
{
    private readonly string _tempDir;

    public CheckpointArchiveTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_ckpt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private RunConfig CreateTestConfig(uint replicas = 2, string? saveTo = "ensemble", string? startFrom = null) =>
        new()
        {
            Simulation = new SimulationParameters
            {
                Name = "CkptTest",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = replicas,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 1, Timeslices = 6, Rng = 12345UL, StartFrom = startFrom },
            Physics = new PhysicsParameters { K0 = 1.0, Kd = 0.92, Delta = 0.5, Epsilon = 0.02, TargetVolume = 600 },
            Output = new OutputParameters { Directory = _tempDir, SaveTo = saveTo }
        };

    [Fact]
    public void ResolveSavePath_AppendsExtensionAndResolvesRelativeAndRootedPaths()
    {
        var relCfg = CreateTestConfig(saveTo: "my_snapshot");
        var resolvedRel = CheckpointArchive.ResolveSavePath(relCfg);
        Assert.Equal(Path.Combine(_tempDir, "my_snapshot.cdtbin"), resolvedRel);

        var rootedInput = Path.Combine(_tempDir, "sub", "exact.cdtbin");
        var rootedCfg = CreateTestConfig(saveTo: rootedInput);
        Assert.Equal(rootedInput, CheckpointArchive.ResolveSavePath(rootedCfg));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sub")));

        Assert.Throws<InvalidOperationException>(() =>
            CheckpointArchive.ResolveSavePath(CreateTestConfig(saveTo: null)));
    }

    [Fact]
    public void SaveEnsemble_And_LoadEnsemble_PreservesBitExactMarkovTrajectoryForDirectContinuation()
    {
        var saveConfig = CreateTestConfig(replicas: 2, saveTo: "exact");
        var harnesses = new ISimulationHarness[2];
        for (uint i = 0; i < 2; i++)
        {
            harnesses[i] = SimulationHarness.Create(i, saveConfig.Physics, saveConfig.Seed);
            harnesses[i].Run(40);
        }

        CheckpointArchive.SaveEnsemble(harnesses, saveConfig);
        var ckptPath = CheckpointArchive.ResolveSavePath(saveConfig);

        var loadConfig = CreateTestConfig(replicas: 2, startFrom: ckptPath);
        var loaded = CheckpointArchive.LoadEnsemble<SimulationHarness>(loadConfig);

        try
        {
            for (var i = 0; i < 2; i++)
            {
                var traceOrig = new Volume(25);
                var traceLoad = new Volume(25);

                harnesses[i].Run(25, traceOrig);
                loaded[i].Run(25, traceLoad);

                Assert.Equal(traceOrig.TotalSimplices, traceLoad.TotalSimplices);
                Assert.Equal(traceOrig.TotalVertices, traceLoad.TotalVertices);
            }
        }
        finally
        {
            foreach (var h in harnesses)
                h.Dispose();
            foreach (var h in loaded)
                h.Dispose();
        }
    }

    [Fact]
    public void LoadEnsemble_ModuloExpansion_PreservesGeometryAndDivergesClonedTrajectories()
    {
        var saveConfig = CreateTestConfig(replicas: 2, saveTo: "expand");
        using var h0 = SimulationHarness.Create(0, saveConfig.Physics, saveConfig.Seed);
        using var h1 = SimulationHarness.Create(1, saveConfig.Physics, saveConfig.Seed);
        h0.Run(30);
        h1.Run(30);

        CheckpointArchive.SaveEnsemble([h0, h1], saveConfig);
        var ckptPath = CheckpointArchive.ResolveSavePath(saveConfig);

        var expandConfig = CreateTestConfig(replicas: 4, startFrom: ckptPath);
        var loaded = CheckpointArchive.LoadEnsemble<SimulationHarness>(expandConfig);

        try
        {
            var profileObs = new VolumeProfile(6);
            Assert.Equal(loaded[0].Measure(profileObs), loaded[2].Measure(profileObs));
            Assert.Equal(loaded[1].Measure(profileObs), loaded[3].Measure(profileObs));

            var trace0 = new Volume(30);
            var trace2 = new Volume(30);
            loaded[0].Run(30, trace0);
            loaded[2].Run(30, trace2);

            Assert.NotEqual(trace0.TotalSimplices, trace2.TotalSimplices);
        }
        finally
        {
            foreach (var h in loaded)
                h.Dispose();
        }
    }

    [Fact]
    public void LoadEnsemble_DownsizingAndOutOfBoundsOffsetTable_BehavesDeterministically()
    {
        var saveConfig = CreateTestConfig(replicas: 2, saveTo: "downsize");
        using var h0 = SimulationHarness.Create(0, saveConfig.Physics, saveConfig.Seed);
        using var h1 = SimulationHarness.Create(1, saveConfig.Physics, saveConfig.Seed);
        h0.Run(25);
        h1.Run(25);

        CheckpointArchive.SaveEnsemble([h0, h1], saveConfig);
        var ckptPath = CheckpointArchive.ResolveSavePath(saveConfig);

        // 1. Downsize 2 -> 1: Replica 0 continues bit-for-bit from slot 0.
        var downsizeCfg = CreateTestConfig(replicas: 1, startFrom: ckptPath);
        var loaded = CheckpointArchive.LoadEnsemble<SimulationHarness>(downsizeCfg);
        try
        {
            var single = Assert.Single(loaded) as ISimulationHarness;
            var traceOrig = new Volume(20);
            var traceLoad = new Volume(20);

            h0.Run(20, traceOrig);
            single!.Run(20, traceLoad);

            Assert.Equal(traceOrig.TotalSimplices, traceLoad.TotalSimplices);
            Assert.Equal(traceOrig.TotalVertices, traceLoad.TotalVertices);
        }
        finally
        {
            foreach (var h in loaded)
                h.Dispose();
        }

        // 2. Corrupt int64 offset[0] at byte 20 (immediately following the five uint32 header fields).
        var badOffsetPath = Path.Combine(_tempDir, "bad_offset.cdtbin");
        var bytes = File.ReadAllBytes(ckptPath);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(20, 8), 999_999_999L);
        File.WriteAllBytes(badOffsetPath, bytes);

        Assert.Throws<InvalidDataException>(() =>
            CheckpointArchive.LoadEnsemble<SimulationHarness>(CreateTestConfig(replicas: 1, startFrom: badOffsetPath)));
    }

    [Fact]
    public void LoadEnsemble_ValidatesHeaderDimensionsTimeslicesAndArenaCapacity()
    {
        var saveConfig = CreateTestConfig(replicas: 1, saveTo: "guards");
        using (var h = SimulationHarness.Create(0, saveConfig.Physics, saveConfig.Seed))
        {
            CheckpointArchive.SaveEnsemble([h], saveConfig);
        }

        var ckptPath = CheckpointArchive.ResolveSavePath(saveConfig);

        var badDims = CreateTestConfig(replicas: 1, startFrom: ckptPath) with
        {
            Simulation = saveConfig.Simulation with { SpacelikeDimensions = 3 }
        };
        Assert.Throws<InvalidOperationException>(() => CheckpointArchive.LoadEnsemble<SimulationHarness>(badDims));

        var badSlices = CreateTestConfig(replicas: 1, startFrom: ckptPath) with
        {
            Seed = saveConfig.Seed with { Timeslices = 12 }
        };
        Assert.Throws<InvalidOperationException>(() => CheckpointArchive.LoadEnsemble<SimulationHarness>(badSlices));

        var oversizedTarget = CreateTestConfig(replicas: 1, startFrom: ckptPath) with
        {
            Physics = saveConfig.Physics with { TargetVolume = 500_000 }
        };
        Assert.Throws<InvalidOperationException>(() => CheckpointArchive.LoadEnsemble<SimulationHarness>(oversizedTarget));

        var corruptPath = Path.Combine(_tempDir, "corrupt.cdtbin");
        File.WriteAllBytes(corruptPath, [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13]);
        Assert.Throws<InvalidDataException>(() =>
            CheckpointArchive.LoadEnsemble<SimulationHarness>(CreateTestConfig(replicas: 1, startFrom: corruptPath)));
    }
}