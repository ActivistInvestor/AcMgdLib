/// EntityExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Entity class.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.Geometry;
using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   public static class ParallelBrepExtensions
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
      /// </summary>
      /// <param name="complex"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      public static IEnumerable<Curve3d[]> ParallelGetLoops(this AcBr.Complex complex)
      {
         if(complex is null)
            throw new ArgumentNullException(nameof(complex));

         var loops = complex.Shells
            .SelectMany(shell => shell.Faces)
            .SelectMany(face => face.Loops)
            .ToArray();

         var results = new Curve3d[loops.Length][];
         loops.ForEach(1, (loop, i) =>
         {
            var geCurves = loop.GetGeCurves();
            if(!geCurves.Any())
               throw new InvalidOperationException("no curves");

            if(geCurves.IsPolyline())   // create polyline from multiple GeCurves
               results[i] = new[] { new CompositeCurve3d(geCurves.Normalize()) };
            else  // return a single curve or create a spline
               results[i] = geCurves.ToArray();
         });

         return results;
      }

      /// <summary>
      /// Returns a Curve3d[] array containing all
      /// edge geometry in the Brep, with contiguous
      /// lines and arcs converted to Polylines.
      /// </summary>
      /// <param name="brep"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<Curve3d> ParallelGetEdgeGeometry(this Brep brep)
      {
         if(brep is null)
            throw new ArgumentNullException(nameof(brep));
         return brep.Complexes.Select(ParallelGetLoops)
            .SelectMany(outer => outer)
            .SelectMany(inner => inner);
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
            input[0].Validate();
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
                  next.Validate();
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

      static void Validate(this Curve3d curve)
      {
         // empty placeholder - not included.
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
      /// This method incorporates the operations performed
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
            current.Validate();
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
                  current.Validate();
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
   }
}

public static class ParallelArrayExtensions
{
   /// <summary>
   /// Conditional parallel execution based on array size:
   /// 
   /// The ParallelizationThreshold property determines the 
   /// point at which the operation is done in parallel. If 
   /// the array length is > ParallelizationThreshold, the 
   /// operation is done in parallel.
   /// 
   /// The threshold can also be passed as an argument.
   /// 
   /// If the operation is not done in parallel, it uses a
   /// Span<T> to access the array elements.
   /// </summary>

   /// User-tunable threshold

   public static int ParallelizationThreshold
   {
      get;set;
   }

   public static void ForEach<T>(this T[] array, Action<T> action)
   {
      ForEach<T>(array, ParallelizationThreshold, action);
   }

   public static void ForEach<T>(this T[] array, int threshold, Action<T> action)
   {
      if(array is null)
         throw new ArgumentNullException(nameof(array));
      if(action is null)
         throw new ArgumentNullException(nameof(action));
      threshold = threshold < 1 ? ParallelizationThreshold : threshold;
      if(array.Length > threshold)
      {
         var options = new ParallelOptions
         {
            MaxDegreeOfParallelism = Environment.ProcessorCount
         };
         Parallel.For(0, array.Length, options, i => action(array[i]));
      }
      else
      {
         var span = array.AsSpan();
         for(int i = 0; i < span.Length; i++)
            action(span[i]);
      }
   }

   /// <summary>
   /// Same as above except the action also takes the index
   /// of the array element.
   /// </summary>

   public static void ForEach<T>(this T[] array, Action<T, int> action)
   {
      ForEach<T>(array, ParallelizationThreshold, action);
   }

   public static void ForEach<T>(this T[] array, int threshold, Action<T, int> action)
   {
      threshold = threshold < 1 ? ParallelizationThreshold : threshold;
      if(array is null)
         throw new ArgumentNullException(nameof(array));
      if(action is null)
         throw new ArgumentNullException(nameof(action));
      if(array.Length > threshold)
      {
         var options = new ParallelOptions
         {
            MaxDegreeOfParallelism = Environment.ProcessorCount
         };
         Parallel.For(0, array.Length, options, i => action(array[i], i));
      }
      else
      {
         var span = array.AsSpan();
         for(int i = 0; i < span.Length; i++)
            action(span[i], i);
      }

   }

}
