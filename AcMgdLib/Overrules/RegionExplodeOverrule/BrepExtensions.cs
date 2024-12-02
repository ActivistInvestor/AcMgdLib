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
using Autodesk.AutoCAD.Geometry;
using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;

namespace AcMgdLib.DatabaseServices
{
   public static class BrepExtensions
   {
      /// <summary>
      /// Returns an sequence of Curve3d[] arrays that can be 
      /// used to generate Curve entities. The process has been
      /// refactored to support parallel execution, by moving
      /// the non thread-safe code to the caller (which must
      /// create the Curve entities, and which cannot be done
      /// in parallel). 
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
      /// Revised: Callers can explicitly specify if the operation
      /// should execute in parallel, via the parallel argument.
      /// </summary>
      /// <param name="complex">The Brep Complex whose loops are
      /// to be obtained.</param>
      /// <param name="parallel">A value indicating if the operation
      /// should execute in parallel (true = yes)</param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      public static IEnumerable<Curve3d[]> GetLoops(this AcBr.Complex complex,
         bool convertToPolylines = true,
         bool parallel = false, 
         Func<BoundaryLoop, bool> predicate = null)

      {
         if(complex is null)
            throw new ArgumentNullException(nameof(complex));
         predicate ??= loop => true;
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
      /// Returns a Curve3d[] array containing all
      /// edge geometry in the Brep, converting loops
      /// containing only contiguous lines and arcs 
      /// to Polylines.
      /// </summary>
      /// <param name="brep"></param>
      /// <param name="convertToPoly"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      /// <param name="parallel">A value indicating if
      /// the operation should execute in parallel</param>
      public static IEnumerable<Curve3d> Explode(this Brep brep, 
         bool convertToPoly = true, 
         bool parallel = false,
         Func<BoundaryLoop, bool> predicate = null)
      {
         if(brep is null || brep.IsDisposed)
            throw new ArgumentNullException(nameof(brep));
         var result = brep.Complexes.Select(c => c.GetLoops(convertToPoly, parallel, predicate))
            .SelectMany(level1 => level1)
            .SelectMany(level2 => level2)
            .ToArray();
         return parallel ? result.ToArray() : result;
      }

      public static IEnumerable<Curve3d> GetGeCurves(this BoundaryLoop loop)
      {
         if(loop is null)
            throw new ArgumentNullException(nameof(loop));
         return loop.Edges.Select(edge =>
         {
            if(edge.Curve is ExternalCurve3d crv && crv.IsNativeCurve)
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
            Point3d endPoint = spOutput[idx - 1].EndPoint;
            bool found = false;
            for(int i = 0; i < count; i++)
            {
               if(spJoined[i])
                  continue;
               var next = spInput[i];
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
               throw new InvalidOperationException("Disjoint curves");
            idx++;
         }
         return output;
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
   }
}
