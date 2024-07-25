using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryUtils
{
   [Flags]
   public enum ExtendType
   {
      None = 0,     // Extend neither line
      First = 1,    // Extend line passing through first and second arguments
      Second = 2,   // Extend line passing through thrid and fourth arguments
      Both = 3      // Extend both lines
   }

   public static class UnusedGeomExtensions
   {
      //public static Point3d? Inters(Point3d p1, Point3d p2, Point3d p3, Point3d p4, ExtendType type = ExtendType.Both)
      //{
      //   LinearEntity3d e1 = (type.HasFlag(ExtendType.First)) ? new Line3d(p1, p2) : new LineSegment3d(p1, p2);
      //   LinearEntity3d e2 = (type.HasFlag(ExtendType.Second)) ? new Line3d(p3, p4) : new LineSegment3d(p3, p4);
      //   var result = e1.IntersectWith(e2);
      //   return result != null && result.Length > 0 ? result[0] : null;
      //}

      //public static Point2d? Inters(Point2d p1, Point2d p2, Point2d p3, Point2d p4, ExtendType type = ExtendType.Both)
      //{
      //   LinearEntity2d e1 = (type.HasFlag(ExtendType.First)) ? new Line2d(p1, p2) : new LineSegment2d(p1, p2);
      //   LinearEntity2d e2 = (type.HasFlag(ExtendType.Second)) ? new Line2d(p3, p4) : new LineSegment2d(p3, p4);
      //   var result = e1.IntersectWith(e2);
      //   return result != null && result.Length > 0 ? result[0] : null;
      //}

   }

   public static partial class GeometryExtensions
   {

      static double lastAngle = 0;
      public static double LastAngle
      {
         get => lastAngle;
         set => lastAngle = Reduce(lastAngle + value);
      }

      public static Point3d LastPoint { get; set; } = default(Point3d);

      public static double Reduce(double angle)
      {
         double modang = angle % (2 * Math.PI);
         if(modang < 0)
            modang += 2 * Math.PI;
         if(modang > 2 * Math.PI)
            modang -= 2 * Math.PI;
         return modang;
      }
      /// <summary>
      /// Returns a point at a specified distance and at an
      /// angle relative to the angle of the vector passing 
      /// through the basepoint argument and result of the 
      /// most-recent call to this API, or the XAxis if the 
      /// API was not previously-called.
      /// 
      /// The LastPoint property of this class holds the
      /// angle of the vector passing through the basepoint
      /// argument to, and the result of the last call to 
      /// this API, and is initialized to 0.0 (the X-axis).
      /// 
      /// The LastPoint property can also be assigned to 
      /// the reference angle that will be used in the next 
      /// call to this API.
      /// 
      /// For a vector passing through the basepoint argument 
      /// and the result of the most-recent call to this API,
      /// a positive angle in the range of 0..PI yields a point 
      /// to the left of the vector, and negative angle in the
      /// range of 0..-PI yields a point to the right of that
      /// vector.
      /// 
      /// Example: Generates the 4 corner points of a 5 x 3 
      /// unit rectangle at a rotation of 45 degrees:
      /// <code>
      ///    
      ///      const double left45 = Math.Pi / 4.0;
      ///      const double left = left45 * 2;
      ///      const double right = 0 - left;
      ///      
      ///      // Reset last angle used by RelativePolar():
      ///      
      ///      GeometryExtensions.LastAngle = 0.0;
      ///      
      ///      Point3d point1 = Point3d.Origin;
      ///      Point3d point2 = point1.RelativePolar(left45, 5.0);
      ///      Point3d point3 = point2.RelativePolar(left, 3.0);
      ///      Point3d point4 = point3.RelativePolar(left, 5.0);
      /// 
      /// Using the iterative version:
      /// 
      ///      Point3d point1 = Point3d.Origin;
      ///      Point3d[] points = point1.RelativePolar(
      ///         (left45, 5.0),
      ///         (left, 3.0),
      ///         (left, 5.0)
      ///      ).ToArray();
      ///      
      /// Draws a U-shaped figure using the overload
      /// that takes the starting reference angle:
      /// 
      ///      Point3d point1 = Point3d.Origin;
      ///      Point3d[] points = point1.RelativePolar(0.0, 
      ///         (0.0, 5.0),
      ///         (left, 7.0),
      ///         (left, 1.0),
      ///         (left, 6.0),
      ///         (right, 3.0),
      ///         (right, 6.0),
      ///         (left, 1.0),
      ///         (left 7.0))
      ///      ).ToArray();
      ///  
      /// 
      /// </code>
      /// </summary>
      /// <param name="basePt">The basePoint</param>
      /// <param name="angle">The angle relative to the angle
      /// of a ray passing through the last two points returned 
      /// by this method.</param>
      /// <param name="distance">The distance</param>
      /// <returns>A point at the specified relative angle and 
      /// distance from the basepoint</returns>

      public static Point3d RelativePolar(this Point3d basePt, 
         double angle, double distance)
      {
         LastAngle += angle;
         return LastPoint = new Point3d(
            basePt.X + distance * Math.Cos(LastAngle),
            basePt.Y + distance * Math.Sin(LastAngle),
            basePt.Z);
      }

      /// <summary>
      /// Uses the point returned by the last call to this
      /// (or the Polar() method), and the relative angle
      /// from the line passing through the two points
      /// returned by the last two calls to this method.
      /// </summary>
      /// <param name="angle"></param>
      /// <param name="distance"></param>
      /// <returns></returns>

      public static Point3d RelativePolar(this double angle, double distance)
      {
         LastAngle += angle;
         Point3d last = LastPoint;
         return LastPoint = new Point3d(
            last.X + distance * Math.Cos(LastAngle),
            last.Y + distance * Math.Sin(LastAngle),
            last.Z);
      }

      /// <summary>
      /// The classic Polar() method that also sets 
      /// the LastPoint property.
      /// </summary>

      public static Point3d Polar(this Point3d basePt, double angle, double distance)
      {
         LastAngle = angle;
         LastPoint = new Point3d(
            basePt.X + distance * Math.Cos(angle),
            basePt.Y + distance * Math.Sin(angle),
            basePt.Z);
         return LastPoint;
      }

      /// <summary>
      /// Uses the point returned by the last call to 
      /// any of the Polar() or RelativePolar() methods 
      /// in this class, as the basepoint.
      /// </summary>
      /// <param name="angle">The absolute angle</param>
      /// <param name="distance">The distance</param>
      /// <returns></returns>

      public static Point3d Polar(this double angle, double distance)
      {
         LastAngle = angle;
         Point3d last = LastPoint;
         return LastPoint = new Point3d(
            last.X + distance * Math.Cos(angle),
            last.Y + distance * Math.Sin(angle),
            last.Z);
      }

      /// <summary>
      /// A way to use relative angles with the
      /// basic Polar() method:
      /// 
      /// Pass Polar()'s angle argument the result
      /// of calling this method on a double that
      /// represents the relative angle. 
      /// 
      /// This method should not be used with 
      /// RelativePolar().
      /// </summary>

      public static double Relative(this double angle)
      {
         return LastAngle += angle;
      }

      /// ?????
      public static Point3d Relative(this Point3d point)
      {
         return LastPoint = point; ;
      }

      /// <summary>
      /// Iterative versions of the above, requires a version 
      /// of .NET that supports ValueTuple. The first version
      /// takes the starting reference angle, the second uses
      /// the LastAngle property.
      /// </summary>

      public static IEnumerable<Point3d> RelativePolar(this Point3d start, 
         double startAngle, params (double angle, double distance)[] values)
      {
         yield return start;
         Point3d next = start;
         double ang = startAngle;
         for(int i = 0; i < values.Length; i++)
         {
            ang += values[i].angle;
            var dist = values[i].distance;
            next = LastPoint = new Point3d(
               next.X + dist * Math.Cos(ang),
               next.Y + dist * Math.Sin(ang),
               next.Z);
            yield return next;
         }
         LastAngle = ang;
      }

      public static IEnumerable<Point3d> RelativePolar(this Point3d start,
         params (double angle, double distance)[] values)
      {
         yield return start;
         Point3d next = start;
         double ang = LastAngle;
         for(int i = 0; i < values.Length; i++)
         {
            ang += values[i].angle;
            var dist = values[i].distance;
            next = LastPoint = new Point3d(
               next.X + dist * Math.Cos(ang),
               next.Y + dist * Math.Sin(ang),
               next.Z);
            yield return next;
         }
         LastAngle = ang;
      }


      /// <summary>
      /// Same as above, except it accepts a Matrix3d
      /// and transforms the results by same.
      /// </summary>
      /// <param name="start"></param>
      /// <param name="transform"></param>
      /// <param name="values"></param>
      /// <returns></returns>

      public static IEnumerable<Point3d> RelativePolar(this Point3d start,
         Matrix3d transform,
         params (double angle, double distance)[] values)
      {
         Point3d next = start.TransformBy(transform);
         yield return next;
         double ang = LastAngle;
         for(int i = 0; i < values.Length; i++)
         {
            ang += values[i].angle;
            var dist = values[i].distance;
            next = LastPoint = new Point3d(
               next.X + dist * Math.Cos(ang),
               next.Y + dist * Math.Sin(ang),
               next.Z).TransformBy(transform);
            yield return next;
         }
         LastAngle = ang;
      }

      public static IEnumerable<Point3d> RelativePolar3(this Point3d start,
         IEnumerable<(double angle, double distance)> values)
      {
         yield return start;
         Point3d next = start;
         double ang = LastAngle;
         foreach(var item in values)
         {
            ang += item.angle;
            var dist = item.distance;
            next = LastPoint = new Point3d(
               next.X + dist * Math.Cos(ang),
               next.Y + dist * Math.Sin(ang),
               next.Z);
            yield return next;
         }
         LastAngle = ang;
      }

      public static Point3d[] RelativePolar2(this Point3d start,
         params (double angle, double distance)[] values)
      {
         if(values.Length == 0)
            return new Point3d[] { start };
         Point3d[] result = new Point3d[values.Length + 1];
         result[0] = start;
         Point3d next = start;
         double ang = LastAngle;
         for(int i = 0; i < values.Length; i++)
         {
            ang += values[i].angle;
            next = LastPoint = new Point3d(
               next.X + values[i].distance * Math.Cos(ang),
               next.Y + values[i].distance * Math.Sin(ang),
               next.Z);
            result[i + 1] = next;
         }
         LastAngle = ang;
         return result;
      }

      /// <summary>
      /// Enlarges/reduces the given Extents3d by the specified
      /// dimension.
      /// 
      /// No check is performed to ensure the result
      /// is not negated in any dimension.
      /// <returns></returns>

      public static Extents3d Offset(this Extents3d ext, double offset)
      {
         return Offset(ext, offset, offset, offset);
      }

      /// <summary>
      /// Enlarges/reduces the given Extents3d by the specified
      /// respective dimensions.
      /// </summary>

      public static Extents3d Offset(this Extents3d ext, double x, double y, double z)
      {
         Point3d min = ext.MinPoint;
         Point3d max = ext.MaxPoint;
         return new Extents3d(
            new Point3d(min.X -x, min.Y - y, min.Z - z),
            new Point3d(max.X + x, max.Y + y, max.Z + z));
      }
   }
}
