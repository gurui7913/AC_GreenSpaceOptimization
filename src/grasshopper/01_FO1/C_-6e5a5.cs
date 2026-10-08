using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Geometry.Intersect;


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_6e5a5 : GH_ScriptInstance
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
  private void RunScript(List<Mesh> greenSurfs, List<Brep> Buildings, double cellSize, ref object gridRects, ref object gridCenters, ref object gridStates)
  {
    List<Rectangle3d> rects = new List<Rectangle3d>();
    List<Point3d> centers = new List<Point3d>();
    List<bool> states = new List<bool>();

    // 打印输入信息
    Print(string.Format("收到 {0} 个绿地表面", greenSurfs.Count));
    Print(string.Format("收到 {0} 个建筑物", Buildings.Count));
    Print(string.Format("网格尺寸: {0}", cellSize));

    // 容差
    double tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

    foreach (Surface srf in greenSurfs)
    {
      BoundingBox bbox = srf.GetBoundingBox(true);
      Plane plane;

      // 尝试获取平面
      if (!srf.TryGetPlane(out plane))
      {
        Print("警告: 无法获取表面平面，使用世界XY平面");
        plane = Plane.WorldXY;
      }

      double minX = bbox.Min.X;
      double minY = bbox.Min.Y;
      double maxX = bbox.Max.X;
      double maxY = bbox.Max.Y;

      int xCount = (int)Math.Floor((maxX - minX) / cellSize);
      int yCount = (int)Math.Floor((maxY - minY) / cellSize);

      Print(string.Format("为表面创建 {0}x{1} 网格", xCount, yCount));

      for (int i = 0; i < xCount; i++)
      {
        for (int j = 0; j < yCount; j++)
        {
          double x0 = minX + i * cellSize;
          double y0 = minY + j * cellSize;

          // 创建四个角点
          Point3d pt1 = new Point3d(x0, y0, 0);
          Point3d pt2 = new Point3d(x0 + cellSize, y0, 0);
          Point3d pt3 = new Point3d(x0 + cellSize, y0 + cellSize, 0);
          Point3d pt4 = new Point3d(x0, y0 + cellSize, 0);

          // 计算中心点
          Point3d center = new Point3d(x0 + cellSize / 2, y0 + cellSize / 2, 0);

          // 检查点是否在表面上
          Point3d closestPt;
          double u, v;

          if (!srf.ClosestPoint(center, out closestPt, out u, out v))
            continue;

          if (center.DistanceTo(closestPt) > tolerance)
            continue;

          // 检查点是否在建筑物内
          bool insideBuilding = false;
          foreach (Brep bld in Buildings)
          {
            // 检查点是否在建筑物内部或靠近建筑物
            Point3d closestPtOnBld;

            // 最简单的ClosestPoint重载
            if (bld.ClosestPoint(center, out closestPtOnBld))
            {
              // 如果点非常接近建筑物，认为它在建筑物内
              if (center.DistanceTo(closestPtOnBld) < tolerance)
              {
                insideBuilding = true;
                break;
              }
            }
          }

          if (!insideBuilding)
          {
            // 创建矩形
            try
            {
              // 创建矩形的两个向量
              Vector3d xDir = new Vector3d(cellSize, 0, 0);
              Vector3d yDir = new Vector3d(0, cellSize, 0);

              // 创建矩形（使用明确的构造函数，避免歧义）
              Rectangle3d rect = new Rectangle3d(plane, pt1, xDir, yDir);

              rects.Add(rect);
              centers.Add(center);
              states.Add(true); // 初始设为true，后续遗传算法可操作
            }
            catch (Exception ex)
            {
              Print("创建矩形时出错: " + ex.Message);
            }
          }
        }
      }
    }

    Print(string.Format("共生成 {0} 个网格单元", rects.Count));

    gridRects = rects;
    gridCenters = centers;
    gridStates = states;
  }
  #endregion
  #region Additional

  #endregion
}