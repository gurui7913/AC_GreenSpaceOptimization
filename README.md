# Green Space Layout Optimization for Aging Communities

**Comfort in Sight — Multi-Objective Genetic Algorithm for Age-Friendly Green Space Layout**

> UCL Bartlett School of Architecture · Architectural Computation – BARC0034 Morphogenetic Programming · 2025  
> Author: **Gu Rui** — Research Design, Algorithm Implementation, Spatial Analysis

> **My contribution:** Problem formulation · 4-objective fitness function design · Connectivity constraint implementation · V2 Python rebuild · Compromise solution selection logic

---

## Table of Contents

- [Research Question](#research-question)
- [Pipeline Overview](#pipeline-overview)
- [Site Representation & Grid Encoding](#site-representation--grid-encoding)
- [Optimization Objectives](#optimization-objectives)
  - [Objective 1 — Green View Index](#objective-1--green-view-index)
  - [Objective 2 — Area Compliance](#objective-2--area-compliance)
  - [Objective 3 — Distribution Evenness](#objective-3--distribution-evenness)
  - [Objective 4 — Spatial Connectivity](#objective-4--spatial-connectivity)
- [NSGA-II Algorithm Design](#nsga-ii-algorithm-design)
  - [Why NSGA-II](#why-nsga-ii)
  - [Representation & Operators](#representation--operators)
  - [Compromise Solution Selection](#compromise-solution-selection)
- [Workflow](#workflow)
  - [Stage 1 — Site Analysis & Grid Generation](#stage-1--site-analysis--grid-generation)
  - [Stage 2 — Fitness Function Implementation](#stage-2--fitness-function-implementation)
  - [Stage 3 — NSGA-II Optimization](#stage-3--nsga-ii-optimization)
  - [Stage 4 — Pareto Analysis & Solution Output](#stage-4--pareto-analysis--solution-output)
- [Versions](#versions)
- [Results](#results)
- [Project Structure](#project-structure)
- [How to Run](#how-to-run)
- [Limitations & Future Work](#limitations--future-work)
- [References](#references)

---

## Research Question

Green space visibility has been consistently linked to physical and mental wellbeing among elderly residents in high-density Chinese residential communities. However, standard planning tools optimise a single objective (typically total green area) and cannot navigate the multi-dimensional trade-offs inherent in layout design.

> **Can a multi-objective genetic algorithm generate green space layouts that simultaneously maximise visual accessibility for elderly residents while satisfying area compliance, spatial distribution, and connectivity constraints in residential communities?**

The core tension: maximising Green View Index tends to cluster green cells near pedestrian paths, which conflicts with even distribution across community zones and creates isolated patches. No single optimal solution exists — only a Pareto front of non-dominated trade-offs.

---

## Pipeline Overview

```
Stage 1              Stage 2                 Stage 3              Stage 4
Site Analysis  ──►   Fitness Function   ──►  NSGA-II         ──►  Pareto Analysis
& Grid                Design                 Optimization          & Output
Generation

• Green boundary     • GVI via raycasting    • Binary chromosome  • Non-dominated
  extraction           (V1) / coverage         (1 gene = 1 cell)    front extraction
• Building           • Area deviation        • Tournament         • Objective
  footprint            objective               selection            normalisation
  exclusion          • Distribution          • Crossover &        • Euclidean
• Path sampling        evenness                mutation             distance to
  (5m intervals)     • Connectivity          • 100 generations,   ideal point
• 5×5m grid            constraint              pop size 100       • Compromise
  generation                                                        solution output
```

---

## Site Representation & Grid Encoding

### Site Processing

- Green boundaries processed via **boolean difference** with building footprints to produce valid candidate green surfaces
- Pedestrian paths sampled at **5-metre intervals** to generate observation points for Green View Index calculation
- Building footprints explicitly excluded from candidate cell pool

### Grid Encoding

- Valid green area divided into uniform **5×5m grid cells**
- Each cell encoded as a **binary gene**: `1 = green`, `0 = non-green`
- Full layout represented as a **1D binary chromosome** of length = number of valid cells

```
Site Boundary
┌──────────────────────────┐
│  [B][B][B][0][0][0][0]  │   B = Building (excluded from chromosome)
│  [B][B][0][1][1][0][0]  │   0 = Non-green candidate cell (gene = 0)
│  [0][0][1][1][0][0][0]  │   1 = Green cell (gene = 1)
│  [0][1][1][0][0][B][B]  │
│  [0][0][0][0][B][B][B]  │
└──────────────────────────┘
         ↓ path sampling ↓
    [p1] [p2] [p3] [p4] ...    ← observation points for GVI
```

---

## Optimization Objectives

Four objectives are simultaneously optimised. All minimisation objectives use negation or deviation formulations to fit the standard minimisation convention in pymoo.

### Objective 1 — Green View Index

**Goal: Maximise** (converted to minimise via negation)

$$f_1 = -\text{GVI} = -\frac{1}{|P|}\sum_{p \in P} \frac{\text{green rays from } p}{\text{total rays from } p}$$

| Symbol | Meaning |
|:---:|:---|
| $P$ | Set of path observation points |
| green rays | Rays from $p$ that terminate at a green cell |
| total rays | Total rays cast from $p$ across the grid |

**V1 implementation:** Isovist-based raycasting in Rhino Grasshopper — each observation point casts directional rays; green cell intersections counted.  
**V2 implementation:** Simplified as green cell coverage ratio within a defined radius of each path observation point.

**Design rationale:** GVI directly measures what elderly residents *see* from pedestrian paths, not just how much green space exists. Coverage area metrics would over-reward remote or hidden green cells.

### Objective 2 — Area Compliance

**Goal: Minimise deviation from planning target**

$$f_2 = \left| \sum_{i} g_i \cdot A_{\text{cell}} - A_{\text{target}} \right|$$

| Symbol | Meaning |
|:---:|:---|
| $g_i$ | Binary gene value for cell $i$ |
| $A_{\text{cell}}$ | Area per cell (25 m²) |
| $A_{\text{target}}$ | Planning standard green area target (6,476 m²) |

**Design rationale:** Chinese residential planning standards specify a minimum green ratio. Objective 2 enforces planning compliance rather than treating it as a hard constraint, allowing the optimizer to explore near-compliant solutions that may excel on other objectives.

### Objective 3 — Distribution Evenness

**Goal: Minimise spatial unevenness across community zones**

$$f_3 = \sum_{z \in Z} \left| \frac{\text{green cells in zone } z}{\text{total green cells}} - \frac{|z|}{|G|} \right|$$

| Symbol | Meaning |
|:---:|:---|
| $Z$ | Set of community zones (subdivisions of the site) |
| $\|z\|$ | Number of candidate cells in zone $z$ |
| $\|G\|$ | Total candidate cells across all zones |

**Design rationale:** Maximising GVI alone tends to cluster green near paths in one sector. Objective 3 distributes green coverage proportionally across all zones, ensuring all residents benefit.

### Objective 4 — Spatial Connectivity

**Goal: Minimise isolated green patches**

$$f_4 = \sum_{i} g_i \cdot \mathbb{1}\left[\sum_{j \in \mathcal{N}(i)} g_j = 0\right]$$

| Symbol | Meaning |
|:---:|:---|
| $\mathcal{N}(i)$ | 4-connected neighbourhood of cell $i$ (N, S, E, W) |
| $\mathbb{1}[\cdot]$ | Indicator: 1 if green cell $i$ has zero green neighbours |

**Design rationale:** Isolated single-cell green patches reduce ecological value and spatial coherence. Objective 4 acts as a soft connectivity constraint, penalising fragmented layouts without imposing hard topological requirements that would reduce search space diversity.

> **Note:** Objective 4 is the primary addition in V2 over V1, and acts as a **binding constraint** in practice — solutions that satisfy Objective 4 consistently improve Objective 3 without sacrificing Objective 1.

---

## NSGA-II Algorithm Design

### Why NSGA-II

| Algorithm | Multi-objective | Pareto-based ranking | Diversity preservation | Complexity |
|:---:|:---:|:---:|:---:|:---:|
| GA (single-obj) | ✗ | ✗ | — | Low |
| NSGA-I | ✓ | ✓ | ✗ (fitness sharing) | Medium |
| **NSGA-II** | **✓** | **✓** | **✓ (crowding distance)** | **Medium** |
| MOEA/D | ✓ | Scalarised | ✓ | High |

NSGA-II was selected for three structural reasons:
- **Pareto front tracking** — explicit non-dominated ranking preserves all viable trade-off solutions simultaneously, not just the current best
- **Crowding distance** — prevents convergence to a single region of the Pareto front; ensures spread across the trade-off surface
- **Scalability to 4 objectives** — established performance on problems with up to ~5 objectives; MOEA/D decomposition is unnecessary at this scale

### Representation & Operators

- **Chromosome:** 1D binary array, one bit per candidate grid cell
- **Selection:** Binary tournament — compares individuals first on non-dominated rank, then on crowding distance if ranks are equal
- **Crossover:** Single-point crossover at probability 0.9 — single random cut point; offspring inherit segments from both parents
- **Mutation:** Bit-flip per gene at probability 0.02 — each gene independently flips with 2% probability, preserving reasonable green area per individual

```python
# pymoo NSGA-II configuration (V2)
from pymoo.algorithms.moo.nsga2 import NSGA2
from pymoo.operators.crossover.sbx import SBX
from pymoo.operators.mutation.bitflip import BitflipMutation
from pymoo.operators.sampling.rnd import BinaryRandomSampling

algorithm = NSGA2(
    pop_size=100,
    sampling=BinaryRandomSampling(),
    crossover=SBX(prob=0.9),       # single-point crossover for binary
    mutation=BitflipMutation(prob=0.02),
    eliminate_duplicates=True
)
```

**Hyperparameter justification:**
- `pop_size=100` — sufficient diversity for a 30-cell (V2) / 613-cell (V1) search space without excessive compute
- `prob_crossover=0.9` — high recombination rate standard for NSGA-II; promotes exploration of new layout configurations
- `prob_mutation=0.02` — per-gene flip rate; at 30 cells this averages ~0.6 flips/individual/generation, preventing drift while maintaining diversity

### Compromise Solution Selection

The Pareto front contains multiple non-dominated solutions with different objective trade-off profiles. A single solution is selected for output via minimum Euclidean distance to the ideal point:

$$s^* = \arg\min_{s \in \mathcal{P}} \left\| \hat{f}(s) - \mathbf{0} \right\|_2$$

where $\hat{f}(s)$ is the objective vector of solution $s$ normalised to $[0, 1]$ per objective.

```python
# Normalise objectives and select compromise solution
from pymoo.decomposition.asf import ASF

weights = np.array([0.25, 0.25, 0.25, 0.25])  # equal weighting across 4 objectives
decomp = ASF()
idx = decomp.do(res.F, 1/weights).argmin()
compromise_solution = res.X[idx]
```

**Design choice:** Equal weighting treats all four objectives as equally important. In a real deployment, weights could be adjusted based on community demographics (e.g., higher weight on GVI for communities with limited mobility residents).

---

## Workflow

```
Stage 1              Stage 2                 Stage 3              Stage 4
Site Analysis  ──►   Fitness Function   ──►  NSGA-II         ──►  Pareto Analysis
& Grid                Evaluation             Loop                  & Selection
Generation
```

### Stage 1 — Site Analysis & Grid Generation

1. Import site geometry (green boundaries, building footprints, pedestrian paths)
2. Apply boolean difference: `green_candidates = green_boundary - building_footprints`
3. Generate 5×5m grid over candidate area; exclude building cells
4. Sample pedestrian paths at 5m intervals → observation point set $P$
5. Map each valid grid cell to a chromosome gene index

### Stage 2 — Fitness Function Implementation

Each NSGA-II evaluation call computes all four objective values for a given binary chromosome:

```python
class GreenSpaceOptimization(ElementwiseProblem):
    def __init__(self, grid, observation_points, zones, target_area):
        super().__init__(
            n_var=len(grid),        # one binary gene per valid cell
            n_obj=4,                # GVI, area deviation, distribution, connectivity
            n_constr=0,             # all objectives are soft (no hard constraints)
            xl=0, xu=1,
            vtype=bool
        )

    def _evaluate(self, x, out, *args, **kwargs):
        f1 = -compute_gvi(x, self.observation_points, self.grid)
        f2 = compute_area_deviation(x, self.target_area)
        f3 = compute_distribution_unevenness(x, self.zones)
        f4 = compute_isolated_patches(x, self.grid)
        out["F"] = [f1, f2, f3, f4]
```

### Stage 3 — NSGA-II Optimization

```python
from pymoo.optimize import minimize
from pymoo.termination import get_termination

termination = get_termination("n_gen", 100)

res = minimize(
    problem,
    algorithm,
    termination,
    seed=42,
    verbose=True
)
```

- **100 generations × 100 population = 10,000 evaluations** per run
- Each evaluation computes 4 objectives for one 30-gene (V2) chromosome
- Non-dominated sorting at each generation maintains the Pareto front

### Stage 4 — Pareto Analysis & Solution Output

1. Extract non-dominated front: `pareto_front = res.F[NonDominated().do(res.F)]`
2. Normalise objectives to $[0, 1]$ per objective range
3. Compute Euclidean distance to ideal point $(0, 0, 0, 0)$ for each Pareto solution
4. Select compromise solution at minimum distance
5. Decode binary chromosome → cell indices → layout geometry

---

## Versions

### V1 — Grasshopper / C# (Original)

Implemented in **Rhino Grasshopper with WallaceiX** multi-objective optimizer.

- Full spatial computation with real building geometry imported from CAD
- **Isovist-based raycasting** for GVI: rays cast from each path observation point; intersections with green surfaces computed in 3D
- Path sampling via Grasshopper curve subdivision
- 10 Pareto-optimal layouts visualised in 3D within Rhino viewport
- **Limitation:** tightly coupled to Rhino; non-reproducible without licensed software

**Tech stack:** C#, Grasshopper, Rhino, WallaceiX

### V2 — Python (Rebuilt)

Rebuilt core optimization logic in Python using **pymoo**, decoupled from geometry software.

- Simplified grid (30 cells vs 613 in V1) — proof of concept for algorithm logic
- GVI approximated as coverage ratio (not raycasting) — acceptable for algorithmic validation
- **Adds Objective 4 (Connectivity)** not implemented in V1
- Fully reproducible: pip-installable dependencies, seeded random state
- **Extensible:** designed for integration with Shapely-based real geometry and raycasting modules

**Tech stack:** Python 3.x, pymoo v0.6.1.6, NumPy

---

## Results

### Quantitative

| Metric | Baseline | Optimised Compromise |
|:---:|:---:|:---:|
| Green View Index | — | **+28% vs baseline** |
| Green Land-use Efficiency | — | **+20% vs baseline** |
| Isolated Patches | Present | **Zero** |
| Distribution Evenness | Uneven | Even across all zones |
| Pareto Solutions Generated | — | 10 non-dominated layouts |

### Objective Trade-off Structure

The Pareto front revealed a consistent trade-off geometry across runs:

- **GVI vs Distribution:** High-GVI solutions cluster green near paths → lower distribution scores
- **Connectivity as binding constraint:** Satisfying Objective 4 (zero isolated patches) consistently improved Objective 3 without sacrificing Objective 1 — connectivity acts as a soft regulariser on the layout
- **Area compliance tension:** Solutions near $A_{\text{target}}$ show wider variance on GVI; the optimizer exploits slight over/under-planting to improve visibility

---

## Project Structure

```
├── NSGA-II_Test.py          # V2: Python implementation — all 4 objectives + compromise selection
├── MorphoGenetic.pdf        # V1: Grasshopper implementation documentation + 3D visualisation
└── README.md
```

---

## How to Run

### Prerequisites

```bash
pip install pymoo numpy
# pymoo v0.6.1.6 recommended — API stable for NSGA2, ElementwiseProblem, ASF
```

### Run Optimization (V2)

```bash
python NSGA-II_Test.py
```

**Expected output:**
- Console: per-generation progress (objective values, Pareto front size)
- Final: compromise solution chromosome + objective values
- Optional: Pareto front scatter plot (requires matplotlib)

### Configuration

Key parameters in `NSGA-II_Test.py`:

| Parameter | Default | Effect |
|:---:|:---:|:---|
| `N_GEN` | 100 | Number of generations; increase for larger grids |
| `POP_SIZE` | 100 | Population size; increase for higher-dimensional search spaces |
| `PROB_CROSS` | 0.9 | Crossover probability |
| `PROB_MUT` | 0.02 | Per-gene mutation probability |
| `TARGET_AREA` | 6476 m² | Planning standard green area target |

### Reproduce V1 (Grasshopper)

Open `MorphoGenetic.pdf` for documented V1 pipeline. Requires Rhino 7+, Grasshopper, WallaceiX plugin.

---

## Limitations & Future Work

### Current Limitations

- **V2 grid size** (30 cells) is a simplified proxy for the original 613-cell site — algorithmic behavior validated but spatial resolution insufficient for real deployment
- **GVI approximation** in V2 uses coverage ratio rather than directional raycasting — overestimates visibility for green cells occluded by buildings
- **Single-site validation** — generalisability across different residential morphologies untested
- **Static pedestrian paths** — observation points fixed; does not account for route choice variation or time-of-day pedestrian distribution

### Planned Extensions

- **Real geometry integration:** Replace synthetic grid with Shapely-based polygons from real site CAD data; maintain cell-to-gene mapping
- **Raycasting-based GVI:** Implement directional ray casting using Shapely LineString intersection with building footprint geometries
- **Rhino bridge:** Export compromise solution cell indices → Rhino via Grasshopper Python component for 3D layout visualisation
- **Multi-site extension:** Batch optimization across sites with varying FAR (Floor Area Ratio) and block morphologies to test generalisability
- **Perception-weighted GVI:** Weight observation points by pedestrian flow data or elderly resident activity patterns — aligns GVI calculation with actual visual exposure

---

## References

1. Deb, K. et al. "A Fast and Elitist Multiobjective Genetic Algorithm: NSGA-II." *IEEE Transactions on Evolutionary Computation*, 6(2), 2002.
2. Blank, J. & Deb, K. "pymoo: Multi-Objective Optimization in Python." *IEEE Access*, 8, 2020.
3. Li, X. et al. "Effects of green space on the physical and mental health of the elderly." *Urban Forestry & Urban Greening*, 2019.
4. Yang, J. et al. "Expanding Greenspace in Dense Urban Environments." *Landscape and Urban Planning*, 2021.
5. Haq, S.M.A. "Urban Green Spaces and an Integrative Approach to Sustainable Environment." *Journal of Environmental Protection*, 2011.
