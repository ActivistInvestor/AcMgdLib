using Autodesk.AutoCAD.DatabaseServices;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Extensions;

namespace Autodesk.AutoCAD.Runtime.Extensions
{
   /// <summary>
   /// Wraps an IEnumerable<TypedValue> and caches
   /// a ResultBuffer produced from it.
   /// </summary>
   
   public class TypedValueIterator : IEnumerable<TypedValue>
   {
      IEnumerable<TypedValue> source = null;
      Cached<ResultBuffer> value;

      public TypedValueIterator(IEnumerable<TypedValue> source)
      {
         Assert.IsNotNull(source, nameof(source));
         this.source = source;
         value = new (() => new ResultBuffer(source.AsArray()));
      }

      public ResultBuffer Value 
      { 
         get
         {
            return value;
         } 
      }

      public void Invalidate()
      {
         value.Invalidate();
      }

      public IEnumerator<TypedValue> GetEnumerator()
      {
         return source.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return ((IEnumerable)source).GetEnumerator();
      }

      public static implicit operator ResultBuffer(TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Value;
      }
   }



}


