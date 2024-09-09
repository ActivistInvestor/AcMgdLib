using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Autodesk.AutoCAD.DatabaseServices;
// using Autodesk.AutoCAD.BoundaryRepresentation;

using AcRx = Autodesk.AutoCAD.Runtime;
// using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;
using AcGe = Autodesk.AutoCAD.Geometry;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;

namespace Autodesk.AutoCAD.Geometry.Extensions
{

	public enum Dimension
	{
		X = 0, Y = 1, Z = 2
	}

	public static class GeometryExtensions
	{
		const double halfPi = 1.5707963267948966;
		const double pi2 = Math.PI * 2;

      /// <summary>
      /// Takes an Extents3d and an Entity as arguments and returns a value
      /// indicating if the entity's bounds is contained in the extents.
      /// </summary>
      /// <param name="extents">The Extents3d to test against the entity's bounds</param>
      /// <param name="entity">The Entity whose bounds is to be compared to extents</param>
      /// <param name="project">If true, the containment test is performed
      /// on the rectangles produced by projecting both bounding boxes into
      /// the XY plane</param>
      /// <returns>true if the given Extents3d entirely contains the bounds 
      /// of the given Entity, or the given Extents3d's projected rectangle 
      /// entirely contains the 2d projection of the Entity's bounding box</returns>

      public static bool Contains(this Extents3d extents, Entity entity, bool project = false)
      {
         Assert.IsNotNullOrDisposed(entity);
         if(entity.TryGetBounds(out Extents3d result).IsOk())
         {
            return Contains(extents, result, project);
         }
         return false;
      }

      /// <summary>
      /// Functional inversion of the above method with the entity as 
      /// the invocation target and the Extents3d as the argument.
      /// </summary>

      public static bool IsContainedBy(this Entity entity, Extents3d bounds, bool project = false)
      {
         return Contains(bounds, bounds, project);
      }

      public static IEnumerable<T> ContainedBy<T>(this IEnumerable<T> source, Extents3d bounds, bool project = false)
         where T : Entity
      {
         Assert.IsNotNull(source);
         return source.Where(entity => bounds.Contains(entity, project));
      }

      public static IEnumerable<T> Containing<T>(this Extents3d bounds, IEnumerable<T> source, bool project = false)
         where T : Entity
      {
         Assert.IsNotNull(source);
         return source.Where(entity => bounds.Contains(entity, project));
      }


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

      delegate bool Extents3dPredicate(Point3d min1, Point3d min2, Point3d max1, Point3d max2);

		public static bool Contains(this Extents3d extents, Extents3d other, bool project = false)
		{
			return (project ? (Extents3dPredicate) Contains2d : Contains)
				(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
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

		/// <summary>
		/// Indicates if the rectangle having the diagonal corners
		/// min1 and max1 entirely contains the rectangle having
		/// diagonal corners min2 and max2.
		/// </summary>

		static bool Contains(Point2d min1, Point2d max1, Point2d min2, Point2d max2)
		{
			return !(min2.X < min1.X || min2.Y < min1.Y
				|| max2.X > max1.X || max2.Y > max1.Y);
		}

		/// <summary>
		/// Indicates if the bounding rectangle which the method is invoked 
		/// on entirely contains the given bounding rectangle argument.
		/// </summary>
		/// <param name="extents">The possibly-containing bounding rectangle</param>
		/// <param name="other">The possibly-contained bounding rectangle</param>
		/// <returns>True if the extents bounding rectangle entirely contains the
		/// other bounding rectangle</returns>

		public static bool Contains(this Extents2d extents, Extents2d other)
		{
			return Contains(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
		}

		/// <summary>
		/// Indicates if the given bounding box contains the given Point3d
		/// </summary>
		/// <param name="extents"></param>
		/// <param name="point"></param>
		/// <param name="project">If true, the base of the bounding box and the point
		/// are projected into the XY plane, and the result indicates if the projected
		/// point is contained within the projected rectangle</param>
		/// <returns>true if the extents contains the point, or its projection into
		/// the XY plane contains the point projected into the XY plane</returns>

		public static bool Contains(this Extents3d extents, Point3d point, bool project = false)
		{
			if(project)
				return Contains2d(extents.MinPoint, extents.MaxPoint, point);
			else
				return Contains(extents.MinPoint, extents.MaxPoint, point);
		}

		/// <summary>
		/// Indicates if the given axis-aligned bounding rectangle contains 
		/// the projection of the given Point3d into the bounding rectangle's
		/// plane.
		/// </summary>
		/// <param name="extents">The axis-aligened bounding rectangle</param>
		/// <param name="point">The point to test</param>
		/// <returns>true if the rectangle contains the point</returns>
		
		public static bool Contains(this Extents2d extents, Point3d point)
		{
			return Contains(extents.MinPoint, extents.MaxPoint, point.ToPoint2d());
		}

		/// <summary>
		/// Indicates if the given bounding rectangle contains the given Point2d
		/// </summary>
		/// <param name="extents">The bounding rectangle</param>
		/// <param name="point">The point to test</param>
		/// <returns>true if the rectangle contains the point</returns>

		public static bool Contains(this Extents2d extents, Point2d point)
		{
			return Contains(extents.MinPoint, extents.MaxPoint, point);
		}

		static bool Contains(Point3d min, Point3d max, Point3d p)
		{
			return !(p.X < min.X || p.X > max.X || p.Y < min.Y || p.Y > max.Y || p.Z < min.Z || p.Z > max.Z);
		}

		static bool Contains2d(Point3d min, Point3d max, Point3d p)
		{
			return !(p.X < min.X || p.X > max.X || p.Y < min.Y || p.Y > max.Y);
		}

		static bool Contains(Point2d min, Point2d max, Point2d p)
		{
			return !(p.X < min.X || p.X > max.X || p.Y < min.Y || p.Y > max.Y);
		}

		/// <summary>
		/// Indicates if the given bounding rectangle intersects the
		/// rectangle produced by projecting the given bounding box 
		/// into the XY plane. 
		/// 
		/// All bounds are presumed to be axis-aligned.
		/// </summary>
		/// <param name="target">The bounding rectangle to test</param>
		/// <param name="extents">The bounding box to test containment of</param>
		/// <returns>true if the rectangular projection of the box
		/// intersects the given bounding rectangle</returns>

		public static bool Intersects(this Extents2d target, Extents3d extents)
		{
			var extents2d = extents.ToExtents2d();
			return Intersects(target.MinPoint, target.MaxPoint, extents2d.MinPoint, extents2d.MaxPoint);
		}

		/// <summary>
		/// Inverts the arguments of the above
		/// </summary>
		
		public static bool Intersects(this Extents3d target, Extents2d extents)
		{
			var ext2d = target.ToExtents2d();
			return Intersects(extents.MinPoint, extents.MaxPoint, ext2d.MinPoint, ext2d.MaxPoint);
		}

		static bool Intersects(Point2d r1Min, Point2d r1Max, Point2d r2Min, Point2d r2Max)
		{
			return !(r1Min.X > r2Max.X || r2Min.X > r1Max.X || r1Min.Y > r2Max.Y || r2Min.Y > r1Max.Y);
		}

		static bool Intersects2d(Point3d r1Min, Point3d r1Max, Point3d r2Min, Point3d r2Max)
		{
			return !(r1Min.X > r2Max.X || r2Min.X > r1Max.X || r1Min.Y > r2Max.Y || r2Min.Y > r1Max.Y);
		}

		static bool Intersects(Point3d r1Min, Point3d r1Max, Point3d r2Min, Point3d r2Max)
		{
			return !(r1Min.X > r2Max.X || r2Min.X > r1Max.X
				|| r1Min.Y > r2Max.Y || r2Min.Y > r1Max.Y
				|| r1Min.Z > r2Max.Z || r2Min.Z > r1Max.Z);
		}

		/// <summary>
		/// Indicates if the two given bounding rectangles intersect
		/// </summary>
		public static bool Intersects(this Extents2d target, Extents2d extents)
		{
			return Intersects(target.MinPoint, target.MaxPoint, extents.MinPoint, extents.MaxPoint);
		}

		/// <summary>
		/// Indicates if the two given bounding boxes intersect, or 
		/// the projection of their bases into the XY plane intersect.
		/// </summary>

		public static bool Intersects(this Extents3d target, Extents3d extents, bool project = false)
		{
			return (project ? (Extents3dPredicate) Intersects2d : Intersects)
				(target.MinPoint, target.MaxPoint, extents.MinPoint, extents.MaxPoint);
		}

		public static bool IsEmpty(this Extents3d extents)
		{
			return extents.MinPoint.IsEqualTo(extents.MaxPoint);
		}

		public static bool IsDefault(this Extents3d extents)
		{
			return extents.IsEqualTo(new Extents3d()) || extents.IsEqualTo(default(Extents3d));
		}

		public static bool IsDefaultOrEmpty(this Extents3d extents)
		{
			return extents.MinPoint.IsEqualTo(extents.MaxPoint) || IsDefault(extents);
		}

		public static bool IsValid(this Extents3d extents)
		{
			return !(IsEmpty(extents) || IsDefault(extents));
		}

		/// <summary>
		/// Returns true if the given bounding boxes intersect, and sets the
		/// result argument to the intersection of the bounding boxes.
		/// </summary>
		/// <param name="extents"></param>
		/// <param name="other"></param>
		/// <param name="result"></param>
		/// <returns></returns>

		public static bool IntersectWith(this Extents3d extents, Extents3d other, out Extents3d result)
		{
			if(Intersects(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint))
			{
				result = Intersection(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
				return true;
			}
			result = default(Extents3d);
			return false;
		}

		/// <summary>
		/// Returns the intersection of the two bounding boxes.
		/// If the two bounding boxes do not intersect, the result
		/// is null.
		/// </summary>
		/// <param name="extents"></param>
		/// <param name="other"></param>
		/// <returns></returns>

		public static Extents3d? IntersectWith(this Extents3d extents, Extents3d other)
		{
			if(Intersects(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint))
				return Intersection(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
			return null;
		}

		/// <summary>
		/// No good. This can return an invalid extents if the two boxes don't intersect
		/// </summary>

		static Extents3d Intersection(Point3d r1Min, Point3d r1Max, Point3d r2Min, Point3d r2Max)
		{
			return new Extents3d(
				new Point3d(Math.Max(r1Min.X, r2Min.X), Math.Max(r1Min.Y, r2Min.Y), Math.Max(r1Min.Z, r2Min.Z)),
				new Point3d(Math.Min(r1Max.X, r2Max.X), Math.Min(r1Max.Y, r2Max.Y), Math.Min(r1Max.Z, r2Max.Z)));
		}

		public static bool IntersectWith(this Extents2d extents, Extents2d other, out Extents2d result)
		{
			if(Intersects(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint))
			{
				result = Intersection(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
				return true;
			}
			result = default(Extents2d);
			return false;
		}

		public static Extents2d? IntersectWith(this Extents2d extents, Extents2d other)
		{
			if(Intersects(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint))
				return Intersection(extents.MinPoint, extents.MaxPoint, other.MinPoint, other.MaxPoint);
			return null;
		}

		static Extents2d Intersection(Point2d r1Min, Point2d r1Max, Point2d r2Min, Point2d r2Max)
		{
			return new Extents2d(
				new Point2d(Math.Max(r1Min.X, r2Min.X), Math.Max(r1Min.Y, r2Min.Y)),
				new Point2d(Math.Min(r1Max.X, r2Max.X), Math.Min(r1Max.Y, r2Max.Y)));
		}

		public static readonly Extents3d EmptyExtents3d = new Extents3d(Point3d.Origin, Point3d.Origin);

		public static Extents2d ToExtents2d(this Extents3d ext)
		{
			return new Extents2d(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);
		}

		public static Point2d ToPoint2d(this Point3d p)
		{
			return new Point2d(p.X, p.Y);
		}

		/// <summary>
		/// Returns a copy of the Extents3d which the method is invoked on,
		/// with the given points added.
		/// </summary>
		/// <remarks>This method does not alter the Extents3d that it is invoked on.
		/// Hence, the caller must use the result in place of same. Typically, the 
		/// result is assigned to the same varible which the invocation target was 
		/// assigned to.</remarks>
		/// <param name="extents">The Extents3d to add the given points to</param>
		/// <param name="points">The points to be added to the given Extents3d</param>
		/// <returns>A copy of the given Extents3d with the points added</returns>

		public static Extents3d AddPoints(this Extents3d extents, params Point3d[] points)
		{
			return AddPoints(extents, (IEnumerable<Point3d>) points);
		}

		public static Extents3d AddPoints(this Extents3d extents, IEnumerable<Point3d> points)
		{
			Assert.IsNotNull(points, "points");
			foreach(Point3d p in points)
				extents.AddPoint(p);
			return extents;
		}

		//public static Extents3d Inflate(this Extents3d extents, Vector3d vector)
		//{
		//   extents.
		//   return new Extents3d(extents.MinPoint - vector, extents.MaxPoint + vector);
		//}

		//public static Extents3d Inflate(this Extents3d extents, double value)
		//{
		//   return Inflate(extents, new Vector3d(value, value, value));
		//}

		public static Point3d ToPoint3d(this Point2d p2d, double z = 0.0)
		{
			return new Point3d(p2d.X, p2d.Y, z);
		}

		/// <summary>
		/// ???????????
		/// </summary>
		public static Point3d ToPoint3d(this Point2d point2d, Plane plane)
		{
			Point3d[] points = plane.IntersectWith(new Line3d(point2d.ToPoint3d(), new Point3d(point2d.X, point2d.Y, 1.0)));
			return points != null && points.Length > 0 ? points[0] : Point3d.Origin;
		}

		public static bool IsIdentity(this Matrix3d m)
		{
			return m.IsEqualTo(Matrix3d.Identity);
		}

		public static Scale3d GetScaleFactors(this Matrix3d mat)
		{
			return new Scale3d(new Point3d(1.0, 1.0, 1.0).TransformBy(mat).ToArray());
		}

		/// <summary>
		/// Gets the transformation matrix that transforms from the 
		/// given CoordinateSystem3d to the coordinate system of the 
		/// matrix which the method is invoked on.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="origin"></param>
		/// <returns></returns>
		
		public static Matrix3d GetTransformFrom(this Matrix3d dest, CoordinateSystem3d origin)
		{
			CoordinateSystem3d to = dest.CoordinateSystem3d;
			if(to == origin)
				return dest;
			return Matrix3d.AlignCoordinateSystem(to.Origin, to.Xaxis, to.Yaxis, to.Zaxis,
				origin.Origin, origin.Xaxis, origin.Yaxis, origin.Zaxis);
		}

		/// <summary>
		/// Gets the transformation matrix that transforms from the 
		/// coordinate system of the given Matrix3d to the coordinate 
		/// system of the Matrix3d which the method is invoked on.
		/// </summary>
		
		public static Matrix3d GetTransformFrom(this Matrix3d dest, Matrix3d origin)
		{
			if(dest == origin)
				return dest;
			CoordinateSystem3d from = dest.CoordinateSystem3d;
			CoordinateSystem3d to = origin.CoordinateSystem3d;
			return Matrix3d.AlignCoordinateSystem(from.Origin, from.Xaxis, from.Yaxis, from.Zaxis,
				to.Origin, to.Xaxis, to.Yaxis, to.Zaxis);
		}

		/// <summary>
		/// Returns the Matrix3d that tranforms from the given coordinate
		/// system to the world coordinate system.
		/// </summary>
		/// <param name="ucs"></param>
		/// <returns></returns>
		public static Matrix3d ToWorld(this CoordinateSystem3d ucs)
		{
			return Matrix3d.Identity.GetTransformFrom(ucs);
		}

		/// <summary>
		/// Transforms a coordinate from the ECS of a block reference to
		/// the WCS (the coordinate system of the block's definition).
		/// </summary>
		/// <param name="blkref"></param>
		/// <param name="ecsPoint"></param>
		/// <returns></returns>
		
		public static Point3d ToWorld(this BlockReference blkref, Point3d ecsPoint)
		{
			return ecsPoint.TransformBy(blkref.BlockTransform.Inverse());
		}

		/// <summary>
		/// Transforms a coordinate from the WCS (and the coordinate system 
		/// of the block's definition) to the ECS of a block reference.
		/// </summary>
		
		public static Point3d ToEcs(this BlockReference blkref, Point3d worldPoint)
		{
			return worldPoint.TransformBy(blkref.BlockTransform);
		}

		//	public static void UsageExample()
		//	{
		//    IEnumerable<Entity> entities = /// (assign to a sequence of Entity)
		//    Extents3d bounds = /// (assign to a valid Extents3d)
		//    // returns the elements of entities whose bounding box intersects bounds:
		//    var results = entities.Intersecting(bounds, e => e.GeometricExtents);
		//   
		//	}

		public static IEnumerable<T> Intersecting<T>(this IEnumerable<T> source, Extents3d extents, Func<T, Extents3d> selector, bool project = false)
		{
			Assert.IsNotNull(source, "source");
			Assert.IsNotNull(selector, "selector");
			foreach(T item in source)
			{
				if(item == null)
					throw new ArgumentNullException("source element");
				if(extents.Intersects(selector(item), project))
					yield return item;
			}
		}

		public static IEnumerable<T> NotIntersecting<T>(this IEnumerable<T> source, Extents3d extents, Func<T, Extents3d> selector, bool project = false)
		{
			Assert.IsNotNull(source, "source");
			Assert.IsNotNull(selector, "selector");
			foreach(T item in source)
			{
				Assert.IsNotNull(item);
				if(!extents.Intersects(selector(item), project))
					yield return item;
			}
		}

		public static double GetBulge(this Arc arc, bool clockwise = false)
		{
			Assert.IsNotNull(arc, "arc");
			return ComputeBulge(arc.StartAngle, arc.EndAngle, clockwise);
		}

		static double ComputeBulge(double sa, double ea, bool clockwise = false)
		{
			double bulge = Math.Tan((ea - ((sa > ea) ? sa - 8 * Math.Atan(1) : sa)) / 4);
			return clockwise ? -bulge : bulge;
		}

		public static double Invert(this double value, bool flag = true)
		{
			return flag ? -value : value;
		}

		/// <summary>
		/// Return the upper-left or lower-right points of an axis-aligned box
		/// </summary>
		
		public static Point3d UpperLeftPoint(this Extents3d box)
		{
			return new Point3d(box.MinPoint.X, box.MaxPoint.Y, box.MaxPoint.Z);
		}

		public static Point3d UpperLeftCorner(this Extents3d box)
		{
			return new Point3d(box.MinPoint.X, box.MaxPoint.Y, box.MinPoint.Z);
		}

		public static Point3d LowerRightPoint(this Extents3d box)
		{
			return new Point3d(box.MaxPoint.X, box.MinPoint.Y, box.MinPoint.Z);
		}

		public static Point2d Centroid(this Extents2d extents)
		{
			return extents.MinPoint + (extents.MinPoint.GetVectorTo(extents.MaxPoint) * 0.5);
		}

		public static Plane GetLeftSidePlane(this Extents3d ext)
		{
			return new Plane(ext.MinPoint, Vector3d.YAxis, Vector3d.ZAxis);
		}

		static Point2d Centroid(Point2d min, Point2d max)
		{
			return min + (min.GetVectorTo(max) * 0.5);
		}

		static Point3d Centroid(Point3d min, Point3d max)
		{
			return min + (min.GetVectorTo(max) * 0.5);
		}

		// revise to take min/max point and make non-public
		public static Point3d Centroid(this Extents3d extents)
		{
			return extents.MinPoint + (extents.MinPoint.GetVectorTo(extents.MaxPoint) * 0.5);
		}

		// The left-side plane of an Extentds3d, with outward-facing normal,
		// and the origin at the intersection with the bottom and back sides.

		public static Plane Left(this Extents3d ext)
		{
			return new Plane(new Point3d(ext.MinPoint.X, ext.MaxPoint.Y, ext.MinPoint.Z),
				Vector3d.YAxis.Negate(), Vector3d.ZAxis);
		}

		public static Plane Right(this Extents3d ext)
		{
			return new Plane(new Point3d(ext.MaxPoint.X, ext.MinPoint.Y, ext.MinPoint.Z),
				Vector3d.YAxis, Vector3d.ZAxis);
		}

		public static Plane Front(this Extents3d ext)
		{
			return new Plane(ext.MinPoint, Vector3d.XAxis, Vector3d.ZAxis);
		}

		public static Plane Back(this Extents3d ext)
		{
			return new Plane(new Point3d(ext.MaxPoint.X, ext.MaxPoint.Y, ext.MinPoint.Z),
				Vector3d.YAxis.Negate(), Vector3d.ZAxis);
		}

		public static Plane Top(this Extents3d ext)
		{
			return new Plane(new Point3d(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.Z),
				Vector3d.XAxis, Vector3d.YAxis);
		}

		public static Plane Bottom(this Extents3d ext)
		{
			return new Plane(new Point3d(ext.MinPoint.X, ext.MaxPoint.Y, ext.MinPoint.Z),
				Vector3d.XAxis, Vector3d.YAxis.Negate());
		}

		public static Plane[] Sides(this Extents3d extents)
		{
			if(!extents.IsValid())
				throw new ArgumentException("Invalid extents");
			return new[] { extents.Front(), extents.Right(), extents.Back(), extents.Left(), extents.Bottom(), extents.Top() };
		}

		public static Point2d[] Corners(this Extents2d extents)
		{
			return new Point2d[]{
				extents.MinPoint, 
				new Point2d(extents.MaxPoint.X, extents.MinPoint.Y),
				extents.MaxPoint,
				new Point2d(extents.MinPoint.X, extents.MaxPoint.Y)};
		}

		public static LineSegment2d[] Sides(this Extents2d extents)
		{
			Point2d[] points = extents.Corners();
			return new LineSegment2d[]
			{
				new LineSegment2d(points[0], points[1]),
				new LineSegment2d(points[1], points[2]),
				new LineSegment2d(points[2], points[3]),
				new LineSegment2d(points[3], points[0])};
		}

		/// <summary>
		/// Sorts a sequence of objects by the distance from a selector-provided 
		/// coordinate to the given plane. The selector is passed each element to
		/// be sorted, and returns the coordinate used to compute a distance from
		/// the given plane. Objects are sorted based on the signed distance from
		/// the given plane, such that objects whose computed coordinate is on the
		/// negative side of the plane preceed those whose computed coordinate is
		/// on the positive side of the plane.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source"></param>
		/// <param name="plane"></param>
		/// <param name="selector"></param>
		/// <returns></returns>
		
		public static IEnumerable<T> OrderByDistanceFrom<T>(this IEnumerable<T> source, Plane plane, Func<T, Point3d> selector)
		{
			return source.OrderBy(t => plane.GetSignedDistanceTo(selector(t)));
		}

		/// <summary>
		/// Orders a sequence of objects by the relative position of a 
		/// 3D coordinate derived from each object by a selector function,
		/// measured along a specified axis.
		/// 
		/// The direction argument defines the axis of measurement used 
		/// to determine the relative locations of the coordinates. 
		/// </summary>
		/// <remarks>
		/// To reverse the ordering, use the inverted direction vector 
		/// (e.g., Vector3d.Negate).</remarks>
		/// <typeparam name="T">The type of objects in the input and 
		/// result sequence.</typeparam>
		/// 
		/// <param name="source">The input sequence of objects</param>
		/// 
		/// <param name="selector">A function that takes an element from
		/// the source sequence, and returns a 3D coordinate representing 
		/// the element's position in 3D space</param>
		/// 
		/// <param name="direction">A 3d vector that defines the axis 
		/// along which the coordinate locations are measured.</param>
		/// 
		/// <returns>The ordered sequence of objects</returns>
		/// 
		/// <example>
		/// 
		///   Sorts a sequence of Circle entities by the position of
		///   each Circle's center point, measured along the X-axis:
		///   
		///   Circle[] circles = GetCircles();  // returns an array of Circle[]
		///   
		///   // Return a sequence consisting of all the Circles
		///   // in the circles array, ordered by the position of
		///   // each circle's Center point, measured along the
		///   // X axis of the WCS:
		///   
		///   circles.OrderBy(circle => circle.Center, Vector3d.XAxis)
		///   
		/// </example>

		public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source, 
			Func<T, Point3d> selector, 
			Vector3d direction)
		{
			if(source == null)
				throw new ArgumentNullException("source");
			if(selector == null)
				throw new ArgumentNullException("selector");
			Plane plane = new Plane(Point3d.Origin, direction);
			return source.OrderBy(item => plane.GetSignedDistanceTo(selector(item)));
		}

		/// <summary>
		/// Alternative to the above that optimizes common use cases where 
		/// the direction is parallel to one of the three principle axes.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source"></param>
		/// <param name="selector"></param>
		/// <param name="direction"></param>
		/// <returns></returns>
		
		public static IEnumerable<T> OrderByPosition2<T>(this IEnumerable<T> source,
			Func<T, Point3d> selector,
			Vector3d direction)
		{
			if(source == null)
				throw new ArgumentNullException("source");
			if(selector == null)
				throw new ArgumentNullException("selector");
			Func<T, double> func = null;
			direction = direction.GetNormal();
			if(direction.IsCodirectionalTo(Vector3d.XAxis))
				func = item => selector(item).X;
			else if(direction.IsCodirectionalTo(Vector3d.XAxis.Negate()))
				func = item => - selector(item).X;
			else if(direction.IsCodirectionalTo(Vector3d.YAxis))
				func = item => selector(item).Y;
			else if(direction.IsCodirectionalTo(Vector3d.YAxis.Negate()))
				func = item => - selector(item).Y;
			else if(direction.IsCodirectionalTo(Vector3d.ZAxis))
				func = item => selector(item).Z;
			else if(direction.IsCodirectionalTo(Vector3d.ZAxis.Negate()))
				func = item => - selector(item).Z;
			else
			{
				Plane plane = new Plane(Point3d.Origin, direction);
				func = item => plane.GetSignedDistanceTo(selector(item));
			}
			return source.OrderBy(func);
		}

		/// <summary>
		/// Orders a sequence of objects by the relative position of a 3D 
		/// coordinate derived from each object using a selector function,
		/// measured along a specified axis, and sub-sorts objects based 
		/// on the relative position of the 3D coordinate measured along 
		/// a specified secondary axis.
		/// </summary>
		/// <typeparam name="T">The type of the objects in the sequence</typeparam>
		/// <param name="source">The sequence of source objects</param>
		/// <param name="selector">A function that produces world coordinates 
		/// representing the position of each object in the source sequence</param>
		/// <param name="direction">The axis of measurement used to 
		/// determine the primary sort key</param>
		/// <param name="secondaryDirection">The axis of measurement 
		/// used to determine the secondary sort key</param>
		/// <param name="tolerance">The tolerance used to define equality 
		/// in the primary sort</param>
		/// <returns>The sorted sequence</returns>
		/// <example>
		/// 
		///  Orders a sequence of Circles by the position of each circle's 
		///  center point measured along the X axis, and then by the position 
		///  of each Circle's center point measured along the Y axis:
		/// 
		///    Circle[] circles = GetCircles(); // returns an array of Circle[]
		///   
		///    circles.OrderBy(circle => circle.Center, Vector3d.XAxis, Vector3d.YAxis)
		/// 
		/// </example>

		public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> source,
			Func<T, Point3d> selector,
			Vector3d direction,
			Vector3d? secondaryDirection = null,
			Func<T, Point3d> secondarySelector = null,
			Tolerance? tolerance = null)
		{
			if(source == null)
				throw new ArgumentNullException("source");
			if(selector == null)
				throw new ArgumentNullException("selector");
			Plane plane = new Plane(Point3d.Origin, direction);
			if(!secondaryDirection.HasValue)
			{
				return source.OrderBy(item => plane.GetSignedDistanceTo(selector(item)));
			}
			Plane plane2 = new Plane(Point3d.Origin, secondaryDirection.Value);
			IComparer<double> comparer = new DistanceComparer(tolerance.GetValueOrDefault(Tolerance.Global));
			if(secondarySelector == null)
			{
				return source.Select(item => new {Item = item, Position = selector(item)})
					.OrderBy(item => plane.GetSignedDistanceTo(item.Position), comparer)
					.ThenBy(item => plane2.GetSignedDistanceTo(item.Position))
					.Select(result => result.Item);
			}
			else
			{
				return source.OrderBy(item => plane.GetSignedDistanceTo(selector(item)), comparer)
					.ThenBy(item => plane2.GetSignedDistanceTo(secondarySelector(item)));
			}
		}

		/// <summary>
		/// Note: this was to be depreciated in favor of the above, but
		/// it can prove useful when sorting along both the X and Y axes, 
		/// where each axis requires a different sort key. An example 
		/// would be to sort rectangles by the position of their left side 
		/// measured along the X axis, and then by the position of their 
		/// top side measured along the Y axis. In that case, the primary
		/// selector returns the lower-left corner of each rectangle, and
		/// the secondary selector returns the upper-left (or upper-right)
		/// corner of each rectangle.
		/// 
		/// Sorts a sequence of objects by the relative position of a 3D 
		/// coordinate derived from each object using a selector function,
		/// and sub-sorts the objects based on the the relative position 
		/// of a 3D coordinate derived from each object using a secondary
		/// selector function, measured along a specified secondary axis.
		/// </summary>
		/// <typeparam name="T">The type of the objects in the sequence</typeparam>
		/// <param name="source">The sequence of source objects</param>
		/// <param name="primarySelector">A function that produces coordinates for
		/// the primary sort</param>
		/// <param name="secondarySelector">The function that produces coordinates 
		/// for the secondary sort. If null, the coordinate produced by the selector 
		/// for the primary sort is used.</param>
		/// <param name="primaryDirection">The axis of measurement used to determine 
		/// the primary sort key</param>
		/// <param name="secondaryDirection">The axis of measurement used to determine 
		/// the secondary sort key</param>
		/// <param name="tolerance">The tolerance used to determine equality 
		/// in the primary sort</param>
		/// <returns>The sorted sequence</returns>
		/// <example>
		/// 
		///  Sorts a sequence of Circles by the position of each circle's 
		///  center point measured along the X axis, and then by the position 
		///  of each Circle's center point measured along the Y axis, using
		///  a default tolerance of 1.0e-6 for the primary sort:
		/// 
		///    Circle[] circles = GetCircles(); // returns an array of Circle[]
		///   
		///    circles.OrderByPosition(cir => cir.Center, Vector3d.XAxis, Vector3d.YAxis)
		/// 
		/// </example>
		
		public static IEnumerable<T> OrderByPosition<T>(this IEnumerable<T> source, 
			Func<T, Point3d> primarySelector, 
			Vector3d primaryDirection, 
			Vector3d secondaryDirection,
			Func<T, Point3d> secondarySelector = null,
			Tolerance? tolerance = null)
		{
			if(source == null)
				throw new ArgumentNullException("source");
			if(primarySelector == null)
				throw new ArgumentNullException("primarySelector");
			Plane primaryPlane = new Plane(Point3d.Origin, primaryDirection);
			Plane secondaryPlane = new Plane(Point3d.Origin, secondaryDirection);
			if(primaryPlane.IsEqualTo(secondaryPlane) && secondarySelector == null)
				return OrderBy(source, primarySelector, primaryDirection);
			if(secondarySelector == null)
			{
				// Ok to use DistanceComparer here, because it doesn't use hash codes
				IComparer<double> comparer = new DistanceComparer(tolerance.GetValueOrDefault(Tolerance.Global));
				return source.Select(item => new {Item = item, Key = primarySelector(item)})
					.OrderBy(a => primaryPlane.GetSignedDistanceTo(a.Key), comparer)
					.ThenBy(b => secondaryPlane.GetSignedDistanceTo(b.Key))
					.Select(result => result.Item);
			}
			else
			{
				return source.OrderBy(x => primaryPlane.GetSignedDistanceTo(primarySelector(x)))
					.ThenBy(y => secondaryPlane.GetSignedDistanceTo(secondarySelector(y)));
			}
		}

		/// https://docs.microsoft.com/en-us/dotnet/api/system.math.round?view=netframework-4.7.1#Precision
		
		static double RoundApproximate(double dbl, int digits, double margin, MidpointRounding mode)
		{
			double fraction = dbl * Math.Pow(10, digits);
			double value = Math.Truncate(fraction);
			fraction = fraction - value;
			if(fraction == 0)
				return dbl;

			double tolerance = margin * dbl;
			// Determine whether this is a midpoint value.
			if((fraction >= .5 - tolerance) & (fraction <= .5 + tolerance))
			{
				if(mode == MidpointRounding.AwayFromZero)
					return (value + 1) / Math.Pow(10, digits);
				else
					if(value % 2 != 0)
						return (value + 1) / Math.Pow(10, digits);
					else
						return value / Math.Pow(10, digits);
			}
			// Any remaining fractional value greater than .5 is not a midpoint value.
			if(fraction > .5)
				return (value + 1) / Math.Pow(10, digits);
			else
				return value / Math.Pow(10, digits);
		}


		/// <summary>
		/// Returns the elements in source whose derived coordinate 
		/// lies on the given Line3d.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source sequence of objects</param>
		/// <param name="line">The line which the coordinates derived the 
		/// of resulting objects lie on</param>
		/// <param name="selector">A delegate that takes an instance of the 
		/// source object, and returns a coordinate derived from its argument.</param>
		/// <param name="tolerance">An optional tolerance used to determine if
		/// the derived coordinates lie on the given line.</param>
		/// <returns>The sequence of objects whose derived coordinates
		/// lie on the given Line3d.</returns>

		public static IEnumerable<T> OnLine<T>(this IEnumerable<T> source, Line3d line, Func<T, Point3d> selector, double? tolerance = null)
		{
			if(source == null)
				throw new ArgumentNullException("source");
			if(selector == null)
				throw new ArgumentNullException("selector");
			Tolerance tol = tolerance.HasValue ? new Tolerance(tolerance.Value, tolerance.Value) : Tolerance.Global;
			return source.Where(item => line.IsOn(selector(item), tol));
		}

		public static IEnumerable<T> OnLine<T>(this IEnumerable<T> source, Point3d point1, Point3d point2, Func<T, Point3d> selector, double? tolerance = null)
		{
			if(point1.IsEqualTo(point2))
				throw new ArgumentException("coincident coordinates");
			return OnLine(source, new Line3d(point1, point2), selector, tolerance);
		}


		/// <summary>
		/// Returns a point on the line passing throuwh line1 and line2, where it
		/// intersects a perpendicular line passing through the point which the
		/// method is invoked one.
		/// </summary>
		
		static Point3d GetPerpendicularPoint(this Point3d point, Point3d line1, Point3d line2)
		{
			return new Line3d(line1, line2).GetClosestPointTo(point).Point;
		}

		/// <summary>
		/// Gets a vector perpendicular to the line passing through line1 and line2
		/// from the point which the method is invoked on, to the point where the
		/// perpendicular vector intersects the line through line1 and line2.
		/// </summary>
		/// <param name="point"></param>
		/// <param name="line1"></param>
		/// <param name="line2"></param>
		/// <returns></returns>
		static Vector3d GetPerpendicularVectorTo(this Point3d point, Point3d line1, Point3d line2)
		{
			return point.GetVectorTo(new Line3d(line1, line2).GetClosestPointTo(point).Point);
		}

		static Vector3d GetPerpendicularVectorFrom(this Point3d p, Point3d line1, Point3d line2)
		{
			return new Line3d(line1, line2).GetClosestPointTo(p).Point.GetVectorTo(p);
		}

		/// <summary>
		/// Describes the cartesian relationship between
		/// a point and an axis-aligned rectangle.
		/// 
		/// The enum value indicates if a point lies above, within, 
		/// or below both the horizontal and vertical extents of the 
		/// rectangle discretely, and if the point is above or below 
		/// and to the left or right of the centroid.
		/// </summary>
		[Flags]
		enum Alignment
		{
			None = 0,
			/// Point X ordinate is below the X extents of the rectangle
			XBelow = 1,
			/// Point X ordinate is within the X extents of the rectangle
			XInside = 2,
			/// Point X ordinate is above the X extents of the rectangle
			XAbove = 4,
			/// Point Y ordinate is below the Y extents of the rectangle
			YBelow = 8,
			/// Point Y ordinate is within the Y extents of the rectangle
			YInside = 0x10,
			/// Point Y ordinate is above the Y extents of the rectangle
			YAbove = 0x20,

			ZBelow = 0x40,
			ZInside = 0x80,
			ZAbove = 0x100,

			MaskZ = ZBelow|ZInside|ZAbove,

			/// Mask out 3d flags
			Mask2d = XBelow | XInside | XAbove 
				| YBelow | YInside | YAbove,


			/// Mask out Centroid flags
			Extents = XBelow | XInside | XAbove 
				| YBelow | YInside | YAbove 
				| ZBelow | ZInside | ZAbove,

			/// indicates the cartesion relationship of the point
			/// to the centroid.

			NegativeX = 0x200,
			PositiveX = 0x400,
			NegativeY = 0x800,
			PositiveY = 0x1000,
			NegativeZ = 0x2000,
			PositiveZ = 0x4000,

			/// Mask out Extents flags
			Centroid = NegativeX | PositiveX | NegativeY | PositiveY | NegativeZ | PositiveZ,

			/// Point is inside the box (or rectangle if (value & Alignment.Mask2d) is used).
			Inside = XInside | YInside | ZInside, // 2d ?
			Inside2d = XInside | YInside,
			Inside3d = Inside2d | ZInside,
			/// Point is to the left of the left side of the rectangle, 
			/// and within its vertical extents
			/// BelowYZPlane (inside the rectangle of the YZ plane and below the YZ plane)
			Left = YInside | XBelow | ZInside,  // again, 2D ? (for 2d, ZInside is always set?)
			/// Point is to the right of the right side of the rectangle, 
			/// and within its vertical extents
			/// AboveYZPlane
			Right = YInside | XAbove,
			/// Point is above the top of the rectangle, 
			/// and within its X extents.
			Above = XInside | YAbove,
			/// Point is below the the bottom of the rectangle, 
			/// and within its horizontal extents.
			Below = XInside | YBelow,
			/// Point is below the bottom of the rectangle, 
			/// and to the right of the right side of the rectangle
			BelowRight = XAbove | YBelow,
			/// Point is below the bottom of the rectangle, 
			/// and to the left of the left side of the rectangle
			BelowLeft = XBelow | YBelow,
			/// Point is above the top of the rectangle, 
			/// and to the right of the right side of the rectangle
			AboveRight = XAbove | YAbove,
			/// Point is above the top of the rectangle, 
			/// and to the left of the left side of the rectangle
			AboveLeft = XBelow | YAbove,


		}

		static Alignment GetAlignment(Point2d min, Point2d max, Point2d p)
		{
			Alignment result = Alignment.MaskZ; // for 2d applications, all 
			                                    // extents-related "Z flags" are set
			                                    
			if(p.X < min.X)
				result = Alignment.XBelow;
			else if(p.X > max.X)
				result = Alignment.XAbove;
			else
				result = Alignment.XInside;

			if(p.Y < min.Y)
				result |= Alignment.YBelow;
			else if(p.Y > max.Y)
				result |= Alignment.YAbove;
			else
				result |= Alignment.YInside;

			//result |= p.X < min.X + ((max.X - min.X) * 0.5) ? Alignment.NegativeX : Alignment.PositiveX;
			//result |= p.Y < min.Y + ((max.Y - min.Y) * 0.5) ? Alignment.NegativeY : Alignment.PositiveY;
			result |= p.X < ((min.X + max.X) * 0.5) ? Alignment.NegativeX : Alignment.PositiveX;
			result |= p.Y < ((max.Y + min.Y) * 0.5) ? Alignment.NegativeY : Alignment.PositiveY;
			return result;
		}

		static Point2d ClosestPointTo(Point2d min, Point2d max, Point2d p)
		{
			//Doesn't work if centroid mask is not applied
			Alignment alignment = GetAlignment(min, max, p);
			switch(alignment & Alignment.Extents)
			{
				case Alignment.Left:
					return new Point2d(min.X, p.Y);
				case Alignment.Right:
					return new Point2d(max.X, p.Y);
				case Alignment.Above:
					return new Point2d(p.X, max.Y);
				case Alignment.Below:
					return new Point2d(p.X, min.Y);
				case Alignment.BelowLeft:
					return min;
				case Alignment.BelowRight:
					return new Point2d(max.X, min.Y);
				case Alignment.AboveLeft:
					return new Point2d(min.X, max.Y);
				case Alignment.AboveRight:
					return max;
				//case Alignment.Inside:
				//   double dx = max.X - min.X;
				//   double dy = max.Y - min.Y;
				//   double dx2 = dx * 0.5;
				//   double dy2 = dy * 0.5;
				//   double px = dx - p.X;
				//   double py = dy - p.Y;
				//   if(px > dx2)
				//   {
				//      if(p
				//   }
				//   Point2d centroid = Centroid(min, max);
				//   if(p.X < ((max.X - min.X) * 0.5))
				//   {
				//   }
				default:
					throw new ArgumentException("Invalid alignment");
			}

		}

		/// <summary>
		/// Return the upper-left or lower-right points of an axis-aligned rectangle
		/// </summary>
		
		public static Point2d UpperLeftPoint(this Extents2d ext)
		{
			return new Point2d(ext.MinPoint.X, ext.MaxPoint.Y);
		}

		public static Point2d LowerRightPoint(this Extents2d ext)
		{
			return new Point2d(ext.MaxPoint.X, ext.MinPoint.Y);
		}

		public static Vector3d Size(this Extents3d ext)
		{
			return ext.MaxPoint - ext.MinPoint;
		}

		public static Vector2d Size(this Extents2d ext)
		{
			return ext.MaxPoint - ext.MinPoint;
		}

		public static double Dimension(this Extents3d ext, int dim)
		{
			if(dim < 0 || dim > 2)
				throw new IndexOutOfRangeException("dim");
			return ext.MaxPoint[dim] - ext.MinPoint[dim];
		}

		public static double Dimension(this Extents3d ext, Dimension dimension)
		{
			return Dimension(ext, (int) dimension);
		}

		public static Point3d MidwayTo(this Point3d point, Point3d other)
		{
			return point + (point.GetVectorTo(other) * 0.5);
		}

		public static Point3d[] Divide(this Point3d from, Point3d to, int divisions)
		{
			Vector3d v = from.GetVectorTo(to) / divisions;
			Point3d[] result = new Point3d[divisions + 1];
			int cnt = divisions + 1;
			for(int i = 0; i < cnt; i++)
			{
				result[i] = from + (v * i);
			}
			return result;
		}

		//public static Point3d[] DivideDuh(Curve3d curve, int divisions)
		//{
		//	double len = curve.GetLength(curve.GetParameterOf(curve.StartPoint), curve.GetParameterOf(curve.EndPoint));
		//	Point3d[] result = new Point3d[divisions + 1];
		//	for(int i = 0; i < divisions; i++)
		//	{
		//		curve.getp
		//		result[i] = curve.GetPointAtDist(len * i);
		//	}
		//}

		public static double Altitude(this double baseWidth, double slopeAngle)
		{
			return ((Math.Sin(slopeAngle) * baseWidth) * (1.0 / Math.Cos(slopeAngle)));
		}

		public static double AngleFromXAxis(this Point3d point)
		{
			return new Vector2d(point.X, point.Y).Angle;
		}

		//public static Point2d AsPoint2d(this Point3d point)
		//{
		//   return new Point2d(point.X, point.Y);
		//}

		//public static Point3d AsPoint3d(this Point2d point, double z = 0.0)
		//{
		//   return new Point3d(point.X, point.Y, z);
		//}

		public static double BaseWidth(this double altitude, double phi)
		{
			double a = halfPi - phi;
			return ((Math.Sin(a) * altitude) * (1.0 / Math.Cos(a)));
		}

		public static Point3d Delta(this Point3d p, double x, double y, double z = 0.0)
		{
			return new Point3d(p.X + x, p.Y + y, p.Z + z);
		}

		/// <summary>
		/// Why is a normal required for this?
		/// </summary>
		
		public static double GetIncludedAngle(this Point3d vertex, Vector3d normal, Point3d endPoint1, Point3d endPoint2)
		{
			if(endPoint1.IsEqualTo(endPoint2))
				return 0.0;
			Vector3d v1 = vertex.GetVectorTo(endPoint1).GetNormal();
			Vector3d v2 = vertex.GetVectorTo(endPoint2).GetNormal();
			// v1.GetAngleTo(v2
			//Plane p = new Plane(endPoint1, vertex, endPoint2); /// ???
			//Vector3d norm = v1.CrossProduct(v2); // ??
			return v1.AngleOnPlane(new Plane(vertex, v2, v2.RotateBy(0.78539816339744828, normal)));
		}

		public static Vector3d GetNormalizedVectorTo(this Point3d origin, Point3d point)
		{
			return origin.GetVectorTo(point).GetNormal();
		}

		public static Point3d MidPointTo(this Point3d from, Point3d to)
		{
			return new LineSegment3d(from, to).EvaluatePoint(0.5);
		}

		public static Point3d OffsetZ(this Point3d point, double z)
		{
			return new Point3d(point.X, point.Y, point.Z + z);
		}

		public static Point3d PointAtParamTo(this Point3d from, Point3d to, double parameter)
		{
			return new LineSegment3d(from, to).EvaluatePoint(parameter);
		}

		public static LineSegment3d Reverse(this LineSegment3d line)
		{
			Assert.IsNotNull(line, "line");
			return (LineSegment3d) line.GetReverseParameterCurve();
		}

		/// <summary>
		/// Converts a double representing an angle in radians to degrees
		/// </summary>
		/// <param name="radians">The angle in radians</param>
		/// <returns>The angle in degrees</returns>
		
		public static double ToDegrees(this double radians)
		{
			return ((radians / 3.1415926535897931) * 180.0);
		}

		/// <summary>
		/// Converts a double representing an angle in degrees to radians
		/// </summary>
		/// <param name="degrees">The angle in degrees</param>
		/// <returns>The angle in radians</returns>
		
		public static double ToRadians(this double degrees)
		{
			return ((degrees / 180.0) * 3.1415926535897931);
		}

		//public static Vector3d Normalize(this Vector3d vector)
		//{
		//   if(vector.IsZeroLength())
		//      throw new ArgumentException("Zero-length vector");
		//   return vector.IsUnitLength() ? vector : vector * (1.0 / vector.Length);
		//}

		/// <summary>
		/// 2D Polar specificiation (angle/distance)
		/// </summary>
		/// <param name="basepoint"></param>
		/// <param name="AngleInXYPlane"></param>
		/// <param name="distance"></param>
		/// <returns></returns>

		public static Point3d To(this Point3d basepoint, double AngleInXYPlane, double distance)
		{
			return new Point3d(
				basepoint.X + (distance * Math.Cos(AngleInXYPlane)),
				basepoint.Y + (distance * Math.Sin(AngleInXYPlane)),
				basepoint.Z);
		}

		public static Point2d To(this Point2d basePoint, Vector2d direction, double distance)
		{
			return basePoint + direction.GetNormal() * distance;
		}

		public static Point3d To(this Point3d basePoint, Vector3d direction, double distance)
		{
			return basePoint + direction.GetNormal() * distance;
		}

		public static void SetLength(this LineSegment2d line, double newLength, bool moveEndPoint = true)
		{
			if(moveEndPoint)
				line.Set(line.StartPoint, line.StartPoint + line.Direction.GetNormal() * newLength);
			else
				line.Set(line.EndPoint + line.Direction.GetNormal().Negate() * newLength, line.EndPoint);
		}

		/// <summary>
		/// Spherical coordinate specification
		/// </summary>
		/// <param name="center">Basepoint</param>
		/// <param name="phi">Angle in XY plane</param>
		/// <param name="theta">Angle from XY plane</param>
		/// <param name="radius">Distance</param>
		/// <returns>The spherical coordinate</returns>

		public static Point3d To(this Point3d center, double phi, double theta, double radius)
		{
			double phicos = Math.Cos(phi);
			return new Point3d(center.X + ((radius * phicos) * Math.Cos(theta)), center.Y + ((radius * phicos) * Math.Sin(theta)), center.Z + (radius * Math.Sin(phi)));
		}

		/// <summary>
		/// Given the length of one side of a right-triangle (X),
		/// and the angle between the same side and the hypotenuse,
		/// this returns the length of the other side (Y).
		/// </summary>
		/// <param name="X">length of one side of right triangle</param>
		/// <param name="phi">adjacent angle between the side whose
		/// length is X, and the hypotenuse</param>
		/// <returns>length of the other side of the triangle</returns>

		public static double Rise(this double X, double phi)
		{
			return Math.Sin(phi) * X * (1.0 / Math.Cos(phi));
		}

		/// <summary>
		/// The lenfth of the 
		/// </summary>
		/// <param name="X"></param>
		/// <param name="phi"></param>
		/// <returns></returns>
		//public static double Run(this double X, double phi)
		//{
		//   return (Math.PI / 4.0) - Rise(X, phi);
		//}

		////////////////////////////////////////////////////////////
		/// 
		/// Extension methods targeting Point3d, Vector3d, 
		/// PlanarEntity, and LinearEntity3d, that compute 
		/// angles between lines or vectors and planes. 
		/// 
		/// These methods don't do very much other than allow the
		/// consuming code to be more expressive in the sense that
		/// they make the intent of the computations which they 
		/// encapsulate more obvious, and provide a common default 
		/// tolerance used to determine if a vector or line is
		/// parallel to a plane. Most methods defer to one of the 
		/// core methods that call Math.ATan2().
		/// 
		/// For performing numerous angle computations against a 
		/// common argument, it is recommended that these methods 
		/// not be used iteratively, as doing so would be highly-
		/// redundant. 
		/// 
		/// Instead, distill the common argument to the form used 
		/// by either of the two core methods (the ones that call 
		/// Math.ATan2), cache that and use it with one of those 
		/// core methods or the code they encapsulate.
		/// 
		/// Because the operations are limited to calculating an
		/// angle between an abstract plane and a line or vector,
		/// these extension methods target the PlanarEntity type,
		/// the abstract base type of both Plane and BoundedPlane,
		/// allowing them to used with either of same.
		///
		/// Because the operations are limited to calculating an
		/// angle between an abstract plane and a line or vector,
		/// these extension methods target the LinearEntity3d type,
		/// the abstract base type of Line3d, LineSegment3d, and
		/// Ray3d, allowing them to used with any of same. In all
		/// cases, methods returning signed angles always use the 
		/// direction of increasing parameter value.
		///
		/// All methods by default, use the tolerance defined below
		/// to determine if lines/vectors are parallel to the plane
		/// from which the angle is computed.
		/// 
		/// All methods that compute an angle from the XY plane
		/// return signed angles. Those that compute angles from
		/// other planes return absolute angles, except for those
		/// whose names start with "GetSignedAngle".
		/// 
		/// All returned values are angles in radians.
		/// 
		/// All plane-line/vector angle computations are derived 
		/// from this simple calculation:
		/// 
		/// Given any line whose endpoints are p1 and p2, the 
		/// angle 'theta' between the line and the XY plane is
		/// calculated thusly:
		/// 
		///   Point3d p1 = // assign to start point of line
		///   Point3d p2 = // assign to end point of line.
		///   
		///   // calculate delta X/Y/Z between the two points:
		///   
		///   double dx = p2.X - p1.X;  
		///   double dy = p2.Y - p1.Y;
		///   double dz = p2.Z - p1.Z;
		///   
		///   // calculate the angle from the XY plane:
		///   
		///   double theta = Math.ATan2(dz, Math.Sqrt((dx * dx) + (dy * dy)));
		///   
		/// Which can also be done more easily using a vector:
		/// 
		///   Vector3d v = p1.GetVectorTo(p2);
		///   double theta = Math.ATan2(v.Z, Math.Sqrt((v.X * v.X) + (v.Y * v.Y)));
		///   
		/// For simple needs such as obtaining the angle of a line
		/// from the XY plane, the extension methods below are not
		/// really needed, as the code shown above does just that.
		/// The extension methods are mostly useful for obtaining
		/// the angle from any given plane to any given line.

		/// <summary>
		/// The minimum absolute difference between the distances 
		/// of two points to a plane, for a line passing through 
		/// the two points to be considered non-parallel to the 
		/// plane. This value is used by all methods that compute
		/// the angle between a line and plane, to determine if
		/// the line is parallel to the plane within the given
		/// tolerance. In the case, the resulting angle is 0.0.
		/// 
		/// If line endpoints or points are extremely remote to
		/// each other this value may be unsuitable, and should
		/// be adjusted to suit.  The default value is the value
		/// of Tolerance.Global.EqualVector.
		/// 
		/// </summary>

		public static double ParallelEqualVector
		{
			get
			{
				return parallelEqualVector;
			}
			set
			{
				parallelEqualVector = value;
			}
		}

		/// <summary>
		/// Used to determine if a line passing through two points 
		/// is parallel to the XY plane. This value is the minimum
		/// absolute difference between the elevations of a line's 
		/// endpoints, in order for the line to not be considered 
		/// parallel to the XY plane.
		/// </summary>
		
		public static double ParallelEqualPoint
		{
			get
			{
				return parallelEqualPoint;
			}
			set
			{
				parallelEqualPoint = value;
			}
		}

		static double parallelEqualPoint = Tolerance.Global.EqualPoint;
		static double parallelEqualVector = Tolerance.Global.EqualVector;

		static bool IsZero(double delta, double tol = double.NaN)
		{
			return Math.Abs(delta) <= (double.IsNaN(tol) ? parallelEqualVector : tol);
		}

		static bool IsZero(Vector3d vec, double tol = double.NaN)
		{
			return vec.GetNormal().Z <= (double.IsNaN(tol) ? parallelEqualVector : tol);
		}

		/// <summary>
		/// The signed angle (theta) between the XY plane and
		/// the given vector in the range of +/- PI/2.
		/// </summary>
		
		public static double AngleFromXYPlane(this Vector3d v, double tolerance = double.NaN)
		{
			return IsZero(v, tolerance) ? 0.0 : Math.Atan2(v.Z, Math.Sqrt((v.X * v.X) + (v.Y * v.Y)));
		}

		/// <summary>
		/// The signed angle (theta) between the XY plane and
		/// a line passing through the two given points in the 
		/// range of +/- PI/2.
		/// </summary>

		public static double AngleFromXYPlaneTo(this Point3d p1, Point3d p2, double tolerance = double.NaN)
		{
			return AngleFromXYPlane(p1.GetVectorTo(p2), tolerance);
		}

		/// <summary>
		/// The signed angle (theta) between the XY plane and
		/// a vector to the given point in the range of +/- PI/2
		/// If the given point is below the XY plane the result 
		/// is negative.
		/// </summary>

		public static double AngleFromXYPlane(this Point3d point, double tolerance = double.NaN)
		{
			return AngleFromXYPlane(point.GetAsVector(), tolerance);
		}

		/// <summary>
		/// Returns absolute angle between the PlanarEntity
		/// the method is invoked on, and a line passing 
		/// through the two given points.
		/// </summary>

		public static double GetAngleTo(this PlanarEntity plane, Point3d p1, Point3d p2, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			if(p1.IsEqualTo(p2))
				return 0.0;
			return Math.Abs(AngleFromXYPlane(p1.GetVectorTo(p2).TransformBy(plane.ToWorld()), tolerance));
		}

		/// <summary>
		/// Returns the absolute angle between the PlanarEntity 
		/// the method is invoked on, and the given Vector3d.
		/// </summary>
		
		public static double GetAngleTo(this PlanarEntity plane, Vector3d vector, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			return Math.Abs(AngleFromXYPlane(vector.TransformBy(plane.ToWorld()), tolerance));
		}

		/// <summary>
		/// Returns the absolute angle between the vector the method 
		/// is invoked on, and the given plane.
		/// </summary>
		
		public static double GetAngleFrom(this Vector3d vector, PlanarEntity plane, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			return Math.Abs(AngleFromXYPlane(vector.TransformBy(plane.ToWorld()), tolerance));
		}

		/// <summary>
		/// Returns the signed angle between the given plane 
		/// and the line passing through the given points:
		/// </summary>

		public static double GetSignedAngleTo(this PlanarEntity plane, Point3d p1, Point3d p2, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			if(p1.IsEqualTo(p2))
				return 0.0;
			return AngleFromXYPlane(p1.GetVectorTo(p2).TransformBy(plane.ToWorld()), tolerance);
		}

		/// <summary>
		/// Returns the signed angle between the given plane 
		/// and the given vector:
		/// </summary>

		public static double GetSignedAngleTo(this PlanarEntity plane, Vector3d vector, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			return AngleFromXYPlane(vector.TransformBy(plane.ToWorld()), tolerance);
		}
		/// <summary>
		/// Returns the signed angle from the XY plane 
		/// to the given line segment.
		/// </summary>

		public static double GetAngleFromXYPlane(this LinearEntity3d line, double tolerance = double.NaN)
		{
			if(line == null)
				throw new ArgumentNullException("line");
			return AngleFromXYPlane(line.Direction, tolerance);
		}

		/// <summary>
		/// Returns the absolute angle between the given plane 
		/// and the given line.
		/// </summary>

		public static double GetAngleTo(this PlanarEntity plane, LinearEntity3d line, double tolerance = double.NaN)
		{
			return GetAngleTo(plane, line.Direction, tolerance);
		}

		/// <summary>
		/// Returns the signed angle between the plane the method
		/// is invoked on and the given line.
		/// </summary>

		public static double GetSignedAngleTo(this PlanarEntity plane, LinearEntity3d line, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			if(line == null)
				throw new ArgumentNullException("line");
			return AngleFromXYPlane(line.Direction.TransformBy(plane.ToWorld()), tolerance);
		}

		/// <summary>
		/// Returns the signed angle between the line the 
		/// method is invoked on and the given plane.
		/// </summary>
		
		public static double GetSignedAngleFrom(this LinearEntity3d line, PlanarEntity plane, double tolerance = double.NaN)
		{
			if(plane == null)
				throw new ArgumentNullException("plane");
			if(line == null)
				throw new ArgumentNullException("line");
			return AngleFromXYPlane(line.Direction.TransformBy(plane.ToWorld()), tolerance);
		}

		/// Get the plane-to-world transform of a PlanarEntity
		/// Supports any PlanarEntity (e.g., Plane and BoundedPlane);

		public static Matrix3d ToWorld(this PlanarEntity planarEntity)
		{
			if(planarEntity == null)
				throw new ArgumentNullException("plane");
			//Plane plane = planarEntity as Plane;
			//if(plane != null)
			//   return Matrix3d.PlaneToWorld(plane);
			var pcs = planarEntity.GetCoordinateSystem();
			var wcs = Matrix3d.Identity.CoordinateSystem3d;
			//if(pcs.IsEqualTo(wcs))
			//   return Matrix3d.Identity;
			//return Matrix3d.Identity.GetTransformFrom(pcs);
			return Matrix3d.AlignCoordinateSystem(pcs.Origin, pcs.Xaxis, pcs.Yaxis, pcs.Zaxis,
				wcs.Origin, wcs.Xaxis, wcs.Yaxis, wcs.Zaxis);
		}

		//public static int GetAngleBetweenPoints(Point3d pt1, Point3d pt2)
		//{
		//   var dx = pt2.X - pt1.X;
		//   var dy = pt2.Y - pt1.Y;

		//   var ang = Math.Atan2(dy, dx) * (180 / Math.PI));
		//   if(deg < 0)
		//   {
		//      deg += 360;
		//   }

		//   return deg;
		//}

		/// <summary>
		/// Given the height of the vertical side (Y) and the 
		/// angle between the vertical side and the hypotenuse 
		/// of a right-triangle (phi), this returns the length 
		/// of the horizontal side (X).
		/// </summary>
		/// <param name="Y">height of the vertical side of the right triangle</param>
		/// <param name="phi">adjacent angle between the vertial side and the hypotenuse</param>
		/// <returns>length of the vertical side of the triangle</returns>
		/// 
		public static double Run(this double Y, double phi)
		{
			return Rise(Y, halfPi - phi);
		}

		public static Point3d ClosestTo(this Point3dCollection points, Point3d pos)
		{
			double min = double.MaxValue;
			int index = 0;
			int cnt = points.Count;
			for(int i = 0; i < cnt; i++)
			{
				double len = pos.GetVectorTo(points[i]).LengthSqrd;
				if(len < min)
				{
					min = len;
					index = i;
				}
			}
			return points[index];
			//return points.Cast<Point3d>().Select(p => new KeyValuePair<Point3d, double>(p, p.GetVectorTo(pos).LengthSqrd))
			//   .Aggregate((a, b) => a.Value < b.Value ? a : b).Key;
			////return .Select(e => new KeyValuePair<T, double>(e, selector(e).DistanceTo(pos)))
			////   .Aggregate((x, y) => x.Value < y.Value ? x : y).Key;
			//return points.Cast<Point3d>().Aggregate((p1, p2) => p1.DistanceTo(pos) > p2.DistanceTo(pos) ? p1 : p2);
		}

		public static T ClosestTo<T>(this IEnumerable<T> source, Point3d pos, Func<T, Point3d> selector)
		{
			//return source.Select(e => new KeyValuePair<T, double>(e, selector(e).DistanceTo(pos)))
			//   .Aggregate((x, y) => x.Value < y.Value ? x : y).Key;
			return source.Aggregate((x, y) => selector(x).DistanceTo(pos) < selector(y).DistanceTo(pos) ? x : y);
		}

		public static double[] ToComArray(this IEnumerable<Point3d> points)
		{
			ICollection<Point3d> list = points as ICollection<Point3d>;
			if(list != null) // try the faster way first
			{
				double[] result = new double[list.Count * 3];
				int i = 0;
				foreach(Point3d point in points)
				{
					result[i] = point.X;
					result[i + 1] = point.Y;
					result[i + 2] = point.Z;
					i += 3;
				}
				return result;
			}
			// otherwise, use the slow way:
			return points.SelectMany(p => p.ToArray()).ToArray();
		}

		public static double[] ToComArray(this IEnumerable<Point2d> points)
		{
			return points.SelectMany(p => p.ToArray()).ToArray();
		}

		//public static Point2d AsPoint2d(this Point3d point)
		//{
		//   return new Point2d(point.X, point.Y);
		//}

		//public static Point3d AsPoint3d(this Point2d point, double Z = 0.0)
		//{
		//   return new Point3d(point.X, point.Y, Z);
		//}

		//public static int Compare( this Curve curve, Point3d pointOnCurve )
		//{

		//}

		//public static double ParameterAtMidpoint( this Curve curve )
		//{
		//   if( curve.Closed )
		//      throw new InvalidOperationException( "Curve is closed" );

		//}

		// angle between two 3d points projected into wcs xy plane:
		public static double AngleInXYPlaneTo(this Point3d thisPoint, Point3d point)
		{
			Vector3d v = point - thisPoint;
			if(v.IsParallelTo(Vector3d.ZAxis))
				throw new ArgumentException("Points are coincident in XY plane");
			return new Vector2d(v.X, v.Y).Angle;
		}

		//public static Point3dCollection GetBaseCorners( this Extents3d extents )
		//{
		//   return new Point3dCollection(
		//      new Point3d[] {
		//         extents.MinPoint,
		//         new Point3d(extents.MaxPoint.X, extents.MinPoint.Y, extents.MinPoint.Z),
		//         extents.MaxPoint,
		//         new Point3d(extents.MinPoint.X, extents.MaxPoint.Y, extents.MinPoint.Z)} );
		//}

		//public static string Format( this Point3d value )
		//{
		//   return Format( value, false );
		//}

		//public static string Format( this Point3d value, bool Ucs )
		//{
		//   if( Ucs )
		//      value = value.ToUcs();
		//   return string.Join( ", ", Array.ConvertAll<double, string>( value.ToArray(), v => v.FormatAsDistance() ) );
		//}

		//public static string Format( this Point3d value, bool Ucs, DistanceUnitFormat units, int precision )
		//{
		//   if( Ucs )
		//      value = value.ToUcs();
		//   return string.Join( ", ", Array.ConvertAll<double, string>( value.ToArray(), v => v.FormatAsDistance( units, precision ) ) );
		//}

		// According to the active document's current UCS
		//public static Point3d ToUcs( this Point3d value )
		//{
		//   Document doc = Application.DocumentManager.MdiActiveDocument;
		//   if( doc == null )
		//      throw new Autodesk.AutoCAD.Runtime.Exception( ErrorStatus.NoDocument );
		//   return value.TransformBy( doc.Editor.CurrentUserCoordinateSystem );
		//   //NativeMethods.NativeMethods32.acdbWcs2Ucs( out value, out value, false );
		//   //return value;
		//}

		//public static Point3d ToUcs( this Point3d value )
		//{
		//   Database db = HostApplicationServices.WorkingDatabase;
		//   if( db == null )
		//      throw new Autodesk.AutoCAD.Runtime.Exception( ErrorStatus.NoDatabase );
		//   return ToUcs( db, value );
		//}

		//public static Point3d ToWcs( this Point3d value )
		//{
		//   Database db = HostApplicationServices.WorkingDatabase;
		//   if( db == null )
		//      throw new Autodesk.AutoCAD.Runtime.Exception( ErrorStatus.NoDatabase );
		//   return ToWcs( db, value );
		//}

		//// According to the active document's current UCS
		//public static Point3d ToUcs( this Database db, Point3d value )
		//{
		//   return value.TransformBy( Matrix3d.WorldToPlane( new Plane( db.Ucsorg, db.Ucsxdir.CrossProduct( db.Ucsydir ) ) ) );
		//}

		//public static Point3d ToWcs( this Database db, Point3d value )
		//{
		//   return value.TransformBy( Matrix3d.PlaneToWorld( new Plane( db.Ucsorg, db.Ucsxdir.CrossProduct( db.Ucsydir ) ) ) );
		//}



		///// <summary>
		///// Returns the transformation from World to the coordinate system with
		///// the given origin and x-axis point.
		///// </summary>
		///// <param name="origin"></param>
		///// <param name="pointOnXAxis"></param>
		///// <returns></returns>

		//public Matrix3d GetVectorTransform( Point3d origin, Point3d pointOnXAxis, Vector3d normal )
		//{
		//   if( origin.DistanceTo( pointOnXAxis ) < Tolerance.Global.EqualPoint )
		//      throw new ArgumentException( "Origin and X-axis points cannot be coincident" );
		//   Vector3d vx = GetNormalizedVectorTo( origin, pointOnXAxis );
		//   if( vx.IsPerpendicularTo( normal ) )
		//   {
		//      Vector3d vy = normal.CrossProduct( vx ).Normalize();
		//      Matrix3d result = Matrix3d.AlignCoordinateSystem(
		//         Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
		//         origin, vx, vy, normal );
		//      return result;
		//   }
		//}


		//public static Matrix3d GetVectorTransform( this Point3d origin, Point3d pointOnXAxis )
		//{
		//   if( origin.DistanceTo( pointOnXAxis ) < Tolerance.Global.EqualPoint )
		//      throw new ArgumentException( "Coordinates cannot be coincident" );
		//   Vector3d vx = origin.GetNormalizedVectorTo( pointOnXAxis );
		//   Vector3d vy = vx.GetPerpendicularVector();
		//   return Matrix3d.AlignCoordinateSystem(
		//      Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
		//      origin, vx, vy, vy.CrossProduct( vx ) );
		//}

		//public static bool IsNullOrEmpty( this Array array )
		//{
		//   return array == null || array.Length == 0;
		//}

		/// <summary>
		/// Performs the inverse of Matrix3d.Mirroring(Plane)
		/// </summary>
		/// <remarks>The method that determines the mirror plane
		/// uses the points [0,0,0], [1,0,0], [0,1,0], and [0,0,1] 
		/// to select a point that is not on the mirror plane, and 
		/// then uses the midpoint of the line from the source point 
		/// to the mirrored point as the point on the plane, and the 
		/// vector from that point to the transformed point as the 
		/// plane's normal. The method relies on the fact that the 
		/// four test points are not co-planar and hence, not all of 
		/// same can be in the mirror plane.
		/// </remarks>
		/// <param name="xform">A Matrix3d that describes a mirroring transformation</param>
		/// <returns>The mirror Plane</returns>

		static Point3d[] mirrorTestPoints = new Point3d[]
		{	
			Point3d.Origin,
			new Point3d(1,0,0),
			new Point3d(0,1,0),
			new Point3d(0,0,1)
		};
		
		public static Plane GetMirroring(this Matrix3d xform)
		{
			Plane result = new Plane();
			for(int i = 0; i < 4; i++)
			{
				Point3d source = mirrorTestPoints[i];
				Point3d dest = source.TransformBy(xform);
				if(!dest.IsEqualTo(source)) // the point is not in the mirror plane, use it
				{
					/// does this have a normal direction of midpoint -> dest ?
					result = new LineSegment3d(source, dest).GetBisector();
					//Point3d midpt = line.EvaluatePoint(0.5);
					//result = new Plane(midpt, midpt.GetNormalizedVectorTo(dest));
					break;
				}
			}
			return result;
		}

		public static LineSegment3d ToLineSegment3d(this Line line)
		{
			return new LineSegment3d(line.StartPoint, line.EndPoint);
		}

		public static LineSegment2d ToLineSegment2d(this Line line)
		{
			return new LineSegment2d(line.StartPoint.ToPoint2d(), line.EndPoint.ToPoint2d());
		}

		public static bool Clockwise(Point2d origin, Point2d direction, Point2d point)
		{
			return SideOf(point, origin, direction) < 0;
		}

		public static int SideOf(this LineSegment2d line, Point2d point, double tolerance = 1.0e-6)
		{
			return SideOf(point, line.StartPoint, line.EndPoint, tolerance);
		}

		/// <summary>
		/// Returns an integer representing the relationship of
		/// the point the method is invoked on to the given line 
		/// segment, where:
		/// 
		///    1:  point is to the left of the line segment.
		///    0:  point is on the infinite line passing through
		///        the endpoints of the line segment.
		///   -1:  point is to the right of the line segment.
		///   
		/// </summary>
		
		public static int SideOf(this Point2d point, LineSegment2d line, double tolerance = 1.0e-6)
		{
			return SideOf(point, line.StartPoint, line.EndPoint, tolerance);
		}

		/// <summary>
		/// Returns an integer representing the relationship of
		/// the Point2d which the method is invoked on, to the 
		/// line passing through the two given Point2d arguments, 
		/// where:
		/// 
		///    1:  point is to the left of the line v1-v2
		///    0:  point is on the infinite line passing through
		///        the endpoints of the line v1 v2.
		///   -1:  point is to the right of the line v1 v2
		///   
		/// </summary>
		/// <param name="line"></param>
		/// <param name="point"></param>
		/// <returns></returns>
		
		public static int SideOf(this Point2d p, Point2d v1, Point2d v2, double tolerance = 1.0e-6)
		{
			double r = ((v2.X - v1.X) * (p.Y - v1.Y)) - ((v2.Y - v1.Y) * (p.X - v1.X));
			return Math.Abs(r) < tolerance ? 0 : Math.Sign(r);
		}

		public static bool IsOn(this Point2d point, Curve2d curve, Tolerance? tolerance = null)
		{
			Assert.IsNotNull(curve, "curve");
			return curve.IsOn(point, tolerance ?? Tolerance.Global);
		}

		public static bool IsOn(this Point3d point, Curve3d curve, Tolerance? tolerance = null)
		{
			Assert.IsNotNull(curve, "curve");
			return curve.IsOn(point, tolerance ?? Tolerance.Global);
		}

		public static CircularArc2d ToCounterClockWise(this CircularArc2d arc)
		{
			return arc.IsClockWise ? (CircularArc2d) arc.GetReverseParameterCurve() : arc;
		}

		public static Point2d IntersectionWith(this LineSegment2d line, LineSegment2d other, double tol = 1.0e-6)
		{
			if(line == null)
				throw new ArgumentNullException("line");
			if(other == null)
				throw new ArgumentNullException("other");
			if(line.IsParallelTo(other, new Tolerance(tol, tol)))
				throw new AcRx.Exception(AcRx.ErrorStatus.InvalidInput, "Lines are parallel");
			return line.GetLine().IntersectWith(other.GetLine())[0];
		}

		public static void Draw(this LineSegment2d segment, int color)
		{
			Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor
				.DrawVector(segment.StartPoint.ToPoint3d(), segment.EndPoint.ToPoint3d(), color, false);
		}

		/// <summary>
		/// returns a value indicating the given point is opposing to
		/// the curve.
		/// </summary>
		/// <remarks>A point is opposing to a curve if a vector from
		/// the point to the closest point on the curve is perpendicular
		/// to the tangent vector of the curve at the point on the curve
		/// nearest to the point. 
		/// 
		/// A point that is opposing to a curve lies on the offset of
		/// the curve in the direction of/distance to the point.
		/// </remarks>
		/// <param name="curve"></param>
		/// <param name="point"></param>
		/// <returns></returns>
		public static bool IsOpposing(this Curve curve, Point3d point)
		{
			try
			{
				var pt = curve.GetClosestPointTo(point, true);
				if(pt.IsEqualTo(point))
					return false;
				return curve.GetFirstDerivative(pt).IsPerpendicularTo(pt.GetVectorTo(point));
			}
			catch(Autodesk.AutoCAD.Runtime.Exception ex)
			{
				if(ex.ErrorStatus == AcRx.ErrorStatus.InvalidInput)
					return false;
				throw;
			}
		}
		//public static Point3d GetClosestPointTo(this Curve curve, Extents2d extents)
		//{
			
		//}
	}


	/// <summary>
	/// Allows optimized array allocation in ToArray() and ToList()
	/// </summary>
	/// <remarks>
	/// By wrapping a Point3dCollection (which does not implement
	/// the generic versions of IList or ICollection) in a class
	/// that implements IList<Point3d>, a significant improvement
	/// in performance in calls to ToArray() and ToList() can be
	/// achieved. This results from the fact that ToArray() and
	/// ToList() attempt to deduce if its argument implements the
	/// ICollection<T> interface, and if so, will allocate the
	/// entire resulting array in a single operation, rather than
	/// incremetally, as is done when the argument implements only 
	/// the IEnumerable<T> interface.
	/// 
	/// Hence, rather than calling Enumerable.Cast<Point3d>() on a
	/// Point3dCollection that is to be passed to ToArray() or 
	/// ToList(), the AsList() extension method can be used to wrap 
	/// the Point3dCollection in a Point3dList, which will result 
	/// in a significant improvement in performance.
	/// 
	/// This class is leveraged in the Curve.GetSplitParameters()
	/// extension method, which uses a variant of ToArray() that
	/// combines the functionality of Select() and ToArray() into
	/// a single operation that can be done more efficiently.
	/// </remarks>

	public class Point3dList : IList<Point3d>
	{
		Point3dCollection source;

		public Point3dList(Point3dCollection source)
		{
			Assert.IsNotNull(source, "source");
			this.source = source;
		}

		public void Add(Point3d item)
		{
			source.Add(item);
		}

		public void Clear()
		{
			source.Clear();
		}

		public bool Contains(Point3d item)
		{
			return source.Contains(item);
		}

		public void CopyTo(Point3d[] array, int arrayIndex)
		{
			source.CopyTo(array, arrayIndex);
		}

		public int Count
		{
			get
			{
				return source.Count;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				return false;
			}
		}

		public bool Remove(Point3d item)
		{
			int cnt = source.Count;
			source.Remove(item);
			return cnt > source.Count;
		}

		public IEnumerator<Point3d> GetEnumerator()
		{
			return source.Cast<Point3d>().GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return source.GetEnumerator();
		}

		public int IndexOf(Point3d item)
		{
			return source.IndexOf(item);
		}

		public void Insert(int index, Point3d item)
		{
			source.Insert(index, item);
		}

		public void RemoveAt(int index)
		{
			source.RemoveAt(index);
		}

		public Point3d this[int index]
		{
			get
			{
				return source[index];
			}
			set
			{
				source[index] = value;
			}
		}
	}

	/// <summary>
	/// A struct that simplifies cumulative relative 
	/// coordinate computations. 
	/// 
	/// This struct can be used to specify a series
	/// of related 3d coordinates, each in terms of
	/// or relative to the most-recently computed
	/// coordinate using either polar angle/distance 
	/// specification or cartesian delta offset.
	/// 
	/// Each call to the Polar() or Delta() methods
	/// computes and returns a coordinate relative to
	/// the coordinate returned by the most-recent 
	/// call to either of those same methods, or the 
	/// initial coordinate passed to the constructor.
	/// </summary>

	public struct ReferencePoint3d
	{
		Point3d value;
		double lastAngle;
		Matrix3d transform;
		public ReferencePoint3d(Point3d value)
		{
			this.value = value;
			this.transform = Matrix3d.Identity;
			lastAngle = 0.0;
		}

		public ReferencePoint3d(Point3d basePoint, double angle, double distance, double deltaZ = 0.0)
		{
			lastAngle = angle;
			this.transform = Matrix3d.Identity;
			value = new Point3d(
				basePoint.X + (distance * Math.Cos(angle)),
				basePoint.Y + (distance * Math.Sin(angle)),
				basePoint.Z + deltaZ);
		}

		public ReferencePoint3d(Point3d basePoint, Vector3d displacement)
		{
			this.transform = Matrix3d.Identity;
			lastAngle = 0.0;
			value = basePoint + displacement;
		}

		ReferencePoint3d(ReferencePoint3d basePoint, double angle, double distance, double deltaZ = 0.0)
		{
			this.transform = Matrix3d.Identity;
			lastAngle = angle;
			Point3d point = basePoint.value;
			value = new Point3d(
				point.X + (distance * Math.Cos(angle)),
				point.Y + (distance * Math.Sin(angle)),
				point.Z + deltaZ);
		}

		ReferencePoint3d(ReferencePoint3d basePoint, Vector3d displacement)
		{
			this.transform = Matrix3d.Identity;
			lastAngle = 0.0;
			value = basePoint.value + displacement;
		}

		/// <summary>
		/// The coordinate transformed by the given transform
		/// </summary>
		public Point3d Value
		{
			get
			{
				return value.TransformBy(transform);
			}
		}

		/// <summary>
		/// The untransformed coordinate.
		/// </summary>
		public Point3d RawValue
		{
			get
			{
				return value;
			}
		}

		/// <summary>
		/// The tranform applied to the result of
		/// Polar(), RelativePolar(), Delta(), and
		/// the Value property.
		/// </summary>
		public Matrix3d Transform
		{
			get
			{
				return this.transform;
			}
			set
			{
				this.transform = value;
			}
		}

		/// <summary>
		/// Returns a coordinate at the specified angle and distance 
		/// from the coordinate represented by the instance which the 
		/// method is invoked on.
		/// </summary>
		/// <param name="angle">The angle to the resulting
		/// coordinate in radians, in the XY plane</param>
		/// <param name="distance">The distance to the resulting
		/// coordinate</param>
		/// <param name="deltaZ">An optional value specifying
		/// the distance above or below the XY plane</param>
		/// <returns>The resulting 3d coordinate.</returns>

		public Point3d Polar(double angle, double distance, double deltaZ = 0)
		{
			lastAngle = angle;
			value = new Point3d(
				value.X + (distance * Math.Cos(angle)),
				value.Y + (distance * Math.Sin(angle)),
				value.Z + deltaZ);
			return value.TransformBy(transform);
		}

		/// <summary>
		/// Spherical relative coordinate.
		/// </summary>
		/// <param name="phi"></param>
		/// <param name="theta"></param>
		/// <param name="distance"></param>
		/// <returns></returns>

		public Point3d Polar3d(double phi, double theta, double distance)
		{
			value = new Point3d(
				value.X + (distance * Math.Cos(phi)),
				value.Y + (distance * Math.Sin(phi)),
				value.Z + (distance * Math.Sin(theta)));
			return value.TransformBy(transform);
		}

		/// <summary>
		/// Specifies a coordinate at an angle relative to the
		/// angle specified in the most-recent call to Polar(), 
		/// at the specified distance. 
		/// </summary>
		/// <param name="angle"></param>
		/// <param name="distance"></param>
		/// <returns></returns>
		public Point3d RelativePolar(double angle, double distance)
		{
			return Polar(angle + lastAngle, distance);
		}

		/// <summary>
		/// Returns a coordinate at the specified delta X,Y, Z
		/// offset from the coordinate represented by the instance 
		/// which the method is invoked on.
		/// </summary>
		/// <param name="dx">The distance to the result measured
		/// along the X axis</param>
		/// <param name="dy">The distance to the result measured
		/// along the Y axis</param>
		/// <param name="dz">The distance to the result measured
		/// along the Z axis</param>
		/// <returns>A ReferencePoint3d instance representing
		/// the resulting 3d coordinate.</returns>
		public Point3d Delta(double dx, double dy, double dz = 0.0)
		{
			this.value += new Vector3d(dx, dy, dz);
			return this.value.TransformBy(transform);
		}

		const double D90 = Math.PI * 0.5;
		const double D0 = 0.0;
		const double D180 = Math.PI;
		const double D270 = Math.PI * 1.5;

		/// <summary>
		/// Returns a coordinate that is a specified distance 
		/// from the coordinate represented by the instance which
		/// the method is invoked on, at a bearing of 90 degrees.
		/// </summary>
		/// <param name="distance"></param>
		/// <returns></returns>
		public Point3d MoveUp(double distance)
		{
			return this.Polar(D90, distance);
		}

		/// <summary>
		/// 180 degrees at the specified distance
		/// </summary>
		/// <param name="distance"></param>
		/// <returns></returns>
		public Point3d MoveLeft(double distance)
		{
			return this.Polar(D180, distance);
		}

		/// <summary>
		/// 0 degress at the specified distance
		/// </summary>
		/// <param name="distance"></param>
		/// <returns></returns>
		public Point3d MoveRight(double distance)
		{
			return this.Polar(0.0, distance);
		}

		/// <summary>
		/// 270 degress at the specified distance.
		/// </summary>
		/// <param name="distance"></param>
		/// <returns></returns>

		public Point3d MoveDown(double distance)
		{
			return this.Polar(D270, distance);
		}

		public Point3d TurnLeft(double distance)
		{
			return this.RelativePolar(D90, distance);
		}

		public Point3d TurnRight(double distance)
		{
			return this.RelativePolar(D270, distance);
		}

		/// <summary>
		/// Allows a ReferencePoint3d to appear wherever
		/// a Point3d is required.
		/// </summary>
		public static implicit operator Point3d(ReferencePoint3d point)
		{
			return point.Value;
		}

	}

	public static class ReferencePoint3dExamples
	{
		/// <summary>
		/// Computes and returns the corner points of a rectangle
		/// of the given width and height, 
		/// </summary>
		public static Point3d[] GetBoxCorners(Point3d lowerLeftCorner, double width, double height)
		{
			ReferencePoint3d point = new ReferencePoint3d(lowerLeftCorner);

			return new Point3d[]
			{
				point,
				point.MoveRight(width),
				point.MoveUp(height),
				point.MoveLeft(width)
			};

			///// Alternately:
			///// 
			//return new Point3d[]
			//{
			//	point,
			//	point.MoveRight(width),
			//	point.TurnLeft(height),
			//	point.TurnLeft(width)
			//}
		}

		/// <summary>
		/// Returns the 6 corner points that form the letter 
		/// "L" with the specified offset thickness.
		/// </summary>
		/// <param name="lowerLeft"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="offset"></param>
		/// <returns></returns>
		public static Point3d[] GetDogLegPoints(Point3d lowerLeft, double width, double height, double offset)
		{
			Point3d[] result = new Point3d[6];
			ReferencePoint3d pos = new ReferencePoint3d(lowerLeft);
			result[0] = pos;
			result[1] = pos.MoveRight(width);
			result[2] = pos.MoveUp(offset);
			result[3] = pos.MoveLeft(width - offset);
			result[4] = pos.MoveUp(height - offset);
			result[5] = pos.MoveLeft(offset);
			return result;
		}

	}


	/// <summary>
	/// Cannot be used with hashing algorithims or anything that
	/// depends on the result of GetHashCode(), because:
	/// 
	///    double a, b, c;
	///    var dc = new DistanceComparer();
	///    
	///    // can fail even if the first two comparisons succeed.
	///    Assert.IsTrue(dc.Equals(a, b) && dc.Equals(b, c) && dc.Equals(a, c)); 
	///    
	///    if a and b compare as equal, and b and c compare as
	///    equal, then a and c must also compare as equal.
	///    
	/// This class is primarly used with OrderBy() to sort doubles
	/// based on the definition of equality as defined by the given
	/// epsilon or tolerance.
	/// </summary>

	public class DistanceComparer : IComparer<double>, IEqualityComparer<double>
	{
		double epsilon = AcGe.Tolerance.Global.EqualPoint;

		public DistanceComparer()
		{
		}

		public DistanceComparer(double equalPoint)
		{
			this.epsilon = Math.Abs(equalPoint);
		}

		public DistanceComparer(Tolerance tolerance)
		{
			this.epsilon = Math.Abs(tolerance.EqualPoint);
		}

		public int Compare(double x, double y)
		{
			return Equals(x, y) ? 0 : x.CompareTo(y);
		}

		public bool Equals(double x, double y)
		{
			return Math.Abs(x - y) < epsilon;
		}

		/// <summary>
		/// Investigate rounding for avoiding rejection
		/// of two doubles that compare as equal using 
		/// the above.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public int GetHashCode(double obj)
		{
			return obj.GetHashCode();
		}

		public static DistanceComparer Default
		{
			get
			{
				if(_default == null)
					_default = new DistanceComparer();
				return _default;
			}
		}

		public double Tolerance
		{
			get
			{
				return this.epsilon;
			}
			set
			{
				this.epsilon = Math.Abs(value);
			}
		}

		static DistanceComparer _default = null;
	}


	/// <summary>
	/// Cannot be used with hashing algorithims or anything that
	/// depends on the result of GetHashCode(), because:
	/// 
	///     if a == b && b == c then a == c must also be true
	///   
	/// </summary>
	
	class Point3dComparer : IEqualityComparer<Point3d>, IComparer<Point3d>
	{
		private Tolerance tolerance = Tolerance.Global;

		public Point3dComparer()
		{
		}

		public Point3dComparer(Tolerance tolerance)
		{
			this.tolerance = tolerance;
		}

		public bool Equals(Point3d x, Point3d y)
		{
			return x.IsEqualTo(y, tolerance);
		}

		public int Compare(Point3d x, Point3d y)
		{
			return Equals(x, y) ? 0 : x.GetAsVector().LengthSqrd > y.GetAsVector().LengthSqrd ? 1 : -1;
		}

		public int GetHashCode(Point3d obj)
		{
			return obj.GetHashCode();
		}

		public static Point3dComparer Default
		{
			get
			{
				if(_default == null)
					_default = new Point3dComparer();
				return _default;
			}
		}

		static Point3dComparer _default;

	}


	//public static class CurveContainmentExtensions
	//{
	//	public static bool IsUnknownOrError(this CurveContainment cc)
	//	{
	//		return cc == CurveContainment.None || (cc & CurveContainment.Error) == CurveContainment.Error;
	//	}

	//	public static bool IsInsideOrPartiallyCoincident(this CurveContainment cc)
	//	{
	//		if((cc & CurveContainment.Coincident) == CurveContainment.Coincident)
	//			return false;
	//		return (cc & (CurveContainment.Inside & CurveContainment.Coincident)) != CurveContainment.None;
	//	}

	//	static CurveContainment MaskNonExclusive(CurveContainment flags, CurveContainment mask)
	//	{
	//		if(!mask.IsUnknownOrError() && flags != mask)
	//			return flags & ~mask;
	//		else
	//			return flags;
	//	}

	//	/// <summary>
	//	/// Implicitly includes partially-coincident, 
	//	/// 
	//	/// The question here is, do we want to include fully-coincident
	//	/// or exclude it. Currently, the code below excludes it.
	//	/// 
	//	/// </summary>
	//	/// <param name="cc"></param>
	//	/// <returns>True if the argument represents a curve that 
	//	/// is entirely or partially inside or partially-coincident 
	//	/// with a boundary (but not entirely coincident)</returns>

	//	public static bool IsInside(this CurveContainment cc)
	//	{
	//		if(cc.IsUnknownOrError())
	//			return false;
	//		cc = MaskNonExclusive(cc, CurveContainment.Coincident);
	//		return cc != CurveContainment.Outside && cc != CurveContainment.Coincident;
	//	}

	//	/// <summary>
	//	/// Like the above, except the caller can specify if portions
	//	/// of the curve that are partially-coincident with the boundary
	//	/// are seen as being inside the boundary.
	//	/// </summary>
	//	/// <param name="cc"></param>
	//	/// <param name="IncludePartiallyCoincident">If true, curves that are
	//	/// partially-coincident with the boundary are seen as being inside
	//	/// the boundary.</param>
	//	/// <returns></returns>

	//	public static bool IsInside(this CurveContainment cc, bool IncludePartiallyCoincident = true)
	//	{
	//		if(cc.IsUnknownOrError())
	//			return false;
	//		cc = MaskNonExclusive(cc, CurveContainment.Coincident);
	//		if(IncludePartiallyCoincident)
	//			return cc != CurveContainment.Outside && cc != CurveContainment.Coincident;
	//		else
	//			return cc == CurveContainment.Inside;
	//	}

	//}

	//public static class PointContainmentExtensions
	//{
	//	public static bool IsOnBoundary(this PointContainment pc)
	//	{
	//		return pc == PointContainment.OnBoundary;
	//	}
	//	public static bool IsInside(this PointContainment pc)
	//	{
	//		return pc == PointContainment.Inside;
	//	}
	//	public static bool IsOutside(this PointContainment pc)
	//	{
	//		return pc == PointContainment.Outside;
	//	}
	//}

	public static class DefaultTolerance
	{
		public static readonly double EqualPoint = Tolerance.Global.EqualPoint;
		public static readonly double EqualVector = Tolerance.Global.EqualVector;
	}

	public static class MiscXFormExtensions
	{
      public static void TransformTo(this DBText dbText, Line line)
      {
         var xAxis = (line.EndPoint - line.StartPoint).GetNormal();
         var mat = Matrix3d.AlignCoordinateSystem(
             Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
             line.StartPoint, xAxis, line.Normal.CrossProduct(xAxis), line.Normal);
      }

      public static Matrix3d GetTransformTo(Point3d destination,
         Vector3d x, Vector3d y, Vector3d z)
      {
         return Matrix3d.AlignCoordinateSystem(
             Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
             destination, x, y, z);
      }

   }

}

