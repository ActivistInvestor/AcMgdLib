/// BrepExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the BrepEntity class.

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
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
         if(complex is null)
            throw new ArgumentNullException(nameof(complex));
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
      /// Like GetLoops() but always converts interconnected
      /// lines and arcs to polylines.
      /// </summary>
      /// <param name="complex"></param>
      /// <param name="parallel"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<Curve3d[]> GetOptimizedLoops(
         this AcBr.Complex complex,
         bool parallel = false)

      {
         if(complex is null)
            throw new ArgumentNullException(nameof(complex));
         var loops = complex.Shells
            .SelectMany(shell => shell.Faces)
            .SelectMany(face => face.Loops)
            .ToArray();
         var results = new Curve3d[loops.Length][];
         parallel &= loops.Length > 1;
         // Optimize() and TryCreatePolyline() will execute in parallel
         // but there is still question of whether GetGeCurves() is
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
      /// arcs to a single Polyline, or converting
      /// an entire loop to a closed spline, depending
      /// on the type argument.
      /// </summary>
      /// <param name="brep"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <param name="parallel">A value indicating if
      /// the operation should execute in parallel</param>

      public static IEnumerable<Curve3d> Explode(this Brep brep,
         RegionExplodeType type = RegionExplodeType.Polylines,
         bool parallel = false)
      {
         if(brep is null || brep.IsDisposed)
            throw new ArgumentNullException(nameof(brep));
         Curve3d[] result = null;
         if(type == RegionExplodeType.Spline)
         {
            result = brep.Complexes
               .SelectMany(c => c.Shells)
               .SelectMany(shell => shell.Faces)
               .SelectMany(face => face.Loops)
               .Select(loop => loop.GetSpline()) 
               .ToArray();
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
      /// Gets the loop geometry as a single, closed spline
      /// normalized to traversal order and direction.
      /// 
      /// Parallelization has yet to be integrated into this,
      /// and will require refactoring at the call site.
      /// </summary>
      /// <param name="loop"></param>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>

      public static Curve3d GetSpline(this BoundaryLoop loop)
      {
         var curves = loop.GetGeCurves(true).Normalize();
         if(curves.Length == 0)
            throw new InvalidOperationException("no curves");
         if(curves.Length == 1)
            return curves[0];
         var first = (NurbCurve3d) curves[0];
         var span = curves.AsSpan();
         for(int i = 1; i < span.Length; i++)
            first.JoinWith((NurbCurve3d)span[i]);
         return first;
      }

      public static IEnumerable<Curve3d> GetGeCurves(this BoundaryLoop loop, bool splines = false)
      {
         if(loop is null)
            throw new ArgumentNullException(nameof(loop));
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
         if(curves is null)
            throw new ArgumentNullException(nameof(curves));
         if(!curves.Any() || !curves.Skip(1).Any())
            return false;
         if(parallel)
            return curves.AsParallel().All(IsPolySegment);
         else
            return curves.All(IsPolySegment);
      }

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

      public static Curve3d[] Normalize(this IEnumerable<Curve3d> curves,
         bool validate = false,
         Tolerance tol = default(Tolerance))
      {
         if(curves is null)
            throw new ArgumentNullException(nameof(curves));
         var input = curves as Curve3d[] ?? curves.ToArray();
         if(input.Length < 2)
            return input;
         if(tol.Equals(default(Tolerance)))
            tol = Tolerance.Global;
         int count = input.Length;
         var joined = new bool[count];
         var output = new Curve3d[count];
         if(validate)
            input[0].AssertIsValid();
         output[0] = input[0];
         joined[0] = true;
         Point3d startPoint = input[0].StartPoint;
         var spInput = input.AsSpan();
         var spOutput = output.AsSpan();
         var spJoined = joined.AsSpan();
         int idx = 1;
         while(idx < count)
         {
            Curve3d next = null;
            Point3d endPoint = spOutput[idx - 1].EndPoint;
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
                  spOutput[idx] = next;
                  spJoined[i] = true;
                  found = true;
                  break;
               }
               else if(endPoint.IsEqualTo(next.EndPoint, tol))
               {
                  spOutput[idx] = next.GetReverseParameterCurve();
                  spJoined[i] = true;
                  found = true;
                  break;
               }
            }
            if(!found)
            {
               throw new InvalidOperationException("Disjoint curves");
            }
            idx++;
         }
         return output;
      }

      /// <summary>
      /// For debugging, creates a Curve in the drawing
      /// when an error is detected.
      /// </summary>
      /// <param name="next"></param>
      private static void Actualize(Curve3d next)
      {
         var curve = Curve.CreateFromGeCurve(next);
         var db = HostApplicationServices.WorkingDatabase;
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
      /// This method consolidates the operations performed
      /// by IsPolyline() and Normalize(), and returns a
      /// CompositeCurve3d representing a Polyline, if the 
      /// input curves can be joined to form one, or null 
      /// otherwise.
      /// 
      /// If one unconditionally intends to create a Polyline
      /// from the input if possible, this method should be 
      /// more efficient as it doesn't require iteration of 
      /// the input to determine if a Polyline can be created 
      /// from it in advance. Instead it checks each input 
      /// curve as they are encountered, and bails out if the 
      /// curve isn't a line or arc.
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
         if(curves is null)
            throw new ArgumentNullException(nameof(curves));
         var input = curves as Curve3d[] ?? curves.ToArray();
         if(input.Length < 2)
            return null;
         if(tol.Equals(default(Tolerance)))
            tol = Tolerance.Global;
         int count = input.Length;
         var joined = new bool[count];
         Curve3d current = input[0];
         if(current.IsClosed() || !(current is LineSegment3d or CircularArc3d))
            return null;
         if(validate)
            current.AssertIsValid();
         var output = new Curve3d[count];
         output[0] = current;
         joined[0] = true;
         var spInput = input.AsSpan();
         var spOutput = output.AsSpan();
         var spJoined = joined.AsSpan();
         int i = 1;
         while(i < count)
         {
            Point3d endPoint = spOutput[i - 1].EndPoint;
            bool found = false;
            for(int j = 0; j < count; j++)
            {
               if(spJoined[j])
                  continue;
               current = spInput[j];
               if(current.IsClosed() || !(current is LineSegment3d or CircularArc3d))
                  return null;
               if(validate)
                  current.AssertIsValid();
               if(endPoint.IsEqualTo(current.StartPoint, tol))
               {
                  spOutput[i] = current;
                  spJoined[j] = true;
                  found = true;
                  break;
               }
               else if(endPoint.IsEqualTo(current.EndPoint, tol))
               {
                  spOutput[i] = current.GetReverseParameterCurve();
                  spJoined[j] = true;
                  found = true;
                  break;
               }
            }
            if(!found)
               throw new InvalidOperationException("Disjoint curves");
            i++;
         }
         return new CompositeCurve3d(output);
      }

      public static void AssertIsValid(this Curve3d curve,
            bool rejectClosed = true,
            bool rejectSelfIntersecting = true)
      {
         if(curve is null)
            throw new ArgumentNullException(nameof(curve));
         var tolerance = Tolerance.Global.EqualPoint;
         // disqualify degenerate curves first:
         if(curve.IsDegenerate(out var entity))
            throw new ArgumentException("degenerate curve");
         var iv = curve.GetInterval();
         // disqualify unbounded curves
         if(iv.IsUnbounded)
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
            var cci = new CurveCurveIntersector3d(curve, curve, plane.Normal);
            if(cci.NumberOfIntersectionPoints > 0)
               throw new ArgumentException("self-intersecting curve");
         }
      }

      /// <summary>
      /// Given a sequence of contiguous Curve3d, this will 
      /// replace sub-sequences consisting of two or more line 
      /// or arc segments with a polyline.
      /// 
      /// The input sequence must form a contiguous chain of
      /// inter-connected curves.
      /// </summary>
      /// <param name="curves"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<Curve3d> Optimize(this IEnumerable<Curve3d> curves)
      {
         if(curves is null)
            throw new ArgumentNullException(nameof(curves));
         if(!curves.Any())
            yield break;
         if(!curves.Skip(1).Any())
            yield return curves.First();
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
               yield return curve;
               continue;
            }
            if(segments.Count == 1)
            {
               yield return segments[0];
               yield return curve;
               segments.Clear();
               continue;
            }
            foreach(var segment in segments.TryConvert())
               yield return segment;
            yield return curve;
            segments.Clear();
         }
         if(segments.Count > 0)
         {
            foreach(var segment in segments.TryConvert())
               yield return segment;
         }
      }

      static IEnumerable<Curve3d> TryConvert(this IEnumerable<Curve3d> segments)
      {
         var pline = segments.TryCreatePolyline();
         if(pline != null)
         {
            return new Curve3d[] { pline };
         }
         else
         {
            return segments;
         }
      }
   }

   /// <summary>
   /// For future use with explode region loops to
   /// a single spline (not implemented yet).
   /// </summary>

   public enum RegionExplodeType
   {
      Default = 0, // Default behavior of EXPLODE command 
      Polylines,   // Convert contiguous lines/arcs to polylines
      Spline       // Convert each loop to a single Spline
   }

}
