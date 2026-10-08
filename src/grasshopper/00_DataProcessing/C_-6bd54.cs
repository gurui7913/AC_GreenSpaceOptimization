using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;



/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_6bd54 : GH_ScriptInstance
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
  private void RunScript(List<Curve> greenBoundaries, List<Curve> buildingFootprints, List<Curve> paths, ref object usableGreenSrfs, ref object samplePoints)
  {
    List<Surface> greenSurfs = new List<Surface>();
    List<Point3d> samplePts = new List<Point3d>();

    // 步骤1：生成有效绿地面（绿地 - 建筑）
    foreach (Curve boundary in greenBoundaries)
    {
      if (!boundary.IsClosed || !boundary.IsPlanar()) continue;

      Brep[] baseBreps = Brep.CreatePlanarBreps(boundary, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
      if (baseBreps == null || baseBreps.Length == 0) continue;
      Brep baseBrep = baseBreps[0];

      List<Brep> holes = new List<Brep>();
      foreach (Curve building in buildingFootprints)
      {
        Point3d testPt = building.PointAtStart;
        if (boundary.Contains(testPt, Plane.WorldXY, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) != PointContainment.Outside)
        {
          Brep[] bldBreps = Brep.CreatePlanarBreps(building, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
          if (bldBreps != null && bldBreps.Length > 0) holes.Add(bldBreps[0]);
        }
      }

      Brep[] trimmed = Brep.CreateBooleanDifference(new Brep[] { baseBrep }, holes, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
      if (trimmed != null && trimmed.Length > 0)
      {
        foreach (Brep b in trimmed)
        {
          greenSurfs.AddRange(b.Surfaces);
        }
      }
    }

    // 步骤2：路径采样点（每隔5m）
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

    // 输出赋值
    usableGreenSrfs = greenSurfs;
    samplePoints = samplePts;
  }
  #endregion
  #region Additional

  #endregion
}