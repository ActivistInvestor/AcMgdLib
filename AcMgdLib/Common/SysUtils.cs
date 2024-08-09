/// SysUtils.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Utility
{
   public static class SysUtils
   {
      public static T OrEmpty<T>(this T value) where T : new()
      {
         if(value != null)
            return value;
         else
            return new T();
      }

      public static string Remove(this string value, string str)
      {
         return value.Replace(str, "");
      }

      /// <summary>
      /// Returns the argument if it is an array, 
      /// or converts it to an array.  
      /// 
      /// Useful for avoiding a costly call to ToArray() in 
      /// cases where it's possible that an IEnumerable<T> 
      /// might already be an array and the operation that 
      /// requires the array does not modify it.
      /// 
      /// It's the caller's reponsibility to verify that the
      /// the array is/is not modified by whatever operations 
      /// it is supplied to.
      /// </summary>

      public static T[] AsArray<T>(this IEnumerable<T> source)
      {
         if(source == null)
            throw new ArgumentNullException(nameof(source));
         return source as T[] ?? source.ToArray();
      }

      public static object[] AsArray(this IEnumerable source)
      {
         return source as object[] ?? source.Cast<Object>().ToArray();
      }


   }
}
