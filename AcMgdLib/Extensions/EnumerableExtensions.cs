/// EnumerableExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System.Collections.Generic;
using System.Diagnostics.Extensions;

namespace System.Linq.Extensions
{
   public static partial class EnumerableExtensions 
   {
      /// <summary>
      /// Like Concat() except allows multiple list to be
      /// concatenated and allows null elements.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="source"></param>
      /// <param name="rest"></param>
      /// <returns></returns>

      public static IEnumerable<T> Append<T>(this IEnumerable<T> source, params IEnumerable<T>[] rest)
      {
         Assert.IsNotNull(source, nameof(source));
         foreach(T item in source)
            yield return item;
         if(rest != null)
         {
            foreach(var collection in rest)
            {
               if(collection != null)
               {
                  foreach(var item in collection)
                     yield return item;
               }
            }
         }
      }
   } 
}