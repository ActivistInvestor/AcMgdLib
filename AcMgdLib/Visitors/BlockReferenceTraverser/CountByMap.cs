
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
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class CountByMap<T> : IEnumerable<KeyValuePair<T, int>>
   {
      Dictionary<T, Box> map;

      public CountByMap(IEqualityComparer<T> comparer = null)
      {
         map = new Dictionary<T, Box>(comparer);
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

      public void Clear() => map.Clear();

      public bool ContainsKey(T key) => map.ContainsKey(key);

      public Dictionary<T, int> ToDictionary()
      {
         return map.ToDictionary(p => p.Key, p => (int)p.Value);
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

      public static implicit operator Dictionary<T, int>(CountByMap<T> operand)
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
