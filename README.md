# Green Space Layout Optimization for Aging Communities
**Comfort in Sight — Optimizing Green Space Layouts using Genetic Algorithm**

## Project Overview
This project applies multi-objective evolutionary optimization to generate age-friendly green space layouts in residential communities.
Using NSGA-II, the system balances green visibility, area compliance, spatial distribution, and connectivity to produce Pareto-optimal layout solutions.

The project targets aging communities in China, where green visibility has been shown to positively impact elderly residents' physical and mental wellbeing.

**Research Question:** How can multi-objective genetic algorithms generate green space layouts that simultaneously maximise visual accessibility for elderly residents while satisfying area, distribution, and connectivity constraints in residential communities?

---

## Pipeline Overview

- Site Analysis (green boundaries, building footprints, pedestrian paths)
- Grid Generation (5×5m cells, building exclusion)
- Fitness Evaluation (4 objectives)
- NSGA-II Optimization (100 generations, population size 100)
- Pareto Front Selection
- Compromise Solution Output

---

## Optimization Objectives

- **Objective 1:** Maximize Green View Index — raycasting from path sampling points to measure visible green coverage
- **Objective 2:** Minimize Area Deviation — actual green area stays close to planning target (6,476 m²)
- **Objective 3:** Minimize Distribution Unevenness — green space distributed proportionally across all zones
- **Objective 4:** Minimize Isolated Patches — green cells should have at least one green neighbour (connectivity constraint)

---

## Methods

### Site Processing
- Green boundaries processed via boolean difference with building footprints to generate valid green surfaces
- Pedestrian paths sampled every 5 metres to generate observation points for green view calculation

### Grid Representation
- Valid green area divided into uniform 5×5m grid cells
- Each cell encoded as boolean gene (1 = green, 0 = non-green)
- Building footprints excluded from candidate cells

### NSGA-II Algorithm
- **Representation:** Binary chromosome (one gene per grid cell)
- **Selection:** Binary tournament based on non-dominated rank and crowding distance
- **Crossover:** Single-point crossover (90% probability)
- **Mutation:** Bit-flip per gene (2% probability)
- **Output:** Pareto-optimal solution set

### Compromise Solution Selection
- Normalise all objectives to [0, 1]
- Calculate Euclidean distance to ideal point
- Select solution with minimum distance as final output

---

## Key Findings
- NSGA-II successfully navigated the trade-off between maximising green visibility and maintaining spatial connectivity — solutions that maximise Green View Index tend to cluster green cells near paths, at the cost of even distribution
- A Pareto front of 10 non-dominated solutions demonstrated the inherent tension between area compliance and distribution evenness
- The compromise solution demonstrated that connectivity (Objective 4) acts as a binding constraint: removing isolated patches consistently improved distribution scores without sacrificing visibility

---

## Results

- Generated 10 Pareto-optimal green space layout solutions
- Selected compromise solution achieved:
  - **28% increase** in Green View Index vs baseline
  - **20% improvement** in green land-use efficiency
  - Even distribution across all community zones
  - Zero isolated green patches

---

## Limitations
- V2 uses simplified grid without real building geometry
- Green View Index in V2 approximated by coverage ratio, not raycasting
- Small grid size (30 cells) compared to original 613 cells
- Sample site limited to one residential community; generalisability untested

## Future Work
- Integrate real site geometry using Shapely or similar
- Replace coverage ratio with raycasting-based green view score
- Connect V2 optimization output back to Rhino for 3D visualisation
- Extend to multi-site comparison across different community morphologies

---

## Versions

### V1 — Grasshopper / C# (Original)
Implemented in Rhino Grasshopper WallaceiX. Full spatial
computation with real building geometry, Isovist-based
raycasting, and path sampling. Produced 10 Pareto-optimal
layouts visualised in 3D.

### V2 — Python (Rebuilt)
Rebuilt core optimization logic in Python using pymoo library.
Implements all 4 objectives including the connectivity constraint
(Objective 4) not present in V1. Decoupled from geometry software
for reproducibility and extensibility.

---

## Tech Stack

**V1 (Original)**
- C#, Grasshopper, Rhino
- Custom NSGA-II implementation
- Isovist raycasting for green view calculation

**V2 (Python)**
- Python, pymoo
- NumPy

---

## Dependencies (V2)

```bash
pip install pymoo numpy
```

---

## Project Structure
```
├── NSGA-II_Test.py       # Python implementation (V2)
├── MorphoGenetic.pdf     # Original Grasshopper implementation (V1)
└── README.md
```

---

## Author
**Rui Gu** — Project Lead, Research Design, Algorithm Implementation

*UCL Architectural Computation, 2025 | BARC0034 Morphogenetic Programming*
