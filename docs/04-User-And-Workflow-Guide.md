# 04 — User, Configuration & Workflow Guide

This guide covers the end-to-end operation of **Open-CDT**, including command-line usage, the `.cdt` YAML configuration schema, the two scientific workflows (`PhaseSpaceMapper` and `CanonicalEnsemble`), binary `.cdtbin` manifold checkpointing, output CSV schemas, the 18 diagnostic/publication plots, and cluster automation from Python and Slurm.

---

## 1. Command-Line Interface (`open-cdt`)

The `open-cdt` executable provides two primary subcommands: `simulate` and `plot`.

```bash
# 1. Execute a simulation workflow from a .cdt YAML configuration file:
open-cdt simulate config/2plus1/Map.cdt
open-cdt simulate config/2plus1/Run.cdt

# 2. Render all applicable diagnostic and publication PNG charts from a run output directory:
open-cdt plot results/Canonical_16k_32T/20261123-010000
```

### 1.1 Interactive Terminal Dashboard vs. Headless Cluster Logging

`ConsoleRenderer` automatically adapts its output mode based on whether standard output is attached to an interactive terminal or redirected to a file/pipe (`Console.IsOutputRedirected`):

1. **Interactive Terminal Mode (`!Console.IsOutputRedirected`):**
   * Renders the truecolor ANSI `OPEN CDT` banner on startup.
   * Displays a live multi-core progress dashboard with a global job progress bar, per-worker/per-replica status rows (`Equilibrating`, `Holding`, `Relaxing seed`, `Measuring τ_int`, `Sampling`), live volume `N`, right-aligned Monte Carlo sweep throughput (`sw/s`), and elapsed wall-clock time.
   * Seals each completed job block in-place and prints a concise statistical summary block upon completion.
2. **Headless Cluster / Slurm Log Mode (`Console.IsOutputRedirected == true`):**
   * Suppresses all ANSI cursor movement sequences and intermediate progress redraws so cluster log files (`sbatch` `.out` files) remain clean and human-readable.
   * Emits the completed job header and statistical summary lines once at the conclusion of each job stage.

### 1.2 Automatic Output Directory & Configuration Provenance

When `Output.Directory` is non-empty, `EntryPoint.Run` validates the configuration and creates a timestamped run folder:

```text
<Output.Directory>/<Simulation.Name>/<yyyyMMdd-HHmmss>/
```

Before starting any Monte Carlo sweeps, `Open-CDT` serializes an exact copy of the active `RunConfig` (`<Simulation.Name>.cdt`) into this timestamped directory, guaranteeing full parameter provenance alongside the generated CSVs, plots, and `.cdtbin` checkpoints.

---

## 2. Configuration File Schema (`.cdt` YAML)

Every `.cdt` file deserializes into a strongly-typed `RunConfig` record hierarchy and undergoes fail-fast pre-flight validation (`IWorkflow.Validate` and `IJob.Validate`) before any output directories or unmanaged memory arenas are created. Unmanaged arena capacities (`MaxSimplices = 1.5 * max(TargetVolume, N_seed)`) are derived automatically by the physics cassette.

### 2.1 Complete YAML Reference

```yaml
Simulation:
  Name: Canonical_16k_32T         # Run identifier (used for output subdirectory and saved .cdt filename)
  Workflow: CanonicalEnsemble     # PhaseSpaceMapper | CanonicalEnsemble
  Replicas: 8                     # Number of parallel replica chains (CanonicalEnsemble) or worker threads (PhaseSpaceMapper)
  SpacelikeDimensions: 2          # Spatial dimensionality d_s (2 = 2+1D Lorentzian spacetime)

Seed:
  K: 2                            # Caspar-Klug icosahedral subdivision frequency (K >= 1 when StartFrom is omitted; N_seed = 60*K^2*T)
  Timeslices: 32                  # Proper-time slices T in the periodic S^1 foliation (T >= 3)
  Rng: 42                         # Base 64-bit seed for deterministic per-replica Pcg32 streams
  StartFrom: null                 # Optional path to a .cdtbin checkpoint archive to resume/expand from

Physics:
  K0: 5.0484                      # Bare inverse gravitational coupling (k_0)
  Kd: 1.369258                    # Bare cosmological coupling (k_d: k_3 in 2+1D, k_4 in 3+1D; initial guess in PhaseSpaceMapper)
  Delta: 0.6857                   # Spacetime causal asymmetry parameter (Delta)
  Epsilon: 0.0005                 # Quadratic volume-fixing stiffness (epsilon > 0)
  TargetVolume: 16000             # Target top-dimensional simplex count N_target

Tuning:                           # Used by Workflow: PhaseSpaceMapper (Job 1)
  ToleranceRatio: 0.005           # Fractional lock band around TargetVolume (0.005 = ±0.5%)
  PlateauSweeps: 1000             # Stationary sweeps required across two half-windows to confirm a probe outcome (>= 2)
  MaxSweepsPerProbe: 20000        # Maximum sweeps allowed per probe before timeout (>= PlateauSweeps)
  K0Low: 2.0                      # Lower bound of k_0 sweep when K0Steps > 1
  K0High: 12.5                    # Upper bound of k_0 sweep when K0Steps > 1
  K0Steps: 32                     # Number of k_0 grid points (1 = single-point tune at Physics.K0)
  DeltaLow: 0.0                   # Lower bound of Delta sweep when DeltaSteps > 1
  DeltaHigh: 1.2                  # Upper bound of Delta sweep when DeltaSteps > 1
  DeltaSteps: 8                   # Number of Delta grid points (1 = single-point tune at Physics.Delta)

Classification:                   # Used by Workflow: PhaseSpaceMapper (Job 2)
  SweepsPerCoordinate: 5000       # Total equilibration + sampling sweeps per locked grid coordinate (>= 96)

Thermalization:                   # Used by Workflow: CanonicalEnsemble (Job 1)
  BurnInSweeps: 300000            # Total deterministic burn-in sweeps per replica (must be >= WindowSweeps)
  WindowSweeps: 5000              # Trailing equilibrium sweep window for means & Madras-Sokal tau_int (>= 24)

Sampling:                         # Used by Workflow: CanonicalEnsemble (Job 2)
  SamplesPerReplica: 50           # Number of decorrelated observable snapshots recorded per replica
  MinDecorrelationSweeps: 2500    # Minimum sweeps between snapshots (overridden by 2 * EnsembleTauInt if larger)
  HausdorffSamples: 25            # Random dual-graph origin simplices sampled per snapshot for BFS shell scaling
  HausdorffMaxRadius: 25          # Maximum geodesic dual-graph radius r_max tracked in Hausdorff profiles (>= 4)
  SpectralSamples: 25             # Random dual-graph origin simplices sampled per snapshot for heat-kernel diffusion
  SpectralMaxSigma: 100           # Maximum discrete diffusion step sigma_max tracked in P(sigma) (>= 5)

Output:
  Directory: results              # Root directory for timestamped run outputs
  SaveTo: ensemble.cdtbin         # Optional relative or absolute path to save the final .cdtbin ensemble archive
```

---

## 3. Workflow 1: `PhaseSpaceMapper` (`Map.cdt`)

`PhaseSpaceMapperWorkflow` automates calibration of the critical cosmological surface $k_d^c(k_0, \Delta)$ and maps the macroscopic phases of the quantum geometry across an $N \times M$ coupling grid. It executes two sequential jobs:

### 3.1 Job 1 — `CosmologicalConstantTunerJob` (`tuning.csv`)

1. **Parallel Work-Stealing Queue & Collinear Warm-Starting (`CriticalSurfaceTracker`):** Distributes $(k_0, \Delta)$ coordinates across `Simulation.Replicas` worker threads. The initial wave starts at `Physics.Kd` (e.g., `0.0`); once grid neighbors lock, subsequent coordinates warm-start via collinear secant projection along constant-$\Delta$ or constant-$k_0$ slices, converging in ~1.27 probes per coordinate on a $32 \times 8$ grid.
2. **Safeguarded Susceptibility Root-Finding:** Using the analytical equilibrium susceptibility $\frac{\partial \langle N_d \rangle}{\partial k_d} = -\frac{1}{2\epsilon}$, each probe updates its critical coupling estimate via:
   $$k_d^{\text{next}} = k_d + 2\epsilon \big(\langle N_d \rangle - \hat{N}_d\big)$$
   while tracking observed `[lowBound, highBound]` outcomes (`AboveTarget`, `BelowTarget`) and falling back to bisection whenever a linear step overshoots an established bound.
3. **Arena Ceiling Guard & Cold-Seed Isolation (`VolumeConvergence`):** Every probe starts from a fresh Caspar-Klug icosahedral seed so collapsed configurations cannot introduce hysteresis, and any probe inflating past `0.85 * MaxSimplices` halts early as `AboveTarget` and steps $k_d$ upward safely without exhausting `SlotStore`.

### 3.2 Job 2 — `PhaseClassifierJob` (`phase_space.csv`)

At each locked critical coordinate $(k_0, \Delta, k_d^c)$, `CoordinateSampler` creates an independent harness (`Pcg32` stream keyed by grid coordinate index for thread-scheduling determinism), relaxes the cold seed over the first quarter of `Classification.SweepsPerCoordinate`, calibrates local Madras-Sokal $\tau_{\text{int}}$ over the second quarter, and samples three dimensionless order parameters at $\ge 2\tau_{\text{int}}$ intervals across the second half:

* **Vertex Density (`VertexDensity` $= \langle N_0 \rangle / \langle N_d \rangle$):** Measures bulk vertex-to-simplex curvature packing relative to the Barnette ceiling $1/(2d_s)$.
* **Peak Slice Fraction (`PeakSliceFraction` $= \langle \max_t N_d(t) / N_d \rangle$):** Measures single-slice localization ($O_{\max} > 0.50$ identifies `PhaseB` collapse).
* **Temporal Slice Continuity (`TemporalContinuity` $= 1 - \frac{\sum_t \vert{}N_d(t+1) - N_d(t)\vert{}}{2 N_d}$):** Measures inter-slice foliation smoothness across `[0 .. 1]`.

Across each constant-$\Delta$ slice, `PhaseBoundaryClassifier` assigns every coordinate to **`PhaseA`**, **`PhaseB`**, or **`PhaseC`**, locates the $C \leftrightarrow A$ saturation elbow via two-segment piecewise-linear regression, and computes the **2D Shoelace polygon centroid** of `PhaseC`—printing the target `(k0, Δ, kd)` triplet for `Run.cdt`.

---

## 4. Workflow 2: `CanonicalEnsemble` (`Run.cdt`)

`CanonicalEnsembleWorkflow` evolves `Simulation.Replicas` independent Markov chains in parallel at fixed physical couplings $(k_0, k_d, \Delta)$ to collect equilibrium observables. It executes two sequential jobs:

### 4.1 Job 1 — `ThermalizerJob` (`thermalization.csv`)

1. **Deterministic Burn-In (`Thermalization.BurnInSweeps`):** Evolves all replica chains from their initial geodesic seed (or loaded `.cdtbin` checkpoint) and records per-sweep `Volume` traces across the trailing `Thermalization.WindowSweeps` window.
2. **Madras-Sokal Integrated Autocorrelation Time ($\tau_{\text{int}}$):** Evaluates equilibrium `<N>` and `<N0>` means and computes the integrated autocorrelation time $\tau_{\text{int}}(N_0)$ on each chain via Madras-Sokal self-consistent windowing ($M \ge 6 \tau_{\text{int}}$, requiring `WindowSweeps >= 24 * tau_int`). The maximum $\tau_{\text{int}}$ across all replicas (`EnsembleTauInt`) is passed directly to `SamplingJob`.
3. **Choosing `BurnInSweeps` & `MinDecorrelationSweeps`:** While bulk vertex counts $N_0$ equilibrate and decorrelate within tens of sweeps inside `Phase C`, condensing cold-seeded periodic chains into a single droplet and averaging over global $\mathcal{O}(T^2)$ droplet-width breathing modes takes $\sim 10^3\text{--}10^5$ sweeps. For $2{+}1$D runs at $\hat{N}_3 = 16,000, \, T = 32$ from a cold `K: 2` seed, `BurnInSweeps: 300000` and `MinDecorrelationSweeps: 2500` ensure complete single-droplet condensation and well-averaged per-replica profiles (verifiable directly in `volume_profile_replicas.png`).

### 4.2 Job 2 — `SamplingJob` (`volume_profile.csv`, `hausdorff.csv`, `spectral.csv`)

1. **Autocorrelation-Decorrelated Stepping:** Between consecutive measurement snapshots, each replica executes:
   $$N_{\text{decorr}} = \max\big(\text{MinDecorrelationSweeps},\, 2 \times \text{EnsembleTauInt}\big)$$
   untraced Monte Carlo sweeps.
2. **Observable Measurements:** At each snapshot, `ReplicaSampler` evaluates three dimension-agnostic observables on the paused manifold:
   * **`VolumeProfile` ($N_d(t)$):** Records simplex counts per proper-time slab, circularly shifts each snapshot by its $k = 1$ circular harmonic phase to align the droplet center at $T/2$, and fits the continuum spherical $\cos^{d_s}(\pi (t - t_0) / W)$ profile.
   * **`Hausdorff` ($D_H$):** Launches `HausdorffSamples` dual-graph breadth-first search (BFS) wavefronts up to radius `HausdorffMaxRadius`, fitting log-log cumulative ball scaling $\langle V(r) \rangle \propto r^{D_H}$ across the bulk scaling window prior to spatial $S^{d_s}$ saturation ($r_{\text{peak}}$).
   * **`SpectralDimension` ($D_S(\sigma)$):** Evolves `SpectralSamples` exact dual-graph heat-kernel diffusions ($\chi = 0.8$) up to step `SpectralMaxSigma`, evaluating symmetric $(\sigma - 1, \sigma + 1)$ logarithmic derivatives, leave-one-replica-out jackknife error bars, and the Ambjørn–Jurkiewicz–Loll rational UV $\to$ IR fit.

---

## 5. Binary Checkpointing (`.cdtbin`) & Ensemble Resizing

`CanonicalEnsembleWorkflow` supports bit-exact binary checkpointing via `Output.SaveTo` and `Seed.StartFrom` (`CheckpointArchive`).

### 5.1 `.cdtbin` Archive Format & Validation

A `.cdtbin` archive is a single indexed binary file containing:
1. **Header & Offset Table:** Stores the `"CDTB"` magic header, format version, `SpacelikeDimensions`, `Timeslices`, saved replica count $M$, and $M$ 64-bit stream offsets.
2. **Replica Payloads ($M$ slots):** Each slot stores the replica's 16-byte `Pcg32` state and raw `Manifold` arenas (`SimplexPool`, `SimplexTypes`, `VertexTimes`, `SlotStore` active/free buffers, `TypeStore` species pools, and `AdjacencyMap` pages) terminated by an integrity sentinel.
3. **Fail-Fast Resume Checks:** Upon load, `CheckpointArchive` verifies header integrity, `SpacelikeDimensions`, `Timeslices`, and `Physics.TargetVolume <= harness.MaxSimplices` so incompatible configurations fail immediately before sweeps begin.

### 5.2 Exact Continuation vs. Modulo Ensemble Expansion (`i % M`)

When loading a `.cdtbin` archive containing $M$ saved manifolds into a run configured with $R =$ `Simulation.Replicas` chains:

* **Direct Continuation ($i < M$):** Replica $i$ restores saved slot $i$ together with its exact `Pcg32` state (`reseedRng: null`). Resuming an $M$-replica run at the same couplings with $R \le M$ is **100% bit-for-bit identical** to an uninterrupted run.
* **Modulo Expansion ($i \ge M$):** Replica $i$ clones saved manifold slot `i % M` and re-keys its `Pcg32` stream using `(Seed.Rng, replicaId: i)` so cloned chains diverge onto statistically independent trajectories from sweep 1—allowing a small thermalized workstation checkpoint (e.g., $M = 4$ or $8$) to seed a larger cluster ensemble without repeating initial burn-in.

---

## 6. Output CSV Schemas & Generated Plots (`open-cdt plot`)

Running `open-cdt plot <run-output-directory>` inspects the CSV datasets in that run directory and renders up to **18 high-DPI (`1280x800`, `ScaleFactor = 1.6`) PNG charts**:

| Job Stage | CSV Dataset & Columns | Generated PNG Plots | Description |
| :--- | :--- | :--- | :--- |
| **1. Tuner** | `tuning.csv`<br>*Headers:* `TargetVolume`, `ToleranceRatio`, `Epsilon`<br>*Cols:* `K0, Delta, Kd, MeanVolume, StdDevVolume, MeanVertices, Outcome` | `tuning_critical_surface.png`<br>`tuning_calibration.png`<br>`tuning_bracket_zoom.png` | Critical sheet $k_d^c(k_0, \Delta)$ & vertex density, universal susceptibility collapse $\langle N_d \rangle$ vs. $\Delta k_d = k_d - k_d^c$, and near-critical $\pm\text{ToleranceRatio}$ convergence corridor. |
| **2. Classifier** | `phase_space.csv`<br>*Headers:* `SpacelikeDimensions`, `Timeslices`, `TargetVolume`<br>*Cols:* `K0, Delta, KdCrit, MeanVolume, StdDevVolume, VertexDensity, PeakSliceFraction, TemporalContinuity, Phase` | `phase_space_diagram.png`<br>`phase_vertex_density.png`<br>`phase_droplet_concentration.png`<br>`phase_slice_continuity.png` | 2D CDT phase diagram (`Phase A`, `Phase B`, `Phase C`, transition ribbon, and centroid target), vertex density $\langle N_0 \rangle / \langle N_d \rangle$ with $1/(2d_s)$ ceiling, peak slice fraction $O_{\max}$, and slice continuity $\mathcal{C}_{\text{time}}$. |
| **3. Thermalizer** | `thermalization.csv`<br>*Headers:* `TargetVolume`, `EnsembleTauInt`<br>*Cols:* `ReplicaId, MeanSimplices, MeanVertices, TauInt, TauConverged` | `thermalization_autocorrelation.png`<br>`thermalization_equilibrium_volumes.png`<br>`thermalization_curvature_ratio.png` | Per-replica Madras-Sokal $\tau_{\text{int}}$ bar chart with $2\tau_{\text{int}}$ ceiling, replica equilibrium volumes $\langle N_d \rangle$ vs. target, and vertex-to-simplex ratio $\langle N_0 \rangle / \langle N_d \rangle$. |
| **4. Sampler** | `volume_profile.csv` (`t_0..t_{T-1}`)<br>`hausdorff.csv` (`FittedDimension, r_0..r_max`)<br>`spectral.csv` (`EstimatedDimension, sigma_0..sigma_max`) | `volume_profile_ensemble.png`<br>`volume_profile_replicas.png`<br>`volume_profile_harmonics.png`<br>`hausdorff_scaling_loglog.png`<br>`hausdorff_replicas.png`<br>`spectral_dimension_running.png`<br>`spectral_return_probability.png`<br>`spectral_replicas.png` | Spherical $\cos^{d_s}$ droplet fit + stalk, per-replica centered profiles, Fourier harmonic spectrum ($k=1\dots 5$), log-log geodesic ball/shell scaling $D_H$, per-replica $D_H$, running spectral dimension $D_S(\sigma)$ with jackknife error bars + AJL rational fit, log-log return probability $\langle P(\sigma) \rangle$, and per-replica $D_S$. |

---

## 7. Python & Slurm Cluster Automation

Because `open-cdt` compiles to a self-contained single-file executable (`open-cdt` on Linux, `open-cdt.exe` on Windows) that automatically switches to clean summary logging when standard output is redirected (`Console.IsOutputRedirected`), it integrates directly with Python orchestration scripts and Slurm (`sbatch`) batch jobs without requiring a .NET runtime installation on compute nodes.

### 7.1 Driving `open-cdt` from Python (`subprocess`)

```python
import subprocess
from pathlib import Path

binary = str(Path("./open-cdt").resolve())

# 1. Execute a simulation workflow from a .cdt configuration file
subprocess.run([binary, "simulate", "config/2plus1/Run.cdt"], check=True)

# 2. Render all diagnostic and publication PNGs for a completed run directory
subprocess.run([binary, "plot", "results/Canonical_16k_32T/20261123-010000"], check=True)
```

### 7.2 Example Slurm Cluster Batch Script (`slurm_job.sh`)

```bash
#!/bin/bash
#SBATCH --job-name=open-cdt-2plus1
#SBATCH --output=cdt_%j.log
#SBATCH --nodes=1
#SBATCH --ntasks=1
#SBATCH --cpus-per-task=8
#SBATCH --time=02:00:00
#SBATCH --mem=4G

set -euo pipefail

# Run the simulation (ConsoleRenderer detects redirected stdout and emits clean log summaries)
./open-cdt simulate config/2plus1/Run.cdt

# Optional: Render all verification PNGs in the newest run directory
LATEST_DIR=$(ls -td results/Canonical_16k_32T/*/ | head -n 1)
./open-cdt plot "$LATEST_DIR"
```