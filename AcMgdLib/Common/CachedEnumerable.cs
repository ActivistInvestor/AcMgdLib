/// CachedEnumerable.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;
using System.Linq;
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
   /// To force a complete enumeration of the List and
   /// caching of the results, call ToArray() or Count() 
   /// on the instance, although doing that should not be
   /// necessary in most useage scenarios, because in all
   /// other ways, the instance behaves exactly like the 
   /// IEnumerable that it wraps.
   /// </summary>
   /// <typeparam name="T">The element type</typeparam>

   public class CachedEnumerable<T> : CachedEnumerable, IEnumerable<T>
   {
      readonly IEnumerable<T> source;
      T[] items = null;
      int hits = 0;

      public CachedEnumerable(IEnumerable<T> source, CachePolicy cachePolicy = CachePolicy.Eager)
         : base(GetCachePolicy(source, cachePolicy))
      {
         this.source = source;
         /// If the source is an array, just wrap it 
         /// and return the array's Enumerator, and 
         /// there is no caching at all:
         if(source is T[] array)
            this.items = array;
      }

      static CachePolicy GetCachePolicy(IEnumerable<T> source, CachePolicy defaultPolicy)
      {
         return source is T[] || source is ICollection<T> ? CachePolicy.None : defaultPolicy;
      }

      public bool HasCache => items != null;

      protected IEnumerable<T> Source => source;

      /// <summary>
      /// Forces enumeration of the source sequence
      /// and caching of the results. The result is
      /// an array of T[]. If the source object is 
      /// an array of T[], it is returned, without
      /// copying it.
      /// 
      /// In essence, this behavior makes an instance
      /// of this type a pass-through when the source
      /// is an array, as the caller of GetEnumerator()
      /// uses the array's enumerator directly without
      /// the overhead of a wrapper that forwards calls 
      /// to MoveNext() and get_Current() to a wrapped
      /// enumerator.
      /// 
      /// </summary>
      
      protected IEnumerable<T> Items
      {
         get
         {
            return items ?? (items = GetArrayFromSource());
         }
      }

      protected virtual T[] GetArrayFromSource()
      {
         if(source is T[] array)
            return array;
         return new Wrapper(GetSourceEnumerator()).ToArray();
      }

      class Wrapper : IEnumerable<T> 
      {
         IEnumerator<T> enumerator;
         public Wrapper(IEnumerator<T> enumerator)
         {
            this.enumerator = enumerator;
         }

         public IEnumerator<T> GetEnumerator()
         {
            return enumerator;
         }

         IEnumerator IEnumerable.GetEnumerator()
         {
            return enumerator;
         }
      }

      public IEnumerator<T> GetEnumerator()
      {
         if(CachePolicy == CachePolicy.None)
            return GetSourceEnumerator();
         if(items == null)
         {
            if(source is IList<T> list)
            {
               Trace("Using source IList<T>");
               items = list.ToArray();
               return ((IEnumerable<T>)items).GetEnumerator();
            }
            Trace($"No Cache for {source.ToIdString()}");
            return CreateEnumerator();
         }
         else
         {
            ++hits;
            Trace($"Using cache for {source.ToIdString()} ({hits} hits)");
            return ((IEnumerable<T>)items).GetEnumerator();
         }
      }

      IEnumerator<T> CreateEnumerator()
      {
         return CachePolicy == CachePolicy.Lazy ? new LazyEnumerator(this) : new Enumerator(this);
      }

      IEnumerator<T> GetSourceEnumerator()
      {
         Assert.IsNotNull(this.source, nameof(source));
         return GetSourceEnumerator(this.source);
      }

      /// <summary>
      /// Allow a derived type to return a different enumerator
      /// that will be used to enumerate the source sequence.
      /// </summary>
      /// <param name="source"></param>

      protected virtual IEnumerator<T> GetSourceEnumerator(IEnumerable<T> source)
      {
         return source.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

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
                  source = owner.Items.GetEnumerator();
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
               source = owner.GetSourceEnumerator();
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
                     T[] array = new T[list.Count];
                     if(list.Count > 0)
                        CollectionsMarshal.AsSpan(list).CopyTo(array.AsSpan());
                     owner.items = array;
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

   public class CachedEnumerable
   {
      public CachedEnumerable(CachePolicy policy)
      {
         this.CachePolicy = policy;
      }

      public static bool TraceEnabled { get; set; }
      public CachePolicy CachePolicy { get; set; }

      protected virtual void Trace(string message)
      {
         if(TraceEnabled)
         {
            AcConsole.Write($"{this.ToIdString()}: {message}");
         }
      }

   }

   public static class CachedEnumerableExtensions
   {
      /// <summary>
      /// Wraps an IEnumerable<T> in a CachedEnumerable<T>
      /// if the IEnumerable<T> is not a CachedEnumerable<T>
      /// </summary>

      public static CachedEnumerable<T> AsCached<T>(this IEnumerable<T> source, 
         CachePolicy cachePolicy = CachePolicy.Eager)
      {
         return source as CachedEnumerable<T> ??
            new CachedEnumerable<T>(source, cachePolicy);
      }
   }

   public enum CachePolicy
   {
      None = 0,
      Eager = 1,
      Lazy = 2
   }


}


