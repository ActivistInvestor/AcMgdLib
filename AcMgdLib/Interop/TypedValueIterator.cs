using AcMgdLib.Interop.Examples;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using System.Collections.Generic;
using System.Collections.Generic.Extensions;
using System.Diagnostics.Extensions;
using System.Extensions;

namespace Autodesk.AutoCAD.Runtime.LispInterop
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
   /// Enumerating the managed types and converting to
   /// a sequence of TypedValue has a cost. Converting 
   /// the TypedValues to a ResultBuffer also has a cost. 
   /// 
   /// This class caches both an array of TypedValue and 
   /// a ResultBuffer, so that if the resulting sequence
   /// of TypedValues is enumerated multiple times, the 
   /// conversion costs are not be replicated with each 
   /// enumeration. 
   /// 
   /// If the ResultBuffer is used or requested multiple 
   /// times, conversion will occur only on the first 
   /// request and be cached and returned on subsequent 
   /// requests. 
   /// 
   /// Caching is feasble in this case, because the
   /// instance and the input sequence are immutable.
   /// 
   /// </summary>

   /// AcMgdLib v0.12: refactored this class to use 
   /// the CachedEnumerable<T> base type.

   public class TypedValueIterator : CachedEnumerable<TypedValue>
   {
      readonly Cached<ResultBuffer> value;

      public TypedValueIterator(IEnumerable<TypedValue> source)
         : base(source)
      {
         Assert.IsNotNull(source, nameof(source));
         value = new(() => new ResultBuffer((TypedValue[]) Items));
      }

      public ResultBuffer Result => value.Value;

      public static implicit operator ResultBuffer(TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Result;
      }

      public static implicit operator TypedValue[](TypedValueIterator operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return (TypedValue[])operand.Items;
      }

   }



}


