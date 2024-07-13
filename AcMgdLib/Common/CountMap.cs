/// CountMap.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for the AcDbLinq library.

using System.Diagnostics.Extensions;
using System.Collections;
using System.Collections.Generic;

namespace System.Linq.Extensions
{

   public static class CountMapExtensions
   {
      public static IDictionary<T, int> CountAll<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
      {
         return new CountMap<T>(source, comparer).ToDictionary();
      }

      public static IDictionary<TKey, int> CountAllBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, IEqualityComparer<TKey> comparer = null)
      {
         return new CountMap<TKey>(source.Select(selector), comparer).ToDictionary();
      }

   }

   public class CountMap<T> : IEnumerable<KeyValuePair<T, int>>
   {
      Dictionary<T, Box> map;

      public CountMap(IEqualityComparer<T> comparer = null)
      {
         map = new Dictionary<T, Box>(comparer);
      }

      public CountMap(IEnumerable<T> source, IEqualityComparer<T> comparer = null)
         : this(comparer)
      {
         Assert.IsNotNull(source, nameof(source));
         foreach(T item in source)
         {
            this.Increment(item);
         }
      }

      public int Increment(T key)
      {
         Box box;
         if(map.TryGetValue(key, out box))
         {
            box++;
            return box.Value;
         }
         map.Add(key, new Box(1));
         return 1;
      }

      public int Decrement(T key)
      {
         Box box;
         if(map.TryGetValue(key, out box))
         {
            if(box.Value < 2)
            {
               map.Remove(key);
               return 0;
            }
            --box;
            return box.Value;
         }
         return 0;
      }

      public int this[T key]
      {
         get
         {
            Box box;
            if(map.TryGetValue(key, out box))
               return box.Value;
            else
               return 0;
         }
      }

      public int Total 
      { 
         get 
         { 
            int rslt = 0;
            foreach(KeyValuePair<T, Box> pair in map)
            {
               rslt += pair.Value;
            }
            return rslt;
         } 
      }

      public int Count => map.Count;

      public bool ContainsKey(T key) => map.ContainsKey(key);

      public Dictionary<T, int> ToDictionary()
      {
         return map.ToDictionary(p => p.Key, p => (int) p.Value);
      }

      public IEnumerator<KeyValuePair<T, int>> GetEnumerator()
      {
         foreach(var pair in map)
         {
            yield return new KeyValuePair<T, int>(pair.Key, pair.Value);
         }
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      public static implicit operator Dictionary<T, int>(CountMap<T> map)
      {
         Assert.IsNotNull(map);
         return map.ToDictionary();
      }
   }

   /// <summary>
   /// Similar to Counter<T>, except that it counts only 
   /// keys that are explicitly-added to the instance 
   /// via the constructor or the Add() method. Also does 
   /// not remove keys when they are decremented to 0.
   /// The constructor requires a sequence of keys. If
   /// no keys are passed to the constructor, there must
   /// be one or more calls to Add().
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class ExclusiveCountMap<T> : IEnumerable<KeyValuePair<T, int>>
   {
      Dictionary<T, Box> map = new Dictionary<T, Box>();

      public ExclusiveCountMap(IEnumerable<T> keys, IEqualityComparer<T> comparer)
      {
         Dictionary<T, Box> dict = null;
         if(keys != null)
         {
            dict = keys.ToDictionary(p => p, p => new Box(0));
            map = new Dictionary<T, Box>(dict, comparer);
         }
         else
         {
            map = new Dictionary<T, Box>(comparer);
         }
      }

      public void Add(T key, int value = 0)
      {
         if(!map.ContainsKey(key))
            map.Add(key, new Box(value));
      }

      public int Increment(T key)
      {
         Box box;
         if(map.TryGetValue(key, out box))
         {
            box++;
            return box.Value;
         }
         return 0;
      }

      public int Decrement(T key)
      {
         Box box;
         if(map.TryGetValue(key, out box))
         {
            if(box.Value > 0)
               --box;
            return box.Value;
         }
         return 0;
      }

      /// <summary>
      /// Returns 0 if the key does not exist
      /// </summary>
      /// <param name="key"></param>
      /// <returns></returns>

      public int this[T key]
      {
         get
         {
            Box box;
            if(map.TryGetValue(key, out box))
               return box.Value;
            else
               return 0;
         }
      }

      public int Total
      {
         get
         {
            int rslt = 0;
            foreach(KeyValuePair<T, Box> pair in map)
            {
               rslt += pair.Value;
            }
            return rslt;
         }
      }

      public int Count => map.Count;

      public bool ContainsKey(T key) => map.ContainsKey(key);

      public Dictionary<T, int> ToDictionary()
      {
         return map.ToDictionary(p => p.Key, p => (int)p.Value);
      }

      public IEnumerator<KeyValuePair<T, int>> GetEnumerator()
      {
         foreach(var pair in map)
         {
            yield return new KeyValuePair<T, int>(pair.Key, pair.Value);
         }
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      public static implicit operator Dictionary<T, int>(ExclusiveCountMap<T> map)
      {
         Assert.IsNotNull(map);
         return map.ToDictionary();
      }
   }

   /// <summary>
   /// Counter classes store values in a reference type,
   /// to avoid an assignment to an existing value, as
   /// it is far-more expensive than assigning a value to
   /// the field of a reference type.
   /// </summary>

   record Box
   {
      public Box(int value = 1) { Value = value; }
      public int Value;

      public static Box operator ++(Box box)
      {
         box.Value++;
         return box;
      }

      public static Box operator --(Box box)
      {
         box.Value--;
         return box;
      }

      public static implicit operator int(Box box)
      {
         return box.Value;
      }
   }


}


