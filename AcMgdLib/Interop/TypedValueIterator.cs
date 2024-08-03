using Autodesk.AutoCAD.DatabaseServices;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Extensions;
using System.Linq;

namespace Autodesk.AutoCAD.Runtime.Extensions
{
   /// <summary>
   /// Wraps an IEnumerable<TypedValue> and caches
   /// a ResultBuffer produced from it. This class
   /// is primarily designed to represent the result
   /// returned by methods of the ListBuilder class,
   /// allowing lazy enumeration and caching of the
   /// result (which in the case of ListBuilder can
   /// be somewhat expensive).
   /// 
   /// Multi-level Caching:
   /// 
   /// Enumerating the managed TypedValues has a cost, 
   /// and converting them to a ResultBuffer also has 
   /// a cost. 
   /// 
   /// This class caches both an array of TypedValue 
   /// and a ResultBuffer, so that in the event it is 
   /// enumerated multiple times, the enumeration costs 
   /// are not be replicated with each enumeration. If 
   /// the ResultBuffer is used or requested multiple 
   /// times, conversion will occur only on the first 
   /// request and be cached and returned on subsequent 
   /// requests. 
   /// 
   /// Caching is feasble in this case, because the
   /// instance and the input sequence are immutable.
   /// 
   /// </summary>
   
   public class TypedValueIterator : IEnumerable<TypedValue>
   {
      IEnumerable<TypedValue> source = null;
      readonly Cached<TypedValue[]> items;
      readonly Cached<ResultBuffer> value;

      public TypedValueIterator(IEnumerable<TypedValue> source)
      {
         Assert.IsNotNull(source, nameof(source));
         this.source = source;
         items = new(() => source.AsArray());
         value = new(() => new ResultBuffer(items));
      }

      public TypedValue[] Items => items.Value;
      public ResultBuffer Result => value.Value;

      public IEnumerator<TypedValue> GetEnumerator()
      {
         return ((IEnumerable<TypedValue>) items.Value).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      public static implicit operator ResultBuffer(TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Result;
      }

      public static implicit operator TypedValue[](TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Items;
      }
   }



}


