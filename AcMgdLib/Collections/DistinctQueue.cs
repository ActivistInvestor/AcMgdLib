using System.Linq;

namespace System.Collections.Generic.Extensions
{
   /// <summary>
   /// A variant of Queue<T> whose elemets are constrained 
   /// to be distinct. This class disallows adding values 
   /// that alredy exist in the queue. 
   /// 
   /// Unlike the Queue.Enqueue() method, the corresponding
   /// method of this class returns a bool indicating if the
   /// item was added.
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class DistinctQueue<T> : IEnumerable<T>, IEnumerable, ICollection, IReadOnlyCollection<T>
   {
      HashSet<T> set;
      Queue<T> queue;

      public DistinctQueue(IEqualityComparer<T> comparer)
      {
         queue = new Queue<T>();
         set = new HashSet<T>(comparer);
      }

      /// <summary>
      /// returns true if all elements were added
      /// </summary>

      public bool Enqueue(IEnumerable<T> items)
      {
         if(items == null)
            throw new ArgumentNullException("items");
         if(items.Any())
         {
            bool result = true;
            foreach(T item in items)
            {
               result &= Enqueue(item);
            }
            return result;
         }
         return false;         
      }

      public bool Enqueue(T item)
      {
         if(item == null)
            throw new ArgumentNullException("item");
         bool result = set.Add(item);
         if(result)
            queue.Enqueue(item);
         return result;
      }

      public bool TryDequeue(out T item)
      {
         if(queue.Count > 0)
         {
            item = queue.Dequeue();
            set.Remove(item);
            return true;
         }
         item = default(T);
         return false;
      }

      public T Dequeue()
      {
         if(queue.Count == 0)
            throw new InvalidOperationException("queue is empty");
         T result = queue.Dequeue();
         set.Remove(result);
         return result;
      }

      public void Clear()
      {
         set.Clear();
         queue.Clear();
      }

      public bool Contains(T item)
      {
         return set.Contains(item);
      }

      public int Count => ((ICollection)queue).Count;

      public object SyncRoot => ((ICollection)queue).SyncRoot;

      public bool IsSynchronized => ((ICollection)queue).IsSynchronized;

      public bool IsReadOnly => ((ICollection<T>)set).IsReadOnly;

      public void CopyTo(Array array, int index)
      {
         ((ICollection)queue).CopyTo(array, index);
      }

      public IEnumerator GetEnumerator()
      {
         return ((IEnumerable)queue).GetEnumerator();
      }

      IEnumerator<T> IEnumerable<T>.GetEnumerator()
      {
         return ((IEnumerable<T>)queue).GetEnumerator();
      }
   }
}

