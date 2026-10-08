import numpy as np
from pymoo.algorithms.moo.nsga2 import NSGA2
from pymoo.core.problem import Problem
from pymoo.optimize import minimize

class GreenSpaceProblem(Problem):
    def __init__(self, n_cells=30, grid_cols=6, target_ratio=0.5, n_regions=3):
        self.n_cells = n_cells
        self.grid_cols = grid_cols
        self.target_ratio = target_ratio
        self.n_regions = n_regions
        self.grid_rows = n_cells // grid_cols

        self.region_map = np.array([
            i // (n_cells // n_regions) for i in range(n_cells)
        ])
        self.region_map = np.clip(self.region_map, 0, n_regions - 1)

        super().__init__(
            n_var=n_cells,
            n_obj=4,
            xl=np.zeros(n_cells),
            xu=np.ones(n_cells)
        )

    def _evaluate(self, x, out, *args, **kwargs):
        green = x > 0.5

        # Objective 1: Maximize green space coverage rate
        green_ratio = green.sum(axis=1) / self.n_cells
        f1 = -green_ratio

        # Objective 2: Minimize the difference from target area
        f2 = np.abs(green_ratio - self.target_ratio)

        # Objective 3: Minimize regional distribution unevenness
        f3 = np.zeros(len(x))
        for region_id in range(self.n_regions):
            region_mask = self.region_map == region_id
            region_cells = region_mask.sum()
            if region_cells > 0:
                region_green = green[:, region_mask].sum(axis=1) / region_cells
                f3 += np.abs(region_green - green_ratio)

        # Objective 4: Minimize isolated green cells
        f4 = np.zeros(len(x))
        grid = green.reshape(len(x), self.grid_rows, self.grid_cols)

        for r in range(self.grid_rows):
            for c in range(self.grid_cols):
                is_green = grid[:, r, c]

                neighbors = []
                if r > 0: neighbors.append(grid[:, r-1, c])
                if r < self.grid_rows-1: neighbors.append(grid[:, r+1, c])
                if c > 0: neighbors.append(grid[:, r, c-1])
                if c < self.grid_cols-1: neighbors.append(grid[:, r, c+1])

                if len(neighbors) > 0:
                    has_green_neighbor = np.stack(neighbors, axis=1).any(axis=1)
                    f4 += is_green & ~has_green_neighbor

        f4 = f4 / self.n_cells

        out["F"] = np.column_stack([f1, f2, f3, f4])


# Run optimization
problem = GreenSpaceProblem(
    n_cells=30,
    grid_cols=6,
    target_ratio=0.5,
    n_regions=3
)

algorithm = NSGA2(pop_size=100)

result = minimize(
    problem,
    algorithm,
    termination=('n_gen', 300),
    seed=1,
    verbose=True
)

# Output results
print("\nOptimization complete")
print("Number of Pareto solutions:", len(result.F))

print("\nFirst 5 Pareto solutions:")
for i, (f, x) in enumerate(zip(result.F[:5], result.X[:5])):
    green_cells = (x > 0.5).sum()
    print(f"  Solution {i+1}: Coverage rate={-f[0]:.2%}, Area difference={f[1]:.3f}, Distribution evenness={f[2]:.3f}, Isolated cells={f[3]:.3f}, Green cells={green_cells}/30")

# Select compromise solution
f_min = result.F.min(axis=0)
f_max = result.F.max(axis=0)
f_normalized = (result.F - f_min) / (f_max - f_min + 1e-9)
distances = np.sqrt((f_normalized**2).sum(axis=1))
best_idx = distances.argmin()

print(f"\nCompromise solution:")
print(f"  Coverage rate:        {-result.F[best_idx][0]:.2%}")
print(f"  Area difference:      {result.F[best_idx][1]:.4f}")
print(f"  Distribution evenness: {result.F[best_idx][2]:.4f}")
print(f"  Isolated cell rate:   {result.F[best_idx][3]:.4f}")
print(f"  Green cells count:    {(result.X[best_idx] > 0.5).sum()}/30")

# Visualization
best_layout = (result.X[best_idx] > 0.5).reshape(5, 6)
print(f"\nCompromise solution green space layout:")
for row in best_layout:
    print("  " + " ".join(["🟩" if c else "⬜" for c in row]))
