/// BrepExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the BrepEntity class.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;

namespace AcMgdLib.BoundaryRepresentation
{
   public static class BrepExtensions
   {
      /// <summary>
      /// Returns a sequence of Curve3d[] arrays that can be 
      /// used to generate Curve entities. The process has been
      /// refactored to support parallel execution, by moving
      /// the non thread-safe code to the caller (which must
      /// create the Curve entities, which cannot be done in 
      /// parallel). 
      /// 
      /// This method will exploit parallel execution for the
      /// creation of all loops within a complex.
      /// 
      /// Each output element will be an array containing either
      /// a single CompositeCurve3d (polyline), or an array of
      /// Curve3d[] (non-polyline). 
      /// 
      /// The caller decides what to do with multiple non-polyline
      /// curves (e.g., create a single spline, multiple polylines
      /// connected to splines or ellipses, or the default behavior 
      /// of the EXPLODE command). 
      /// 
      /// Callers can explicitly specify if the operation should 
      /// execute in parallel, via the parallel argument.
      /// </summary>
      /// <param name="convertToPolylines">A value indicating if
      /// contiguous chains of two or more line/arc segments should
      /// be converted to a single polyline.</param>
      /// <param name="complex">The Brep Complex whose loops are
      /// to be obtained.</param>
      /// <param name="parallel">A value indicating if the operation
      /// should execute in parallel (true = yes)</param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      public static IEnumerable<Curve3d[]> GetLoops(
         this AcBr.Complex complex,
         bool convertToPolylines = true,
         bool parallel = false,
         Func<BoundaryLoop, bool> predicate = null)

      {
         Assert.IsNotNullOrDisposed(complex);
         predicate ??= defaultPredicate;
         var loops = complex.Shells
            .SelectMany(shell => shell.Faces)
            .SelectMany(face => face.Loops)
            .Where(predicate)
            .ToArray();
         var results = new Curve3d[loops.Length][];
         parallel &= loops.Length > 1;
         loops.ForEach(parallel ? 1 : 0, (loop, i) =>
         {
            var geCurves = loop.GetGeCurves();
            if(!geCurves.Any())
               throw new InvalidOperationException("no curves");
            Curve3d result = null;
            if(convertToPolylines && null != (result = geCurves.TryCreatePolyline()))
            {
               results[i] = new[] { result };
            }
            else  // return the input curves.
            {
               results[i] = geCurves.ToArray();
            }
         });
         return results;
      }

      /// <summary>
      /// Like GetLoops() but always converts two or more
      /// interconnected lines and/or arcs to polylines.
      /// </summary>
      /// <param name="complex"></param>
      /// <param name="parallel"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<Curve3d[]> GetOptimizedLoops(
         this AcBr.Complex complex,
         bool parallel = false)

      {
         Assert.IsNotNullOrDisposed(complex);
         var loops = complex.Shells
            .SelectMany(shell => shell.Faces)
            .SelectMany(face => face.Loops)
            .ToArray();
         var results = new Curve3d[loops.Length][];
         parallel &= loops.Length > 1;
         // Optimize() and TryCreatePolyline() will execute in parallel
         // but there is still the question of whether GetGeCurves() is
         // entirely thread-safe. 
         loops.ForEach(parallel ? 1 : 0, (loop, i) =>
         {
            results[i] = loop.GetGeCurves().Optimize().ToArray();
         });
         return results;
      }

      static readonly Func<BoundaryLoop, bool> defaultPredicate
         = loop => true;

      /// <summary>
      /// Returns a Curve3d[] array containing all
      /// edge geometry in the Brep, converting all
      /// contiguous sequences of 2 or more lines or
      /// arcs to a single Polyline, or entire loops 
      /// to closed splines, depending on the type 
      /// argument.
      /// </summary>
      /// <param name="brep"></param>
      /// <param name="type">The type of conversion to
      /// create</param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <param name="parallel">A value indicating if
      /// the operation should execute in parallel</param>

      public static IEnumerable<Curve3d> Explode(this Brep brep,
         RegionExplodeType type = RegionExplodeType.Polylines,
         bool parallel = false)
      {
         Assert.IsNotNullOrDisposed(brep);
         Curve3d[] result = null;
         if(type == RegionExplodeType.Splines)
         {
            result = LoopsToSplines(brep);
         }
         else if(type == RegionExplodeType.Polylines)
         {
            result = brep.Complexes.Select(c => c.GetOptimizedLoops(parallel))
               .SelectMany(arrays => arrays)
               .SelectMany(array => array)
               .ToArray();
         }
         if(result == null)
            return Array.Empty<Curve3d>();
         else 
            return parallel ? result.ToArray() : result;
      }

      /// <summary>
      /// Gets all BoundaryLoops to as closed Splines
      /// </summary>
      /// <param name="brep"></param>
      /// <returns></returns>

      public static Curve3d[] LoopsToSplines(Brep brep)
      {
         Assert.IsNotNullOrDisposed(brep);
         return brep.Complexes
            .SelectMany(c => c.Shells)
            .SelectMany(shell => shell.Faces)
            .SelectMany(face => face.Loops)
            .Select(ToNurbCurve3d)
            .ToArray();
      }

      /// <summary>
      /// Gets the loop geometry as a single, closed spline,
      /// normalized to traversal order and direction.
      /// 
      /// Parallelization has yet to be integrated into this,
      /// and will require refactoring at the call site.
      /// </summary>
      /// <param name="loop"></param>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>

      public static Curve3d ToNurbCurve3d(this BoundaryLoop loop)
      {
         Assert.IsNotNullOrDisposed(loop);
         Edge[] edges = loop.Edges.ToArray();
         if(edges.Length == 0)
            throw new InvalidOperationException("No edges");
         NurbCurve3d[] curves = Array.ConvertAll(edges,
            edge => edge.GetCurveAsNurb());
         if(curves.Length == 1)
            return curves[0];
         curves = curves.Normalize();
         var first = curves[0];
         var span = curves.AsSpan(1);
         for(int i = 0; i < span.Length; i++)
            first.JoinWith(span[i]);
         return first;
      }

      /// <summary>
      /// Returns the Curve3d elements comprising the given
      /// BoundaryLoop. If the splines argument is true, the
      /// elements are returned as NurbCurve3d instances.
      /// </summary>
      /// <param name="loop"></param>
      /// <param name="splines"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="NotSupportedException"></exception>

      public static IEnumerable<Curve3d> GetGeCurves(this BoundaryLoop loop, bool splines = false)
      {
         Assert.IsNotNullOrDisposed(loop);
         return loop.Edges.Select(edge =>
         {
            if(splines)
               return edge.GetCurveAsNurb();
            else if(edge.Curve is ExternalCurve3d crv && crv.IsNativeCurve)
               return crv.NativeCurve;
            else
               throw new NotSupportedException();
         });
      }

      /// <summary>
      /// Returns a value indicating if the BoundaryLoop 
      /// can be converted to a polyline.
      /// </summary>
      /// <param name="loop"></param>
      /// <returns></returns>
     
      public static bool IsPolyline(this BoundaryLoop loop)
      {
         return loop.GetGeCurves().IsPolyline();
      }

      /// <summary>
      /// Indicates if the contents of the input sequence
      /// can be converted to a Polyline.
      /// 
      /// If the input sequence contains only a single
      /// curve element, the result is false, regardless
      /// of the curve type.
      /// 
      /// Requires C# 10.0 or later.
      /// </summary>
      /// <param name="curves"></param>
      /// <returns></returns>

      public static bool IsPolyline(this IEnumerable<Curve3d> curves, bool parallel = false)
      {
         Assert.IsNotNull(curves);
         if(!curves.Any() || !curves.Skip(1).Any())
            return false;
         if(parallel)
            return curves.AsParallel().All(IsPolySegment);
         else
            return curves.All(IsPolySegment);
      }

      /// <summary>
      /// Returns a value indicating if the argument is a curve 
      /// that can be converted to a polyline segment.
      /// </summary>
      /// <param name="curve"></param>
      /// <returns></returns>
      
      public static bool IsPolySegment(this Curve3d curve)
         => curve is LineSegment3d or CircularArc3d && !curve.IsClosed();

      /// <summary>
      /// Ensures that the result is enumerated in order of
      /// traversal, with coincident start/endpoints. This
      /// method rearranges the order of, and reverses the 
      /// direction of input curves as needed.
      /// </summary>
      /// <param name="curves">The unordered set of curves</param>
      /// <param name="validate">True = validate each curve
      /// (should only be used for user-selected curves,
      /// but not for curves coming from a BRep)</param>
      /// <returns>The input curves in order of traversal</returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static T[] Normalize<T>(this IEnumerable<T> curves,
         bool validate = false,
         Tolerance tol = default(Tolerance)) where T: Curve3d
      {
         Assert.IsNotNull(curves);
         var input = curves as T[] ?? curves.ToArray();
         if(input.Length < 2)
            return input;
         if(tol.IsDefault())
            tol = Tolerance.Global;
         if(validate)
            input[0].AssertIsValid();
         int count = input.Length;
         var joined = new bool[count];
         var result = new T[count];
         if(input[0].StartPoint.IsEqualTo(input[1].EndPoint, tol))
            result[0] = input[0].GetReverseCurve(true);
         else
            result[0] = input[0];
         joined[0] = true;
         var spInput = input.AsSpan();
         var spResult = result.AsSpan();
         var spJoined = joined.AsSpan();
         for(int index = 1; index < count; index++)
         {
            T next = null;
            Point3d endPoint = spResult[index - 1].EndPoint;
            bool found = false;
            for(int i = 0; i < count; i++)
            {
               if(spJoined[i])
                  continue;
               next = spInput[i];
               if(validate)
                  next.AssertIsValid();
               if(endPoint.IsEqualTo(next.StartPoint, tol))
               {
                  spResult[index] = next;
                  spJoined[i] = true;
                  found = true;
                  break;
               }
               else if(endPoint.IsEqualTo(next.EndPoint, tol))
               {
                  spResult[index] = next.GetReverseCurve(true); 
                  spJoined[i] = true;
                  found = true;
                  break;
               }
            }
            if(!found)
            {
               throw new InvalidOperationException("Disjoint curves");
            }
         }
         return result;
      }

      /// <summary>
      /// Attempts to create a CompositeCurve3d that can be
      /// used to create a Polyline, from an input sequence 
      /// of Curve3d elements. If the input sequence cannot
      /// be used to create a Polyline, this returns null.
      /// If the input sequence can be used to create a
      /// Polyline, this method normalizes the input elements
      /// to be in traversal order and direction before using
      /// them to create the result.
      /// 
      /// This method returns null if the input sequence is 
      /// empty or contains a single element, regardless of 
      /// its type.
      /// </summary>
      /// <param name="curves"></param>
      /// <param name="validate"></param>
      /// <param name="tol"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      public static CompositeCurve3d TryCreatePolyline(this IEnumerable<Curve3d> curves,
         bool validate = false,
         Tolerance tol = default(Tolerance))
      {
         Assert.IsNotNull(curves);
         var input = curves as Curve3d[] ?? curves.ToArray();
         if(input.Length < 2)
            return null;
         if(tol.Equals(default(Tolerance)))
            tol = Tolerance.Global;
         int count = input.Length;
         var joined = new bool[count];
         Curve3d current = input[0];
         if(!current.IsPolySegment())
            return null;
         if(validate)
            current.AssertIsValid();
         var output = new Curve3d[count];
         if(current.StartPoint.IsEqualTo(input[1].EndPoint, tol))
            current = current.GetReverseCurve(true);
         output[0] = current;
         joined[0] = true;
         var spInput = input.AsSpan();
         var spOutput = output.AsSpan();
         var spJoined = joined.AsSpan();
         for(int index = 1; index < count; index++)
         {
            Point3d endPoint = spOutput[index - 1].EndPoint;
            bool found = false;
            for(int i = 0; i < count; i++)
            {
               if(spJoined[i])
                  continue;
               current = spInput[i];
               if(!current.IsPolySegment())
                  return null;
               if(validate)
                  current.AssertIsValid();
               if(endPoint.IsEqualTo(current.StartPoint, tol))
               {
                  spOutput[index] = current;
                  spJoined[i] = true;
                  found = true;
                  break;
               }
               else if(endPoint.IsEqualTo(current.EndPoint, tol))
               {
                  spOutput[index] = GetReverseCurve(current);
                  spJoined[i] = true;
                  found = true;
                  break;
               }
            }
            if(!found)
            {
               throw new InvalidOperationException($"Disjoint curve (index = {index})");
            }
         }
         return new CompositeCurve3d(output);
      }

      public static void AssertIsValid(this Curve3d curve,
            bool rejectClosed = true,
            bool rejectSelfIntersecting = true)
      {
         Assert.IsNotNullOrDisposed(curve);
         var tolerance = Tolerance.Global.EqualPoint;
         // disqualify degenerate curves first:
         if(curve.IsDegenerate(out var entity))
            throw new ArgumentException("degenerate curve");
         var iv = curve.GetInterval();
         // disqualify unbounded curves
         if(iv.IsUnbounded || !iv.IsBoundedAbove)
            throw new ArgumentException("unbounded curve");
         // disqualify zero-length curves
         if(curve.GetLength(iv.LowerBound, iv.UpperBound, tolerance) < tolerance)
            throw new ArgumentException("Zero-length curve");
         // disqualify closed curves if rejectClosed == true
         if(rejectClosed && curve.IsClosed())
            throw new ArgumentException("closed curve");
         // disqualify non-planar curves
         if(!curve.IsPlanar(out Plane plane))
            throw new ArgumentException("non-planar curve");
         // disqualify self-intersecting curves if rejectSelfIntersecting is true:
         if(rejectSelfIntersecting)
         {
            intersector.Set(curve, curve, plane.Normal);
            if(intersector.NumberOfIntersectionPoints > 0)
               throw new ArgumentException("self-intersecting curve");
         }
      }

      static CurveCurveIntersector3d intersector = new CurveCurveIntersector3d();

      /// <summary>
      /// Given a sequence of contiguous, interconnected Curve3d
      /// instances, this method will replace all occurrences of
      /// two or more contiguous, interconnected lines or arcs 
      /// with polylines.
      /// 
      /// The input sequence must form a contiguous chain of
      /// interconnected curves. If any curve is disjoint, an 
      /// exception is thrown.
      /// </summary>
      /// <param name="curves"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static Curve3d[] Optimize(this IEnumerable<Curve3d> curves)
      {
         Assert.IsNotNull(curves);
         if(!curves.Any())
            return Array.Empty<Curve3d>();
         Curve3d[] input = curves as Curve3d[] ?? curves.ToArray();
         if(input.Length < 2)
            return new Curve3d[] { input[0] };
         List<Curve3d> result = new List<Curve3d>(input.Length);
         List<Curve3d> segments = new List<Curve3d>();
         foreach(Curve3d curve in curves)
         {
            if(curve is LineSegment3d or CircularArc3d)
            {
               segments.Add(curve);
               continue;
            }
            if(segments.Count == 0)
            {
               result.Add(curve);
               continue;
            }
            if(segments.Count == 1)
            {
               result.Add(segments[0]);
               result.Add(curve);
               segments.Clear();
               continue;
            }
            result.AddRange(segments.TryConvert());
            result.Add(curve);
            segments.Clear();
         }

         /// If the sequence starts with a polyline and ends
         /// with a polyline, and the endpoint of the ending 
         /// polyline is coincident with the start point of 
         /// the starting polyline, then join the polylines:

         if(segments.Count > 0)
         {
            var remainder = segments.TryConvert();
            if(remainder.Length == 1 && remainder[0] is CompositeCurve3d end)
            {
               if(result.Count > 1 && result[0] is CompositeCurve3d start
                  && end.EndPoint.IsEqualTo(start.StartPoint))
               {
                  result[0] = end.Append(start);
               }
               else
               {
                  result.Add(end);
               }
            }
            else
            {
               result.AddRange(remainder);
            }
         }
         return result.ToArray();
      }

      static bool IsPolyline(this Curve3d[] array)
         => array?.Length == 1 && array[0] is CompositeCurve3d;

      static Curve3d[] TryConvert(this IEnumerable<Curve3d> segments)
      {
         var pline = segments.TryCreatePolyline();
         return pline != null ? new Curve3d[] { pline }
         : segments as Curve3d[] ?? segments.ToArray();
      }

      /// <summary>
      /// An overload of GetReverseParameterCurve() that
      /// optionally returns a reversed clone of the input
      /// curve, without modifying the input curve, and 
      /// returns the result having the same type as the
      /// argument.
      /// 
      /// </summary>
      /// <param name="curve"></param>
      /// <param name="clone"></param>
      /// <returns></returns>
      
      public static T GetReverseCurve<T>(this T curve, bool clone = false)
         where T: Curve3d
      {
         if(clone)
            return (T) ((Curve3d) curve.Clone()).GetReverseParameterCurve();
         else
            return (T) curve.GetReverseParameterCurve();
      }

   }

   public static class UtilityExtensionMethods
   {
      // Returns a value indicating if the argument
      // is equal to the default value of the argument
      // type (a struct).
      
      public static bool IsDefault<T>(this T value) where T : struct 
         => value.Equals(default(T));

      // Concatenates two arrays:
      public static T[] Append<T>(this T[] array1, T[] array2)
      {
         T[] result = new T[array1.Length + array2.Length];
         var resultSpan = result.AsSpan();

         array1.AsSpan().CopyTo(resultSpan);
         array2.AsSpan().CopyTo(resultSpan.Slice(array1.Length));

         return result;
      }

      /// <summary>
      /// Concatenates two CompositeCurve3d instances
      /// </summary>
      /// <param name="first"></param>
      /// <param name="second"></param>
      /// <returns></returns>
      
      public static CompositeCurve3d Append(this CompositeCurve3d first, CompositeCurve3d second)
      {
         Assert.IsNotNull(first);
         Assert.IsNotNull(second);
         return new CompositeCurve3d(first.GetCurves().Append(second.GetCurves()));
      }

      /// <summary>
      /// Concatenates two or more CompositeCurve3d instances.
      /// </summary>
      /// <param name="first"></param>
      /// <param name="rest"></param>
      /// <returns></returns>
      public static CompositeCurve3d AppendRange(this CompositeCurve3d first, params CompositeCurve3d[] rest)
      {
         Assert.IsNotNull(first);
         Assert.IsNotNull(rest);
      if(rest.Any(x => x is null))
         throw new ArgumentException("null element");
         return new CompositeCurve3d(
            first.GetCurves().Append(
               rest.SelectMany(crv => crv.GetCurves()).ToArray()));
      }

      /// <summary>
      /// Disposes the elements in the input sequence.
      /// 
      /// (Cannot parallelize this)
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="items"></param>

      public static void DisposeItems<T>(this IEnumerable<T> items) where T : IDisposable
      {
         if(!(items is null))
         {
            foreach(var item in items)
               item?.Dispose();
         }
      }

      /// <summary>
      /// For debugging, creates a Curve entity in 
      /// active document when an error is detected
      /// that is related to the input curve.
      /// </summary>
      /// <param name="next"></param>

      [Conditional("DEBUG")]
      internal static void Realize(this Curve3d next, Database db = null)
      {
         if(next == null)
            return;
         var curve = Curve.CreateFromGeCurve(next);
         db ??= HostApplicationServices.WorkingDatabase;
         try
         {
            using(var tr = new OpenCloseTransaction())
            {
               BlockTableRecord btr =
                  (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
               btr.AppendEntity(curve);
               tr.AddNewlyCreatedDBObject(curve, true);
               curve.ColorIndex = 1;
               tr.Commit();
            }
         }
         catch
         {
         }
      }

   }

   public class InvalidCurve3dException : System.InvalidOperationException
   {
      public InvalidCurve3dException(string msg, Curve3d curve = null) : base(msg)
      {
         if(curve != null)
         {
            curve.Realize();
         }
      }
   }

   public enum RegionExplodeType
   {
      Default = 0, // Default behavior of EXPLODE command 
      Polylines,   // Convert interconnected lines/arcs to polylines
      Splines      // Convert each loop to a single Spline
   }

}
