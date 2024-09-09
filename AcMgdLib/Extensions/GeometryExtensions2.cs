/// OffLineTests.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;

public static partial class GeometryExtensions2
{
   const double halfPi = 1.5707963267948966;
   const double pi2 = Math.PI * 2;

   /// <summary>
   /// Indicates if the bounding box which the method is invoked 
   /// on entirely contains the given bounding box argument.
   /// </summary>
   /// <param name="extents">The containing candidate bounding box</param>
   /// <param name="other">The contained candidate bounding box</param>
   /// <param name="project">If true, the containment test is performed
   /// on the rectangles produced by projecting both bounding boxes into
   /// the XY plane</param>
   /// <returns>true if the first bounding box entirely contains the second
   /// bounding box, or the first bounding box's projected rectangle 
   /// entirely contains the second bounding box's projected rectangle</returns>

   public static bool Contains(this Extents3d extents, Extents3d other, bool project = false)
   {
      if(project)
         return Contains2d(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
      else
         return Contains(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
   }


   /// <summary>
   /// Indicates if the bounding box having the diagonal 
   /// corners min1 and max1 entirely contains the bounding
   /// box having the diagonal corners min2 and max2.
   /// </summary>

   static bool Contains(Point3d min1, Point3d max1, Point3d min2, Point3d max2)
   {
      return !(min2.X < min1.X || min2.Y < min1.Y || min2.Z < min1.Z
         || max2.X > max1.X || max2.Y > max1.Y || max2.Z > max1.Z);
   }

   /// <summary>
   /// Indicates if the rectangle produced by projecting the
   /// bounding box having the diagonal corners min1 and max1 
   /// into the XY plane entirely contains the rectangle produced
   /// by projecting the bounding box having diagonal corners 
   /// min2 and max2 into the XY plane.
   /// </summary>

   static bool Contains2d(Point3d min1, Point3d max1, Point3d min2, Point3d max2)
   {
      return !(min2.X < min1.X || min2.Y < min1.Y
         || max2.X > max1.X || max2.Y > max1.Y);
   }

   static Extents3d empty = new Extents3d();
}



