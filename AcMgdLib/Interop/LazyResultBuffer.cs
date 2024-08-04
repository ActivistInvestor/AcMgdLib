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
   /// conversion costs are avoided on all but the first
   /// enumeration. 
   /// 
   /// If the ResultBuffer is used or requested multiple 
   /// times, conversion to a ResultBuffer occurs only on 
   /// the first request and is cached and returned on all
   /// subsequent requests. 
   /// 
   /// If a ResultBuffer is never requested, there is no
   /// conversion to a ResultBuffer.
   /// 
   /// Caching of the source enumeration and the result is 
   /// feasble if not warranted in this case, because the
   /// instance and the input sequence are immutable.
   /// 
   /// While using Enumerable.Cast() to cast a ResultBuffer 
   /// to an IEnumerable<TypedValue> is possible, doing that
   /// is unwise, because ResultBuffers store data as native 
   /// resbufs and must do some work to convert them back to 
   /// managed TypedValues. For that reason, instances of this
   /// type cache the original sequence of TypedValues that 
   /// are used to create ResultBuffers, and enumerates those 
   /// rather than enumerating the ResultBuffer's contents.
   /// 
   /// This type is designed so that an instance of it can be
   /// used both as a final result of ListBuilder operations
   /// that can be returned back to LISP, and as arguments
   /// to ListBuilder operations. When used as arguments to
   /// ListBuilder operations, there is no overhead related
   /// to conversion to a ResultBuffer.
   /// </summary>

   /// AcMgdLib v0.12: refactored this class to use 
   /// the CachedEnumerable<T> base type.

   public class LazyResultBuffer : CachedEnumerable<TypedValue>
   {
      ResultBuffer value;

      public LazyResultBuffer(IEnumerable<TypedValue> source)
         : base(source)
      {
         Assert.IsNotNull(source, nameof(source));
      }

      public ResultBuffer Value =>
         value ?? (value = new ResultBuffer((TypedValue[])Items));

      public static implicit operator ResultBuffer(LazyResultBuffer operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Value;
      }
   }



}


