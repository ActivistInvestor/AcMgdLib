/// Cache.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for the AcDbLinq library.

using System;
using System.Collections.Generic;

namespace System.Extensions
{
   public class Cache<TKey, TValue>
   {
      Dictionary<TKey, TValue> map;
      IEqualityComparer<TKey> comparer;
      Func<TKey, TValue> selector;

      public Cache(Func<TKey, TValue> selector, IEqualityComparer<TKey> comparer = null)
      {
         this.comparer = comparer;
         this.selector = selector;
      }

      public TValue this[TKey key]
      {
         get
         {
            if(map == null)
            {
               TValue value = selector(key);
               map = new Dictionary<TKey, TValue>(comparer);
               map[key] = value;
               return value;
            }
            if(!map.TryGetValue(key, out TValue result))
               map[key] = result = selector(key);
            return result;
         }
      }
   }

}



