namespace CDT_2plus1.Seed;

/// <summary>
/// Combines the combinatorial topology of a Platonic icosahedron with <c>k</c>-frequency barycentric subdivision
/// to generate a closed 2D <c>S^2</c> spatial triangulation (<c>V = 10k^2 + 2</c>, <c>F = 20k^2</c>).
/// Runs in managed C# once during initial universe bootstrapping.
/// </summary>
internal static class GeodesicSphere
{
    // =====================================================================================
    // 64-BIT CANONICAL KEY MEMORY LAYOUT (Barycentric Deduplication)
    // =====================================================================================
    // Bits 00-11 : 'j' coordinate or 't' parametric edge distance (Max 4095)
    // Bits 12-23 : 'i' coordinate or 'max_v' vertex ID (Max 4095)
    // Bits 24-35 : 'face' ID or 'min_v' vertex ID (Max 4095)
    // Bits 60-63 : Geometry Type Mask (Corner, Edge, or Interior)
    private const long CORNER_MASK = 0L << 60;
    private const long EDGE_MASK = 1L << 60;
    private const long INTERIOR_MASK = 2L << 60;

    /// <summary>
    /// Standard oriented vertex triplets for the 20 triangular faces of a base icosahedron.
    /// </summary>
    private static readonly int[] IcoFaces =
    [
         0,  1,  2,    0,  2,  3,    0,  3,  4,    0,  4,  5,    0,  5,  1,
         1,  6,  2,    2,  6,  7,    2,  7,  3,    3,  7,  8,    3,  8,  4,
         4,  8,  9,    4,  9,  5,    5,  9, 10,    5, 10,  1,    1, 10,  6,
         6, 11,  7,    7, 11,  8,    8, 11,  9,    9, 11, 10,   10, 11,  6
    ];

    /// <summary>
    /// Subdivides an icosahedron into a <c>k × k</c> triangular grid per face and canonicalizes
    /// shared corners and edges so no boundary vertices are duplicated.
    /// </summary>
    /// <param name="subdivisions">The subdivision frequency <c>k</c> (must be at least 1).</param>
    /// <returns>The total unique vertex count (<c>10k^2 + 2</c>) and the array of <c>20k^2</c> triangle vertex triplets.</returns>
    internal static (int VertexCount, (int A, int B, int C)[] Triangles) Build(uint subdivisions)
    {
        ArgumentOutOfRangeException.ThrowIfZero(subdivisions);

        var k = (int)subdivisions;
        var totalExpectedVertices = 10 * k * k + 2;
        var globalIdMap = new Dictionary<long, int>(totalExpectedVertices);
        var nextId = 0;

        int GetOrAddId(long canonicalKey)
        {
            if (!globalIdMap.TryGetValue(canonicalKey, out var id))
            {
                id = nextId++;
                globalIdMap[canonicalKey] = id;
            }
            return id;
        }

        var faceCount = IcoFaces.Length / 3;
        var triangleList = new List<(int, int, int)>(20 * k * k);

        for (var face = 0; face < faceCount; face++)
        {
            for (var i = 0; i <= k; i++)
            {
                for (var j = 0; j <= k - i; j++)
                {
                    var idA = GetOrAddId(GetCanonicalKey(face, i, j, k));

                    // Emit "Up-facing" triangle (relative to the local barycentric grid)
                    if (i + j < k)
                    {
                        var idB = GetOrAddId(GetCanonicalKey(face, i + 1, j, k));
                        var idC = GetOrAddId(GetCanonicalKey(face, i, j + 1, k));

                        triangleList.Add((idA, idB, idC));

                        // Emit "Down-facing" inverted triangle to fill the interior grid cell
                        if (j > 0)
                        {
                            var idD = GetOrAddId(GetCanonicalKey(face, i + 1, j - 1, k));
                            triangleList.Add((idA, idD, idB));
                        }
                    }
                }
            }
        }

        return (nextId, triangleList.ToArray());
    }

    /// <summary>
    /// Hashes a local <c>(face, i, j)</c> barycentric coordinate into a globally unique 64-bit canonical key.
    /// Safely merges boundary edges and corners shared by adjacent icosahedral faces.
    /// </summary>
    private static long GetCanonicalKey(int face, int i, int j, int k)
    {
        var vA = IcoFaces[face * 3 + 0];
        var vB = IcoFaces[face * 3 + 1];
        var vC = IcoFaces[face * 3 + 2];

        // 1. Absolute Corners (maps back to the 12 base icosahedron vertices)
        if (i == 0 && j == 0) return CORNER_MASK | (long)vA;
        if (i == k && j == 0) return CORNER_MASK | (long)vB;
        if (i == 0 && j == k) return CORNER_MASK | (long)vC;

        // 2. Shared Edge AB
        if (j == 0)
        {
            var t = (vA < vB) ? i : (k - i);
            return EdgeKey(vA, vB, t);
        }

        // 3. Shared Edge AC
        if (i == 0)
        {
            var t = (vA < vC) ? j : (k - j);
            return EdgeKey(vA, vC, t);
        }

        // 4. Shared Edge BC
        if (i + j == k)
        {
            var t = (vB < vC) ? j : (k - j);
            return EdgeKey(vB, vC, t);
        }

        // 5. Unique Face Interior
        return INTERIOR_MASK | ((long)face << 24) | ((long)i << 12) | (long)j;
    }

    /// <summary>
    /// Computes a canonical key for any interior edge point by ordering the endpoint vertex IDs.
    /// </summary>
    private static long EdgeKey(int v1, int v2, int t)
    {
        var min = Math.Min(v1, v2);
        var max = Math.Max(v1, v2);

        return EDGE_MASK | ((long)min << 24) | ((long)max << 12) | (long)t;
    }
}