# Repository and source audit

Audit date: 2026-10-08. Scope: organize the local assignment archive and the existing GitHub repository, correct documentation, and validate what can be checked without Rhino. Historical algorithm files were not modified.

## Organization decisions

The original assignment archive contained 823 files across `A1`, `A2`, `A3`, `A4`, `Final_Project`, and `Finalc#`, totaling approximately 979 MiB. The maintained clone now lives in a separate `AC_GreenSpaceOptimization` subfolder. No original coursework was moved or deleted.

Published selections include student GH definitions, C# source/project exports, the final submitted Rhino/GH bundle, boards, presentation slides and three figure exports. Instructor assignment briefs, third-party teaching videos/examples, the undergraduate thesis archive, editable design iterations, backups and IDE state remain local. `Finalc#` contains an empty console `Main` scaffold and is not a functional optimizer, so it remains local as well.

The existing Python script, combined boards and `GreenSpaceOptimization_GH` assets remain at their published paths. [source-map.csv](source-map.csv) records original relative paths and SHA-256 checksums for imported or matched existing assets. The Python script comes from the existing GitHub history, not the local coursework source folders.

## Findings

### 1. Documentation described capabilities absent from the Python source

The previous README described a path-dependent Green View Index, radius-based coverage, binary sampling, configured crossover/mutation probabilities, 100 generations and a file named `NSGA-II_Test.py`. The published script instead has:

- A global green-cell fraction as its first objective, without geometry or observation points.
- `NSGA2(pop_size=100)` with default continuous operators, followed by thresholding at 0.5.
- 300 generations and seed 1.
- A ratio target of 0.5, with no physical cell area or 6,476-square-metre target.
- Min-max normalization and Euclidean compromise selection, rather than the README's separate ASF example.
- The filename `NSGA-II based green space optimization.py`.

The replacement READMEs describe the actual source. Unsupported improvement percentages were removed from the documentation. Historical PDF boards were preserved as original presentation material.

### 2. Main exported C# optimizer fails during non-dominated sorting

File: [C_-1f522.cs](../src/grasshopper/00_NSGA-II/C_-1f522.cs), `FastNonDominatedSort`, lines 1177–1200.

The loop reads `fronts[i]`, increments `i`, and only appends `nextFront` when it is nonempty. After processing the last nonempty front, `i` is beyond the list because the empty terminating front was not appended. The next loop condition throws `ArgumentOutOfRangeException`.

**Reproduction:** extracted the unchanged geometry-independent `Individual` and `NSGA2Optimizer` classes into a .NET 8 console harness; called the constructor with a population of 4, 6 genes, two finite fitness values and maximization/minimization flags. The constructor called `InitializePopulation`, which called `FastNonDominatedSort`, and threw the exception. The source file SHA-256 was `9a5f96e3b95786ca02a8ab0722a4e3fa6ee55ad4002d765ca09ae9e0ef130a30`.

The local reproducer is retained under the assignment folder's `_organization_audit/2026-10-08/`; it is not an application entry point. This confirms a defect in the exported class, not that every embedded GH definition necessarily has identical code.

### 3. C# crowding distance normalization needs correction before reuse

Same file, `CalculateCrowdingDistance`, lines 1234–1275. Objective gaps are accumulated before normalization; after sorting for the last objective, the code uses that ordering's endpoints to derive ranges for every objective and divides the accumulated distance repeatedly. This can give incorrect distances for multi-objective fronts. Full-front crowding values are also not consistently recomputed before all tournament selections.

This is a source-review finding, not a claim of successful Rhino execution. A repair should normalize each objective contribution using its own sorted range, then verify ordering on a deliberately conflicting objective front.

### 4. Export folders are historical development artifacts

- `00_NSGA-II/C__Script-1f522.cs` has an empty `RunScript` body and overlaps the main export's class name.
- `00_GeneticAlgorithm/C_-a9760.cs` contains undefined names, a value return inside a void method, and statements outside the intended method. It is an incomplete draft.
- `11/C__Script-1f522.cs` is a substantial alternate export, retained for provenance; it is not designated as a repaired version.
- `.csproj` files use absolute Rhino assembly paths and old language/framework settings. Some exported syntax requires newer language settings. Opening or building these projects unchanged is not a portability guarantee.
- Some C# exports use a legacy Windows Chinese encoding. Source bytes were retained rather than silently transcoded; select the appropriate encoding if an editor displays damaged characters.

These files are kept for review and recovery, with README status labels. There is no successful standalone C# application build claimed here.

### 5. Asset naming does not reliably identify different versions

The submitted `OptimizeGreenlandLayout_C#.gh` and `OptimizeGreenlandLayout_ResultGenerated.gh` are byte-identical. The old GitHub GH/model pair differs from the submitted pair. Preserve bundle associations and verify hashes before choosing a version. The final submission folder was selected by its provenance, not by assuming it is a corrected runtime implementation.

### 6. Python demonstration limitations

- Changing `n_cells` / `grid_cols` can break the grid reshape if dimensions do not divide evenly; the final visualization is hard-coded to 5 × 6.
- The printed cell-count denominator is hard-coded to 30.
- Importing the script triggers the full optimization.
- Continuous-vector duplicate elimination does not guarantee distinct thresholded binary layouts.
- Isolated-cell penalties do not enforce global connectivity.
- Region allocation follows contiguous flat indices, not physical site zones.
- UTF-8 console output is needed for the printed emoji grid.

No source changes were made to address these limitations in this organization pass.

## Validation evidence

### Python full default run

Environment: Windows, Python 3.10, NumPy 2.2.6, pymoo 0.6.1.6, local isolated `.venv`, `PYTHONIOENCODING=utf-8`.

Command: `python "NSGA-II based green space optimization.py"`

The original script completed 300 generations, exited with code 0 and returned 100 Pareto solution entries. Its selected compromise reported:

| Quantity | Observed value |
| --- | --- |
| Green cells | 21 / 30 |
| Coverage | 70.00% |
| Target ratio deviation | 0.2000 |
| Regional unevenness | 0.0000 |
| Isolated cell rate | 0.0000 |

These are results of one seeded synthetic demonstration under the recorded environment, not a real-site validation or proof that every returned layout is unique. The complete console log and dependency freeze are retained in the local audit folder.

### File integrity and publication checks

- Original source inventory retained locally with paths, sizes and SHA-256.
- Every source-map entry checked against both original and curated file.
- Existing published Python and binary assets retained without algorithm or asset edits.
- Markdown relative links checked against repository files.
- Publication set checked for file size limits, IDE/build artifacts and obvious credential patterns.
- Original `.vs` data, Copilot chat/session state, `bin` / `obj`, tutorial material and large draft assets excluded.

### Not validated

Rhino/Grasshopper geometry, embedded script execution, plugin dependencies, CAD reference binding, and historical site-performance claims were not run or revalidated. The historical Rhino assembly path recorded in the projects was unavailable in the current environment. No repaired C# result is claimed.
