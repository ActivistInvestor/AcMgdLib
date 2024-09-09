/// ReferenceStack.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

/// Notes:
/// 
/// These classes are currently unused. They were
/// developed as part of a major refactoring of the
/// BlockReferenceVisitor and EntityVisitor-based
/// classes, but that work was never completed.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   public abstract partial class BlockVisitor<T>
   {
      public class ReferenceStack: IReadOnlyList<T> 
      {
         BlockVisitor<T> owner;

         Dictionary<BlockReference, T> map =
            new Dictionary<BlockReference, T>();

         List<T> list = new List<T>();

         public ReferenceStack(BlockVisitor<T> owner)
         { 
            this.owner = owner;
         }

         // Stack order (index 0 refers to TOP of stack)
         public T this[int index] => list[list.Count - 1 - index]; 
         public T this[BlockReference index] => map[index];

         /// <summary>
         /// Botton-to-top as a read-only List:
         /// </summary>
         public IList<T> List => list.AsReadOnly();

         public int Count => list.Count;

         public BlockVisitor<T> Owner => owner;

         /// <summary>
         /// Enumerates elements in stack-order
         /// (top-to-bottom).
         /// </summary>

         public IEnumerator<T> GetEnumerator()
         {
            return Reversed.GetEnumerator();
         }

         /// <summary>
         /// Top-to-bottom
         /// </summary>

         IEnumerable<T> Reversed
         {
            get
            {
               if(list.Count > 0)
               {
                  for(int i = list.Count - 1; i > -1; i--)
                  {
                     yield return list[i];
                  }
               }
            }
         }

         IEnumerator IEnumerable.GetEnumerator()
         {
            return this.GetEnumerator();
         }

         public int Push(T entry)
         {
            if(map.ContainsKey(entry.BlockRef))
               throw new InvalidOperationException("Duplicate entry");
            map[entry.BlockRef] = entry;
            list.Add(entry);
            return list.Count - 1;
         }

         /// <summary>
         /// Throws if stack is empty
         /// </summary>
         /// <returns></returns>

         public void Pop()
         {
            AssertIsNotEmpty();
            TryRemoveLast()?.Dispose();
         }

         public T Peek()
         {
            return Last;
         }

         public T Bottom
         {
            get
            {
               AssertIsNotEmpty();
               return list[0];
            }
         }

         T Last // synonym for Peek()
         {
            get
            {
               AssertIsNotEmpty();
               return list[list.Count - 1];
            }
         }

         public bool TryGetValue(BlockReference key, out T value)
         {
            return map.TryGetValue(key, out value);
         }

         public bool Contains(BlockReference key)
         {
            return map.ContainsKey(key);
         }

         /// <summary>
         /// Does not throw if stack is empty
         /// </summary>
         /// <returns></returns>

         T TryRemoveLast()
         {
            T result = null;
            if(list.Count > 0)
            {
               result = list[list.Count - 1];
               result.Remove();
               list.RemoveAt(list.Count - 1);
               map.Remove(result.BlockRef);
            }
            return result;
         }

         void AssertIsNotEmpty()
         {
            if(list.Count == 0)
               throw new InvalidOperationException("Collection is empty");
         }

         public void Clear()
         {
            list.Clear();
            map.Clear();
         }

      }
   }
}
