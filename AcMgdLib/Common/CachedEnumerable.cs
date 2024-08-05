/// CachedEnumerable.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System.Runtime.InteropServices;
using System.Utility;

namespace System.Collections.Generic.Extensions
{
   /// <summary>
   /// A type that implements IEnumerable<T>, which wraps 
   /// another IEnumerable<T>, and caches its enumerated 
   /// elements to avoid the need to enumerate the wrapped 
   /// IEnumerable<T> multiple times.
   /// 
   /// This class is useful when the cost of enumerating 
   /// the wrapped IEnumerable<T> is expensive, and the
   /// wrapped IEnumerable<T>'s result does not change
   /// across multiple enumerations.
   /// 
   /// The caching performed by this class is fully-lazy
   /// and does not occur until the instance is enumerated.
   /// 
   /// To force a complete enumeration of the source and
   /// caching of the results, call ToArray() or Count() 
   /// on the instance, although doing that should not be
   /// necessary in most useage scenarios.
   /// </summary>
   /// <typeparam name="T">The element type</typeparam>

   public class CachedEnumerable<T> : IEnumerable<T>
   {
      readonly IEnumerable<T> source;
      bool lazy = true;
      T[] items = null;

      /// <summary>
      /// The instance can use either of two caching policies,
      /// One is 'eager', and the other fully-lazy. The second
      /// argument to this constructor specifies which caching 
      /// policy should be used. The default is the fully-lazy 
      /// caching policy.
      /// </summary>
      
      public CachedEnumerable(IEnumerable<T> source, bool lazy = true)
      {
         this.source = source;
         this.lazy = lazy;
      }

      protected IEnumerable<T> Source => source;

      /// <summary>
      /// Forces enumeration of the source sequence
      /// and caching of the results. The result is
      /// an array of T[]. If the source object is an
      /// array of T[], it is not copied.
      /// </summary>
      
      protected IEnumerable<T> Items
      {
         get
         {
            return items ?? (items = source.AsArray());
         }
      }

      public IEnumerator<T> GetEnumerator()
      {
         if(items != null)
            return ((IEnumerable<T>)items).GetEnumerator();
         else
            return lazy ? new LazyEnumerator(this) : new Enumerator(this);
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      /// <summary>
      /// Uses the 'eager' caching policy
      /// </summary>
      struct Enumerator : IEnumerator<T>
      {
         readonly CachedEnumerable<T> owner;
         IEnumerator<T> source;

         public Enumerator(CachedEnumerable<T> owner)
         {
            this.owner = owner;
         }

         public bool MoveNext()
         {
            if(source == null)
            {
               lock(owner)
               {
                  if(owner.items == null)
                     owner.items = owner.source.AsArray();
                  source = ((IEnumerable<T>)owner.items).GetEnumerator();
               }
            }
            return source.MoveNext();
         }

         public T Current
         {
            get
            {
               if(source == null)
                  throw new InvalidOperationException("MoveNext() not called.");
               return source.Current;
            }
         }

         object IEnumerator.Current => this.Current;

         public void Dispose()
         {
            (source as IDisposable)?.Dispose();
         }

         public void Reset()
         {
            source?.Reset();
         }
      }

      /// <summary>
      /// Uses the fully-lazy caching policy
      /// </summary>

      struct LazyEnumerator : IEnumerator<T>
      {
         readonly CachedEnumerable<T> owner;
         readonly List<T> list = new List<T>();
         IEnumerator<T> source;
         T current;

         public LazyEnumerator(CachedEnumerable<T> owner)
         {
            this.owner = owner;
         }

         public bool MoveNext()
         {
            if(source == null)
               source = owner.source.GetEnumerator();
            bool result = source.MoveNext();
            if(result)
            {
               current = source.Current;
               list.Add(current);
            }
            else
            {
               lock(owner)
               {
                  if(owner.items == null || owner.items.Length < list.Count)
                  {
                     owner.items = new T[list.Count];
                     if(list.Count > 0)
                     {
                        var destination = owner.items.AsSpan();
                        var src = CollectionsMarshal.AsSpan(list);
                        src.CopyTo(destination);
                     }
                  }
               }
            }
            return result;
         }

         public T Current
         {
            get
            {
               if(source == null)
                  throw new InvalidOperationException("MoveNext() not called.");
               return current;
            }
         }

         object IEnumerator.Current => this.Current;

         public void Dispose()
         {
            (source as IDisposable)?.Dispose();
         }

         public void Reset()
         {
            list?.Clear();
            source?.Reset();
            source = null;
         }
      }

   }



}


