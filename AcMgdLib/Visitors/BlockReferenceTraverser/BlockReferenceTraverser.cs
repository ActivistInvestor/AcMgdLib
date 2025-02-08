
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

      public BlockReferenceTraverser(ObjectId blockId)
      {
         if(blockId.ObjectClass != RXObject.GetClass(typeof(BlockTableRecord)))
            throw new ArgumentException("Invalid objectId");
         rootBlockId = blockId;
      }

      public void Visit()
      {
         trans = new OpenCloseTransaction();
         try
         {
            Stack<BlockReference> refs = new Stack<BlockReference>();
            foreach(var id in GetBlockReferenceIds(rootBlockId))
            {
               Visit(id, refs);
            }
         }
         finally
         {
            trans.Commit();
            trans = null;
         }
      }

      /// <summary>
      /// Takes the ObjectId of a BlockReference and
      /// processes the nested BlockReferences contained
      /// in the referenced block's definition, recursively.
      /// </summary>
      /// <param name="id"></param>

      void Visit(ObjectId blockRefId, Stack<BlockReference> refs)
      {
         var blkref = GetObject<BlockReference>(blockRefId);
         refs.Push(blkref);
         if(VisitBlockReference(refs))
         {
            var nested = GetBlockReferenceIds(GetBlockTableRecordId(blkref));
            foreach(ObjectId id in nested)
               Visit(id, refs);
         }
         refs.Pop();
      }

      /// <summary>
      /// To count dynamic blocks rather than anonymous 
      /// dynamic blocks, override this and return the 
      /// argument's DynamicBlockTableRecord property 
      /// value:
      /// </summary>
      /// <param name="blockref"></param>
      
      protected virtual ObjectId GetBlockTableRecordId(BlockReference blockref)
      {
         return blockref.BlockTableRecord;
      }

      protected virtual bool VisitBlockReference(Stack<BlockReference> refs)
      {
         return true;
      }

      Dictionary<ObjectId, IEnumerable<ObjectId>> map = 
         new Dictionary<ObjectId, IEnumerable<ObjectId>>();

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
            if(IsBlockReference(blkref) && OnAddBlockReference(btr, blkref))
               list.Add(id);
         }
         return list;
      }

      protected virtual bool OnAddBlockReference(BlockTableRecord btr, BlockReference blkref)
      {
         return true;
      }

      static bool IsBlockReference(Entity entity)
      {
         return entity is BlockReference && !(entity is Table)
            && !DBObject.IsCustomObject(entity.ObjectId);
      }

      T GetObject<T>(ObjectId id) where T: DBObject
      {
         return (T)trans.GetObject(id, OpenMode.ForRead);
      }
   }

}
