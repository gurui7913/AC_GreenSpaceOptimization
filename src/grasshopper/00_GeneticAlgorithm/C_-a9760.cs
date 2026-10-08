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


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_a9760 : GH_ScriptInstance
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
  private void RunScript(int populationSize, int maxGenerations, List<Point3d> samplePoints, List<Point3d> gridCenters, List<Brep> buildings, object areaFunc, ref object A, ref object bestIndividuals)
  {

    double totalHits = 0;
    var greenPatches = new List<Sphere>();
    for (int i = 0; i < genes.Length; i++)
      if (genes[i]) greenPatches.Add(new Sphere(gridCenters[i], 1.5));

    foreach (var pt in samplePoints)
    {
      foreach (var ray in GenerateRays(pt, rayCount, radius))
      {
        var line = new Line(ray.from, ray.to);
        bool hitGreen = greenPatches.Any(s => Intersection.LineSphere(line, s, out _, out _));
        if (!hitGreen) continue;

        bool blocked = buildings.Any(b => Intersection.LineBrep(line, b, 0.01, out _));
        if (!blocked) totalHits++;
      }
    }
    return totalHits;
  }

  private static List<(Point3d from, Point3d to)> GenerateRays(Point3d origin, int count, double radius)
  {
    var list = new List<(Point3d, Point3d) > ();
    double angleStep = 2 * Math.PI / count;
    for (int i = 0; i < count; i++)
    {
      double angle = i * angleStep;
      Vector3d dir = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
      list.Add((origin, origin + dir * radius));
    }
    return list;
  }
    if (populationSize <= 0) {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Population size must be positive");
      return;
    }

    if (maxGenerations <= 0) {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Max generations must be positive");
      return;
    }

    // 确保函数被正确传递
    Func<bool[], double> fitness1Func = null;
    Func<bool[], double> fitness2Func = null;

    try {
      fitness1Func = (Func<bool[], double>) greenViewFunc;
      fitness2Func = (Func<bool[], double>) areaFunc;
    } catch {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Fitness functions must be of type Func<bool[], double>");
      return;
    }

    // 获取基因长度 (需要根据实际情况确定)
    int geneLength = 50; // 示例值，应该根据实际模型修改

    // 初始化NSGA-II种群
    NSGA2Population nsga2 = new NSGA2Population(populationSize, geneLength, fitness1Func, fitness2Func);

    // 运行遗传算法
    for (int i = 0; i < maxGenerations; i++) {
      nsga2.Evolve();
      Print("Generation {0} completed", i + 1);
    }

    // 提取非支配解
    List<Individual> pareto = new List<Individual>();
    foreach(var ind in nsga2.pop) {
      if(ind.Rank == 0) {
        pareto.Add(ind);
      }
    }

    // 输出结果
    bestIndividuals = pareto;

    // 显示结果信息
    Print("Found {0} solutions on the Pareto front", pareto.Count);
    for (int i = 0; i < Math.Min(5, pareto.Count); i++) {
      Print("Solution {0}: GreenView = {1:F2}, AreaDiff = {2:F2}",
        i + 1, pareto[i].Fitness1, pareto[i].Fitness2);
    }
  }
  #endregion
  #region Additional

  public class Individual
  {
    public bool[] Genes;
    public double Fitness1; // 绿视域
    public double Fitness2; // 面积差值
    public int Rank;
    public double CrowdingDistance;

    public Individual(bool[] genes)
    {
      Genes = (bool[]) genes.Clone();
    }
  }

  public class NSGA2Population
  {
    public Individual[] pop;
    private int geneLength;
    private Random rnd = new Random();
    private Func<bool[], double> fitness1Func;
    private Func<bool[], double> fitness2Func;

    public NSGA2Population(int popSize, int geneLen, Func<bool[], double> f1, Func<bool[], double> f2)
    {
      geneLength = geneLen;
      fitness1Func = f1;
      fitness2Func = f2;
      pop = new Individual[popSize];
      for (int i = 0; i < popSize; i++)
      {
        bool[] genes = new bool[geneLength];
        for (int j = 0; j < geneLength; j++)
          genes[j] = rnd.NextDouble() < 0.5;
        pop[i] = new Individual(genes);
        Evaluate(pop[i]);
      }
    }

    public void Evolve()
    {
      List<Individual> offspring = new List<Individual>();
      while (offspring.Count < pop.Length)
      {
        Individual a = SelectParent();
        Individual b = SelectParent();

        Individual c1 = Crossover(a, b, true);
        Individual c2 = Crossover(a, b, false);

        Mutate(c1);
        Mutate(c2);
        Evaluate(c1);
        Evaluate(c2);
        offspring.Add(c1);
        offspring.Add(c2);
      }

      List<Individual> combined = new List<Individual>();
      foreach(Individual ind in pop)
        combined.Add(ind);
      foreach(Individual ind in offspring)
        combined.Add(ind);

      List<Individual> nextGen = CreateNextGeneration(FastNonDominatedSort(combined), pop.Length);
      pop = nextGen.ToArray();
    }

    private void Evaluate(Individual ind)
    {
      ind.Fitness1 = fitness1Func(ind.Genes);
      ind.Fitness2 = fitness2Func(ind.Genes);
    }

    private Individual SelectParent()
    {
      // Tournament selection
      Individual a = pop[rnd.Next(pop.Length)];
      Individual b = pop[rnd.Next(pop.Length)];
      return (a.Rank < b.Rank) ? a : b;
    }

    private Individual Crossover(Individual p1, Individual p2, bool firstHalf)
    {
      bool[] genes = new bool[geneLength];
      int cp = rnd.Next(1, geneLength - 1);

      for (int i = 0; i < geneLength; i++)
      {
        if (firstHalf)
          genes[i] = i < cp ? p1.Genes[i] : p2.Genes[i];
        else
          genes[i] = i < cp ? p2.Genes[i] : p1.Genes[i];
      }

      return new Individual(genes);
    }

    private void Mutate(Individual ind)
    {
      for (int i = 0; i < geneLength; i++)
      {
        if (rnd.NextDouble() < 0.02)
          ind.Genes[i] = !ind.Genes[i];
      }
    }

    private List<List<Individual>> FastNonDominatedSort(List<Individual> population)
    {
      List<List<Individual>> fronts = new List<List<Individual>>();
      Dictionary<Individual, List<Individual>> S = new Dictionary<Individual, List<Individual>>();
      Dictionary<Individual, int> n = new Dictionary<Individual, int>();
      List<Individual> front = new List<Individual>();

      foreach (var p in population)
      {
        S[p] = new List<Individual>();
        n[p] = 0;
        foreach (var q in population)
        {
          if (Dominates(p, q)) S[p].Add(q);
          else if (Dominates(q, p)) n[p]++;
        }
        if (n[p] == 0)
        {
          p.Rank = 0;
          front.Add(p);
        }
      }
      fronts.Add(front);
      int i = 0;
      while (fronts[i].Count > 0)
      {
        List<Individual> nextFront = new List<Individual>();
        foreach (var p in fronts[i])
        {
          foreach (var q in S[p])
          {
            n[q]--;
            if (n[q] == 0)
            {
              q.Rank = i + 1;
              nextFront.Add(q);
            }
          }
        }
        i++;
        if (nextFront.Count > 0) fronts.Add(nextFront);
      }
      return fronts;
    }

    private bool Dominates(Individual a, Individual b)
    {
      return (a.Fitness1 >= b.Fitness1 && a.Fitness2 <= b.Fitness2) && (a.Fitness1 > b.Fitness1 || a.Fitness2 < b.Fitness2);
    }

    private List<Individual> CreateNextGeneration(List<List<Individual>> fronts, int size)
    {
      List<Individual> nextGen = new List<Individual>();
      foreach (var front in fronts)
      {
        if (nextGen.Count + front.Count > size)
        {
          AssignCrowdingDistance(front);
          // Sort by crowding distance
          List<Individual> sortedFront = new List<Individual>(front);
          sortedFront.Sort(delegate(Individual a, Individual b) {
              return b.CrowdingDistance.CompareTo(a.CrowdingDistance);
            });

          // Add individuals until reaching size
          int remaining = size - nextGen.Count;
          for (int i = 0; i < remaining; i++) {
            nextGen.Add(sortedFront[i]);
          }
          break;
        }
        nextGen.AddRange(front);
      }
      return nextGen;
    }

    private void AssignCrowdingDistance(List<Individual> front)
    {
      int count = front.Count;
      if (count == 0) return;

      foreach (var ind in front) ind.CrowdingDistance = 0;

      // Sort by Fitness1
      List<Individual> sortedByF1 = new List<Individual>(front);
      sortedByF1.Sort(delegate(Individual a, Individual b) {
          return a.Fitness1.CompareTo(b.Fitness1);
        });
      sortedByF1[0].CrowdingDistance = double.PositiveInfinity;
      sortedByF1[count - 1].CrowdingDistance = double.PositiveInfinity;

      double rangeF1 = sortedByF1[count - 1].Fitness1 - sortedByF1[0].Fitness1;
      if (rangeF1 > 0) {
        for (int i = 1; i < count - 1; i++) {
          sortedByF1[i].CrowdingDistance += (sortedByF1[i + 1].Fitness1 - sortedByF1[i - 1].Fitness1) / rangeF1;
        }
      }

      // Sort by Fitness2
      List<Individual> sortedByF2 = new List<Individual>(front);
      sortedByF2.Sort(delegate(Individual a, Individual b) {
          return a.Fitness2.CompareTo(b.Fitness2);
        });
      sortedByF2[0].CrowdingDistance = double.PositiveInfinity;
      sortedByF2[count - 1].CrowdingDistance = double.PositiveInfinity;

      double rangeF2 = sortedByF2[count - 1].Fitness2 - sortedByF2[0].Fitness2;
      if (rangeF2 > 0) {
        for (int i = 1; i < count - 1; i++) {
          sortedByF2[i].CrowdingDistance += (sortedByF2[i + 1].Fitness2 - sortedByF2[i - 1].Fitness2) / rangeF2;
        }
      }
    }
  }
  #endregion
}