
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
      Func<BlockReference, ObjectId> ResolveBlockId = null;
      bool resolveDynamic = true;
      bool visiting = false;

      public BlockReferenceTraverser(ObjectId blockId, bool resolveDynamic = true)
      {
         if(blockId.IsNull || !blockId.IsValid)
            throw new ArgumentException("Null or Invalid ObjectId");
         if(blockId.ObjectClass != RXObject.GetClass(typeof(BlockTableRecord)))
            throw new ArgumentException("Wrong object type");
         rootBlockId = blockId;
         this.resolveDynamic = resolveDynamic;
         if(resolveDynamic)
            ResolveBlockId = blkref => blkref.DynamicBlockTableRecord;
         else
            ResolveBlockId = blkref => blkref.BlockTableRecord;
      }

      protected Database Database => rootBlockId.Database;
      protected ObjectId RootBlockId => rootBlockId;

      public virtual void Visit()
      {
         Clear();
         visiting = true;
         try
         {
            using(trans = new OpenCloseTransaction())
            {
               Stack<BlockReference> refs = new Stack<BlockReference>();
               var root = GetObject<BlockTableRecord>(rootBlockId);
               foreach(var id in GetBlockReferenceIds(root))
               {
                  Visit(id, refs);
               }
               trans.Commit();
            }
         }
         finally
         {
            visiting = false;
         }
      }

      public virtual void Clear()
      {
         map.Clear();
      }

      void Visit(ObjectId blockRefId, Stack<BlockReference> containers)
      {
         var blkref = GetObject<BlockReference>(blockRefId);
         ObjectId blockId = GetReferencedBlockId(blkref);
         var block = GetObject<BlockTableRecord>(blockId);
         containers.Push(blkref);
         if(VisitBlockReference(block, containers))
         {
            foreach(ObjectId id in GetBlockReferenceIds(block))
               Visit(id, containers);
         }
         containers.Pop();
      }

      protected virtual bool VisitBlock(BlockTableRecord btr)
      {
         return true;
      }

      protected ObjectId GetReferencedBlockId(BlockReference blockref)
      {
         return ResolveBlockId(blockref);
      }

      protected virtual bool VisitBlockReference(
         BlockTableRecord block, 
         Stack<BlockReference> path)
      {
         return path.Count > 0 && IsTrueBlockReference(path.Peek());
      }

      protected Dictionary<ObjectId, IEnumerable<ObjectId>> Map => map;

      private IEnumerable<ObjectId> GetBlockReferenceIds(BlockTableRecord btr)
      {
         IEnumerable<ObjectId> result;
         if(!map.TryGetValue(btr.ObjectId, out result))
         {
            if(VisitBlock(btr))
               result = GetNestedBlockRefIds(btr);
            else
               result = Enumerable.Empty<ObjectId>();
            map[btr.ObjectId] = result;
         }
         return result;
      }

      List<ObjectId> GetNestedBlockRefIds(BlockTableRecord btr)
      {
         List<ObjectId> list = new List<ObjectId>();
         foreach(ObjectId id in btr.Cast<ObjectId>().Where(IsBlockRefId))
         {
            BlockReference blkref = GetObject<BlockReference>(id);
            if(IsTrueBlockReference(blkref)) 
               list.Add(id);
         }
         return list;
      }

      static bool IsTrueBlockReference(BlockReference entity)
      {
         return !(entity is Table) && !DBObject.IsCustomObject(entity.ObjectId);
      }

      protected T GetObject<T>(ObjectId id) where T: DBObject
      {
         return (T)trans.GetObject(id, OpenMode.ForRead);
      }
   }

}
