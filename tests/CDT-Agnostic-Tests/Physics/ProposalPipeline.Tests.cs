using CDT_Agnostic.Accessors;
using CDT_Agnostic.Physics;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.Physics;

public sealed unsafe class ProposalPipelineTests
{
    private readonly record struct DummyContext(int SiteId);

    private struct PipelineTracker
    {
        public bool SiteValid;
        public bool EnergeticallyFavourable;
        public int ExpectedSiteId;

        public int SelectCalls;
        public int EnergyCalls;
        public int PerformCalls;
        public int PerformedSiteId;
    }

    private readonly struct DummyInspector(PipelineTracker* tracker) : IInspectingAccessor
    {
        public readonly PipelineTracker* Tracker = tracker;
        public static int FacetsPerSimplex => 4;
        public static int VerticesPerSimplex => 4;
        public VertexId GetVertex(SimplexId simplexId, int vertexIndex) => new(0);
        public TimeSlice GetVertexTime(VertexId vertexId) => new(0);
        public SimplexId GetNeighbor(SimplexId simplexId, int faceIndex) => new(0);
    }

    private readonly struct DummyMutator(PipelineTracker* tracker) : IMutatingAccessor<DummyInspector>
    {
        public readonly PipelineTracker* Tracker = tracker;
        public DummyInspector Inspect => new(Tracker);
    }

    private readonly struct DummyRandom : IRandomAccessor
    {
        public uint GetRandom() => 0;
        public int GetRandom(int bound) => 0;
        public uint GetRandomBoundedAtFour() => 0;
        public SimplexId SelectRandomActiveSimplex() => new(0);
        public VertexId SelectRandomVertex() => new(0);
    }

    private readonly struct DummyPhysics;

    private readonly struct SpyMove
        : IPachnerMove<DummyContext, DummyRandom, DummyInspector, DummyMutator, DummyPhysics>
    {
        public static bool TrySelectValidSiteForMove(
            DummyRandom random,
            DummyInspector inspector,
            out DummyContext context)
        {
            inspector.Tracker->SelectCalls++;
            if (!inspector.Tracker->SiteValid)
            {
                context = default;
                return false;
            }

            context = new DummyContext(inspector.Tracker->ExpectedSiteId);
            return true;
        }

        public static bool IsMoveEnergeticallyFavourable(
            DummyRandom random,
            DummyInspector inspector,
            DummyPhysics physics)
        {
            inspector.Tracker->EnergyCalls++;
            return inspector.Tracker->EnergeticallyFavourable;
        }

        public static void PerformMove(DummyMutator mutator, in DummyContext context)
        {
            mutator.Tracker->PerformCalls++;
            mutator.Tracker->PerformedSiteId = context.SiteId;
        }
    }

    [Fact]
    public void Propose_WhenSiteInvalid_ShortCircuitsBeforeThermodynamicsAndExecution()
    {
        PipelineTracker tracker = new() { SiteValid = false, EnergeticallyFavourable = true, ExpectedSiteId = 42 };

        ProposalPipeline.Propose<SpyMove, DummyContext, DummyRandom, DummyInspector, DummyMutator, DummyPhysics>(
            default, new DummyMutator(&tracker), default);

        Assert.Equal(1, tracker.SelectCalls);
        Assert.Equal(0, tracker.EnergyCalls);
        Assert.Equal(0, tracker.PerformCalls);
    }

    [Fact]
    public void Propose_WhenSiteValidButEnergeticallyUnfavourable_ShortCircuitsBeforeExecution()
    {
        PipelineTracker tracker = new() { SiteValid = true, EnergeticallyFavourable = false, ExpectedSiteId = 42 };

        ProposalPipeline.Propose<SpyMove, DummyContext, DummyRandom, DummyInspector, DummyMutator, DummyPhysics>(
            default, new DummyMutator(&tracker), default);

        Assert.Equal(1, tracker.SelectCalls);
        Assert.Equal(1, tracker.EnergyCalls);
        Assert.Equal(0, tracker.PerformCalls);
    }

    [Fact]
    public void Propose_WhenSiteValidAndEnergeticallyFavourable_ExecutesPerformMoveWithContext()
    {
        PipelineTracker tracker = new() { SiteValid = true, EnergeticallyFavourable = true, ExpectedSiteId = 777 };

        ProposalPipeline.Propose<SpyMove, DummyContext, DummyRandom, DummyInspector, DummyMutator, DummyPhysics>(
            default, new DummyMutator(&tracker), default);

        Assert.Equal(1, tracker.SelectCalls);
        Assert.Equal(1, tracker.EnergyCalls);
        Assert.Equal(1, tracker.PerformCalls);
        Assert.Equal(777, tracker.PerformedSiteId);
    }
}