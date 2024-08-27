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

   public abstract partial class BlockReferenceVisitor
   {
      protected class ReferenceStack : IReadOnlyList<StackEntry>
      {
         Dictionary<BlockReference, StackEntry> map =
            new Dictionary<BlockReference, StackEntry>();

         List<StackEntry> list = new List<StackEntry>();

         // Stack order (index 0 refers to TOP of stack)
         public StackEntry this[int index] => list[list.Count - 1 - index];
         public StackEntry this[BlockReference index] => map[index];

         public int Count => list.Count;

         /// <summary>
         /// Enumerates elements in stack-order
         /// (top-to-bottom).
         /// </summary>
         /// <returns></returns>

         public IEnumerator<StackEntry> GetEnumerator()
         {
            return StackOrdered.GetEnumerator();
         }

         /// <summary>
         /// Enumerates elements in list order (bottom-to-top)
         /// </summary>
         
         public IEnumerable<StackEntry> Reverse => list.AsReadOnly();

         IEnumerable<StackEntry> StackOrdered
         {
            get
            {
               for(int i = list.Count - 1; i > -1; i--)
               {
                  yield return list[i];
               }
            }
         }

         IEnumerator IEnumerable.GetEnumerator()
         {
            return this.GetEnumerator();
         }

         public int Push(StackEntry entry)
         {
            if(map.ContainsKey(entry.BlockRef))
               throw new InvalidOperationException("Duplicate entry");
            map[entry.BlockRef] = entry;
            list.Add(entry);
            return list.Count - 1;
         }

         public StackEntry Pop()
         {
            return RemoveLast();
         }

         public StackEntry Peek()
         {
            return Last;
         }

         public StackEntry Bottom
         {
            get
            {
               AssertIsNotEmpty();
               return list[0];
            }
         }

         StackEntry Last // synonym for Peek()
         {
            get
            {
               AssertIsNotEmpty();
               return list[list.Count - 1];
            }
         }

         public bool TryGetValue(BlockReference key, out StackEntry value)
         {
            return map.TryGetValue(key, out value);
         }

         public bool Contains(BlockReference key)
         {
            return map.ContainsKey(key);
         }

         StackEntry RemoveLast()
         {
            var result = Last;
            list.RemoveAt(list.Count - 1);
            map.Remove(result.BlockRef);
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
