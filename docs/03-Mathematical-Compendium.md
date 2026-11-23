# 03 — Mathematical Compendium, Thermodynamics & Observables

This document provides the mathematical derivations underlying the discrete Regge action, critical cosmological constant calibration, phase-boundary classification, statistical autocorrelation analysis, and geometric observables in **Open-CDT**.

---

## 1. Regge Action & Quadratic Volume-Fixing Thermodynamics

### 1.1 The 2+1D Discrete Regge Action
In $2+1$ dimensions with foliated topology $S^2 \times S^1$, the Wick-rotated Lorentzian Regge action simplifies (via Euler and Dehn-Sommerville topological identities) to a linear combination of global simplex and vertex counts (`Regge.cs`):
$$S_{\text{Regge}}(N_0, N_3, N_{31}, N_{13}) = -k_0 N_0 + k_3 N_3 + \Delta (N_{31} + N_{13})$$
where:
- $N_0$ is the total number of vertices ($0$-simplices), conjugate to the bare inverse Newton constant $k_0 \propto 1/G_0$.
- $N_3 = N_{31} + N_{22} + N_{13}$ is the total number of tetrahedra ($3$-simplices), conjugate to the bare cosmological constant $k_3$ (`Kd` in `PhysicsParameters`).
- $\Delta$ is the causal asymmetry parameter encoding the ratio of timelike to spacelike edge lengths squared ($l_t^2 = -\alpha l_s^2$).

### 1.2 Saddle-Point Equilibrium & The Cosmological Constant Calibration Slope
In general spacetime dimension $d = d_s + 1$, the number of distinct causal triangulations grows exponentially with top-dimensional volume $N_d$ as $\mathcal{N}(N_d; k_0, \Delta) \sim e^{+k_d^c(k_0, \Delta) N_d}$. Combining the partition function with the quadratic volume-fixing potential $S_{\text{vol}} = \epsilon (N_d - \hat{N}_d)^2$ around target volume $\hat{N}_d$ yields the effective volume action:
$$S_{\text{eff}}(N_d) = (k_d - k_d^c) N_d + \epsilon (N_d - \hat{N}_d)^2$$

The equilibrium expectation value $\langle N_d \rangle$ sits at the saddle-point minimum $\frac{\partial S_{\text{eff}}}{\partial N_d} = 0$:
$$(k_d - k_d^c) + 2\epsilon (\langle N_d \rangle - \hat{N}_d) = 0 \implies \langle N_d \rangle = \hat{N}_d - \frac{k_d - k_d^c}{2\epsilon}$$

Solving for $k_d^c$ gives the exact single-probe saddle-point estimator used to step $k_d$ during calibration:
$$k_d^c = k_d + 2\epsilon (\langle N_d \rangle - \hat{N}_d)$$
and differentiating with respect to $k_d$ gives the universal linear susceptibility slope:
$$\frac{\partial \langle N_d \rangle}{\partial k_d} = -\frac{1}{2\epsilon}$$
- **Empirical Verification:** At $\epsilon = 0.0005$, the theoretical slope is $-1/(2 \times 0.0005) = -1000.0\text{ simplices}/k_d$, with tolerance band half-width $\delta k_d = 2\epsilon \hat{N}_d \cdot \text{ToleranceRatio}$ ($\pm 0.0800$ at $\hat{N}_3 = 16,000$ and $\pm 0.5\%$ tolerance).

### 1.3 Gaussian Thermal Fluctuation Width ($\sigma_{N_d} = 1/\sqrt{2\epsilon}$)
Expanding $S_{\text{eff}}(N_d)$ to second order around $\langle N_d \rangle$ yields a Gaussian volume distribution:
$$P(N_d) \propto \exp\!\left(-\epsilon (N_d - \langle N_d \rangle)^2\right) = \exp\!\left(-\frac{(N_d - \langle N_d \rangle)^2}{2\sigma_{N_d}^2}\right) \implies \sigma_{N_d} = \frac{1}{\sqrt{2\epsilon}}$$
- At $\epsilon = 0.02$: $\sigma_{N_d} = 5.00\text{ simplices}$ (Open-CDT measured $4.96$).
- At $\epsilon = 0.0005$: $\sigma_{N_d} = 31.62\text{ simplices}$ (Open-CDT measured $31.3\text{--}35.9$ across the 256-point critical sheet).
- `Dynamics.Create` sizes each L1 lookup table radius (`VolumeRadius`) to $R_{\text{vol}} = \min(4096, \lceil 5/\sqrt{2\epsilon} \rceil)$ (padded up to a 16-entry / 64-byte cache-line multiple), enclosing $99.99994\%$ of equilibrium volume fluctuations while keeping gentle $\epsilon$ values (e.g., $\epsilon = 0.0005$) from damping physical quantum fluctuations.

### 1.4 Linearized FMA Volume-Fixing Differential (`VolumeFixing.cs`)
For a Pachner move changing total volume by $\Delta N_d \in \{-4, -1, +1, +4\}$ from current signed offset $\delta N = N_d - \hat{N}_d$, the quadratic change in volume-fixing action expands as:
$$\Delta S_{\text{vol}} = \epsilon (\delta N + \Delta N_d)^2 - \epsilon (\delta N)^2 = \underbrace{\epsilon (\Delta N_d)^2}_{\text{BaseVolumeS}} + \underbrace{(2\epsilon \Delta N_d)}_{\text{DriftFactor}} \cdot \delta N$$
The $(\delta N)^2$ term cancels identically, reducing the quadratic potential differential to a single linear Fused Multiply-Add (`Math.FusedMultiplyAdd(driftFactor, currentVolumeDiff, baseVolumeS)`).

---

## 2. Detailed Balance & Combinatorial Hastings Ratios

For each move pair $(M, M^{-1})$, the Metropolis-Hastings acceptance probability is:
$$P_{\text{accept}}(x \to x') = \min\!\left(1, \, H(x \to x') \cdot e^{-(\Delta S_{\text{Regge}} + \Delta S_{\text{vol}})}\right)$$
where the combinatorial Hastings proposal ratio $H(x \to x') = \frac{P_{\text{select}}(x' \to x)}{P_{\text{select}}(x \to x')}$ accounts for the species-partitioned `TypeStore` pool sizes and local sub-element selection probabilities:

1. **`Move23` ($2 \to 3$, $\Delta N_{22} = +1$):**
   - Forward selection: picks 1 of $N_{22}$ simplices, and 1 of its 4 facets ($P_{\text{fwd}} = \frac{1}{4 N_{22}}$).
   - Reverse `(3,2)` selection: 2 of the 3 new simplices around the axis are `(2,2)`, and 1 of the 6 edges of each is the central axis ($P_{\text{rev}} = \frac{2}{6 (N_{22} + 1)}$).
   $$H_{23} = \frac{4}{3} \frac{N_{22}}{N_{22} + 1}$$
2. **`Move32` ($3 \to 2$, $\Delta N_{22} = -1$):**
   $$H_{32} = \frac{3}{4} \frac{N_{22} - 1}{N_{22}}$$
3. **`Move26` ($2 \to 6$, $\Delta N_0 = +1, \Delta N_{13} = +2$):**
   - Forward selection: picks 1 of $N_{13}$ simplices; its single spacelike face opposite `slotDown` is unique ($P_{\text{fwd}} = \frac{1}{N_{13}}$).
   - Reverse `(6,2)` selection: picks 1 of $N_0 + 1$ vertices ($P_{\text{rev}} = \frac{1}{N_0 + 1}$).
   $$H_{26} = \frac{N_{13}}{N_0 + 1}$$
4. **`Move62` ($6 \to 2$, $\Delta N_0 = -1, \Delta N_{13} = -2$):**
   $$H_{62} = \frac{N_0}{N_{13} - 2}$$
5. **`Move44` ($4 \to 4$, $\Delta N = 0$):**
   - Both forward and reverse moves select 4 of the $N_3$ simplices and 1 of 6 edges, with $\Delta S = 0$, giving $H_{44} = 1$ and $P_{\text{accept}} \equiv 1$.

---

## 3. Calibration, Phase Classification & Autocorrelation Mathematics

### 3.1 Collinear Secant Warm-Starting & Phase-Boundary Detection
- **Collinear Secant Extrapolation (`CriticalSurfaceTracker`):** Because $k_d^c(k_0, \Delta)$ varies smoothly across the bare coupling plane, whenever two coordinates $(x_A, k_d^c(x_A))$ and $(x_B, k_d^c(x_B))$ have already locked along the same constant-$\Delta$ or constant-$k_0$ grid line, the initial probe at $x$ is seeded via collinear secant projection:
  $$k_d^{(0)}(x) = k_d^c(x_A) + \frac{k_d^c(x_B) - k_d^c(x_A)}{x_B - x_A}(x - x_A)$$
  reducing 256-coordinate grid calibration from $\sim 2.0$ probes/coordinate down to $\sim 1.27$ probes/coordinate.
- **Topological Vertex-Density Ceiling (`PhaseBoundaryClassifier`):** By Barnette's Lower Bound Theorem (Barnette, 1973), any simplicial $(d_s)$-sphere with $V$ vertices contains at least $F_{\min}(V) \sim d_s V$ spatial $(d_s)$-simplices, and each spatial $(d_s)$-simplex bounds two adjacent causal $(d_s + 1)$-simplices ($(d_s + 1, 1)$ below and $(1, d_s + 1)$ above). In the decoupled branched-polymer phase (`Phase A`), where intermediate $(2,2)$-type simplices vanish, the bulk vertex density saturates against the universal dimension-dependent ceiling:
  $$\left(\frac{\langle N_0 \rangle}{\langle N_d \rangle}\right)_{\max} = \frac{1}{2 d_s} \qquad \left(\frac{1}{4} = 0.25\text{ in }2{+}1\text{D}, \quad \frac{1}{6} \approx 0.167\text{ in }3{+}1\text{D}\right)$$
  *(up to a small positive finite-size shift $+\mathcal{O}(T / \hat{N}_d)$ from the $T$ spherical Euler constants)*. Along each constant-$\Delta$ slice, `PhaseBoundaryClassifier` screens single-slice collapse (`Phase B`) via $O_{\max} > 0.50$ and locates the `Phase C` $\leftrightarrow$ `Phase A` saturation elbow by minimizing the combined residual sum of squares (RSS) of a two-segment piecewise-linear fit to $\langle N_0 \rangle / \langle N_d \rangle$ versus $k_0$.
- **Shoelace Polygon Centroid (`PhaseCentroid`):** Given the ordered vertices $(x_i, y_i)_{i=0}^{M-1}$ of the closed bounding polygon enclosing all `Phase C` grid coordinates, the interior target coordinate is selected nearest to the area-weighted 2D Shoelace barycentre:
  $$A = \frac{1}{2}\sum_{i=0}^{M-1} (x_i y_{i+1} - x_{i+1} y_i), \qquad C_x = \frac{1}{6A}\sum_{i=0}^{M-1}(x_i + x_{i+1})(x_i y_{i+1} - x_{i+1} y_i), \qquad C_y = \frac{1}{6A}\sum_{i=0}^{M-1}(y_i + y_{i+1})(x_i y_{i+1} - x_{i+1} y_i)$$

### 3.2 Madras-Sokal Self-Consistent Autocorrelation Window ($W_{\min} = 24\tau_{\text{int}}$)
Given a stationary trace $X_1, \dots, X_W$ of length $W$, the normalized autocorrelation function at lag $k$ is:
$$\rho(k) = \frac{\frac{1}{W-k}\sum_{i=1}^{W-k}(X_i - \bar{X})(X_{i+k} - \bar{X})}{\frac{1}{W}\sum_{i=1}^{W}(X_i - \bar{X})^2}$$
The integrated autocorrelation time $\tau_{\text{int}} = \frac{1}{2} + \sum_{k=1}^{M} \rho(k)$ is truncated at the first lag $M$ satisfying the Madras-Sokal windowing condition $M \ge 6\tau_{\text{int}}$ to prevent tail variance blowup. Since the maximum reliable lag estimator requires $M \le W/4$, resolving $\tau_{\text{int}}$ requires a minimum trace length of:
$$6\tau_{\text{int}} \le \frac{W}{4} \implies W \ge 24\tau_{\text{int}}$$
- **Two Relaxation Timescales in CDT:** Evaluating $\tau_{\text{int}}(N_0)$ over `WindowSweeps` calibrates an automatic lower bound $2\tau_{\text{int}}$ for local bulk curvature decorrelation (which rises sharply near phase boundaries). Deep inside `Phase C`, local vertex counts mix rapidly ($\tau_{\text{int}}(N_0) \sim 20\text{--}65$ sweeps at $\hat{N}_3 = 16,000$), whereas global redistribution of spatial volume across $T$ proper-time slices diffuses on a slower $\mathcal{O}(T^2)$ timescale ($\sim 10^3$ sweeps); production runs therefore pair $2\tau_{\text{int}}(N_0)$ with a user-configured floor `MinDecorrelationSweeps` ($\max(\text{MinDecorrelationSweeps}, 2\tau_{\text{int}})$).

---

## 4. Dimension-Agnostic Quantum Gravity Observables (`CDT-Agnostic`)

All observables implement `IManifoldObservable<TResult>` and operate purely through `IInspectingAccessor<TAdjacency>` and `IRandomAccessor`, making them 100% dimension-agnostic across `2+1D` and `3+1D`.

### 4.1 Proper-Time Volume Profile (`VolumeProfile.cs`) & Spherical Droplet Scaling
Every top-dimensional causal simplex spans two adjacent proper-time slices $t$ and $(t + 1) \bmod T$. To assign each simplex to its originating slab $t \in [0 .. T-1]$ without branching on simplex types or executing modulo division per vertex, `VolumeProfile` computes $t_{\min}$ and $t_{\max}$ across the simplex's vertices:
- **Interior slabs ($0 \le t < T - 1$):** $t_{\max} - t_{\min} = 1 \implies \text{slab} = t_{\min}$.
- **Periodic boundary wrap ($t = T - 1 \to 0$):** $t_{\min} = 0$ and $t_{\max} = T - 1$ ($t_{\max} - t_{\min} = T - 1 > 1$) $\implies \text{slab} = t_{\max} = T - 1$.
$$\text{slab} = (t_{\max} - t_{\min} > 1) \; ? \; t_{\max} : t_{\min}$$

Each periodic snapshot is circularly shifted via its fundamental $k = 1$ Fourier harmonic phase $\theta_1 = \operatorname{atan2}(\sum_t N_d(t)\sin\frac{2\pi t}{T}, \, \sum_t N_d(t)\cos\frac{2\pi t}{T})$ so its center of volume aligns near $T/2$. In `Phase C`, slicing an emergent Euclidean $(d_s + 1)$-sphere ($S^d$) perpendicular to proper time yields spatial cross-sections scaling as $\cos^{d_s}$ (`DropletProfileFit.cs`):
$$\langle N_d(t) \rangle = N_{\text{stalk}} + A \cos^{d_s}\!\left(\frac{\pi (t - t_0)}{W}\right), \qquad |t - t_0| \le \frac{W}{2}$$
with $W \propto \hat{N}_d^{1/(d_s + 1)}$ ($\cos^2$ in $2{+}1$D, $\cos^3$ in $3{+}1$D) and sub-slice center $t_0 \in [T/2 - 1, T/2 + 1]$ absorbing $\pm 0.5$-slice integer circular-shift rounding.

### 4.2 Geodesic Hausdorff Dimension (`Hausdorff.cs`): Shell Area vs. Ball Volume
`Hausdorff` performs multi-origin wavefront Breadth-First Search (BFS) on the dual simplicial graph using pre-allocated arena-sized queues and an integer `_bfsGeneration` stamp-on-enqueue counter (avoiding `Array.Clear` between origins).
- Let $N(r)$ be the mean number of simplices at discrete dual-graph geodesic link distance $r$ from an origin simplex (the concentric shell area), and $V(r) = \sum_{k=0}^{r} N(k)$ be the cumulative geodesic ball volume:
  $$V(r) \propto r^{D_H} \implies N(r) = \frac{dV(r)}{dr} \propto r^{D_H - 1}$$
- Prior to compact spatial slice saturation ($r_{\text{peak}}$ where $N(r)$ is maximal), Ordinary Least Squares (OLS) linear regression of $\ln V(r)$ against $\ln r$ over the bulk scaling window $r \in [\max(3, \lfloor r_{\text{peak}}/4 \rfloor), \, \lfloor 0.75 \, r_{\text{peak}} \rfloor]$ extracts the macroscopic Hausdorff dimension:
$$D_H = \frac{d \ln V(r)}{d \ln r} \approx d_s + 1 \quad (D_H \approx 3\text{ in }2{+}1\text{D})$$

### 4.3 Running Spectral Dimension (`SpectralDimension.cs`) & Heat-Kernel Diffusion
`SpectralDimension` evaluates exact discrete heat-kernel return probabilities $P(\sigma) = K(x_0, x_0; \sigma)$ on the dual graph up to diffusion step $\sigma_{\max}$ from uniformly sampled active origin simplices $x_0$:
$$K(x, x_0; \sigma + 1) = (1 - \chi)\, K(x, x_0; \sigma) + \frac{\chi}{d + 1} \sum_{y \sim x} K(y, x_0; \sigma), \qquad K(x, x_0; 0) = \delta_{x, x_0}$$
with diffusion weight $\chi = 0.8$ (Benedetti & Henson, 2009), where the $(1 - \chi) = 0.2$ hold weight damps period-two parity oscillations on nearly bipartite dual subgraphs without introducing stochastic walker shot noise.
- **Symmetric Central-Difference Derivative & Jackknife Resampling (`SpectralFlowAnalysis.cs`):**
  The scale-dependent running spectral dimension $D_S(\sigma) = -2\,\frac{d \ln \langle P(\sigma) \rangle}{d \ln \sigma}$ is evaluated via equal-parity central differences $(\sigma - 1, \sigma + 1)$:
  $$D_S(\sigma) = -2\,\frac{\ln \langle P(\sigma + 1) \rangle - \ln \langle P(\sigma - 1) \rangle}{\ln(\sigma + 1) - \ln(\sigma - 1)}$$
  Because $D_S(\sigma)$ is a nonlinear ratio of ensemble means, cross-replica standard errors are propagated via leave-one-replica-out jackknife resampling across the $M$ independent Markov chains:
  $$\text{SEM}_{\text{JK}}\!\left[D_S(\sigma)\right] = \sqrt{\frac{M - 1}{M} \sum_{r=1}^{M} \left(D_S^{(-r)}(\sigma) - \overline{D_S^{(*)}(\sigma)}\right)^2}$$
- **Ambjørn-Jurkiewicz-Loll Rational Fit:** Over the pre-turnover diffusion regime, $D_S(\sigma)$ follows the rational dimensional-reduction trajectory:
  $$D_S(\sigma) = D_{\infty} - \frac{b}{c + \sigma}$$
  flowing from $D_S(\text{UV}) \approx 2.0$ at Planckian diffusion scales toward $D_{\infty} \approx d_s + 1$ in the macroscopic continuum limit (with $D_\infty \approx 2.8\text{--}2.9$ at finite lattice volume $\hat{N}_3 = 16,000$ prior to $\hat{N}_3 \to \infty$ finite-size extrapolation).

---

## 5. Academic Literature & Software References

### 5.1 Causal Dynamical Triangulations & Quantum Gravity
1. **Ambjørn, J., Jurkiewicz, J., & Loll, R. (2000).** *"A non-perturbative Lorentzian path integral for gravity."* Physical Review Letters, 85(5), 924–927. [`arXiv:hep-th/0002050`] — Foundational formulation of Lorentzian Causal Dynamical Triangulations and dual-graph Hausdorff dimension scaling.
2. **Ambjørn, J., Jurkiewicz, J., & Loll, R. (2001).** *"Non-perturbative 3D Lorentzian quantum gravity."* Physical Review D, 64(4), 044011. [`arXiv:hep-th/0011276`] — Comprehensive derivation of 2+1D (3D Lorentzian) CDT, the Wick-rotated discrete Regge action, and the five ergodic 2+1D Pachner moves.
3. **Ambjørn, J., Jurkiewicz, J., & Loll, R. (2004).** *"Emergence of a 4D World from Causal Quantum Gravity."* Physical Review Letters, 93(13), 131301. [`arXiv:hep-th/0404156`] — Phase structure of CDT and emergence of macroscopic De Sitter geometry.
4. **Ambjørn, J., Jurkiewicz, J., & Loll, R. (2005a).** *"Reconstructing the Universe."* Physical Review D, 72(6), 064014. [`arXiv:hep-th/0505154`] — Detailed derivation of volume-fixing thermodynamics, mini-superspace action, and De Sitter volume profiles.
5. **Ambjørn, J., Jurkiewicz, J., & Loll, R. (2005b).** *"The Spectral Dimension of the Universe is Scale Dependent."* Physical Review Letters, 95(17), 171301. [`arXiv:hep-th/0505113`] — Discovery of dynamic dimensional reduction in the running spectral dimension $D_S(\sigma)$.
6. **Benedetti, D., & Henson, J. (2009).** *"Spectral geometry as a probe of quantum spacetime."* Physical Review D, 80(12), 124036. [`arXiv:0911.0401`] — Discrete dual-graph heat-kernel diffusion recurrence ($\chi = 0.8$) and finite-volume spectral dimension scaling in 2+1D CDT.
7. **Görlich, A. (2010).** *"Causal Dynamical Triangulations in Four Dimensions."* PhD Thesis, Jagiellonian University. [`arXiv:1111.6938`] — Computational implementation reference for CDT moves, Metropolis-Hastings acceptance ratios, and jackknife observable error analysis.
8. **Brunekreef, J., Görlich, A., & Loll, R. (2024).** *"Simulating CDT quantum gravity."* Computer Physics Communications, 300, 109170. [`arXiv:2310.16744`] — Methods reference for 2+1D CDT simulation algorithms and benchmarks.

### 5.2 Simplicial Topology, MCMC Statistics & Algorithms
9. **Pachner, U. (1991).** *"P.L. homeomorphic manifolds are equivalent by elementary shellings."* European Journal of Combinatorics, 12(2), 129–145. — Proof that finite sequences of bistellar (Pachner) moves connect all piecewise-linear triangulations of a given manifold.
10. **Barnette, D. (1973).** *"A proof of the lower bound conjecture for convex polytopes."* Pacific Journal of Mathematics, 46(2), 349–354. — Simplicial sphere vertex-to-facet lower bound underlying the topological vertex-density ceiling $1/(2d_s)$ in `PhaseBoundaryClassifier`.
11. **Caspar, D. L. D., & Klug, A. (1962).** *"Physical principles in the construction of regular viruses."* Cold Spring Harbor Symposia on Quantitative Biology, 27, 1–24. — Combinatorial icosahedral geodesic sphere triangulation ($V = 10k^2 + 2, \, F = 20k^2$) used in `GeodesicSphere`.
12. **Madras, N., & Sokal, A. D. (1988).** *"The pivot algorithm: A highly efficient Monte Carlo method for the self-avoiding walk."* Journal of Statistical Physics, 50(1–2), 109–186. — Self-consistent automatic windowing estimator ($M \ge 6\tau_{\text{int}}$) for integrated autocorrelation time.
13. **Briggs, P., & Torczon, L. (1993).** *"An efficient representation for sparse sets."* ACM Letters on Programming Languages and Systems (LOPLAS), 2(1–4), 59–69. — The $\mathcal{O}(1)$ bidirectional dense-array sparse-set architecture used in `SlotStore` and `TypeStore`.
14. **O'Neill, M. E. (2014).** *"PCG: A family of simple fast space-efficient statistically good algorithms for random number generation."* Harvey Mudd College Technical Report HMC-CS-2014-0905. — Permuted Congruential Generator (`Pcg32` XSH-RR).
15. **Lemire, D. (2019).** *"Fast random integer generation in an interval."* ACM Transactions on Modeling and Computer Simulation (TOMACS), 29(1), 1–12. — Fast divisionless 64-bit multiply-shift bounded integer sampling (`Pcg32.NextBounded`).

### 5.3 Prior Open-Source CDT Implementations
16. **Getchell, A., et al.** **`CDT-plusplus`** [`https://github.com/acgetchell/CDT-plusplus`] — Open-source C++ implementation of Causal Dynamical Triangulations using CGAL; referenced during initial verification of 2+1D ergodic move topological invariants.
17. **Brunekreef, J., Görlich, A., & Loll, R.** **`2d-cdt`** [`https://github.com/JorenB/2d-cdt`] & **`3d-cdt`** [`https://github.com/JorenB/3d-cdt`] — Open-source C++ companion codes to Brunekreef, Görlich & Loll (2024) for 1+1D and 2+1D CDT.