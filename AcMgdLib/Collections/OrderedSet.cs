using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace System.Collections.Generic.Extensions
{
   /// <summary>
   /// Marshals both a List<T> and a HashSet<T> as a quick/dirty
   /// means of implementing an ordered set (whose elements are
   /// ordered like a List, but have fast membership lookup like
   /// a HashSet). It uses the list to maintain ordering and the
   /// HashSet to perform membership testing.
   /// 
   /// Obviously, this approach has the downside of having to 
   /// store each element in duplicate, meaning that it will use 
   /// slightly more than 2x the memory used by a simple List<T>.
   /// 
   /// This class will use a List<T> exclusively until the number
   /// of elements exceeds the threshold value. At that point, it
   /// will switch over to using the HashSet for lookups.
   /// </summary>
   /// <typeparam name="T">The element type</typeparam>

   public class OrderedSet<T> : ICollection<T>
   {
      int threshold = 10;
      HashSet<T> set;
      List<T> list = new List<T>();
      IEqualityComparer<T> comparer;
      static bool isEquatable = typeof(IEquatable<T>).IsAssignableFrom(typeof(T));

      public OrderedSet(IEqualityComparer<T> comparer = null, int threshold = 10)
      {
         this.comparer = comparer ?? EqualityComparer<T>.Default;
         if(threshold < 10)
            this.threshold = isEquatable ? threshold * 2 : threshold;
         else
            this.threshold = threshold;
      }

      public OrderedSet(IEnumerable<T> items, IEqualityComparer<T> comparer = null, int threshold = 10)
         : this(comparer, threshold) 
      {
         if(items == null)
            throw new ArgumentNullException(nameof(items));
         UnionWith(items);
      }

      public T this[int index]
      {
         get { return list[index]; }
      }

      public bool ExceptWhere(Func<T, bool> predicate)
      {
         if(predicate == null)
            throw new ArgumentNullException(nameof(predicate));
         return ExceptWith(list.Where(predicate));
      }

      public bool ExceptWith(IEnumerable<T> enumerable)
      {
         if(enumerable == null)
            throw new ArgumentNullException(nameof(enumerable));
         int count = list.Count;
         if(count > 0)
         {
            if(set != null)
            {
               set.ExceptWith(enumerable);
               if(set.Count < count)
               {
                  list.RemoveAll(e => !set.Contains(e));
                  return true;
               }
            }
            else
            {
               list = list.Except(enumerable).ToList();
               return this.Count < count;
            }
         }
         return false;
      }

      public bool UnionWith(IEnumerable<T> enumerable)
      {
         if(enumerable == null)
            throw new ArgumentNullException(nameof(enumerable));
         int count = this.Count;
         foreach(T item in enumerable)
         {
            if(item == null)
               throw new ArgumentException("null element");
            this.Add(item);
         }
         return this.Count > count;
      }

      public int Count => list.Count;

      public bool IsReadOnly => false;

      public void Add(T item)
      {
         if(set?.Add(item) ?? true)
            list.Add(item);
         if(list.Count >= threshold && set == null)
            set = new HashSet<T>(list, comparer);
      }

      public void Clear()
      {
         set?.Clear();
         list.Clear();
      }

      public bool Contains(T item)
      {
         return set?.Contains(item) ?? list.Contains(item);
      }

      public void CopyTo(T[] array, int arrayIndex = 0)
      {
         list.CopyTo(array, arrayIndex);
      }

      public bool Remove(T item)
      {
         if(set == null || set.Remove(item))
         {
            int pos = IndexOf(item);
            if(pos > -1)
               list.RemoveAt(pos);
            return true;
         }
         return false;
      }

      /// <summary>
      /// Because a user-specified IEqualityComparer<T> is
      /// supported, we cannot rely on comparisons done by
      /// List<T>, which always uses the default equality
      /// comparer for the element type. In this case, the
      /// same equality comparer used by the HashSet must
      /// be used by the List.
      /// </summary>

      public int IndexOf(T item)
      {
         for(int i = 0; i < list.Count; i++)
         {
            if(comparer.Equals(list[i], item))
               return i;
         }
         return -1;
      }

      public IEnumerator<T> GetEnumerator()
      {
         return list.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }
   }


}