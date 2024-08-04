using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
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
   /// the wrapped IEnumerable<T> is expensive.
   /// 
   /// The caching performed by this class is fully-lazy
   /// and does not occur until the instance is enumerated.
   /// 
   /// To force a complete enumeration of the source and
   /// caching of the results, call ToArray() or Count() 
   /// on the instance.
   /// </summary>
   /// <typeparam name="T">The element type</typeparam>

   public class CachedEnumerable<T> : IEnumerable<T>
   {
      readonly IEnumerable<T> source;
      readonly bool lazy = true;
      T[] items = null;
      
      public CachedEnumerable(IEnumerable<T> source, bool lazy = true)
      {
         this.source = source;
         this.lazy = lazy;
      }

      protected IEnumerable<T> Source => source;

      /// <summary>
      /// Forces enumeration of the source sequence
      /// and caching of the results
      /// </summary>
      
      protected IEnumerable<T> Items
      {
         get
         {
            if(items == null)
               this.Count();
            Assert.IsNotNull(items, "Failed to enumerate source");
            return items;
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
      /// Uses an 'eager' caching policy
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
               if(owner.items == null)
                  owner.items = owner.source.AsArray();
               source = ((IEnumerable<T>)owner.items).GetEnumerator();
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
      /// Uses a fully-lazy caching policy
      /// </summary>

      struct LazyEnumerator : IEnumerator<T>
      {
         readonly CachedEnumerable<T> owner;
         IEnumerator<T> source;
         List<T> list = new List<T>();
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
               if(owner.items == null || owner.items.Length < list.Count)
               {
                  owner.items = new T[list.Count];
                  list.CopyTo(owner.items, 0);
               }
            }
            return result;
         }

         public T Current
         {
            get
            {
               Assert.IsNotNull(source, "MoveNext() not called.");
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
            list.Clear();
            source?.Reset();
            source = null;
         }
      }

   }



}


