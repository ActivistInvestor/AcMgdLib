/// DwgDataList.cs  -  Tony Tanzillo
/// Distributed under the terms of the
/// MIT License.

using System.Collections.Generic;
using System.Collections;

namespace System.Collections.Generic.Extensions
{
   public class OrderedHashSet<T> : ICollection<T>
   {
      private readonly IDictionary<T, LinkedListNode<T>> m_Dictionary;
      private readonly LinkedList<T> m_LinkedList;

      public OrderedHashSet()
          : this(EqualityComparer<T>.Default)
      {
      }

      public OrderedHashSet(IEqualityComparer<T> comparer)
      {
         m_Dictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
         m_LinkedList = new LinkedList<T>();
      }

      public int Count => m_Dictionary.Count;

      public virtual bool IsReadOnly => m_Dictionary.IsReadOnly;

      void ICollection<T>.Add(T item)
      {
         Add(item);
      }

      public bool Add(T item)
      {
         if(m_Dictionary.ContainsKey(item)) return false;
         var node = m_LinkedList.AddLast(item);
         m_Dictionary.Add(item, node);
         return true;
      }

      public void Clear()
      {
         m_LinkedList.Clear();
         m_Dictionary.Clear();
      }

      public bool Remove(T item)
      {
         if(item == null) return false;
         var found = m_Dictionary.TryGetValue(item, out var node);
         if(!found) return false;
         m_Dictionary.Remove(item);
         m_LinkedList.Remove(node);
         return true;
      }

      public IEnumerator<T> GetEnumerator()
      {
         return m_LinkedList.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }

      public bool Contains(T item)
      {
         return item != null && m_Dictionary.ContainsKey(item);
      }

      public void CopyTo(T[] array, int arrayIndex)
      {
         m_LinkedList.CopyTo(array, arrayIndex);
      }
   }
}
