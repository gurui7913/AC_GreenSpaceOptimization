using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.Linq;
using Rhino.Geometry.Intersect;


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_1f522 : GH_ScriptInstance
{
  #region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { /* Implementation hidden. */ }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { /* Implementation hidden. */ }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { /* Implementation hidden. */ }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
  #endregion

  #region Members
  /// <summary>Gets the current Rhino document.</summary>
  private readonly RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private readonly GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private readonly IGH_Component Component;
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private readonly int Iteration;
  #endregion
  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments,
  /// Output parameters as ref arguments. You don't have to assign output parameters,
  /// they will have a default value.
  /// </summary>
  #region Runscript
  private void RunScript(bool reset, List<Curve> greenBoundaries, List<Curve> buildingFootprints, List<Curve> paths, object populationSize, object maxGenerations, object cellSize, object targetArea, ref object BestSolutions, ref object BestGeometry, ref object GridData, ref object GreenStatistics)
  {
    // Check if reset was triggered (from false to true)
    bool resetTriggered = reset && !lastResetState;
    lastResetState = reset;

    // If reset was triggered, invalidate cache
    if (resetTriggered)
    {
      Print("Reset triggered. Recomputing optimization...");
      needsRecalculation = true;
    }

    // If we have cached results and don't need recalculation, use them
    if (!needsRecalculation && cachedParetoFront != null)
    {
      Print("Using cached results. Set 'Reset' to True to recompute.");
      BestSolutions = cachedParetoFront;
      BestGeometry = cachedBestGeometry;
      GreenStatistics = cachedGreenStats;
      GridData = cachedGridData;
      return;
    }

    // Parameter conversion
    int popSize = 100;
    int maxGen = 50;
    double cellSizeValue = 5.0;
    double targetAreaValue = 6476.0;

    // Try to convert input parameters
    if (populationSize != null) int.TryParse(populationSize.ToString(), out popSize);
    if (maxGenerations != null) int.TryParse(maxGenerations.ToString(), out maxGen);
    if (cellSize != null) double.TryParse(cellSize.ToString(), out cellSizeValue);
    if (targetArea != null) double.TryParse(targetArea.ToString(), out targetAreaValue);

    // Parameter validation
    if (greenBoundaries == null || greenBoundaries.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No green boundaries provided");
      return;
    }

    if (buildingFootprints == null || buildingFootprints.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No building footprints provided");
      return;
    }

    if (paths == null || paths.Count == 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No paths provided");
      return;
    }

    if (popSize <= 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Population size must be positive");
      return;
    }

    if (maxGen <= 0)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Maximum generations must be positive");
      return;
    }

    // Set algorithm parameters
    int rayCount = 30; // Number of rays per sample point
    double rayRadius = 100.0; // Ray radius

    Print("Starting green space layout optimization...");

    // 1. Preparation: Generate valid green surfaces and sample points
    List<Surface> greenSurfs;
    List<Point3d> samplePoints;
    PrepareGreenAreaAndSamplePoints(greenBoundaries, buildingFootprints, paths, out greenSurfs, out samplePoints);

    // Convert building curves to Brep objects
    List<Brep> buildings = new List<Brep>();
    foreach (Curve curve in buildingFootprints)
    {
      if (curve.IsClosed && curve.IsPlanar())
      {
        Brep[] breps = Brep.CreatePlanarBreps(curve, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
        if (breps != null && breps.Length > 0)
        {
          buildings.Add(breps[0]);
        }
      }
    }

    // 2. Generate uniform grid
    List<Rectangle3d> gridRects;
    List<Point3d> gridCenters;
    List<bool> gridStates;
    Dictionary<int, int> regionMapping;
    GenerateUniformGrid(greenSurfs, buildings, cellSizeValue, out gridRects, out gridCenters, out gridStates, out regionMapping);

    // 3. Run NSGA-II algorithm
    // Store generation statistics
    List<GenerationStatistics> allGenerationStats = new List<GenerationStatistics>();

    // Determine region count
    int regionCount = 0;
    if (regionMapping.Count > 0)
    {
      regionCount = regionMapping.Values.Max() + 1;
    }

    // Define fitness function
    Func<bool[], double[]> fitnessFunc = (genes) => {
      double greenViewScore = CalculateGreenViewFitness(genes, gridCenters, samplePoints, buildings, rayCount, rayRadius);
      double areaDiff = CalculateAreaFitness(genes, gridCenters, targetAreaValue, cellSizeValue);

      // Calculate distribution penalty
      double distributionPenalty = 0;
      if (regionCount > 0)
      {
        distributionPenalty = CalculateDistributionPenalty(genes, regionMapping, regionCount);
      }

      return new double[] {
        greenViewScore,                // First objective: maximize green view (higher is better)
        areaDiff + distributionPenalty // Second objective: minimize area difference and distribution unevenness (lower is better)
        };
      };

    // Initialize NSGA-II algorithm
    NSGA2Optimizer optimizer = new NSGA2Optimizer(
      popSize,
      gridCenters.Count,
      fitnessFunc,
      new bool[] { true, false }  First objective is maximization, second is minimization
    );

    // Run genetic algorithm
    for (int i = 0; i < maxGen; i++)
    {
      optimizer.Evolve();

      // Get best individuals and statistics for current generation
      Individual bestGreenView = optimizer.GetBestIndividual(0);
      Individual bestArea = optimizer.GetBestIndividual(1);
      List<Individual> currentParetoFront = optimizer.GetParetoFront();

      // Create statistics data
      GenerationStatistics genStats = new GenerationStatistics
        {
          Generation = i + 1,
          BestGreenViewScore = bestGreenView.FitnessValues[0],
          BestAreaDifference = bestArea.FitnessValues[1],
          ParetoFrontSize = currentParetoFront.Count,
          AverageGreenViewScore = currentParetoFront.Average(ind => ind.FitnessValues[0]),
          AverageAreaDifference = currentParetoFront.Average(ind => ind.FitnessValues[1]),
          BestCompromiseSolution = SelectCompromiseSolution(currentParetoFront)
          };

      allGenerationStats.Add(genStats);

      // Output information at intervals
      if (i % 5 == 0 || i == maxGen - 1)
      {
        Print("Generation " + (i + 1) + "/" + maxGen);
        Print("  Best GreenView: " + bestGreenView.FitnessValues[0].ToString("F4"));
        Print("  Best Area Diff: " + bestArea.FitnessValues[1].ToString("F4"));
        Print("  Pareto Front Size: " + currentParetoFront.Count);
      }
    }

    // Get final Pareto front
    List<Individual> paretoFront = optimizer.GetParetoFront();
    Print("Optimization complete, found " + paretoFront.Count + " non-dominated solutions");

    // Select a compromise solution from the Pareto front
    Individual selectedSolution = SelectCompromiseSolution(paretoFront);
    Print("Selected best solution: Green View = " + selectedSolution.FitnessValues[0].ToString("F4") + ", Area Difference = " + selectedSolution.FitnessValues[1].ToString("F4"));

    // Convert best solution to geometry
    List<Brep> bestGeometry = ConvertSolutionToGeometry(selectedSolution.Genes, gridCenters, cellSizeValue);

    // Calculate green area statistics
    GreenAreaStatistics greenStats = CalculateGreenAreaStatistics(selectedSolution.Genes, gridCenters, cellSizeValue, regionMapping);
    Print("Green Area Statistics:");
    Print("  Total Green Cells: " + greenStats.TotalGreenCells);
    Print("  Total Area: " + greenStats.TotalArea.ToString("F2") + " square meters");
    Print("  Region Distribution: " + string.Join(", ", greenStats.RegionDistribution.Select(kv => "Region " + kv.Key + ": " + kv.Value)));

    // Output results
    BestSolutions = paretoFront;
    BestGeometry = bestGeometry;
    GreenStatistics = greenStats; // Output green area statistics

    // Output grid data for visualization
    DataTree<object> gridData = new DataTree<object>();
    gridData.AddRange(gridRects.Cast<object>().ToList(), new GH_Path(0));
    gridData.AddRange(gridCenters.Cast<object>().ToList(), new GH_Path(1));
    gridData.AddRange(selectedSolution.Genes.Cast<object>().ToList(), new GH_Path(2));
    gridData.AddRange(allGenerationStats.Cast<object>().ToList(), new GH_Path(3)); // Add all generation statistics
    GridData = gridData;

    // Cache results for next time
    cachedParetoFront = paretoFront;
    cachedBestGeometry = bestGeometry;
    cachedGreenStats = greenStats;
    cachedGridData = gridData;
    needsRecalculation = false;
  }
  #endregion
  #region Additional
  /// <summary>
  /// Step 1: Generate valid green surfaces and sample points
  /// </summary>
  void PrepareGreenAreaAndSamplePoints(
    List<Curve> greenBoundaries,
    List<Curve> buildingFootprints,
    List<Curve> paths,
    out List<Surface> greenSurfs,
    out List<Point3d> samplePts)
  {
    greenSurfs = new List<Surface>();
    samplePts = new List<Point3d>();
    double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

    // Step 1: Generate valid green surfaces (green area - buildings)
    foreach (Curve boundary in greenBoundaries)
    {
      if (!boundary.IsClosed || !boundary.IsPlanar())
      {
        Print("Warning: Skipping non-closed or non-planar boundary curve");
        continue;
      }

      Brep[] baseBreps = Brep.CreatePlanarBreps(boundary, tolerance);
      if (baseBreps == null || baseBreps.Length == 0) continue;
      Brep baseBrep = baseBreps[0];

      List<Brep> holes = new List<Brep>();
      foreach (Curve building in buildingFootprints)
      {
        if (!building.IsClosed || !building.IsPlanar()) continue;

        // Improvement: More reliable building-green boundary intersection detection
        Curve[] intersections = Curve.CreateBooleanIntersection(boundary, building, tolerance);
        if (intersections != null && intersections.Length > 0)
        {
          Brep[] bldBreps = Brep.CreatePlanarBreps(building, tolerance);
          if (bldBreps != null && bldBreps.Length > 0) holes.Add(bldBreps[0]);
        }
      }

      Brep[] trimmed = Brep.CreateBooleanDifference(new Brep[] { baseBrep }, holes, tolerance);
      if (trimmed != null && trimmed.Length > 0)
      {
        foreach (Brep b in trimmed)
        {
          greenSurfs.AddRange(b.Surfaces);
        }
      }
    }

    // Step 2: Path sample points (every 5m)
    double interval = 5.0;
    foreach (Curve path in paths)
    {
      double len = path.GetLength();
      int count = (int) Math.Floor(len / interval);
      for (int i = 0; i <= count; i++)
      {
        double t;
        if (path.LengthParameter(i * interval, out t))
        {
          samplePts.Add(path.PointAt(t));
        }
      }
    }

    Print("Generated " + greenSurfs.Count + " green surfaces and " + samplePts.Count + " sample points");
  }

  /// <summary>
  /// Step 2: Generate uniform grid
  /// </summary>
  void GenerateUniformGrid(
    List<Surface> greenSurfs,
    List<Brep> buildings,
    double cellSize,
    out List<Rectangle3d> gridRects,
    out List<Point3d> gridCenters,
    out List<bool> gridStates,
    out Dictionary<int, int> regionMapping)
  {
    gridRects = new List<Rectangle3d>();
    gridCenters = new List<Point3d>();
    gridStates = new List<bool>();
    regionMapping = new Dictionary<int, int>(); // Maps grid cells to region IDs
    double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

    // Improvement 1: Get all green area outer boundaries for region identification
    List<Curve> regionBoundaries = new List<Curve>();
    List<Brep> greenBreps = new List<Brep>();

    foreach (Surface srf in greenSurfs)
    {
      Brep brep = Brep.CreateFromSurface(srf);
      if (brep != null)
      {
        greenBreps.Add(brep);

        // Extract outer boundary for region identification
        Curve[] edges = brep.DuplicateEdgeCurves(true);
        if (edges != null && edges.Length > 0)
        {
          Curve[] joined = Curve.JoinCurves(edges);
          if (joined != null && joined.Length > 0 && joined[0].IsClosed)
          {
            regionBoundaries.Add(joined[0]);
          }
        }
      }
    }

    // Calculate overall bounding box for all green areas
    BoundingBox overallBBox = BoundingBox.Empty;
    foreach (Surface srf in greenSurfs)
    {
      overallBBox.Union(srf.GetBoundingBox(true));
    }

    // Improvement 2: Use overall bounding box to create more uniform grid
    Plane gridPlane = Plane.WorldXY;
    double minX = overallBBox.Min.X;
    double minY = overallBBox.Min.Y;
    double maxX = overallBBox.Max.X;
    double maxY = overallBBox.Max.Y;

    int xCount = (int) Math.Ceiling((maxX - minX) / cellSize);
    int yCount = (int) Math.Ceiling((maxY - minY) / cellSize);

    Print("Creating " + xCount + "x" + yCount + " grid, total " + (xCount * yCount) + " cells");

    int cellId = 0;
    for (int i = 0; i < xCount; i++)
    {
      for (int j = 0; j < yCount; j++)
      {
        double x0 = minX + i * cellSize;
        double y0 = minY + j * cellSize;

        // Create rectangle corner points
        Point3d pt1 = new Point3d(x0, y0, 0);
        Point3d pt2 = new Point3d(x0 + cellSize, y0, 0);
        Point3d pt3 = new Point3d(x0 + cellSize, y0 + cellSize, 0);
        Point3d pt4 = new Point3d(x0, y0 + cellSize, 0);

        // Calculate center point
        Point3d center = new Point3d(x0 + cellSize / 2, y0 + cellSize / 2, 0);

        // Check if point is within any green surface
        bool insideGreenArea = false;
        int regionId = -1;

        // First check which region the point is in
        for (int r = 0; r < regionBoundaries.Count; r++)
        {
          if (regionBoundaries[r].Contains(center, gridPlane, tolerance) == PointContainment.Inside)
          {
            regionId = r;
            insideGreenArea = true;
            break;
          }
        }

        // If not in a region, try using closest point check
        if (!insideGreenArea)
        {
          foreach (Surface srf in greenSurfs)
          {
            Point3d closestPt;
            double u, v;
            if (srf.ClosestPoint(center, out u, out v))
            {
              // Getting 3D points with UV parameters
              closestPt = srf.PointAt(u, v);

              if (center.DistanceTo(closestPt) < tolerance)
              {
                insideGreenArea = true;
                break;
              }
            }
          }
        }

        // Check if point is inside a building
        bool insideBuilding = false;
        if (insideGreenArea)
        {
          foreach (Brep bld in buildings)
          {
            if (bld.IsPointInside(center, tolerance, true))
            {
              insideBuilding = true;
              break;
            }

            // Check if point is very close to a building
            Point3d closestPt;
            double u, v;
            ComponentIndex componentIndex = ComponentIndex.Unset;
            Vector3d normal;
            if (bld.ClosestPoint(center,
              out closestPt,
              out componentIndex,
              out u,
              out v,
              tolerance, // ← Maximum distance to check
              out normal))  // ← Normal vector
            {
              if (center.DistanceTo(closestPt) < tolerance)
              {
                insideBuilding = true;
                break;
              }
            }
          }
        }

        // If not inside building, and inside green area
        if (insideGreenArea && !insideBuilding)
        {
          try
          {
            // Create rectangle vectors
            Vector3d xDir = new Vector3d(cellSize, 0, 0);
            Vector3d yDir = new Vector3d(0, cellSize, 0);

            // Create rectangle
            Rectangle3d rect = new Rectangle3d(gridPlane, pt1, pt3);

            gridRects.Add(rect);
            gridCenters.Add(center);
            gridStates.Add(true); // Initially set to true, genetic algorithm will operate on these

            // Record grid cell's region ID
            if (regionId >= 0)
            {
              regionMapping[cellId] = regionId;
            }

            cellId++;
          }
          catch (Exception ex)
          {
            Print("Error creating rectangle: " + ex.Message);
          }
        }
      }
    }

    Print("Generated " + gridRects.Count + " valid grid cells");
  }

  /// <summary>
  /// Step 3: Calculate green view fitness
  /// </summary>
  double CalculateGreenViewFitness(
    bool[] genes,
    List<Point3d> gridCenters,
    List<Point3d> samplePoints,
    List<Brep> buildings,
    int rayCount = 30,
    double radius = 100.0)
  {
    // Improvement: Use more precise green geometry representation
    List<Brep> greenPatches = new List<Brep>();
    double gridSize = 5.0; // Assume grid size of 5 meters, could also be passed as a parameter
    double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

    for (int i = 0; i < genes.Length; i++)
    {
      if (!genes[i]) continue;

      Point3d center = gridCenters[i];
      double half = gridSize / 2.0;

      // Build rectangle corners
      Point3d pt0 = new Point3d(center.X - half, center.Y - half, center.Z);
      Point3d pt1 = new Point3d(center.X + half, center.Y - half, center.Z);
      Point3d pt2 = new Point3d(center.X + half, center.Y + half, center.Z);
      Point3d pt3 = new Point3d(center.X - half, center.Y + half, center.Z);

      // Construct Polyline
      Polyline pl = new Polyline(new List<Point3d>() { pt0, pt1, pt2, pt3, pt0 });
      if (!pl.IsValid || !pl.IsClosed) continue;

      // Convert to curve and generate planar Brep
      Curve curve = pl.ToNurbsCurve();
      Brep[] patches = Brep.CreatePlanarBreps(curve, tolerance);
      if (patches != null && patches.Length > 0)
        greenPatches.Add(patches[0]);
    }

    // If no green areas, return 0
    if (greenPatches.Count == 0)
      return 0;

    double totalHits = 0;
    double totalRays = samplePoints.Count * rayCount;

    // Generate rays from each sample point and check intersections with green areas
    foreach (Point3d origin in samplePoints)
    {
      List<Line> rays = GenerateRays(origin, rayCount, radius);

      foreach (Line ray in rays)
      {
        bool hitGreen = false;

        // Check if ray intersects with any green area
        foreach (Brep greenPatch in greenPatches)
        {
          Curve[] overlapCurves = null;
          Point3d[] intersectionPoints = null;
          Intersection.CurveBrep(new LineCurve(ray), greenPatch, tolerance, out overlapCurves, out intersectionPoints);
          if ((intersectionPoints != null && intersectionPoints.Length > 0) ||
            (overlapCurves != null && overlapCurves.Length > 0))
          {
            hitGreen = true;
            break;
          }
        }

        if (!hitGreen) continue;

        // Check if blocked by buildings
        bool blocked = false;
        foreach (Brep building in buildings)
        {
          Curve[] bldOverlapCurves = null;
          Point3d[] bldIntersectionPoints = null;
          Intersection.CurveBrep(new LineCurve(ray), building, tolerance, out bldOverlapCurves, out bldIntersectionPoints);

          if ((bldIntersectionPoints != null && bldIntersectionPoints.Length > 0) ||
            (bldOverlapCurves != null && bldOverlapCurves.Length > 0))
          {
            // Find nearest intersection with green areas and buildings
            double distToGreen = double.MaxValue;
            foreach (Brep g in greenPatches)
            {
              Curve[] gOverlapCurves = null;
              Point3d[] gIntersectionPoints = null;
              Intersection.CurveBrep(new LineCurve(ray), g, tolerance, out gOverlapCurves, out gIntersectionPoints);

              if (gIntersectionPoints != null && gIntersectionPoints.Length > 0)
              {
                foreach (Point3d gInt in gIntersectionPoints)
                {
                  double dist = origin.DistanceTo(gInt);
                  if (dist < distToGreen)
                    distToGreen = dist;
                }
              }
            }

            // Check if building intersection blocks the green view
            if (bldIntersectionPoints != null)
            {
              foreach (Point3d bInt in bldIntersectionPoints)
              {
                double distToBld = origin.DistanceTo(bInt);
                if (distToBld < distToGreen)
                {
                  blocked = true;
                  break;
                }
              }
            }

            if (blocked) break;
          }
        }

        if (!blocked) totalHits++;
      }
    }

    // Normalize green view score
    return totalHits / totalRays;
  }

  /// <summary>
  /// Generate uniformly distributed rays from a point
  /// </summary>
  List<Line> GenerateRays(Point3d origin, int count, double radius)
  {
    List<Line> rays = new List<Line>();
    double angleStep = 2 * Math.PI / count;

    for (int i = 0; i < count; i++)
    {
      double angle = i * angleStep;
      Vector3d dir = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
      Point3d endpoint = origin + dir * radius;
      rays.Add(new Line(origin, endpoint));
    }

    return rays;
  }

  /// <summary>
  /// Step 4: Calculate area fitness
  /// </summary>
  double CalculateAreaFitness(bool[] genes, List<Point3d> gridCenters, double targetArea, double cellSize)
  {
    int activeCount = 0;
    for (int i = 0; i < genes.Length; i++)
    {
      if (genes[i]) activeCount++;
    }
    double actualArea = activeCount * (cellSize * cellSize);

    // Return difference from target area (lower is better)
    return Math.Abs(actualArea - targetArea);
  }

  /// <summary>
  /// Step 5: Region distribution constraint
  /// </summary>
  double CalculateDistributionPenalty(bool[] genes, Dictionary<int, int> regionMapping, int regionCount)
  {
    // Calculate grid cell count for each region
    Dictionary<int, int> regionCellCount = new Dictionary<int, int>();
    Dictionary<int, int> regionActiveCount = new Dictionary<int, int>();

    // Initialize counters
    for (int r = 0; r < regionCount; r++)
    {
      regionCellCount[r] = 0;
      regionActiveCount[r] = 0;
    }

    // Count total grids and active grids for each region
    for (int i = 0; i < genes.Length; i++)
    {
      if (regionMapping.ContainsKey(i))
      {
        int regionId = regionMapping[i];
        regionCellCount[regionId]++;

        if (genes[i])
          regionActiveCount[regionId]++;
      }
    }

    // Calculate proportion for each region
    double penalty = 0;
    for (int r = 0; r < regionCount; r++)
    {
      if (regionCellCount[r] > 0)
      {
        double ratio = (double) regionActiveCount[r] / regionCellCount[r];

        // If region has no green areas, add high penalty
        if (ratio == 0)
          penalty += 1.0;
          // If region has low green area ratio, add appropriate penalty
        else if (ratio < 0.1)
          penalty += (0.1 - ratio) * 5.0;
      }
    }

    return penalty;
  }

  /// <summary>
  /// Select a compromise solution from the Pareto front
  /// </summary>
  Individual SelectCompromiseSolution(List<Individual> paretoFront)
  {
    // If only one solution, return it directly
    if (paretoFront.Count == 1)
      return paretoFront[0];

    // Calculate min and max values for each objective
    double minObj1 = double.MaxValue;
    double maxObj1 = double.MinValue;
    double minObj2 = double.MaxValue;
    double maxObj2 = double.MinValue;

    foreach (Individual ind in paretoFront)
    {
      if (ind.FitnessValues[0] < minObj1) minObj1 = ind.FitnessValues[0];
      if (ind.FitnessValues[0] > maxObj1) maxObj1 = ind.FitnessValues[0];
      if (ind.FitnessValues[1] < minObj2) minObj2 = ind.FitnessValues[1];
      if (ind.FitnessValues[1] > maxObj2) maxObj2 = ind.FitnessValues[1];
    }

    // Calculate normalized Euclidean distance to ideal point (1, 0)
    // Note: Objective 1 is maximization, Objective 2 is minimization
    Individual bestSolution = null;
    double minDistance = double.MaxValue;

    foreach (Individual ind in paretoFront)
    {
      // Normalize fitness values
      double normObj1 = maxObj1 > minObj1 ? (ind.FitnessValues[0] - minObj1) / (maxObj1 - minObj1) : 0;
      double normObj2 = maxObj2 > minObj2 ? (ind.FitnessValues[1] - minObj2) / (maxObj2 - minObj2) : 0;

      // Calculate distance to ideal point (1, 0)
      double distance = Math.Sqrt(Math.Pow(1 - normObj1, 2) + Math.Pow(normObj2, 2));

      if (distance < minDistance)
      {
        minDistance = distance;
        bestSolution = ind;
      }
    }

    return bestSolution;
  }

  /// <summary>
  /// Convert solution to geometry
  /// </summary>
  List<Brep> ConvertSolutionToGeometry(bool[] genes, List<Point3d> gridCenters, double cellSize)
  {
    List<Brep> geometry = new List<Brep>();
    double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

    for (int i = 0; i < genes.Length; i++)
    {
      if (!genes[i]) continue;

      Point3d center = gridCenters[i];
      double half = cellSize / 2.0;

      // Build rectangle corners
      Point3d pt0 = new Point3d(center.X - half, center.Y - half, center.Z);
      Point3d pt1 = new Point3d(center.X + half, center.Y - half, center.Z);
      Point3d pt2 = new Point3d(center.X + half, center.Y + half, center.Z);
      Point3d pt3 = new Point3d(center.X - half, center.Y + half, center.Z);

      // Construct polygon
      Polyline pl = new Polyline(new List<Point3d>() { pt0, pt1, pt2, pt3, pt0 });
      if (!pl.IsValid || !pl.IsClosed) continue;

      // Convert to curve and generate planar Brep
      Curve curve = pl.ToNurbsCurve();
      Brep[] patches = Brep.CreatePlanarBreps(curve, tolerance);
      if (patches != null && patches.Length > 0)
        geometry.Add(patches[0]);
    }

    return geometry;
  }

  /// <summary>
  /// Calculate green area statistics
  /// </summary>
  GreenAreaStatistics CalculateGreenAreaStatistics(bool[] genes, List<Point3d> gridCenters, double cellSize, Dictionary<int, int> regionMapping)
  {
    GreenAreaStatistics stats = new GreenAreaStatistics();

    // Calculate total green grid cells
    int totalCells = 0;
    for (int i = 0; i < genes.Length; i++)
    {
      if (genes[i]) totalCells++;
    }
    stats.TotalGreenCells = totalCells;

    // Calculate total area
    stats.TotalArea = stats.TotalGreenCells * (cellSize * cellSize);

    // Calculate region distribution
    Dictionary<int, int> regionCounts = new Dictionary<int, int>();

    // Count green cells in each region
    for (int i = 0; i < genes.Length; i++)
    {
      if (!genes[i]) continue; // Skip if not green

      if (regionMapping.ContainsKey(i))
      {
        int regionId = regionMapping[i];
        if (!regionCounts.ContainsKey(regionId))
          regionCounts[regionId] = 0;

        regionCounts[regionId]++;
      }
    }

    stats.RegionDistribution = regionCounts;

    // Calculate green area percentage for each region
    Dictionary<int, double> regionAreaPercentage = new Dictionary<int, double>();

    foreach (var kvp in regionCounts)
    {
      double regionArea = kvp.Value * (cellSize * cellSize);
      double percentage = stats.TotalArea > 0 ? (regionArea / stats.TotalArea) * 100 : 0;
      regionAreaPercentage[kvp.Key] = percentage;
    }

    stats.RegionAreaPercentage = regionAreaPercentage;

    return stats;
  }

  /// <summary>
  /// Individual classes in NSGA-II multi-objective optimization algorithm
  /// </summary>
  public class Individual
  {
    private bool[] genes;
    private double[] fitnessValues;

    public bool[] Genes
    {
      get { return genes; }
      private set { genes = value; }
    }

    public double[] FitnessValues
    {
      get { return fitnessValues; }
      set { fitnessValues = value; }
    }

    public int Rank { get; set; }
    public double CrowdingDistance { get; set; }

    public Individual(bool[] genes)
    {
      Genes = (bool[]) genes.Clone();
      FitnessValues = new double[2]; // Two optimization objectives
    }

    public Individual Clone()
    {
      Individual clone = new Individual(Genes);
      clone.FitnessValues = (double[]) FitnessValues.Clone();
      clone.Rank = Rank;
      clone.CrowdingDistance = CrowdingDistance;
      return clone;
    }
  }

  /// <summary>
  /// NSGA-II Multi-Objective Optimization Algorithm Implementation
  /// </summary>
  public class NSGA2Optimizer
  {
    private List<Individual> population;
    private int populationSize;
    private int geneLength;
    private Random random = new Random();
    private Func<bool[], double[]> fitnessFunction;
    private bool[] objectiveDirections; // true: maximize, false: minimize

    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="popSize">population size</param>
    /// <param name="geneLen">gene length</param>
    /// <param name="fitnessFunc">a fitness function that returns fitness values for multiple targets</param>
    /// <param name="maximize">the question of whether the objectives are maximized</param>
    public NSGA2Optimizer(int popSize, int geneLen, Func<bool[], double[]> fitnessFunc, bool[] maximize)
    {
      populationSize = popSize;
      geneLength = geneLen;
      fitnessFunction = fitnessFunc;
      objectiveDirections = maximize;

      // Initialize the population
      InitializePopulation();
    }

    /// <summary>
    /// Initializing populations
    /// </summary>
    private void InitializePopulation()
    {
      population = new List<Individual>();

      for (int i = 0; i < populationSize; i++)
      {
        bool[] genes = new bool[geneLength];

        // Randomly initialize genes
        for (int j = 0; j < geneLength; j++)
        {
          genes[j] = random.NextDouble() < 0.5;
        }

        Individual individual = new Individual(genes);
        EvaluateFitness(individual);
        population.Add(individual);
      }

      // Non-dominated sorting of initial populations
      FastNonDominatedSort(population);
    }

    /// <summary>
    /// Assessing Individual Adaptation
    /// </summary>
    private void EvaluateFitness(Individual individual)
    {
      individual.FitnessValues = fitnessFunction(individual.Genes);
    }

    /// <summary>
    /// Implementing Generation Evolution
    /// </summary>
    public void Evolve()
    {
      // Creating children
      List<Individual> offspring = CreateOffspring();

      // Merge parent and child
      List<Individual> combinedPopulation = new List<Individual>();
      combinedPopulation.AddRange(population);
      combinedPopulation.AddRange(offspring);

      // Non-dominated order
      List<List<Individual>> fronts = FastNonDominatedSort(combinedPopulation);

      // Select the next generation
      List<Individual> nextGeneration = new List<Individual>();
      int frontIndex = 0;

      // Add the entire frontier until it exceeds the population size
      while (nextGeneration.Count + fronts[frontIndex].Count <= populationSize)
      {
        nextGeneration.AddRange(fronts[frontIndex]);
        frontIndex++;

        if (frontIndex >= fronts.Count)
          break;
      }

      // If more individuals are needed, choose based on congestion distance
      if (nextGeneration.Count < populationSize && frontIndex < fronts.Count)
      {
        // Calculate the congestion distance of the last frontier
        CalculateCrowdingDistance(fronts[frontIndex]);

        // Sort by congestion distance
        fronts[frontIndex].Sort((a, b) => b.CrowdingDistance.CompareTo(a.CrowdingDistance));

        // Add individuals with the largest crowding distance until the population size is satisfied
        int remainingSlots = populationSize - nextGeneration.Count;
        nextGeneration.AddRange(fronts[frontIndex].Take(remainingSlots));
      }

      // Renewal of stocks
      population = nextGeneration;
    }

    /// <summary>
    /// Creating children
    /// </summary>
    private List<Individual> CreateOffspring()
    {
      List<Individual> offspring = new List<Individual>();

      // Generating offspring using tournament selection, crossover and mutation
      while (offspring.Count < populationSize)
      {
        // Select Parent
        Individual parent1 = TournamentSelection();
        Individual parent2 = TournamentSelection();

        // Crossover
        if (random.NextDouble() < 0.9) // 90% crossover probability
        {
          var children = Crossover(parent1, parent2);

          // Mutation
          Mutate(children.Item1);
          Mutate(children.Item2);

          // Assessing fitness
          EvaluateFitness(children.Item1);
          EvaluateFitness(children.Item2);

          offspring.Add(children.Item1);
          if (offspring.Count < populationSize)
            offspring.Add(children.Item2);
        }
        else
        {
          // No crossover, direct copy of parent
          offspring.Add(parent1.Clone());
          if (offspring.Count < populationSize)
            offspring.Add(parent2.Clone());
        }
      }

      return offspring;
    }

    /// <summary>
    /// Tournament Selection
    /// </summary>
    private Individual TournamentSelection()
    {
      int tournamentSize = 2; // Tournament Size
      Individual best = null;

      for (int i = 0; i < tournamentSize; i++)
      {
        int index = random.Next(population.Count);
        Individual candidate = population[index];

        if (best == null || candidate.Rank < best.Rank ||
          (candidate.Rank == best.Rank && candidate.CrowdingDistance > best.CrowdingDistance))
        {
          best = candidate;
        }
      }

      return best;
    }

    /// <summary>
    /// Crossover operation
    /// </summary>
    private Tuple<Individual, Individual> Crossover(Individual parent1, Individual parent2)
    {
      bool[] child1Genes = new bool[geneLength];
      bool[] child2Genes = new bool[geneLength];

      // Single-point crossover
      int crossoverPoint = random.Next(1, geneLength);

      for (int i = 0; i < geneLength; i++)
      {
        if (i < crossoverPoint)
        {
          child1Genes[i] = parent1.Genes[i];
          child2Genes[i] = parent2.Genes[i];
        }
        else
        {
          child1Genes[i] = parent2.Genes[i];
          child2Genes[i] = parent1.Genes[i];
        }
      }

      return new Tuple<Individual, Individual>(
        new Individual(child1Genes),
        new Individual(child2Genes)
        );
    }

    /// <summary>
    /// Mutation operation
    /// </summary>
    private void Mutate(Individual individual)
    {
      double mutationRate = 0.02; // 2% mutation probability

      for (int i = 0; i < geneLength; i++)
      {
        if (random.NextDouble() < mutationRate)
        {
          individual.Genes[i] = !individual.Genes[i];
        }
      }
    }

    /// <summary>
    /// Fast Non-Dominated Sorting
    /// </summary>
    private List<List<Individual>> FastNonDominatedSort(List<Individual> pop)
    {
      // store the Pareto front
      List<List<Individual>> fronts = new List<List<Individual>>();

      // first front
      List<Individual> front0 = new List<Individual>();

      // initialize dominated and domination count
      Dictionary<Individual, List<Individual>> dominated = new Dictionary<Individual, List<Individual>>();
      Dictionary<Individual, int> dominationCount = new Dictionary<Individual, int>();

      foreach (Individual p in pop)
      {
        dominated[p] = new List<Individual>();
        dominationCount[p] = 0;

        foreach (Individual q in pop)
        {
          if (Dominates(p, q))
          {
            dominated[p].Add(q);
          }
          else if (Dominates(q, p))
          {
            dominationCount[p]++;
          }
        }

        // If p is not dominated by any other individual, add it to the first front
        if (dominationCount[p] == 0)
        {
          p.Rank = 0;
          front0.Add(p);
        }
      }

      fronts.Add(front0); // Add the first front to the list of fronts

      // Generate subsequent fronts
      int i = 0;
      while (fronts[i].Count > 0)
      {
        List<Individual> nextFront = new List<Individual>();

        foreach (Individual p in fronts[i])
        {
          foreach (Individual q in dominated[p])
          {
            dominationCount[q]--;

            if (dominationCount[q] == 0)
            {
              q.Rank = i + 1;
              nextFront.Add(q);
            }
          }
        }

        i++;
        if (nextFront.Count > 0)
        {
          fronts.Add(nextFront);
        }
      }

      return fronts;
    }

    /// <summary>
    /// Determine if individual p dominates individual q
    /// </summary>
    private bool Dominates(Individual p, Individual q)
    {
      bool better = false;

      for (int i = 0; i < p.FitnessValues.Length; i++)
      {
        // For maximization goals
        if (objectiveDirections[i])
        {
          if (p.FitnessValues[i] < q.FitnessValues[i]) return false;
          if (p.FitnessValues[i] > q.FitnessValues[i]) better = true;
        }
          // For minimization goals
        else
        {
          if (p.FitnessValues[i] > q.FitnessValues[i]) return false;
          if (p.FitnessValues[i] < q.FitnessValues[i]) better = true;
        }
      }

      return better; // If p is better on at least one goal and not worse than q on the others, then p dominates q
    }

    /// <summary>
    /// Calculate congestion distance
    /// </summary>
    private void CalculateCrowdingDistance(List<Individual> front)
    {
      int frontSize = front.Count;

      // Initialize congestion distance to 0
      foreach (Individual ind in front)
      {
        ind.CrowdingDistance = 0;
      }

      // Rank each target and compute congestion distance
      for (int m = 0; m < front[0].FitnessValues.Length; m++)
      {
        // Sort by target m
        int objectiveIndex = m;
        front.Sort((a, b) => a.FitnessValues[objectiveIndex].CompareTo(b.FitnessValues[objectiveIndex]));

        // The congestion distance of the boundary points is set to infinity
        front[0].CrowdingDistance = double.PositiveInfinity;
        front[frontSize - 1].CrowdingDistance = double.PositiveInfinity;

        // Calculate the congestion distance at the midpoint
        for (int i = 1; i < frontSize - 1; i++)
        {
          front[i].CrowdingDistance += (front[i + 1].FitnessValues[m] - front[i - 1].FitnessValues[m]);
        }
      }

      // Normalized congestion distance
      for (int m = 0; m < front[0].FitnessValues.Length; m++)
      {
        double min = front[0].FitnessValues[m];
        double max = front[frontSize - 1].FitnessValues[m];

        if (max > min)
        {
          for (int i = 1; i < frontSize - 1; i++)
          {
            front[i].CrowdingDistance /= (max - min);
          }
        }
      }
    }

    /// <summary>
    /// Getting the best individuals (based on specific goals)
    /// </summary>
    public Individual GetBestIndividual(int objectiveIndex)
    {
      if (objectiveDirections[objectiveIndex]) // Maximization
      {
        return population.OrderByDescending(ind => ind.FitnessValues[objectiveIndex]).First();
      }
      else // Minimization
      {
        return population.OrderBy(ind => ind.FitnessValues[objectiveIndex]).First();
      }
    }

    /// <summary>
    /// Taking the Pareto Frontier (First Frontier)
    /// </summary>
    public List<Individual> GetParetoFront()
    {
      return FastNonDominatedSort(population)[0];
    }
  }

  /// <summary>
  /// Generation statistics category
  /// </summary>
  public class GenerationStatistics
  {
    private int generation;
    private double bestGreenViewScore;
    private double bestAreaDifference;
    private int paretoFrontSize;
    private double averageGreenViewScore;
    private double averageAreaDifference;
    private Individual bestCompromiseSolution;

    public int Generation
    {
      get { return generation; }
      set { generation = value; }
    }

    public double BestGreenViewScore
    {
      get { return bestGreenViewScore; }
      set { bestGreenViewScore = value; }
    }

    public double BestAreaDifference
    {
      get { return bestAreaDifference; }
      set { bestAreaDifference = value; }
    }

    public int ParetoFrontSize
    {
      get { return paretoFrontSize; }
      set { paretoFrontSize = value; }
    }

    public double AverageGreenViewScore
    {
      get { return averageGreenViewScore; }
      set { averageGreenViewScore = value; }
    }

    public double AverageAreaDifference
    {
      get { return averageAreaDifference; }
      set { averageAreaDifference = value; }
    }

    public Individual BestCompromiseSolution
    {
      get { return bestCompromiseSolution; }
      set { bestCompromiseSolution = value; }
    }

    public override string ToString()
    {
      return "Generation " + Generation + ": Best GreenView=" + BestGreenViewScore.ToString("F4") + ", Best Area Diff=" + BestAreaDifference.ToString("F4") + ", Pareto Front Size=" + ParetoFrontSize;
    }
  }

  /// <summary>
  /// Greenfield statistics category
  /// </summary>
  public class GreenAreaStatistics
  {
    private int totalGreenCells;
    private double totalArea;
    private Dictionary<int, int> regionDistribution;
    private Dictionary<int, double> regionAreaPercentage;

    public GreenAreaStatistics()
    {
      regionDistribution = new Dictionary<int, int>();
      regionAreaPercentage = new Dictionary<int, double>();
    }

    public int TotalGreenCells
    {
      get { return totalGreenCells; }
      set { totalGreenCells = value; }
    }

    public double TotalArea
    {
      get { return totalArea; }
      set { totalArea = value; }
    }

    public Dictionary<int, int> RegionDistribution
    {
      get { return regionDistribution; }
      set { regionDistribution = value; }
    }

    public Dictionary<int, double> RegionAreaPercentage
    {
      get { return regionAreaPercentage; }
      set { regionAreaPercentage = value; }
    }

    public override string ToString()
    {
      return "Greenarea statistics: " + TotalGreenCells + "units, total area=" + TotalArea.ToString("F2") + "square meters, distributed in" + RegionDistribution.Count + "region";
    }
  }
}
  #endregion
}