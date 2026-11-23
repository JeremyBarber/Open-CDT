# 01 — Engine Architecture & Unmanaged Memory Model

This document specifies the systems architecture, memory model, and hardware-sympathetic design of the `CDT-Agnostic` and `CDT-2plus1` libraries—the core simulation engine of **Open-CDT**.

---

## 1. The Three-Tier "Air-Gap" Architecture

Open-CDT enforces a strict compile-time and runtime boundary between high-level scientific orchestration and low-level simplicial mutation:

| Tier | Assembly | Execution Domain | Responsibilities |
| :--- | :--- | :--- | :--- |
| **Tier 1 (Orchestration)** | `Open-CDT` / `Open-CDT-CLI` | Managed C# (.NET GC) | YAML configuration (`RunConfig`), parallel susceptibility root-finding with collinear neighbor warm-starting (`CosmologicalConstantTunerJob`), macroscopic phase classification (`PhaseClassifierJob`), deterministic burn-in & Madras-Sokal $\tau_{\text{int}}$ calibration (`ThermalizerJob`), ensemble sampling (`SamplingJob`), `.cdtbin` checkpoint archiving, CSV serialization, and ScottPlot rendering. |
| **Tier 2 (Core Engine)** | `CDT-Agnostic` | Dimension-Blind Unmanaged Primitives | Fixed-stride slab allocators (`SlotStore`, `TypeStore`, `AdjacencyMap`), `Pcg32` PRNG, `Q4.28` fixed-point `Metropolis` & `MoveWeights` LUTs, generic `ProposalPipeline`, Pinned Object Heap (`Volume`) telemetry, and dimension-agnostic observables (`VolumeProfile`, `Hausdorff`, `SpectralDimension`). |
| **Tier 3 (Physics Cassette)** | `CDT-2plus1` | 2+1D Unmanaged C-ABI Engine | 32-byte SIMD `Tetrahedron`, `Manifold` & `Dynamics` arenas, `GeodesicSphere` & `Icosahedral` ($S^2 \times S^1$) seeder, branchless Pachner moves (`Move23`, `Move32`, `Move26`, `Move62`, `Move44`), `.cdtbin` snapshot checkpointing, and `[UnmanagedCallersOnly] SimulationEngine.ExecuteSweepBurst`. |

### 1.1 Crossing the Boundary (`SimulationHarness` -> `SimulationEngine`)
`SimulationHarness` is the sole managed class in `CDT-2plus1`. It owns the `Universe` instance and caches a raw C-ABI function pointer to the unmanaged engine loop:

```csharp
private static readonly unsafe delegate* unmanaged<
    RandomAccessor,
    MutatingAccessor,
    PhysicsAccessor,
    Volume.Unmanaged*,
    int,
    void> _executeSweepBurst = &SimulationEngine.ExecuteSweepBurst;
```

When `harness.Run(sweepsToRun, trace)` is invoked:
1. Execution transitions via a hardware `calli` instruction directly into `[UnmanagedCallersOnly] SimulationEngine.ExecuteSweepBurst` in `< 1 ns`, bypassing the .NET P/Invoke marshaller.
2. Inside `ExecuteSweepBurst`, zero heap allocations, zero interface boxing, and zero Garbage Collector (GC) interruptions occur across millions of Monte Carlo proposals.

---

## 2. Unmanaged Memory Arenas & Cache-Line Sympathy

All dynamic triangulation state lives on the OS native heap (`NativeMemory.Alloc` / `NativeMemory.AlignedAlloc`), encapsulated inside `Manifold` (`CDT-2plus1`) and `MemoryModel` (`CDT-Agnostic`).

### 2.1 64-Byte Cache-Line Alignment & Hot/Cold Separation
Modern x86-64 and ARM64 CPUs fetch memory in **64-byte L1 cache lines**. Open-CDT aligns its hottest structures to exact 64-byte boundaries (`RuntimeConstants.CacheLineBytes = 64`) and separates hot type tags from 32-byte simplex geometry:

1. **Dense `SimplexTypes` (`byte*`, 1 Byte per Simplex):**
   - Foliation classifications (`ThreeOne = 0`, `TwoTwo = 1`, `OneThree = 2`) are stored in a dedicated contiguous `byte*` array rather than embedded inside `Tetrahedron`.
   - 64 simplex type tags pack into a single 64-byte L1 cache line, and `Tetrahedron` remains exactly 32 bytes instead of padding out to 36 or 40 bytes.
2. **`Tetrahedron` (32 Bytes) in `SimplexPool`:**
   - Each tetrahedron consists of `fixed int Vertices[4]` (16 bytes) + `fixed int NeighborSimplices[4]` (16 bytes) = **32 bytes**.
   - `SimplexPool` is allocated on a 64-byte alignment boundary (`AllocateAligned<Tetrahedron>`). Every pair of simplices `(2k, 2k + 1)` fits inside a single 64-byte cache line with zero cache-line straddling, allowing `Tetrahedron.FindVertexSlot` and `GetFacetFacingSimplexIndex` to load all 4 vertices or all 4 neighbors in a single `Vector128<int>` SIMD instruction.
3. **`AdjacencyBlock` (64 Bytes) in `AdjacencyMap`:**
   - Vertex-to-simplex incidence lists vary dynamically in degree. Rather than allocating heap containers per vertex, `AdjacencyMap` manages an arena of 64-byte cache-aligned `AdjacencyBlock` pages:
     - `Count` (`int`, 4 bytes): number of valid entries in the head block.
     - `NextBlock` (`int`, 4 bytes): index of the overflow page (`-1` if none).
     - `Simplices[14]` (`fixed int[14]`, 56 bytes): 14 packed simplex IDs.
   - Total size: `4 + 4 + 56 = 64 bytes` (exactly 1 CPU cache line).
   - For typical vertices (degree $\le 14$), the entire incidence list resides in a single L1 cache line. During `EdgeExists`, `TriangleExists`, or `TryGetExactlyNSharedSimplices`, `AdjacencyMap` loads the 14 slots into two 256-bit `Vector256<int>` AVX2 registers and compares 8 simplices per instruction cycle.

### 2.2 O(1) Briggs-Torczon Sparse-Sets (`SlotStore` & `TypeStore`)
To achieve strictly $\mathcal{O}(1)$ ID allocation, $\mathcal{O}(1)$ deletion, and $\mathcal{O}(1)$ uniform random sampling without scanning dead slots:
- **`SlotStore`** manages global `VertexId` and `SimplexId` lifecycles using three unmanaged buffers of length `MaxSlots`:
  - `_freeStack`: LIFO stack of available IDs (pre-populated with `0 .. MaxSlots - 1`).
  - `_active`: densely packed array of currently living IDs in `[0 .. _activeCount - 1]`.
  - `_activeIndex`: inverse lookup mapping `id -> index in _active` (`-1` when free).
- **`TypeStore`** maintains species-specific dense active lists (`TypeStore31`, `TypeStore22`, `TypeStore13`) so moves that target a specific foliation species (`Move23` and `Move32` selecting a `(2,2)` simplex; `Move26` selecting a `(1,3)` simplex) draw uniformly in $\mathcal{O}(1)$ time (`_active[rng.NextBounded(_count)]`) with zero type-rejection waste.

### 2.3 Automatic Arena Capacity Sizing (`MaxSimplices`)
Rather than requiring users to guess manual memory limits in `.cdt` configuration files, each physics cassette derives its native arena capacities automatically at startup:
- In `CDT-2plus1`, `Universe.Build` sizes the simplex pool with $1.5\times$ headroom (`SimplexHeadroom = 1.5`) above the larger of `TargetVolume` and the initial cold seed volume ($N_{\text{seed}} = 60k^2 T$), and derives `maxVertices` (`MemoryHeadroom = 1.2` above the $2{+}1$D topological vertex bound) and `maxAdjacencyBlocks` accordingly.
- The allocated simplex slot capacity (`SimplexCount + SimplexFreeCount`) is exposed upward via `ISimulationHarness.MaxSimplices` so Tier 1 jobs can query the live arena capacity directly—both to pre-allocate dual-graph observable buffers (`Hausdorff` and `SpectralDimension`) and to trigger an early overvolume bailout (`0.85 * MaxSimplices`) if an exploratory `k_d = 0` tuning probe inflates toward the ceiling.

---

## 3. Why the Unmanaged Arena Omits a Memory Lineariser / Compactor

A common assumption when designing custom memory arenas for dynamic meshes is that long-running simulations require a periodic "lineariser" or defragmentation pass to compact live entities into contiguous `[0 .. N - 1]` indices. In Open-CDT, linearisation is deliberately omitted—and would actually be counterproductive—for four mathematical and systems-level reasons:

1. **Zero External Fragmentation (Fixed-Stride Slab Allocation):**
   `SlotStore` and `AdjacencyMap` allocate fixed-stride slots (`Tetrahedron` = 32 B, `VertexTimes` = 4 B, `AdjacencyBlock` = 64 B). Every recycled ID popped from the LIFO `_freeStack` is 100% interchangeable with a freshly allocated slot; external fragmentation is mathematically impossible.
2. **O(1) Dense Active-Set Tracking (Swap-With-Last):**
   Whenever a simplex or vertex is deleted, `Free` / `Remove` performs an $\mathcal{O}(1)$ swap-with-last operation, keeping active IDs 100% densely packed in `_active[0 .. _count - 1]` at all times. Both uniform random draws (`SelectRandom`) and sequential observable traversals (`GetNthSimplexId`) execute in $\mathcal{O}(1)$ time without ever scanning dead slots.
3. **Stochastic MCMC Access Patterns vs. Spatial Stencil Locality:**
   Unlike static finite-element meshes where Cuthill-McKee or Morton ordering aids sequential matrix sweeps, CDT Markov chains propose moves uniformly at random across the entire manifold and continuously rewire adjacency millions of times per second. Any spatial ordering established by a compactor is destroyed within a few autocorrelation times ($\tau_{\text{int}}$).
4. **Preserving Bit-Exact Markov Chain Determinism:**
   Because `TypeStore.Remove` uses swap-with-last, the exact permutation of IDs inside `TypeStore31`, `TypeStore22`, and `TypeStore13` is a path-dependent function of the entire Markov history. Relabeling or sorting active simplices alters `_active[k]`, causing the very next call to `SelectRandom(ref rng)` to pick a different simplex even with an identical `Pcg32` state.

---

## 4. Zero-Cost Generic Monomorphization & Accessor Lenses

To share `ProposalPipeline`, `Metropolis`, and the observables (`VolumeProfile`, `Hausdorff`, `SpectralDimension`) across arbitrary spacetime dimensions (`2+1D`, `3+1D`) without virtual method dispatch (`vtable`) or interface boxing overhead:
- `CDT-Agnostic` defines `static abstract` and struct-constrained generic interfaces:
  - `IRandomAccessor`
  - `IAdjacencyAccessor`
  - `IInspectingAccessor<TAdjacency>`
  - `IMutatingAccessor<TInspector>`
  - `IPachnerMove<TContext, TRandom, TInspector, TMutator, TPhysics>`
- In `CDT-2plus1`, the concrete accessors (`RandomAccessor`, `InspectingAccessor`, `MutatingAccessor`, `PhysicsAccessor`, `SeederAccessor`) are `readonly unsafe struct` wrappers around `Manifold*` and `Dynamics*`, declared as nested `partial` structs inside `Manifold` and `Dynamics`.
- **Why Nested `partial` Structs?** Making the raw pointer fields of `Manifold` and `Dynamics` `private` ensures that even inside `CDT-2plus1`, no Pachner move or engine routine can bypass the accessor methods to mutate raw memory directly, while the nested accessors compile down to zero-offset pointer loads.
- **Why `SeederAccessor` Is Quarantined:** `SeederAccessor` exposes `InvertOrientation(SimplexId)`, which swaps local slots `0` and `1` to fix simplex chirality during initial `Icosahedral` BFS stitching. Quarantining this method on `SeederAccessor` guarantees that runtime MCMC moves can never accidentally flip simplex orientation.
- **Compile-Time Monomorphization & `Move44` Dead Code Elimination (DCE):** When `SimulationEngine.ExecuteSweepBurst` switches on `PachnerMove` and calls `Propose<TMove, TContext>(...)`, RyuJIT emits five dedicated, inline-specialized assembly pipelines. For `Move44`, where $\Delta S \equiv 0$ and $H \equiv 1$ (`IsMoveEnergeticallyFavourable => true`), the JIT compiler's Dead Code Elimination completely strips the thermodynamic check, LUT read, and RNG roll from the `(4,4)` execution path.

---

## 5. Q4.28 Fixed-Point Metropolis-Hastings & 5-Sigma L1 Lookup Tables

Evaluating `Math.Exp(-deltaS)` in 64-bit floating-point software on every Monte Carlo proposal stalls the CPU pipeline. Open-CDT separates **kinetics** from **thermodynamics** and replaces floating-point exponentials in the hot loop with **unsigned `Q4.28` fixed-point integer arithmetic** and cache-aligned Look-Up Tables (`MoveWeights`):

### 5.1 Separating Kinetics ($H$) from Thermodynamics ($E$)
The Metropolis-Hastings acceptance probability factors into two independent terms:
$$P_{\text{accept}} = \min\!\left(1, \, \underbrace{\frac{P_{\text{rev}}(N)}{P_{\text{fwd}}(N)}}_{\text{Kinetics } (H)} \times \underbrace{e^{-\Delta S_{\text{eff}}(k_0, k_d, \Delta, \epsilon, \delta N)}}_{\text{Thermodynamics } (E)}\right)$$
- **Thermodynamics ($E$):** Depends only on the fixed bare couplings $(k_0, k_d, \Delta)$, stiffness $\epsilon$, and integer volume offset $\delta N = N_d - \hat{N}_d$; precomputed once per harness into immutable `MoveWeights` L1 LUTs.
- **Kinetics ($H$):** Purely combinatorial; depends only on current active pool counts ($N_{22}, N_{13}, N_0$ in $2{+}1$D) and static proposal ratios; evaluated dynamically in integer ALU registers (`h_Q28`).

### 5.2 Why `Q4.28` Specifically? (And the Branchless $P \ge 1.0$ Guarantee)
In a 32-bit `uint`, 4 bits are allocated to the integer part ($[0, 16)$) and 28 bits to the fractional part (`Q_ONE = 1u << 28 = 268,435,456`, resolution $\approx 3.72 \times 10^{-9}$).
1. **Dynamic Headroom Above $1.0$:** Even when the product $H \cdot E < 1$, either factor alone can exceed $1.0$ (e.g., $H = 0.10$ and $E = 8.0 \implies P_{\text{accept}} = 0.80$). Using `Q0.32` would clamp $E$ at $1.0$ and violate Detailed Balance. Allocating 4 integer bits allows both $H$ and $E$ to range up to $15.999999996$ without saturation.
2. **L1 Cache Density:** Storing weights as 32-bit `uint` packs **16 LUT entries per 64-byte L1 cache line** (twice as dense as 64-bit `double`).
3. **Branchless $P \ge 1.0$ Acceptance:**
   In `Metropolis.ShouldAccept`:
   ```csharp
   ulong pAccept_Q28 = ((ulong)h_Q28 * e_Q28) >> MoveWeights.Q_SHIFT;
   uint randomQ28 = random.GetRandom() >> (32 - MoveWeights.Q_SHIFT);
   return randomQ28 < pAccept_Q28;
   ```
   Because `randomQ28` shifts a 32-bit `uint` right by 4 bits, it is strictly bounded in $[0, 2^{28} - 1]$ (`[0, Q_ONE - 1]`). Whenever $P_{\text{accept}} \ge 1.0$, `pAccept_Q28 >= Q_ONE`, so `randomQ28 < pAccept_Q28` **automatically evaluates to `true` without an `if (p >= 1.0)` branch**.

### 5.3 The $\pm 5\sigma$ Fast-Path Window & FPU Fallback Gate
At thermal equilibrium under the quadratic volume-fixing potential $S_{\text{vol}} = \epsilon(N_d - \hat{N}_d)^2$, total volume fluctuates as a Gaussian with standard deviation $\sigma = 1/\sqrt{2\epsilon}$.
- `Dynamics.Create` sizes each `MoveWeights` LUT radius to $R_{\text{vol}} = \min(4096, \lceil 5/\sqrt{2\epsilon} \rceil)$ (padded up to a multiple of 16 entries for 64-byte cache-line alignment; e.g., $R_{\text{vol}} = 160$ at $\epsilon = 5\times 10^{-4}$), covering $99.99994\%$ of equilibrium states in a few kilobytes of L1 cache.
- Using the unsigned range check `(uint)(volumeDiff + VolumeRadius) < (uint)(2 * VolumeRadius)`, `Metropolis.ShouldAccept` checks both bounds in a single CPU comparison:
  - **Fast Path (Inside $\pm R_{\text{vol}}$):** 1 L1 array load + 1 64-bit integer multiply + 1 bit-shift.
  - **Slow Path (Outside $\pm R_{\text{vol}}$ or $\epsilon = 0$, e.g., during cold-seed inflation):** Branches to `[MethodImpl(MethodImplOptions.NoInlining)] EvaluateSlowPath`, computing exact IEEE 754 `double` thermodynamics so cold seeds inflate smoothly toward $\hat{N}_d$.

---

## 6. Zero-Copy Telemetry (`Volume`) & Dual-Graph Observables

- **Pinned Object Heap (`Volume`):** To record sweep-by-sweep $N_0$ and $N_d$ time series without pausing the native engine or pinning/unpinning arrays on every sweep burst, `Volume` (`CDT_Agnostic.Measurements.Traces.Volume`) allocates `int[] TotalVertices`, `int[] TotalSimplices`, and a 16-byte `Volume.Unmanaged` pointer header directly on .NET's **Pinned Object Heap (POH)** via `GC.AllocateArray(..., pinned: true)`. POH buffers are never moved by GC compaction, so `trace.UnmanagedPointer` remains permanently valid across `[UnmanagedCallersOnly]` calls while still being automatically reclaimed by the GC when the owning job finishes.
- **Reusable Dual-Graph Observable Buffers (`Hausdorff` & `SpectralDimension`):** Both dual-graph observables pre-allocate $N_{\max}$-length arrays once per replica thread (`MaxSimplices`) so facet-neighbor lookups index directly by physical `SimplexId` in $\mathcal{O}(1)$ time, while radial wavefront expansion (`_bfsGeneration` stamp-on-enqueue) and $\chi = 0.8$ heat-kernel diffusion steps iterate strictly over the $N_d$ dense active IDs in `SlotStore` without clearing inactive slots between origins.

---

## 7. Conservation-Derived `.cdtbin` Snapshot Checkpointing

Saving and restoring a live universe (`Manifold.WriteCheckpoint` / `Manifold.ReadCheckpoint`) preserves **100% bit-for-bit Markov trajectory identity** without bloating runtime structs with extra capacity fields:
- In `SlotStore`, every `Alloc()` decrements `_freeHead` and increments `_activeCount`, while every `Free()` increments `_freeHead` and decrements `_activeCount`. Therefore, at all times:
  $$\text{MaxSlots} \equiv \text{\_activeCount} + \text{\_freeHead}$$
- `Manifold.WriteCheckpoint` recovers `maxVertices = VertexCount + VertexFreeCount` and `maxSimplices = SimplexCount + SimplexFreeCount` directly from this conservation law and blits the exact raw unmanaged byte spans (`SimplexPool`, `SimplexTypes`, `VertexTimes`, `TypeStore31/22/13`, `SlotStore` arrays, `AdjacencyMap` blocks, and `Pcg32` state) to the stream with a 64-bit `0x454E445F43445431` (`"1TDC_DNE"`) tail integrity sentinel.
- At the ensemble level, `CheckpointArchive` wraps per-replica snapshots with header provenance and validates `SpacelikeDimensions`, `Timeslices`, and `TargetVolume <= harness.MaxSimplices` upon resumption so mismatched configurations fail fast on load rather than exhausting `SlotStore` mid-run.

---

## 8. Algorithmic Complexity Contract & Rejection Semantics

### 8.1 Per-Operation Complexity Contract
Every hot-loop operation in `CDT-Agnostic` and `CDT-2plus1` obeys a strict upper bound on memory touches:

| Operation | Complexity | Hardware Mechanism |
| :--- | :--- | :--- |
| **Simplex / Vertex Allocation & Removal** | $\mathcal{O}(1)$ | `SlotStore` LIFO `_freeStack` + Briggs-Torczon swap-with-last |
| **Typed Simplex Proposal (`SelectRandomType22`, etc.)** | $\mathcal{O}(1)$ | Direct array index into `TypeStore31`, `TypeStore22`, or `TypeStore13` |
| **Simplex Vertex / Neighbor Slot Lookup** | $\mathcal{O}(1)$ | Single 128-bit `Vector128<int>` SIMD register comparison (`Tetrahedron`) |
| **Vertex Incidence Iteration (Degree $D$)** | $\mathcal{O}(D / 14)$ cache lines | 64-byte cache-aligned `AdjacencyBlock` pages + 256-bit `Vector256<int>` AVX2 scan |
| **Move Validation (`IsValidSite`)** | $\mathcal{O}(1) \to \mathcal{O}(D)$ | All $\mathcal{O}(1)$ foliation/causality fast-fails execute strictly *before* any $\mathcal{O}(D)$ link-condition walk |
| **Thermodynamic Acceptance (`ShouldAccept`)** | $\mathcal{O}(1)$ | Single L1 `Q4.28` LUT array read + 64-bit integer multiply-shift |
| **Move Execution (`PerformMove`)** | $\mathcal{O}(1)$ | Fixed number of branchless slot updates and reciprocal neighbor links |

### 8.2 Why One Sweep Equals $N_d$ Proposals & Role of Topological Rejections
- **Volume-Scaled Sweep Definition:** One Monte Carlo sweep is defined as $N_d$ attempted Pachner moves (`mutator.Inspect.Adjacency.TotalSimplices`), corresponding to one proposal per top-dimensional simplex on average. Scaling sweep length with $N_d$ ensures that intensive relaxation timescales remain comparable across different lattice volumes.
- **Topological Rejections in Markov Chain Monte Carlo:** When `TrySelectValidSiteForMove` rejects a candidate site (for instance, due to a manifold link-condition violation), the Markov chain remains at its current state $x_t$ for that step ($x_{t+1} = x_t$). In Metropolis-Hastings sampling, remaining at the current configuration on a rejected proposal is a required, unbiased transition that preserves the stationary Boltzmann distribution.