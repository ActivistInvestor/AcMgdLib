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
      Func<TKey, TValue> selector;

      public Cache(Func<TKey, TValue> selector, IEqualityComparer<TKey> comparer = null)
      {
         map = new Dictionary<TKey, TValue>(comparer);
         this.selector = selector;
      }

      public TValue this[TKey key]
      {
         get
         {
            TValue result = default(TValue);
            if(!map.TryGetValue(key, out result))
            {
               map[key] = result = selector(key);
            }
            return result;
         }
      }
   }

}



