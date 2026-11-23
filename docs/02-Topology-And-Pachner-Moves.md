# 02 — 2+1D Simplicial Topology, Seeding & Branchless Pachner Moves

This document details the geometric representation, global topological invariants, initial manifold bootstrapping (`GeodesicSphere` and `Icosahedral`), and branchless bitwise mechanics of the five ergodic Pachner moves in `CDT-2plus1`.

---

## 1. Simplicial Conventions, Invariants & SIMD `Tetrahedron` Layout

In 2+1D Causal Dynamical Triangulations, spacetime is discretized into $3$-simplices (**tetrahedra**) spanning adjacent discrete proper-time slices $t$ and $(t + 1) \bmod T$.

### 1.1 Foliation Species (`SimplexType : byte`)
Every causal tetrahedron has its 4 vertices distributed across two adjacent time slices $t_{\text{low}}$ and $t_{\text{high}} = (t_{\text{low}} + 1) \bmod T$, yielding three distinct species:
- **`ThreeOne` (`(3,1)`, value `0`):** 3 vertices on $t_{\text{low}}$ (forming a spacelike triangle base) and 1 apex vertex on $t_{\text{high}}$.
- **`TwoTwo` (`(2,2)`, value `1`):** 2 vertices on $t_{\text{low}}$ and 2 vertices on $t_{\text{high}}$ (connecting a spacelike edge on $t_{\text{low}}$ to a spacelike edge on $t_{\text{high}}$ via 4 timelike edges).
- **`OneThree` (`(1,3)`, value `2`):** 1 apex vertex on $t_{\text{low}}$ and 3 vertices on $t_{\text{high}}$ (forming a spacelike triangle roof).

### 1.2 Closed $S^2 \times S^1$ Foliation & Euler Invariants
On any valid closed 2+1D $S^2 \times S^1$ manifold with $T$ timeslices:
1. **Foliation Symmetry:** Because every spacelike triangle on slice $t$ forms the top face of exactly one `(1,3)` simplex in slab $[t-1, t]$ and the bottom face of exactly one `(3,1)` simplex in slab $[t, t+1]$:
   $$N_{31} = N_{13}$$
2. **Spatial Sphere Euler Identity:** Every timeslice $t$ is a triangulated 2-sphere ($V_t - E_t + F_t = 2$ with $3F_t = 2E_t \implies F_t = 2V_t - 4$). Summing over all $T$ slices yields the exact global conservation law between vertices and asymmetric simplices:
   $$N_{31} - 2N_0 = -4T \quad \Longleftrightarrow \quad N_{31} + N_{13} - 4N_0 = -8T$$

### 1.3 Opposite-Vertex Facet Indexing, Complete-Graph Identity & SIMD Intrinsics
Inside `Tetrahedron` (32 bytes):
```csharp
public fixed int Vertices[4];          // Global VertexId at local slot i in [0..3]
public fixed int NeighborSimplices[4]; // Global SimplexId across facet opposite slot i
```
By invariant convention, **local facet `i` is the triangle opposite local vertex `i`** (i.e., the triangle formed by the 3 vertices in slots $\{0,1,2,3\} \setminus \{i\}$).
- **The Complete-Graph Edge Identity ($\binom{V}{2} = \sum_{i=0}^{V-1} i$):**
  A $d$-simplex is a complete graph $K_V$ on $V = d + 1$ vertices, containing $\binom{V}{2} = \frac{V(V-1)}{2}$ edges (`EdgesPerSimplex = 6` for a tetrahedron). Because 0-indexed local vertex slots are numbered $0, 1, \dots, V - 1$, the sum of all local vertex slot indices is identically $0 + 1 + 2 + 3 = 6 = \text{EdgesPerSimplex}$. Given any 3 local vertex slots `slot1, slot2, slot3` of a shared triangle, the opposite facet index is simply:
  ```csharp
  int oppositeFacet = EdgesPerSimplex - (slot1 + slot2 + slot3);
  ```
- **`FindVertexSlot(VertexId)` & `GetFacetFacingSimplexIndex(SimplexId)`:**
  Marked `readonly` to prevent 32-byte defensive struct copies on `ref readonly Tetrahedron` references. Loads the 16-byte `Vertices` or `NeighborSimplices` block into a 128-bit `Vector128<int>` register, broadcasts the target ID across all 4 lanes, executes a single hardware `Vector128.Equals` + `ExtractMostSignificantBits()`, and returns `BitOperations.TrailingZeroCount(mask)` in $\mathcal{O}(1)$ time with zero branches.
- **`GetNonEdgeVertices(VertexId vA, VertexId vB)`:**
  Finds local slots `slotA` and `slotB`, constructs the 4-bit mask `(1 << slotA) | (1 << slotB)`, inverts the lower 4 bits `(~mask) & 0b1111`, and extracts the two complementary local vertex slots via `TrailingZeroCount` and `invertedMask & (invertedMask - 1)`.

---

## 2. Bootstrapping the $S^2 \times S^1$ Spacetime (`GeodesicSphere` & `Icosahedral`)

### 2.1 Why Caspar-Klug Icosahedral Subdivision? (Avoiding Preferential Attachment)
By **Pachner's Theorem (Pachner, 1991)**, any two piecewise-linear triangulations of the same manifold are connected by a finite sequence of bistellar (Pachner) moves, so any valid $S^2 \times S^1$ seed lies in the same ergodic configuration space. However, the *initial vertex degree distribution* strongly governs thermalization burn-in time:
- **Iterative $(1 \to 3)$ Triangle Splitting (Preferential Attachment Artifact):** Starting from a 4-vertex tetrahedron boundary ($S^2$) and repeatedly splitting random triangles via $(1 \to 3)$ vertex insertions suffers from **preferential attachment**: the 4 original vertices participate in more triangles at each step, accumulating spatial degrees of $\sim 32$ ($\sim 128$ incident tetrahedra, spanning $\sim 10$ `AdjacencyBlock` pages) while newly inserted vertices have degree 3. Unwinding this artificial hub-vertex skew requires $\mathcal{O}(N_3)$ extra burn-in sweeps.
- **Caspar-Klug Icosahedral Geodesic Subdivision (`GeodesicSphere` & `Icosahedral`):** Subdividing the 20 faces of a regular icosahedron into a $k \times k$ barycentric grid is 100% deterministic, requires zero floating-point coordinates, and produces a **maximally uniform degree distribution**: every interior vertex has spatial degree **6**, and the 12 icosahedral poles have spatial degree **5** (at most $\sim 24$ incident 3D tetrahedra, fitting in $\le 2$ `AdjacencyBlock` cache lines for every vertex in the manifold).

### 2.2 `GeodesicSphere`: 64-Bit Canonical Barycentric Subdivision
Starting from a base Platonic icosahedron ($V = 12$ vertices, $E = 30$ edges, $F = 20$ faces), each triangular face is subdivided into a $k \times k$ barycentric grid ($k \ge 1$).
To weld shared corners and edges between adjacent icosahedral faces without floating-point rounding or duplicate vertices, each local grid point `(face, i, j)` is hashed into a **64-bit canonical key**:
- **Bits `60..63`:** Geometry type tag (`Corner = 0`, `Edge = 1`, `Interior = 2`).
- **Corners (`i == 0, j == 0`, etc.):** Maps directly to the base icosahedron vertex ID $v \in [0..11]$.
- **Shared Edges (`j == 0`, `i == 0`, or `i + j == k`):** Orders the endpoint base vertices $(\min(v_1, v_2), \max(v_1, v_2))$ and normalizes the parametric edge distance $t \in [1 .. k-1]$ relative to the lower vertex ID so both incident faces produce the identical 64-bit key.
- **Resulting $S^2$ Slice Metrics:**
  $$V_{\text{slice}} = 10k^2 + 2, \quad E_{\text{slice}} = 30k^2, \quad F_{\text{slice}} = 20k^2, \quad \chi(S^2) = V - E + F = 2$$

### 2.3 `Icosahedral`: Prism Extrusion & BFS Orientation Fixing
Across $T \ge 3$ timeslices, each of the $20k^2$ spatial triangles at slice $t$ is extruded to slice $t_{\text{next}} = (t + 1) \bmod T$ as a triangular prism (`SimplicesPerPrism = 3`):
1. **Canonical Vertex Ordering ($a < b < c$):**
   Sorting the three spatial vertex indices of each triangle before slicing guarantees that adjacent prisms agree on how their shared rectangular timelike faces are bisected by a diagonal edge.
2. **3-Tetrahedra Prism Decomposition:**
   Each prism between $(a_0, b_0, c_0)$ at $t$ and $(a_1, b_1, c_1)$ at $t_{\text{next}}$ is decomposed into:
   - `t31 = (a0, b0, c0, a1)` (`SimplexType.ThreeOne`)
   - `t22 = (b0, c0, a1, b1)` (`SimplexType.TwoTwo`)
   - `t13 = (c0, a1, b1, c1)` (`SimplexType.OneThree`)
   yielding $N_{31} = N_{22} = N_{13} = 20k^2 T$ and total simplices $N_3 = 60k^2 T$.
3. **Facet Welding & BFS Orientation (`WireNeighbors` & `FixGlobalOrientation`):**
   `WireNeighbors` matches all $4 N_3$ facets in a single pass via sorted vertex-triplet keys `(v0, v1, v2)` (verifying `openFacets.Count == 0` so the manifold is strictly closed). `FixGlobalOrientation` then traverses the dual graph via Breadth-First Search (BFS) and calls `SeederAccessor.InvertOrientation` whenever adjacent simplices have mismatched permutation parity.

---

## 3. The Five Ergodic 2+1D Pachner Moves

Every move implements `IPachnerMove` via three methods: `TrySelectValidSiteForMove`, `IsMoveEnergeticallyFavourable`, and `PerformMove`.

| Move | Physical Role | $\Delta N_0$ | $\Delta N_1^{\text{SL}}$ | $\Delta N_1^{\text{TL}}$ | $\Delta N_2$ | $\Delta N_{31}$ | $\Delta N_{22}$ | $\Delta N_{13}$ | $\Delta N_3$ | Hastings Ratio $H$ |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **`Move23`** | Timelike Face $\to$ Edge | $0$ | $0$ | $+1$ | $+2$ | $0$ | $+1$ | $0$ | $+1$ | $\frac{4}{3} \frac{N_{22}}{N_{22} + 1}$ |
| **`Move32`** | Timelike Edge $\to$ Face | $0$ | $0$ | $-1$ | $-2$ | $0$ | $-1$ | $0$ | $-1$ | $\frac{3}{4} \frac{N_{22} - 1}{N_{22}}$ |
| **`Move26`** | Spatial Vertex Insertion | $+1$ | $+3$ | $+2$ | $+8$ | $+2$ | $0$ | $+2$ | $+4$ | $\frac{N_{13}}{N_0 + 1}$ |
| **`Move62`** | Spatial Vertex Deletion | $-1$ | $-3$ | $-2$ | $-8$ | $-2$ | $0$ | $-2$ | $-4$ | $\frac{N_0}{N_{13} - 2}$ |
| **`Move44`** | Spatial Edge Flip | $0$ | $0$ | $0$ | $0$ | $0$ | $0$ | $0$ | $0$ | $1$ ($P_{\text{accept}} \equiv 1$) |

---

### 3.1 `Move23`: Timelike `(2,3)` Expansion
Replaces a timelike triangle $(v_J, v_K, v_L)$ shared by a `(2,2)` simplex (`s0`) and an asymmetric `(3,1)` or `(1,3)` simplex (`s1`) with a new central timelike edge $(v_{\text{Target}}, v_{\text{Neighbour}})$ surrounded by three simplices (two `(2,2)`s and one asymmetric).
- **Validation (`IsValidSite`):**
  1. **Foliation Check:** Neighbor `id1` across the chosen facet of `s0` must not be `TwoTwo` (if both were `TwoTwo`, the new edge would connect two vertices on the same timeslice, creating an illegal spacelike edge).
  2. **Simplicial Link Condition:** Neither `s0` nor `s1` may share multiple facets with the same neighbor (prevents local boundary folding).
  3. **Causality Check:** Poles $v_{\text{Target}}$ and $v_{\text{Neighbour}}$ must lie on different timeslices.
  4. **Manifold Link Condition:** Edge $(v_{\text{Target}}, v_{\text{Neighbour}})$ must not already exist (`!Adjacency.EdgeExists`).
- **Branchless Execution (`PerformMove`):**
  1. **Parity-Preserving Shared Vertex Extraction:** Uses `p = slot & 1` and `& 3` masking to extract $(v_J, v_K, v_L)$ with consistent outward orientation.
  2. **Periodic-Boundary-Aware Base Slice (`minT` + `wrapMask`):**
     Computes `minT` and `maxT` of the two poles using sign-bit smearing (`diff >> 63`), then applies `wrapMask = (1 - (maxT - minT)) >> 63` so that across the periodic boundary ($t = T - 1 \to 0$, where `maxT - minT > 1`), `minT` selects the true lower slice $T - 1$.
  3. **Two's Complement Zero Trick & Shift-Register Classification:**
     Counts vertices on `minT` via `~((ulong)tDiff | (ulong)(-tDiff)) >> 63` and shifts the 64-bit `PackedTypes` register (`OneThree << 8 | TwoTwo << 16 | ThreeOne << 24`) to classify all three new simplices without a single branch.

---

### 3.2 `Move32`: Timelike `(3,2)` Contraction
The exact inverse of `Move23`: collapses a degree-3 timelike edge $(v_A, v_B)$ shared by two `(2,2)` simplices and one asymmetric simplex (`idAsym`) into a single timelike triangle $(P, Q, R)$ shared by one `(2,2)` and one asymmetric simplex.
- **Validation (`IsValidSite`):**
  1. **Causality Check:** Edge $(v_A, v_B)$ on the chosen `(2,2)` simplex must be timelike ($t_A \ne t_B$).
  2. **Degree-3 Check:** `Adjacency.TryGetExactlyNSharedSimplices(vA, vB, incident, 3)` must return `true`.
  3. **Foliation Sum Check:** Reinterpreting `SimplexIsType(..., TwoTwo)` booleans as bytes (`Unsafe.As<bool, byte>`) must sum to `2`.
  4. **Bitwise Asymmetric ID Isolation:** Uses `mask = is22 - 1` (`0` for `TwoTwo`, `-1` for asymmetric) to isolate `idAsym = (id0 & mask0) | (id1 & mask1) | (id2 & mask2)`.
  5. **Manifold Link Condition:** Bounding triangle $(P, Q, R)$ must not already exist (`!Adjacency.TriangleExists(P, Q, R)`).
- **Branchless Execution (`PerformMove`):**
  Deduces the singleton pole via `GetVertexTime(eqV) != GetVertexTime(VA)`, routes external neighbors via bitwise masks, and computes the inversion count of `s0` (`inversions & 1`) to preserve orientation parity when registering `t22` and `tAsym`.

---

### 3.3 `Move26`: Spatial `(2,6)` Vertex Insertion
Inserts a new vertex $v_{\text{New}}$ into the center of the shared spacelike triangle $(A, B, C)$ between a downward `(1,3)` simplex and an upward `(3,1)` simplex, expanding 2 simplices into 6 (three `(3,1)`s and three `(1,3)`s).
- **Validation (`IsValidSite`):**
  1. **Pigeonhole Equator Deduction:** A `(1,3)` simplex has 3 vertices on `timeEquator` and 1 pole. Sampling slots `0, 1, 2` guarantees at least two share `timeEquator`:
     ```csharp
     int isT0 = eq01 | eq02;
     TimeSlice timeEquator = new((isT0 * t0.Value) + ((1 - isT0) * t1.Value));
     ```
  2. **Pole Isolation:** Identifies the unique vertex slot `slotDown` whose time differs from `timeEquator` and verifies that the neighbor across `slotDown` is a valid `ThreeOne` simplex.
  3. **Zero Link-Condition Overhead:** Because `Move26` is purely additive (inserting a brand-new vertex), it can never create duplicate edges or pinch the manifold, bypassing graph traversal completely.

---

### 3.4 `Move62`: Spatial `(6,2)` Vertex Deletion
The exact inverse of `Move26`: deletes a spatial vertex $v_{\text{Target}}$ of total degree 6 (surrounded by three `(3,1)` simplices above and three `(1,3)` simplices below) and collapses the 6-simplex bipyramid into a single `(3,1)` / `(1,3)` pair sharing spatial triangle $(A, B, C)$.
- **Validation (`IsValidSite`):**
  1. **Degree-6 & Hemisphere Symmetry:** `GetExactlyNSimplices(vTarget, incident, 6)` must yield 3 `ThreeOne` and 3 `OneThree` simplices.
  2. **Causality Guard:** In both `s31_0` and `s13_0`, exactly 1 vertex may differ in time from $v_{\text{Target}}$ (`isU0 + isU1 + isU2 + isU3 == 1`). This prevents selecting the apex pole of a bipyramid instead of an equatorial vertex.
  3. **Manifold Link Condition:** Spatial triangle $(A, B, C)$ must not already exist (`!Adjacency.TriangleExists(A, B, C)`).
- **The Algebraic Sum Trick (`PerformMove`):**
  To match each of the 6 old simplices to the external boundary opposite $A$, $B$, or $C$ in $\mathcal{O}(1)$ time without nested loops:
  - Each upper `(3,1)` simplex contains $\{v_{\text{Target}}, U, X, Y\}$ where $X, Y \in \{A, B, C\}$.
  - Subtracting the pole sum $p_{31} = v_{\text{Target}} + U$ from the 4-vertex sum of the simplex leaves $X + Y$.
  - Subtracting $X + Y$ from the equator sum $\text{eqSum} = A + B + C$ algebraically isolates the missing equatorial vertex $Z = (A + B + C) - (X + Y)$ in a single arithmetic expression.

---

### 3.5 `Move44`: Spatial `(4,4)` Edge Flip
Flips a spacelike diagonal edge $(v_A, v_B)$ shared by two `(3,1)` simplices above and two `(1,3)` simplices below to the alternate spacelike diagonal $(C, D)$ of the equatorial quadrilateral.
- **Validation (`IsValidSite`):**
  1. **Spacelike Edge Check:** $v_A$ and $v_B$ must lie on the same timeslice ($t_A == t_B$).
  2. **Degree-4 & Foliation Check:** `TryGetExactlyNSharedSimplices(vA, vB, incident, 4)` must contain 2 `ThreeOne` (and thus 2 `OneThree`) simplices.
  3. **Manifold Link Condition:** Alternate diagonal $(C, D)$ must not already exist (`!Adjacency.EdgeExists(C, D)`).
- **O(1) Matrix Role Routing (`PerformMove`):**
  Classifies the 4 unordered incident simplices into their exact geometric roles (`idC31`, `idD31`, `idC13`, `idD13`) via binary indicator products `(is31 & hasC) * id`, extracts poles $U$ and $V$, and rewires the flipped bipyramid around $(C, D)$ with zero branches.

