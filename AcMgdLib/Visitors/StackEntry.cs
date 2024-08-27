/// StackEntry.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Notes:
/// 
/// These classes are currently unused. They were
/// developed as part of a major refactoring of the
/// BlockReferenceVisitor and EntityVisitor-based
/// classes, but that work was never completed.

using System;
using System.Diagnostics.Extensions;
using System.Linq;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   public abstract partial class BlockReferenceVisitor
   {
      protected class StackEntry
      {
         ReferenceStack containers = null;

         /// <summary>
         /// The ObjectIds of BlockReferences
         /// that were on the stack when this
         /// entry was pushed onto it, plus the
         /// ObjectId of this entry's blockref,
         /// and an entry set to ObjectId.Null,
         /// that acts as a placeholder for the
         /// Entity for which a FullSubentityPath
         /// is being requested.
         /// 
         /// This is populated lazily on the first 
         /// call to GetFullSubentityPath().
         /// </summary>

         ObjectId[] containerIds = null;

         /// <summary>
         /// The number of entries below
         /// this entry on the stack. This
         /// is the 'reverse-index' of this
         /// entry.
         /// </summary>

         int index;

         public StackEntry(ReferenceStack containers, BlockReference blkref)
         {
            this.BlockRef = blkref;
            index = containers.Count;
            containers.Push(this);
            this.containers = containers;
         }

         public BlockReference BlockRef { get; set; }

         public FullSubentityPath GetFullSubentityPath(Entity entity)
         {
            Assert.IsNotNullOrDisposed(entity);
            if(containerIds == null)
            {
               /// Bugggggg This uses the current stack,
               /// not the stack as it was when this instance
               /// was pushed onto it. The only entries from
               /// the stack that should be used are those that
               /// are below the instance. That presumes that
               /// this method can be called on the instance
               /// even when it is not at the top of the stack,
               /// which is legitimate.
               /// The index field must be used with CopyTo()
               /// to copy only the current instance and all
               /// entries below it to the stack array.

               int idx = this.index;

               /// Optimize for the case where this instance is
               /// at the bottom of the stack:
               if(index == 0)
               {
                  return new FullSubentityPath(
                     new ObjectId[] { BlockRef.ObjectId, entity.ObjectId },
                     nullSubEntityId);
               }
               int len = containers.Count;
               /// Need to fix this:
               var stack = containers.ToArray();
               var src = stack.AsSpan();
               /// ****************************************
               /// containerIds must contain only the Ids of
               /// block references from entries that are below 
               /// this entry on the stack:
               containerIds = new ObjectId[index + 2];
               var dest = containerIds.AsSpan();
               int k = 0;
               for(int i = src.Length - 1; i >= index; i--)
                  dest[k++] = src[i].BlockRef.ObjectId;
            }
            containerIds[containerIds.Length - 1] = entity.ObjectId;
            return new FullSubentityPath(containerIds, nullSubEntityId);
         }
      }
   }
}
