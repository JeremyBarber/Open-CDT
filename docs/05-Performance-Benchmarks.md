# 05 — Empirical Performance Benchmarks & Hardware Analysis

This document presents the empirical performance benchmarks, memory allocation profiles, and RyuJIT x86-64 disassembly verification of the **Open-CDT** simulation engine (`CDT-Agnostic` and `CDT-2plus1`), measured using `BenchmarkDotNet v0.14.0`.

---

## 1. Test Environment & Hardware Architecture

All benchmarks were executed in isolated, high-priority processes under the Windows `High performance` power plan using `.NET 10.0` with Tiered PGO, AVX2 hardware intrinsics, and `MemoryDiagnoser` (`displayGenColumns: true`) enabled.

| Component | Specification |
| :--- | :--- |
| **Processor** | Intel Core i9-14900K (Raptor Lake Refresh, 24 cores / 32 threads: 8 P-cores + 16 E-cores) |
| **Clock Frequencies** | 3.20 GHz nominal P-core base; **5.60 GHz** all-core P-core turbo; **5.80–6.00 GHz** single-core Thermal Velocity Boost (`1 cycle ≈ 0.167–0.172 ns`) |
| **Cache Hierarchy (Per P-Core)** | **48 KB** L1d (data), **32 KB** L1i (instruction), **2.0 MB** private L2 cache |
| **Shared Last-Level Cache (LLC)** | **36 MB** Intel Smart Cache (L3) shared across all cores |
| **Runtime & JIT** | `.NET 10.0.10` (`10.0.1026.32716`), 64-bit RyuJIT, `x86-64-v3` (`AVX2`, `BMI1`, `BMI2`, `FMA`, `LZCNT`, `POPCNT`, `TZCNT`) |
| **Operating System** | Windows 11 (`10.0.26200`) |
| **Benchmark Harness** | `BenchmarkDotNet v0.14.0` (`DefaultJob`, isolated child process per benchmark) |

To reproduce these benchmarks from source, run:

```bash
dotnet run -c Release --project benchmarks/CDT-Benchmarks -- --filter *
```

All generated Markdown, CSV, HTML, and x86-64 disassembly reports are written to `results/benchmarks/`.

---

## 2. Micro-Primitives (`MicroPrimitives`)

The inner Markov Chain Monte Carlo (MCMC) loop executes two primitive operations on every proposed move: drawing a uniformly bounded random integer (`Pcg32.NextBounded`) to select a candidate simplex from a `TypeStore`, and evaluating the Metropolis-Hastings acceptance probability $P_{\text{accept}} = \min(1, h \cdot e^{-\Delta S})$ (`Metropolis.ShouldAccept`).

| Benchmark Method | Mean Time | Error | StdDev | Est. P-Core Cycles (`@ 5.8 GHz`) | Speedup vs. Baseline | Heap Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Metropolis: Double Math.Exp(-dS) Slow Path` | `3.8693 ns` | `0.0186 ns` | `0.0174 ns` | `~22.4 cycles` | `1.00x` *(Baseline)* | `0 B` |
| **`Metropolis: Q4.28 Fixed-Point L1 LUT Fast Path`** | **`0.3488 ns`** | **`0.0066 ns`** | **`0.0062 ns`** | **`~2.0 cycles`** | **`11.09x faster`** | **`0 B`** |
| `RNG: System.Random.Next(bound)` | `1.2386 ns` | `0.0051 ns` | `0.0048 ns` | `~7.2 cycles` | `1.00x` *(Baseline)* | `0 B` |
| **`RNG: Pcg32.NextBounded(bound) [Lemire]`** | **`0.1955 ns`** | **`0.0025 ns`** | **`0.0020 ns`** | **`~1.1 cycles`** | **`6.34x faster`** | **`0 B`** |

### Architectural Takeaways
1. **`Q4.28` Fixed-Point LUT vs. `Math.Exp` (`11.09x` speedup):** Precomputing $e^{-\Delta S}$ across a 64-byte cache-aligned window (`MoveWeights`) replaces a transcendental floating-point evaluation with an L1d cache hit (`_expDeltaS[diff + VolumeRadius]`) and a single 64-bit integer multiply-shift (`((ulong)h_Q28 * e_Q28) >> 28`), completing in ~2 clock cycles.
2. **Pipelined `Pcg32` + Lemire Bounded Draw (`6.34x` speedup):** Because `Pcg32` permutes the *previous* 64-bit state (`oldState`) while advancing the linear congruential state (`_state = oldState * Multiplier + _inc`), the CPU's out-of-order execution engine overlaps the 64-bit LCG multiplication with the XSH-RR bit-rotation and Lemire's divisionless `(ulong)r * bound >> 32` multiply-shift, achieving a throughput of ~1.1 cycles per draw.

---

## 3. Hardware SIMD vs. Scalar Topology Lookups (`SimdTopology`)

Topological validation and rewiring rely heavily on two geometric queries: resolving local vertex and neighbor facet slots `[0..3]` inside a 32-byte `Tetrahedron` struct, and testing whether vertices share a common simplex across 64-byte `AdjacencyBlock` incidence pages (`EdgeExists`, `TriangleExists`).

| Benchmark Method | Mean Time | Error | StdDev | Est. P-Core Cycles (`@ 5.8 GHz`) | Speedup vs. Scalar | Heap Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Tetrahedron Slot Lookup: Scalar 4-Step Loop` | `1.6951 ns` | `0.0137 ns` | `0.0128 ns` | `~9.8 cycles` | `1.00x` *(Baseline)* | `0 B` |
| **`Tetrahedron Slot Lookup: Vector128<int> + TZCNT`** | **`0.1860 ns`** | **`0.0045 ns`** | **`0.0042 ns`** | **`~1.1 cycles`** | **`9.11x faster`** | **`0 B`** |
| `Adjacency TriangleExists: Scalar Page Scan` | `51.2153 ns` | `0.3173 ns` | `0.2968 ns` | `~297.0 cycles` | `1.00x` *(Baseline)* | `0 B` |
| **`Adjacency TriangleExists: AVX2 Vector256<int>`** | **`16.0025 ns`** | **`0.0414 ns`** | **`0.0388 ns`** | **`~92.8 cycles`** | **`3.20x faster`** | **`0 B`** |

### Architectural Takeaways
1. **Branchless 128-Bit Slot Resolution (`9.11x` speedup):** A 4-iteration scalar `for`/`if` loop incurs loop-counter overhead and branch-prediction pressure. `Tetrahedron.FindVertexSlot` loads all four 32-bit IDs in a single 128-bit register read (`Vector128<int>`) and resolves the matching slot index via hardware bit-scanning (`tzcnt`) in `0.1860 ns` (~1.1 clock cycles for two back-to-back lookups).
2. **256-Bit AVX2 Page Intersection (`3.20x` speedup):** Each `AdjacencyBlock` stores 14 simplex IDs within a single 64-byte CPU cache line. Testing 8 integer IDs per instruction via `Vector256.Equals` + `vmovmskps` accelerates 3-way vertex incidence intersections (`TriangleExists`) from `51.2 ns` down to `16.0 ns`.

---

## 4. Pachner Move Cost & Disassembly Verification (`PachnerMoves`)

Benchmarked on an equilibrated 2+1D De Sitter manifold at $N_3 \approx 16,000$ simplices ($T = 16$). The top five rows isolate site selection, topological manifoldness validation, and `Q4.28` LUT evaluation on a stationary manifold; the bottom three rows benchmark the full mutating `ProposalPipeline.Propose<TMove>` execution path.

| Benchmark Method | Mean Time | Error | StdDev | Native Code Size | Heap Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: |
| `Move23: Select + Validate + LUT Metropolis` | `67.164 ns` | `0.1911 ns` | `0.1787 ns` | `1,072 B` | `0 B` |
| `Move32: Select + Validate + LUT Metropolis` | `188.270 ns` | `0.9813 ns` | `0.7661 ns` | `1,732 B` | `0 B` |
| `Move26: Select + Validate + LUT Metropolis` | `7.621 ns` | `0.0262 ns` | `0.0218 ns` | `630 B` | `0 B` |
| `Move62: Select + Validate + LUT Metropolis` | `6.695 ns` | `0.0257 ns` | `0.0240 ns` | `1,971 B` | `0 B` |
| `Move44: Select + Validate + LUT Metropolis` | `96.346 ns` | `0.4947 ns` | `0.4628 ns` | `1,855 B` | `0 B` |
| **`Full Pipeline: Move44 (Volume-Preserving)`** | **`106.918 ns`** | **`0.4552 ns`** | **`0.4258 ns`** | `13,467 B` | **`0 B`** |
| **`Full Pipeline: Move23 + Move32 Conjugate Pair`** *(per move)* | **`164.385 ns`** | **`0.7391 ns`** | **`0.6913 ns`** | `23,002 B` | **`0 B`** |
| **`Full Pipeline: Move26 + Move62 Conjugate Pair`** *(per move)* | **`16.921 ns`** | **`0.0877 ns`** | **`0.0732 ns`** | `27,188 B` | **`0 B`** |

### Verified RyuJIT x86-64 Disassembly (`PachnerMoves-asm.md`)
1. **Zero-Cost Generic Monomorphization:** Inspecting the generated assembly confirms that the struct-constrained `IPachnerMove<TContext, TRandom, TInspector, TMutator, TPhysics>` pipeline compiles with **zero interface boxing** and **zero virtual method table (`vtable`) lookups**. All accessor properties (`InspectingAccessor.GetTetrahedron`, `MutatingAccessor.Inspect`, `LocalFacetSlot`, `VertexId`) inline down to raw register offset loads (`shl rcx, 5; add rcx, [rax]`).
2. **L1 Instruction Cache (L1i) Residency:** The combined native machine code footprint of all five move validation and Metropolis evaluation routines is **`7,260 bytes` (`7.09 KB`)**—occupying less than **23%** of a single P-core's **32 KB L1i cache**.
3. **5-Instruction SIMD Slot Lookup:** `Tetrahedron.FindVertexSlot` compiles to 22 bytes of branchless AVX2 + BMI1 assembly:
   ```assembly
   ; CDT_2plus1.Engine.Topology.Tetrahedron.FindVertexSlot(VertexId)
          vmovd        xmm0, edx
          vpbroadcastd xmm0, xmm0
          vpcmpeqd     xmm0, xmm0, [rcx]
          vmovmskps    eax, xmm0
          tzcnt        eax, eax
          ret
   ; Total bytes of code 22
   ```
4. **Inlined 64-Byte Cache-Line AVX2 Intersection:** Inside `Move23`, `Move32`, `Move44`, and `Move62`, the `AdjacencyBlock` (`shl r15, 6`) set-intersection loop compiles directly into dual 256-bit `vpcmpeqd ymm` comparisons across the 14-element page with a `shlx` active-count bitmask:
   ```assembly
          shl          r15, 6                ; 64-byte AdjacencyBlock stride
          add          r15, r12
          mov          ebp, [r15+3Ch]        ; page.Count
          mov          r9d, 1
          shlx         r9d, r9d, ebp
          dec          r9d                   ; (1 << page.Count) - 1 mask
          vpcmpeqd     ymm2, ymm1, [r15]     ; Lanes 0..7
          vmovmskps    ebp, ymm2
          vpcmpeqd     ymm1, ymm1, [r15+20h] ; Lanes 8..13
          vmovmskps    r15d, ymm1
          shl          r15d, 8
          or           ebp, r15d
          test         r9d, ebp
   ```

---

## 5. End-to-End Throughput, Cache Scaling & Fragmentation Immunity (`EngineThroughput`)

Benchmarks full Monte Carlo sweeps across the `[UnmanagedCallersOnly]` C-ABI air-gap with live Pinned Object Heap (`Volume`) telemetry recording on every sweep.

| Benchmark Method | Mean Time | Error | StdDev | Throughput (`Proposals/s`) | Time / Proposal | GC (`Gen0/1/2`) | Heap Allocated |
| :--- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `100 Sweeps (N3 = 4,000) [~400k Proposals]` | `35.317 ms` | `0.4763 ms` | `0.4456 ms` | **`11.33M / sec`** | `88.29 ns` | `0 / 0 / 0` | **`0 B`** |
| `100 Sweeps (N3 = 16,000) [~1.6M Proposals]` | `171.155 ms` | `1.8721 ms` | `1.7512 ms` | **`9.35M / sec`** | `106.97 ns` | `0 / 0 / 0` | **`0 B`** |
| `100 Sweeps (N3 = 64,000) [~6.4M Proposals]` | `785.178 ms` | `6.4761 ms` | `6.0578 ms` | **`8.15M / sec`** | `122.68 ns` | `0 / 0 / 0` | **`0 B`** |
| `1 Sweep (N3 = 16k, Freshly Inflated Arena)` | `1.708 ms` | `0.0303 ms` | `0.0284 ms` | **`9.37M / sec`** | `106.75 ns` | `0 / 0 / 0` | **`0 B`** |
| `1 Sweep (N3 = 16k, 25,000-Sweep Churned Arena)` | `1.701 ms` | `0.0154 ms` | `0.0144 ms` | **`9.41M / sec`** | `106.31 ns` | `0 / 0 / 0` | **`0 B`** |
| `Ensemble: 8 Replicas × 10 Sweeps (Parallel)` | `21.410 ms` | `0.2443 ms` | `0.2165 ms` | **`59.79M / sec`** (`7.47M/core`) | `16.73 ns` *(wall)* | `0 / 0 / 0` | `3,398 B`* |

*\*The `3,398 B` allocation on the parallel ensemble row is the one-time managed `Parallel.For` task-scheduling overhead outside the engine; the five single-core benchmarks prove that `SimulationHarness.Run` allocates strictly **`0 B`** with **zero Garbage Collector interruptions** across 6.4 million consecutive proposals.*

### Hardware & Systems Analysis

1. **Mapping Volume Scaling ($4\text{k} \to 16\text{k} \to 64\text{k}$) to the i9-14900K Cache Hierarchy:**
   * **$N_3 = 4,000$ (`~276 KB` active working set):** The active `Tetrahedron`, sparse-set, and `AdjacencyBlock` footprint sits entirely inside a single P-core's **2.0 MB private L2 cache**, delivering **`11.33M proposals/sec`** (`88.29 ns/proposal`).
   * **$N_3 = 16,000$ (`~1.08 MB` active working set):** The dense `TypeStore` arrays and hot simplices reside in the **2.0 MB private L2 cache**, while scattered arena slots straddle the **36 MB shared L3 Smart Cache**, adding `~18.7 ns` of average memory latency per proposal (**`9.35M proposals/sec`**, `106.97 ns/proposal`).
   * **$N_3 = 64,000$ (`~4.31 MB` active working set):** Increasing the manifold volume by **$16\times$** ($4,000 \to 64,000$) increases per-proposal time by only **$1.39\times$** (`88.29 ns` $\to$ `122.68 ns`, **`8.15M proposals/sec`**), because the entire working set remains resident within the **36 MB L3 Smart Cache** and all `SlotStore`/`TypeStore` lookups execute in strictly $\mathcal{O}(1)$ time.
2. **Empirical Proof of Fragmentation Immunity (Why No Memory Lineariser Is Used):**
   * Comparing a freshly inflated $N_3 = 16,000$ manifold (`1,500` burn-in sweeps) against an identical $N_3 = 16,000$ manifold subjected to **`25,000` sweeps (~400,000,000 proposals of random swap-with-last `SlotStore`, `TypeStore`, and `AdjacencyBlock` churn)** yields **`1.708 ms ± 0.030 ms`** vs. **`1.701 ms ± 0.015 ms`** per sweep—a difference of **`-0.41%`** (statistically indistinguishable within 1 standard error).
   * Because MCMC proposals draw simplices uniformly at random across the entire manifold (`SelectRandom`) and every active entity is densely tracked via Briggs-Torczon sparse-sets (`_active[0 .. _count - 1]`), physical index scrambling in `SimplexPool` incurs zero dead-slot scanning and zero cache degradation over multi-million-sweep runs.
3. **Multi-Core Ensemble Scaling across 8 P-Cores (`59.79M proposals/sec`):**
   * Running 8 independent $N_3 = 16,000$ replicas simultaneously across the 8 P-cores achieves **`59.79M proposals/sec`** (`7.47M proposals/sec` per core, a **`6.40x`** total throughput multiplier over a single core at 5.8–6.0 GHz single-core TVB boost).
   * The per-core difference between 1 active core (`9.35M/s`) and 8 active cores (`7.47M/s`) reflects the hardware transition from single-core Thermal Velocity Boost (`5.8–6.0 GHz`) to all-core sustained P-core turbo (`5.6 GHz`) and shared L3 cache bandwidth across eight active `1.08 MB` working sets inside the `36 MB` L3 Smart Cache, with zero lock contention and zero GC pauses.