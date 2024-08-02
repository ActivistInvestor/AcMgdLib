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
   /// a ResultBuffer produced from it.
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

      public TypedValue[] Items => items;
      public ResultBuffer Value => value;

      public IEnumerator<TypedValue> GetEnumerator()
      {
         return ((IEnumerable<TypedValue>) Items).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      public static implicit operator ResultBuffer(TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Value;
      }

      public static implicit operator TypedValue[](TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Items;
      }
   }



}


