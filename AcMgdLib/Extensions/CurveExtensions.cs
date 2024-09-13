/// CurveExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Curve extension methods supporting the use of 
/// the Curve.GetSplitCurves() method.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.EditorExtensions;
using Autodesk.AutoCAD.Geometry;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static partial class CurveExtensions
   {

      /// <summary>
      /// A wrapper for GetSplitCurves() that performs several
      /// related operations efficiently (e.g., no Linq).
      /// 
      /// 1. It's common to require input coordinates to be
      ///    ordered by their distance from the start of the
      ///    curve, as IntersectWith() returns coordinates in
      ///    an arbitrary order, and multiple calls to that
      ///    API can produce numerous points on a curve in no
      ///    particular order.
      ///    
      /// 2. It's also common to haveo to deal with coordinates 
      ///    that may not lie exactly on a curve.
      ///    
      /// 3. Although not commonly needed, removal of duplicate 
      ///    points/parameters from ordered results. If the snap 
      ///    to curve option is used, equality comparisons are
      ///    done against the snapped-to coordinates, which can
      ///    result in an equal parameter from two input points 
      ///    that are not equal and for that reason, this option 
      ///    should be used with care.
      ///    
      /// This API encapsulates all of these operations allowing
      /// simplified use of GetSplitCurves(). All operations are
      /// optional.
      /// 
      /// This API also addresses an issue with the Polyline3d type, 
      /// whose implementation of GetSplitCurves() does not behave
      /// like most other types of Curve (it does not join the last 
      /// and first fragments produced from a closed curve).
      /// 
      /// Two overloads are provided, accepting an array of Point3d
      /// or a Point3dCollection.
      /// </summary>
      /// <param name="curve">The curve from which fragments are
      /// to be produced.</param>
      /// <param name="points">The array or Point3dCollection of
      /// points identifying the start/end of each fragment.</param>
      /// <param name="reorder">A value indicating if the input
      /// points should be reordered based on their distance from
      /// the start of the input curve.</param>
      /// <param name="snap">A value indicating if the input
      /// points should be modified to ensure they lie on the input
      /// Curve</param>
      /// <returns>A DBObjectCollection containing the generated
      /// curve fragments</returns>

      public static DBObjectCollection GetFragmentsAt(this Curve curve,
         Point3dCollection points,
         bool reorder = false,
         bool snap = false,
         bool distinct = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         return GetFragments(curve, points.ToArray(), snap, reorder, distinct);
      }

      public static DBObjectCollection GetFragmentsAt(this Curve curve,
         Point3d[] points,
         bool reorder = false,
         bool snap = false, 
         bool distinct = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         Point3d[] array = new Point3d[points.Length];
         points.CopyTo(array, 0);
         return GetFragments(curve, array, snap, reorder, distinct);
      }

      /// <summary>
      /// Returns the parameters of multiple points on the given Curve, 
      /// optionally snapped to the nearest point on the curve and/or 
      /// ordered by their distance from the start of the curve, and
      /// optionally, with duplicate elements removed.
      /// </summary>
      /// <param name="curve">The Curve to operate on</param>
      /// <param name="points">An array of Point3d or a Point3dCollection
      /// contianing the points whose parameters are being requested</param>
      /// <param name="snap">A value indicating if the input points 
      /// should be modified to ensure they lie on the input Curve</param>
      /// <param name="ordered">A value indicating if the resulting
      /// parameters should be returned in ascending order</param>
      /// <param name="distinct">Only applicable if <paramref name="ordered"/>
      /// is true. A value indicating if duplicate values should be removed 
      /// from the result. This argument usually doesn't need to be specified, 
      /// as most AutoCAD APIs taking the result will not create zero-length 
      /// curves. A value of true can incurr significant overhead.</param>
      /// <returns>An array of the resulting parameters</returns>

      public static double[] GetParametersAtPoints(this Curve curve, 
         Point3d[] points, 
         bool snap = false, 
         bool ordered = false, 
         bool distinct = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         double[] result = new double[points.Length];
         var span = points.AsSpan();
         var spanRes = result.AsSpan();
         for(int i = 0; i < points.Length; i++)
         {
            if(snap)
               spanRes[i] = curve.GetParameterAtPoint(curve.GetClosestPointTo(span[i], false));
            else
               spanRes[i] = curve.GetParameterAtPoint(span[i]);
         }
         if(ordered)
         {
            static int comparer(double a, double b)
            {
               return Math.Abs(a - b) < 1.0e-8 ? 0 : a > b ? 1 : -1;
            }
            spanRes.Sort(comparer);
            if(distinct && points.Length > 1) 
               return Distinct(spanRes);
         }
         return result;
      }

      /// <summary>
      /// The doubles in the argument must be ordered.
      /// 
      /// The epsilon is harded-coded at 1.0e-8
      /// </summary>

      public static double[] Distinct(Span<double> span, double epsilon = 1.0e-8)
      {
         Assert.MustBeTrue(span != null);
         if(span.Length < 2)
            return span.ToArray();
         List<double> list = new List<double>(span.Length);
         double last = span[0];
         list.Add(last);
         for(int i = 1; i < span.Length; i++)
         {
            double next = span[i];
            if(Math.Abs(next-last) > epsilon)
            {
               list.Add(next);
            }
            last = next;
         }
         if(list.Count == span.Length)
            return span.ToArray();
         else
            return list.ToArray();
      }

      public static double[] GetParametersAtPoints(this Curve curve,
         Point3dCollection points,
         bool snap = false,
         bool ordered = false,
         bool distinct = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         double[] result = new double[points.Count];
         var spanRes = result.AsSpan();
         for(int i = 0; i < points.Count; i++)
         {
            if(snap)
               spanRes[i] = curve.GetParameterAtPoint(curve.GetClosestPointTo(points[i], false));
            else
               spanRes[i] = curve.GetParameterAtPoint(points[i]);
         }
         if(ordered)
         {
            spanRes.Sort(static (a, b) => Math.Abs(a - b) < 1.0e-8 ? 0 : a > b ? 1 : -1);
            if(distinct && points.Count > 1)
               return Distinct(spanRes);
         }
         return result;
      }

      /// <summary>
      /// The following APIs modify their array argument,
      /// and for that reason are not publicly-exposed. 
      /// </summary>

      static DBObjectCollection GetFragments(this Curve curve, 
         Point3d[] points, 
         bool snap = false,
         bool ordered = false,
         bool distinct = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         if(points.Length == 0)
            throw new ArgumentException("Empty array");
         var doubles = new DoubleCollection(curve.GetParametersAtPoints(points, snap, ordered, distinct));
         var result = curve.GetSplitCurves(doubles);
         try
         {
            if(curve.Closed && curve is Polyline3d && result.Count > 2)
            {
               Curve start = (Curve)result[0];
               Curve end = (Curve)result[result.Count - 1];
               end.JoinEntity(start);
               result.Remove(start);
               start.Dispose();
            }
            return result;
         }
         catch(System.Exception ex)
         {
            result.Dispose();
            throw ex;
         }
      }

      /// <summary>
      /// Sorts the given array of Point3d[] by each element's
      /// distance from the start of the curve. 
      /// 
      /// Note: This method modifies its argument
      /// </summary>
      /// <param name="curve"></param>
      /// <param name="points"></param>
      /// <param name="snapToCurve">A value indicating if the input
      /// coordinates should be adjusted to the closest point on the
      /// input curve.</param>

      static void OrderByParameter(this Point3d[] points, Curve curve)
      {
         var values = points.AsSpan();
         double[] parameters = new double[values.Length];
         var keys = parameters.AsSpan();
         for(int i = 0; i < values.Length; i++)
            keys[i] = curve.GetParameterAtPoint(values[i]);
         keys.Sort<double, Point3d>(values);
      }

      /// <summary>
      /// Translates the input array coordinates to lie on
      /// the curve, at a point closest to each element.
      ///
      /// Note: This method modifies its argument
      /// </summary>
      /// <param name="points"></param>
      /// <param name="curve"></param>

      static void SnapTo(this Point3d[] array, Curve curve)
      {
         var span = array.AsSpan();
         for(int i = 0; i < array.Length; i++)
            span[i] = curve.GetClosestPointTo(span[i], false);
      }
   }

   public class CurveCollection : DBObjectCollection<Curve>
   {
      public CurveCollection(DBObjectCollection source) : base(source)
      {
      }
   }

   public class DBObjectCollection<T> : IReadOnlyList<T>, IDisposable where T: DBObject
   {
      DBObjectCollection source;

      protected internal DBObjectCollection(DBObjectCollection source)
      {
         Assert.IsNotNull(source);
         this.source = source;
      }

      public DBObject this[int index] => source[index];

      /// <summary>
      /// Returns the 'nth' occurence of the generic argument
      /// </summary>

      T IReadOnlyList<T>.this[int index]
      {
         get
         {
            int i = 0;
            foreach(DBObject item in source)
            {
               if(item is T result)
               {
                  if(index == i++)
                     return result;
               }
            }
            throw new IndexOutOfRangeException(i.ToString());
         }
      }

      public int Count => source.Count;

      public void Dispose()
      {
         if(source != null)
         {
            foreach(DBObject item in source)
            {
               if(item != null && item.ObjectId.IsNull)
                  item.Dispose();
            }
            source.Dispose();
            source = null;
         }
      }

      public static implicit operator DBObjectCollection<T>(DBObjectCollection source)
      {
         Assert.IsNotNull(source,nameof(source));
         return new DBObjectCollection<T>(source);
      }

      public static implicit operator DBObjectCollection(DBObjectCollection<T> source)
      {
         Assert.IsNotNull(source, nameof(source));
         return source.source ?? 
            throw new ObjectDisposedException(nameof(DBObjectCollection<T>));
      }

      public IEnumerator<T> GetEnumerator()
      {
         return source.OfType<T>().GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }
   }


}
