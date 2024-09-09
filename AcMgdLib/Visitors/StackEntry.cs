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

using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Windows.Navigation;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   public abstract partial class BlockVisitor<T>
   {
      public class StackEntry : IDisposable
      {
         bool disposed = false;
         ReferenceStack owner;

         /// <summary>
         /// Includes this instance's BlockReference Id, plus
         /// an empty last element that acts as a placeholder 
         /// for the leaf/entity ObjectId.
         /// 
         /// The array is created lazily on the first request 
         /// for a FullSubentityPath.
         /// </summary>
         
         ObjectId[] pathIds = null;

         /// <summary>
         /// The number of entries below
         /// this entry on the stack. This
         /// is the 'reverse-index' of this
         /// entry.
         /// </summary>

         int index;

         public StackEntry(ReferenceStack owner, BlockReference blkref)
         {
            this.BlockRef = blkref;
            this.owner = owner;
            index = owner.Count;
            AcConsole.Trace($"{blkref.ToDebugString()} index = {index}");
         }

         public int Index => index;

         public StackEntry Parent
         {
            get
            {
               if(owner.Count > 0 && this.Index > 0)
               {
                  return owner[this.Index - 1];
               }
               return null;
            }
         }

         public bool BlockEntitiesModified { get; protected set; }

         public BlockReference BlockRef { get; set; }

         public void Dispose()
         {
            if(!disposed)
            {
               this.GetType().CSharpName();
               AcConsole.Trace($"index = {index}");
               Dispose(true);
               disposed = true;
            }
         }

         /// <summary>
         /// Specializations of this type can override this
         /// to get control just after the instance is popped 
         /// off of the stack.
         /// </summary>
         /// <param name="disposing"></param>
         
         protected virtual void Dispose(bool disposing)
         {
         }

         /// <summary>
         /// Specializations of this type can override this
         /// to get control just before the instance is popped 
         /// off of the stack.
         /// </summary>

         public virtual void Remove()
         {
         }

         public FullSubentityPath GetFullSubentityPath(Entity entity)
         {
            return GetFullSubentityPath(entity, nullSubEntityId);
         }

         public FullSubentityPath GetFullSubentityPath(Entity entity, SubentityId id)
         {
            Assert.IsNotNullOrDisposed(entity);
            if(pathIds == null)
            {
               /// Use an optimized path when this instance is
               /// at the bottom of the stack:
               if(index == 0)
               {
                  pathIds = 
                     new ObjectId[] 
                     { 
                        BlockRef.ObjectId, 
                        ObjectId.Null 
                     };
               }
               else
               {
                  pathIds = new ObjectId[index + 2];
                  for(int i = 0; i < index; i++)
                  {
                     pathIds[i] = owner.List[i].BlockRef.ObjectId;
                  }
                  pathIds[index] = BlockRef.ObjectId;
               }
            }
            pathIds[pathIds.Length - 1] = entity.ObjectId;
            return new FullSubentityPath(pathIds, id);
         }
      }
   }
}
