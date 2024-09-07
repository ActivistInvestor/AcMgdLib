/// MemberFilter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// High-level APIs that help simplify and streamline 
/// development of managed AutoCAD extensions.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Expressions.Predicates;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// Automates filtering items by set membership of values
   /// derived from each item by a user-supplied delegate.
   /// 
   /// An item satisifies the filter criteria if the TKey value
   /// produced by the delegate given an item, is contained in 
   /// the set of values passed to the constructor, or added to
   /// the instance via the Add() method.
   /// 
   /// The Inverted property can be set to true to invert that
   /// logic, causing items to satisfy the filter critera if the
   /// TKey value produced by each is NOT contained in the set
   /// of keys.
   /// 
   /// Once the value of the MatchPredicate property is retrieved,
   /// any subsequent changes to the filter will not apply to the
   /// retrieved predicate. Changes to the filter should be made 
   /// before allowing the MatchPredicate property to be accessed.
   /// </summary>
   /// <typeparam name="T">The type of item to be filtered</typeparam>
   /// <typeparam name="TKey">The type of the key that determines
   /// if an item satisfies the filter criteria</typeparam>

   public class MemberFilter<T, TKey> : Filter<T>, IEnumerable<TKey>
   {
      readonly HashSet<TKey> keys;
      IEqualityComparer<TKey> comparer;
      Func<T, TKey> selector;
      TKey singleton;
      bool inverted = false;

      public MemberFilter(Func<T, TKey> selector, params TKey[] keys)
         : this(selector, keys as IEnumerable<TKey>)         
      {
      }

      public MemberFilter(Func<T, TKey> selector, IEqualityComparer<TKey> comparer, params TKey[] keys)
         : this(selector, keys as IEnumerable<TKey>, comparer)
      {
      }

      public MemberFilter(Func<T, TKey> selector, IEnumerable<TKey> keys, IEqualityComparer<TKey> comparer = null)
      {
         this.selector = selector;
         this.comparer = comparer ?? EqualityComparer<TKey>.Default;
         this.keys = keys != null && keys.Any() ? 
            new HashSet<TKey>(keys, this.comparer) 
            : new HashSet<TKey>(this.comparer);
      }

      protected override Expression<Func<T, bool>> GetBaseExpression()
      {
         Expression<Func<T, bool>> result;
         switch(keys.Count)
         {
            case 0: 
               return x => EmptyDefault;
            case 1:
               singleton = keys.First();
               result = x => comparer.Equals(selector(x), singleton);
               break;
            default:
               result = x => keys.Contains(selector(x));
               break;
         }
         if(inverted && keys.Count > 0)
            return result.Not();
         else
            return result;
      }

      /// <summary>
      /// Specifies if the predicate should return the 
      /// logical complement of the filter criteria.
      /// 
      /// For example, if this property is set to true,
      /// elements meet the filter criteria if the key 
      /// derived from each is NOT contained in the set 
      /// of keys.
      /// 
      /// This property does not apply to an empty set,
      /// which produces the value of the DefaultIfEmpty
      /// property.
      /// </summary>
      
      public bool Inverted
      {
         get
         {
            return inverted;
         }
         set
         {
            if(inverted ^ value)
            {
               inverted = value;
               Invalidate();
            }
         }
      }

      /// <summary>
      /// The value returned if the set of matching
      /// keys is empty
      /// </summary>
      
      public bool EmptyDefault { get; set; }

      /// <summary>
      /// Adds a value to the set of matching keys
      /// </summary>
      
      public bool Add(TKey key)
      {
         bool result = keys.Add(key);
         if(result)
            Invalidate();
         return result;
      }

      /// <summary>
      /// Removes a value from the set of matching keys
      /// </summary>

      public bool Remove(TKey key)
      {
         bool result = keys.Remove(key);
         if(result) 
            Invalidate();
         return result;
      }

      public int Count => keys.Count;

      public bool Contains(T item)
      {
         return keys.Contains(selector(item));
      }

      public bool ContainsKey(TKey key)
      {
         return keys.Contains(key);
      }

      public IEnumerator<TKey> GetEnumerator()
      {
         return keys.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }
   }



}



