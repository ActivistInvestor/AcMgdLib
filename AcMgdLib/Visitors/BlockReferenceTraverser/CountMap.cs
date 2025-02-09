
/// HighlightCirclesVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the EntityVisitor class.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AcMgdLib.Collections.Generic
{
   /// <summary>
   /// A class that accumulates the 
   /// count of multiple keys.
   /// 
   /// The generic argument is the type of the
   /// key to be counted. Create an instance of
   /// this class, and pass values of the generic
   /// argument type to the Increment() method or
   /// the += operator.
   /// 
   /// The class is functionally-similar to a
   /// standard Dictionary<T, int>, where each
   /// value holds the number of occurrenes of
   /// the associated key.
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class CountMap<T> : IEnumerable<KeyValuePair<T, int>>
   {
      Dictionary<T, Box> map;

      public CountMap(IEqualityComparer<T> comparer = null)
      {
         map = new Dictionary<T, Box>(comparer);
      }

      public int Increment(T key, int delta = 1)
      {
         Box box;
         if(map.TryGetValue(key, out box))
         {
            box.Value += delta;
            if(box.Value < 1)
               map.Remove(key);
            return box.Value;
         }
         else
         {
            box = new Box(delta);
            if(box.Value > 0)
               map.Add(key, box);
            return box.Value > 0 ? box.Value : 0;
         }
      }

      public int Decrement(T key, int delta = 1)
      {
         Box box;
         if(map.TryGetValue(key, out box))
         {
            box.Value -= delta;
            if(box.Value < 1)
            {
               map.Remove(key);
               return 0;
            }
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
            return map.Values.Sum(box => box.Value);
         }
      }

      /// <summary>
      /// Can be used to combine two CountByMap<T> instances
      /// </summary>
      /// <param name="other"></param>

      public void UnionWith(Dictionary<T, int> other)
      {
         if(other is null)
            throw new ArgumentNullException(nameof(other));
         foreach(var pair in other)
         {
            Box box;
            if(map.TryGetValue(pair.Key, out box))
            {
               box.Value += pair.Value;
            }
            else
            {
               box = new Box(pair.Value);
               map.Add(pair.Key, box);
            }
         }
      }

      public void ExceptWith(Dictionary<T, int> other)
      {
         if(other is null)
            throw new ArgumentNullException(nameof(other));
         foreach(var pair in other)
         {
            Box box;
            if(map.TryGetValue(pair.Key, out box))
            {
               box.Value -= pair.Value;
               if(box.Value < 1)
                  map.Remove(pair.Key);
            }
         }
      }

      public int Count => map.Count;

      public void Clear() => map.Clear();

      public bool ContainsKey(T key) => map.ContainsKey(key);

      public Dictionary<T, int> ToDictionary()
      {
         return map.ToDictionary(p => p.Key, p => (int)p.Value);
      }

      public Dictionary<TKey, int> ToDictionary<TKey>(Func<T, TKey> selector)
      {
         return map.ToDictionary(p => selector(p.Key), p => (int)p.Value);
      }

      public bool TryGetValue(T key, out int value)
      {
         value = 0;
         bool result = map.TryGetValue(key, out Box box);
         if(result)
            value = box.Value;
         return result;
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

      public static CountMap<T> operator +(CountMap<T> map, T key)
      {
         if(map is null)
            throw new ArgumentNullException(nameof(map));
         map.Increment(key);
         return map;
      }

      public static CountMap<T> operator -(CountMap<T> map, T key)
      {
         if(map is null)
            throw new ArgumentNullException(nameof(map));
         map.Decrement(key);
         return map;
      }

      public static implicit operator Dictionary<T, int>(CountMap<T> operand)
      {
         if(operand is null)
            throw new ArgumentNullException(nameof(operand));
         return operand.ToDictionary();
      }

      class Box
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

}
