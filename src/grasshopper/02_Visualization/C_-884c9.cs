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
public abstract class Script_Instance_884c9 : GH_ScriptInstance
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
  private void RunScript(List<Point3d> gridCenters, double gridSize, bool gene, ref object A)
  {

    List<Brep> greenPatches = new List<Brep>();

    // 安全性检查
    if (gridCenters == null || gene == null || gridCenters.Count != gene.Length)
    {
      Print("输入数据有误：布尔数组与网格中心点数量不一致。");
      return;
    }

    // 构建绿色块区域
    for (int i = 0; i < gene.Length; i++)
    {
      if (!gene[i]) continue; // 不是绿地，跳过

      Point3d center = gridCenters[i];
      double half = gridSize / 2.0;

      // 构建矩形四角
      Point3d pt0 = new Point3d(center.X - half, center.Y - half, center.Z);
      Point3d pt1 = new Point3d(center.X + half, center.Y - half, center.Z);
      Point3d pt2 = new Point3d(center.X + half, center.Y + half, center.Z);
      Point3d pt3 = new Point3d(center.X - half, center.Y + half, center.Z);

      // 构造Polyline
      Polyline pl = new Polyline(new List<Point3d>() { pt0, pt1, pt2, pt3, pt0 });
      if (!pl.IsValid || !pl.IsClosed) continue;

      // 转换为曲线并生成平面Brep
      Curve curve = pl.ToNurbsCurve();
      Brep[] patch = Brep.CreatePlanarBreps(curve);
      if (patch != null && patch.Length > 0)
        greenPatches.Add(patch[0]);
    }

    A = greenPatches;
  }
  #endregion
  #region Additional

  #endregion
}