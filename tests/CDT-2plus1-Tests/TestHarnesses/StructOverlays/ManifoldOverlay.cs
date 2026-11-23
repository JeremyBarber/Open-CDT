using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory;
using CDT_Agnostic.RuntimeMemory.Stores;

using System.Runtime.InteropServices;
using Xunit;

namespace CDT_2plus1_Tests.TestHarnesses.StructOverlays;

/// <summary>
/// Testing-only memory overlay that mirrors the exact sequential field layout of <see cref="Manifold"/>.
/// Allows white-box memory mutation in tests while keeping production <see cref="Manifold"/> pointers strictly <see langword="private"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ManifoldOverlay
{
    public Tetrahedron* SimplexPool;
    public byte* SimplexTypes;
    public int* VertexTimes;
    public MemoryModel Memory;
    public TypeStore TypeStore31;
    public TypeStore TypeStore22;
    public TypeStore TypeStore13;

    static ManifoldOverlay()
    {
        Assert.Equal(sizeof(Manifold), sizeof(ManifoldOverlay));
    }
}