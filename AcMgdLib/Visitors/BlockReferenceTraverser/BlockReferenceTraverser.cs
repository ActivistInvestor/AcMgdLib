
/// BlockReferenceTraverser.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// A 'lightweight' version of the BlockVisitor class
/// that can be used for counting block references.

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.DatabaseServices
{
   public class BlockReferenceTraverser
   {
      ObjectId rootBlockId;
      Transaction trans;
      static RXClass blockRefClass = RXObject.GetClass(typeof(BlockReference));
      static bool IsBlockRefId(ObjectId id) => id.ObjectClass == blockRefClass;
      Dictionary<ObjectId, IEnumerable<ObjectId>> map =
         new Dictionary<ObjectId, IEnumerable<ObjectId>>();
      Dictionary<ObjectId, bool> included =
         new Dictionary<ObjectId, bool>();

      Func<BlockReference, ObjectId> ResolveBlockId = null;
      bool resolveDynamic = true;
      bool faulted = true;

      public BlockReferenceTraverser(ObjectId blockId, bool resolveDynamic = true)
      {
         if(blockId.ObjectClass != RXObject.GetClass(typeof(BlockTableRecord)))
            throw new ArgumentException("Invalid objectId");
         rootBlockId = blockId;
         this.resolveDynamic = resolveDynamic;
         if(resolveDynamic)
            ResolveBlockId = blkref => blkref.DynamicBlockTableRecord;
         else
            ResolveBlockId = blkref => blkref.BlockTableRecord;
      }

      protected Database Database => rootBlockId.Database;
      protected ObjectId RootBlockId => rootBlockId;

      public void Visit()
      {
         trans = new OpenCloseTransaction();
         Clear();
         try
         {
            Stack<BlockReference> refs = new Stack<BlockReference>();
            foreach(var id in GetBlockReferenceIds(rootBlockId))
            {
               Visit(id, refs);
            }
            faulted = false;
         }
         finally
         {
            if(!faulted)
               trans.Commit();
            trans.Dispose();
            trans = null;
         }
      }

      public virtual void Clear()
      {
         map.Clear();
         faulted = true;
      }

      /// <summary>
      /// Takes the ObjectId of a BlockReference and
      /// processes the nested BlockReferences contained
      /// in the referenced block's definition, recursively.
      /// </summary>
      /// <param name="id"></param>

      void Visit(ObjectId blockRefId, Stack<BlockReference> path)
      {
         var blkref = GetObject<BlockReference>(blockRefId);
         path.Push(blkref);
         if(VisitBlockReference(path))
         {
            var nested = GetBlockReferenceIds(GetReferencedBlockId(blkref));
            foreach(ObjectId id in nested)
               Visit(id, path);
         }
         path.Pop();
      }

      //bool CanVisit(Stack<BlockReference> path) 
      //{
      //   if(VisitBlockReference(path))
      //   { }
      //   var blkref = path.Peek();
      //   var blkDefId = GetReferencedBlockId(blkref);
      //   bool result;
      //   if(!included.TryGetValue(blkDefId, out result))
      //   {
      //      var btr = GetObject<BlockTableRecord>(blkDefId);
      //      included[blkDefId] = result = VisitBlock(btr);
      //   }
      //   return result && VisitBlockReference(path);
      //}

      /// <summary>
      /// Override to specify if all references to a given
      /// BlockTableRecord should be visited. For anonymous
      /// dynamic blocks, the argument can be either the
      /// dynamic block definition, or the anonymous block
      /// definition, depending on what GetReferencedBlockId()
      /// returns. By default, the argument is the anonymous
      /// block definition.
      /// 
      /// This method will be called only once for each
      /// BlockTableRecord, the first time a reference to
      /// it is encountered.
      /// 
      /// A common use of overrides of this method is to 
      /// exclude anonymous and/or external reference 
      /// block definitions from being traversed.
      /// 
      /// The default implementation returns true for all
      /// BlockTableRecords.
      /// </summary>
      /// <param name="btr">The BlockTableRecord whose
      /// references are to be visited.</param>
      /// <returns>True to visit references to the given
      /// BlockTableRecord.</returns>

      protected virtual bool VisitBlock(BlockTableRecord btr)
      {
         return true;
      }

      /// <summary>
      /// To traverse dynamic blocks rather than anonymous 
      /// dynamic blocks, override this and return the 
      /// argument's DynamicBlockTableRecord property 
      /// value, or pass true to the second argument to
      /// the constructor.
      /// </summary>
      /// <param name="blockref"></param>

      protected virtual ObjectId GetReferencedBlockId(BlockReference blockref)
      {
         return ResolveBlockId(blockref);
      }

      /// <summary>
      /// Override this to determine if the BlockReference at
      /// the top of the given stack should be visited. The
      /// stack contains the BlockReference which this method
      /// is being queried about at the top, followed by its 
      /// container BlockReferences.
      /// </summary>
      /// <param name="refs"></param>
      /// <returns></returns>
      
      protected virtual bool VisitBlockReference(Stack<BlockReference> refs)
      {
         return true;
      }

      protected Dictionary<ObjectId, IEnumerable<ObjectId>> Map => map;

      /// <summary>
      /// Returns the ObjectIds of BlockReferences that are
      /// directly inserted into the definition of the Block 
      /// whose ObjectId is passed as the argument.
      /// </summary>
      
      private IEnumerable<ObjectId> GetBlockReferenceIds(ObjectId blockDefid)
      {
         IEnumerable<ObjectId> result;
         if(!map.TryGetValue(blockDefid, out result))
         {
            var btr = GetObject<BlockTableRecord>(blockDefid);
            map[blockDefid] = result = GetNestedBlockRefIds(btr);
         }
         return result;
      }

      List<ObjectId> GetNestedBlockRefIds(BlockTableRecord btr)
      {
         List<ObjectId> list = new List<ObjectId>();
         foreach(ObjectId id in btr.Cast<ObjectId>().Where(IsBlockRefId))
         {
            BlockReference blkref = GetObject<BlockReference>(id);
            if(IsBlockReference(blkref)) //  && OnAddBlockReference(btr, blkref))
               list.Add(id);
         }
         return list;
      }

      //protected virtual bool OnAddBlockReference(BlockTableRecord owner, BlockReference blkref)
      //{
      //   return true;
      //}

      static bool IsBlockReference(BlockReference entity)
      {
         return !(entity is Table) && !DBObject.IsCustomObject(entity.ObjectId);
      }

      protected T GetObject<T>(ObjectId id) where T: DBObject
      {
         return (T)trans.GetObject(id, OpenMode.ForRead);
      }
   }

}
