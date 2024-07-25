

using CacheUtils;
using System.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Security.Cryptography.X509Certificates;
using TestCases;
using Autodesk.AutoCAD.Geometry;
using System.Security.RightsManagement;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Windows.Controls.Primitives;

namespace System.Linq.Extensions
{
   public static class EnumerableExtensions
   {
      /// <summary>
      /// Counts the number of occurences of equal elements
      /// in the input sequence. 
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="source"></param>
      /// <param name="comparer"></param>
      /// <returns></returns>

      public static IDictionary<T, int> CountMany<T>(this IEnumerable<T> source,
         IEqualityComparer<T> comparer = null)
      {
         if(source == null) throw new ArgumentNullException("source");
         CountMap<T> map = new CountMap<T>(comparer);
         foreach(T item in source)
         {
            map.Increment(item);
         }
         return map.ToDictionary();
      }

      /// <summary>
      /// Counts the number of occurences of TKey from
      /// a sequence of T, where a TKey is obtained from
      /// each T in the sequence. The returned dictionary
      /// maps TKeys to their count. The same operation
      /// can be performed using CountMany<T>, by passing
      /// the output of doing a Select(keySelector) on 
      /// the input sequence of T. Hence, this combines
      /// the operations of Select(...).CountMany() but
      /// without the overhead of an additional iterator.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <typeparam name="TKey"></typeparam>
      /// <param name="source"></param>
      /// <param name="keySelector"></param>
      /// <param name="comparer"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IDictionary<TKey, int> CountManyBy<T, TKey>(this IEnumerable<T> source,
         Func<T, TKey> keySelector,
         IEqualityComparer<TKey> comparer = null)
      {
         if(keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));
         CountMap<TKey> counter = new CountMap<TKey>(comparer);

         foreach(T item in source)
         {
            counter.Increment(keySelector(item));
         }
         return counter.ToDictionary();
      }

      /// <summary>
      /// Returns its argument if it is an array, otherwise
      /// returns the result of ToArray(). Primarily used
      /// in cases where the result is not being modified
      /// by the caller.
      /// </summary>

      public static T[] AsArray<T>(this IEnumerable<T> source, bool createIfNull = false)
      {
         if(source == null && !createIfNull) throw new ArgumentNullException("source");
         return source as T[] ?? source?.ToArray() ?? new T[0];
      }

      /// <summary>
      /// Returns a copy of a Dictionary<TKey, TValue> that
      /// uses the same IEqualityComparer<T> as the source.
      /// </summary>

      public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this Dictionary<TKey, TValue> source)
      {
         if(source == null)
            throw new ArgumentNullException(nameof(source));
         return new Dictionary<TKey, TValue>(source, source.Comparer);
      }

      /// <summary>
      /// Returns a copy of a HashSet<T> that uses the same
      /// IEqualityComparer<T> as the source.
      /// </summary>

      public static HashSet<T> Clone<T>(this HashSet<T> source)
      {
         if(source == null)
            throw new ArgumentNullException(nameof(source));
         return new HashSet<T>(source, source.Comparer);
      }

      /// <summary>
      /// Returns the number of elements enumerated by 
      /// the sequence, up to the max argument. Otherwise, 
      /// returns -1.
      /// 
      /// This method is useful for determining if a given 
      /// sequence will enumerate fewer/more than a specified 
      /// number of elements, without forcing the enumeration 
      /// of an entire longer sequence.
      /// </summary>
      /// <param name="source">The sequence whose count is being requested</param>
      /// <param name="max">The maximum number of elements to count</param>
      /// <returns>The number of elements enumerated by the
      /// sequence if less than or equal to max, otherwise -1.
      /// </returns>

      public static int CountTo(this IEnumerable source, int max)
      {
         if(source == null)
            throw new ArgumentNullException(nameof(source));
         var e = source.GetEnumerator();
         try
         {
            int i = 0;
            while(e.MoveNext())
            {
               if(++i > max)
                  return -1;
            }
            return i;
         }
         finally
         {
            if(e is IDisposable d)
               d.Dispose();
         }
      }

      /// <summary>
      /// Aggregates a function taking two instances of T
      /// as arguments and returns a bool, across a sequence 
      /// of T, and returns true if all invocations return 
      /// true, or false otherwise.
      /// 
      /// If the sequence contains less than two elements, 
      /// the result is true.
      /// 
      /// Evaluation of the function stops at the first 
      /// result of false.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="values"></param>
      /// <param name="compare"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="InvalidOperationException"></exception>

      /// Possible alternative names:
      /// 
      ///    AggregateWhile()
      ///    

      public static bool All<T>(this IEnumerable<T> values, Func<T, T, bool> compare)
      {
         if(values == null)
            throw new ArgumentNullException(nameof(values));
         if(compare == null)
            throw new ArgumentNullException(nameof(compare));
         using(IEnumerator<T> e = values.GetEnumerator())
         {
            if(e.MoveNext())
            {
               T last = e.Current;
               while(e.MoveNext())
               {
                  T next = e.Current;
                  if(!compare(last, next))
                     return false;
                  last = next;
               }
            }
            return true;
         }
      }


      /// <summary>
      /// Aggregates a function that takes two arguments of 
      /// TValue and returns a bool, across a sequence of T,
      /// and returns true if all invocations return true,
      /// or false otherwise. TValue arguments are obtained
      /// from elements using the selector function.
      /// 
      /// </summary>

      public static bool AllBy<T, TValue>(this IEnumerable<T> values,
         Func<T, TValue> selector,
         Func<TValue, TValue, bool> compare)
      {
         if(values == null)
            throw new ArgumentNullException(nameof(values));
         if(selector == null)
            throw new ArgumentNullException(nameof(selector));
         if(compare == null)
            throw new ArgumentNullException(nameof(compare));
         using(IEnumerator<T> e = values.GetEnumerator())
         {
            if(e.MoveNext())
            {
               TValue last = selector(e.Current);
               while(e.MoveNext())
               {
                  TValue next = selector(e.Current);
                  if(!compare(last, next))
                     return false;
                  last = next;
               }
            }
            return true;
         }
      }

      /// <summary>
      /// Convenience wrappers for the above that indicate if
      /// a given sequence is ordered (ascending/descending)
      /// </summary>

      public static bool IsAscending<T>(this IEnumerable<T> source,
         IComparer<T> comparer = null)
      {
         comparer = comparer ?? Comparer<T>.Default;
         return source.All((a, b) => comparer.Compare(a, b) < 0);
      }

      public static bool IsDescending<T>(this IEnumerable<T> source,
         IComparer<T> comparer = null) 
      {
         comparer = comparer ?? Comparer<T>.Default;
         return source.All((a, b) => comparer.Compare(a, b) > 0);
      }

      public static bool IsAscendingBy<T, TValue>(this IEnumerable<T> source,
            Func<T, TValue> selector) where TValue : IComparable<TValue>
      {
         return source.AllBy(selector, (a, b) => a.CompareTo(b) < 0);
      }

      public static bool IsDescendingBy<T, TValue>(this IEnumerable<T> source,
            Func<T, TValue> selector) where TValue : IComparable<TValue>
      {
         return source.AllBy(selector, (a, b) => a.CompareTo(b) > 0);
      }

      /// Can be depreciated in favor of MinBy() in .NET 7+

      public static TSource Nearest<TSource, T>(this IEnumerable<TSource> source, 
         Func<TSource, T> selector, // Selects the comparison value from each T
         Func<T, T, T> difference,  // computes the deviation from value to each value
         Func<T, T, bool> comparer, // return true if first arg is < second arg
         T value)
      {
         TSource result = source.First();
         T m = selector(result);
         T deviation = difference(value, m);
         foreach(TSource item in source.Skip(1))
         {
            T d = difference(value, selector(item));
            if(comparer(d, deviation))
            {
               deviation = d;
               result = item;
            }
         }
         return result;
      }

      /// <summary>
      /// Can be depreciated in favor of MinBy() in .NET 7+
      /// </summary>
      /// <typeparam name="TSource"></typeparam>
      /// <typeparam name="T"></typeparam>
      /// <param name="source"></param>
      /// <param name="value"></param>
      /// <param name="selector"></param>
      /// <param name="difference"></param>
      /// <returns></returns>
      public static TSource NearestTo<TSource, T>(this IEnumerable<TSource> source,
         T value,
         Func<TSource, T> selector, // Selects the comparison value from each T
         Func<T, T, T, bool> difference) //  return true if first is closer to last 
         
      {
         TSource result = source.First();
         T m = selector(result);
         foreach(TSource item in source.Skip(1))
         {
            if(difference(selector(item), m, value))
            {
               result = item;
            }
         }
         return result;
      }

      //static void NearestTest()
      //{
      //   int[] array = new[] { 1, 2, 3, 4, 5, 8, 11, 12, 13, 14, 15 };
      //   int nearest = array.NearestTo(7, a => a, 
      //      (a, b, c) => Math.Abs(a - c) < Math.Abs(b - c));

      //   int nearest2 = array.MinBy()
      //}
   }

   public static class Test
   { 
      public static void BinaryAllTests()
      {
         int[] values = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

         /// true if the elements appear in ascending order:
         
         values.All((a, b) => a < b);

         (string Name, float Price)[] items =
            { ("Apples", 2.5f), ("Pears", 2.25f), ("Bananas", 1.5f), ("Oranges", 1.5f) };

         /// Simple case, does not consider sub-ordering by name.
         /// true if items are in decending order by price.

         items.AllBy(item => item.Price, (a, b) => a > b);

         // comparer function that orders by price descending,
         // and then by name ascending:

         static int Compare((string Name, float Price) a, (string Name, float Price) b)
         {
            int n = a.Price.CompareTo(b.Price);
            return n != 0 ? n : b.Name.CompareTo(a.Name);
         }

         /// Complex case, indicates if the items array is
         /// ordered in descending order by price, and then
         /// ascending order by name:

         items.All((a, b) => Compare(a, b) > 0);

         // Using All() to determine if a list of
         // related items appear in natural order:
         
         var item = new LinkedItem();
         List<LinkedItem> list = new List<LinkedItem>() { item };
         for(int i = 0; i < 10; i++)
            list.Add(item = new LinkedItem(item));

         // true if the items in list are in natural order:

         list.All((a, b) => a.Next == b);

      }

      class LinkedItem
      {
         public LinkedItem(LinkedItem prev = null)
         {
            prev.Next = this;
         }

         public LinkedItem Next { get; private set; }
      }


   }


}
