/// ListResult.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System.Collections.Generic;
using System.Collections.Generic.Extensions;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.DatabaseServices;

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
   /// Caching:
   /// 
   /// Enumerating managed objects and converting them
   /// to sequence of TypedValues has a cost. Converting 
   /// a sequence of TypedValues to a ResultBuffer also 
   /// has a cost. 
   /// 
   /// This class caches both an array of TypedValue and 
   /// a ResultBuffer, so that if the resulting sequence
   /// of TypedValues is enumerated multiple times, the 
   /// enumeration cost is avoided on all but the first
   /// enumeration (by enumerating the cached array rather
   /// than the iterator that produced the array).
   /// 
   /// If the ResultBuffer is used or requested multiple 
   /// times, conversion to a ResultBuffer occurs only on 
   /// the first request and is cached and returned on all
   /// subsequent requests. 
   /// 
   /// If the ResultBuffer is never requested, there is no
   /// conversion to a ResultBuffer.
   /// 
   /// Caching of the List enumeration and the result is 
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
   /// used both as the final result of ListBuilder operations
   /// that can be returned back to LISP, and as arguments
   /// to ListBuilder operations. When used as arguments to
   /// ListBuilder operations, there is no overhead related
   /// to conversion to a ResultBuffer.
   /// </summary>

   /// AcMgdLib v0.12: refactored this class to use 
   /// the CachedEnumerable<T> base type.
   ///
   /// AcMgdLib v0.14: Work started on refactoring this
   /// class to alter the behavior of the enumeration 
   /// when a ResultBuffer is created. 
   /// 
   /// That behavior may differ from the 'default' 
   /// behavior of the IEnumerator<TypedValue> that
   /// is returned by an instance.
   /// 
   /// The purpose of the planned work is to allow
   /// different behavior of the enumeration when the
   /// result is being returned by a LispFunction,
   /// in contrast to being used as an argument to a
   /// ListBuilder method.

   public class ListResult : CachedEnumerable<TypedValue>
   {
      ResultBuffer result;

      ListResult(IEnumerable<TypedValue> source, CachePolicy policy = CachePolicy.Eager)
         : base(source, policy)
      {
         Assert.IsNotNull(source, nameof(source));
      }

      internal static ListResult Create(IEnumerable<TypedValue> source, CachePolicy policy = CachePolicy.Eager)
      {
         return source as ListResult ?? new ListResult(source, policy);
      }

      public ResultBuffer Result =>
         result ?? (result = new ResultBuffer((TypedValue[])Items));

      public static implicit operator ResultBuffer(ListResult operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Result;
      }
   }

   public static class ListResultExtensions
   {
      public static ListResult ToResult(this IEnumerable<TypedValue> arg, CachePolicy policy = CachePolicy.Eager)
      {
         // Need to avoid allowing a ListResult
         // to wrap another ListResult:
         
         return ListResult.Create(arg, policy);
      }

   }



}


