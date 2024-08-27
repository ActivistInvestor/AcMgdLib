/// CurveExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Curve class.

using Autodesk.AutoCAD.ApplicationServices.EditorExtensions;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

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
      /// This API encapsulates both of these operations allowing
      /// simplified use of GetSplitCurves(). Both operations are
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
      /// <param name="snapToCurve">A value indicating if the input
      /// points should be modified to ensure they lie on the input
      /// Curve</param>
      /// <returns>A DBObjectCollection containing the generated
      /// curve fragments</returns>

      public static DBObjectCollection GetFragmentsAt(this Curve curve,
         Point3dCollection points,
         bool reorder = false,
         bool snapToCurve = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         return GetFragments(curve, points.ToArray(), snapToCurve, reorder);
      }

      public static DBObjectCollection GetFragmentsAt(this Curve curve,
         Point3d[] points,
         bool reorder = false,
         bool snapToCurve = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         Point3d[] array = new Point3d[points.Length];
         points.CopyTo(array, 0);
         return GetFragments(curve, array, snapToCurve, reorder);
      }

      /// <summary>
      /// Returns the parameters of multiple points on the given Curve, 
      /// optionally snapped to the nearest point on the curve and/or 
      /// ordered by their distance from the start of the curve.
      /// </summary>
      /// <param name="curve">The Curve to operate on</param>
      /// <param name="points">An array of Point3d or a Point3dCollection
      /// contianing the points whose parameters are being requested</param>
      /// <param name="snap">A value indicating if the input points 
      /// should be modified to ensure they lie on the input Curve</param>
      /// <param name="ordered">A value indicating if the resulting
      /// parameters should be ordered</param>
      /// <returns>An array of the resulting parameters</returns>

      public static double[] GetParametersAtPoints(this Curve curve, Point3d[] points, bool snap = false, bool ordered = false, bool removeDuplicates = false)
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
            double eps = Tolerance.Global.EqualVector;
            int comparer(double a, double b)
            {
               if(Math.Abs(a - b) < eps)
                  return 0;
               else
                  return a > b ? 1 : -1;
            }
            spanRes.Sort(comparer);
            if(removeDuplicates && points.Length > 1) 
            {
               List<double> list = new List<double>(spanRes.Length);
               double cur = spanRes[0];
               list.Add(cur);
               for(int i = 1; i < spanRes.Length; i++)
               {
                  if(Math.Abs(spanRes[i] - cur) < eps)
                  {
                     list.Add(spanRes[i]);
                  }
                  cur = spanRes[i];
               }
               var spanList = CollectionsMarshal.AsSpan(list);
            }
         }
         return result;
      }

      public static double[] Distinct(this double[] array, Func<double, double, bool> comparer)
      {
         Assert.IsNotNull(array);
         if(array.Length < 2)
            return array.ToArray();
         var span = array.AsSpan();
         List<double> list = new List<double>(span.Length);
         double cur = span[0];
         list.Add(cur);
         for(int i = 1; i < span.Length; i++)
         {
            double next = span[i];
            if(!comparer(next, cur))
            {
               list.Add(next);
            }
            cur = next;
         }
         if(list.Count == span.Length)
            return span.ToArray();
         else
            return CollectionsMarshal.AsSpan(list).ToArray();
      }

      public static double[] GetParametersAtPoints(this Curve curve, Point3dCollection points, bool snap = false, bool ordered = false)
      {
         double[] result = new double[points.Count];
         for(int i = 0; i < points.Count; i++)
         {
            if(snap)
               result[i] = curve.GetParameterAtPoint(curve.GetClosestPointTo(points[i], false));
            else
               result[i] = curve.GetParameterAtPoint(points[i]);
         }
         if(ordered)
            Array.Sort(result);
         return result;
      }


      //Complicated by the need to ensure that duplicate points
      //are not included in the points passed to GetSplitCurves()
      //public static DBObjectCollection GetFragmentsAt(this Curve curve,
      //   IEnumerable<Entity> entities)
      //{
      //   Point3dCollection points = new Point3dCollection();
      //}

      /// <summary>
      /// The following APIs modify their array argument,
      /// and for that reason are not publicly-exposed. 
      /// </summary>

      static DBObjectCollection GetFragments(this Curve curve, 
         Point3d[] points, 
         bool snapToCurve = false,
         bool reorder = false)
      {
         Assert.IsNotNullOrDisposed(curve, nameof(curve));
         Assert.IsNotNull(points, nameof(points));
         if(points.Length == 0)
            throw new ArgumentException("Empty array");
         var doubles = new DoubleCollection(curve.GetParametersAtPoints(points, snapToCurve, reorder));
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

   //public class DBObjectList<T> : IList<T>, IDisposable where T : DBObject
   //{
   //   DBObjectCollection source;

   //   protected internal DBObjectList(DBObjectCollection source)
   //   {
   //      Assert.IsNotNull(source);
   //      this.source = source;
   //   }

   //   public DBObject this[int index] => source[index];

   //   /// <summary>
   //   /// Returns the 'nth' occurence of the generic argument
   //   /// </summary>

   //   T IList<T>.this[int index]
   //   {
   //      get
   //      {
   //         int i = 0;
   //         foreach(DBObject item in source)
   //         {
   //            if(item is T result)
   //            {
   //               if(index == i++)
   //                  return result;
   //            }
   //         }
   //         throw new IndexOutOfRangeException(i.ToString());
   //      }
   //      set
   //      {
   //         throw new NotImplementedException();
   //      }
   //   }

   //   public int Count => source.Count;

   //   public bool IsReadOnly => ((IList)source).IsReadOnly;

   //   public void Dispose()
   //   {
   //      if(source != null)
   //      {
   //         foreach(DBObject item in source)
   //         {
   //            if(item != null && item.ObjectId.IsNull)
   //               item.Dispose();
   //         }
   //         source.Dispose();
   //         source = null;
   //      }
   //   }

   //   public static implicit operator DBObjectList<T>(DBObjectCollection source)
   //   {
   //      Assert.IsNotNull(source, nameof(source));
   //      return new DBObjectList<T>(source);
   //   }

   //   public static implicit operator DBObjectCollection(DBObjectList<T> source)
   //   {
   //      Assert.IsNotNull(source, nameof(source));
   //      return source.source ??
   //         throw new ObjectDisposedException(nameof(DBObjectCollection<T>));
   //   }

   //   public IEnumerator<T> GetEnumerator()
   //   {
   //      return source.OfType<T>().GetEnumerator();
   //   }

   //   IEnumerator IEnumerable.GetEnumerator()
   //   {
   //      return this.GetEnumerator();
   //   }

   //   public int IndexOf(T item)
   //   {
   //      return source.IndexOf(item);
   //   }

   //   public void Insert(int index, T item)
   //   {
   //      source.Insert(index, item);
   //   }

   //   public void RemoveAt(int index)
   //   {
   //      source.RemoveAt(index);
   //   }

   //   public void Add(T item)
   //   {
   //      source.Add(item);
   //   }

   //   public void Clear()
   //   {
   //      source.Clear();
   //   }

   //   public bool Contains(T item)
   //   {
   //      return source.Contains(item);
   //   }

   //   public void CopyTo(T[] array, int arrayIndex)
   //   {
   //      var items = source.OfType<T>().ToArray();
   //      items.CopyTo(array, arrayIndex);
   //   }

   //   public bool Remove(T item)
   //   {
   //      bool result = source.Contains(item);
   //      if(result)
   //         source.Remove(item);
   //      return result;
   //   }
   //}

}
