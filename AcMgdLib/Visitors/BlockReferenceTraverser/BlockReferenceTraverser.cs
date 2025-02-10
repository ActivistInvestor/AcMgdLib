
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
      private IEnumerable<ObjectId> EmptyHashSet;

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
         Clear();
         using(trans = new OpenCloseTransaction())
         {
            Stack<BlockReference> refs = new Stack<BlockReference>();
            foreach(var id in GetBlockReferenceIds(rootBlockId))
            {
               Visit(id, refs);
            }
            trans.Commit();
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
         ObjectId blockId = GetReferencedBlockId(blkref);
         if(VisitBlockReference(blockId, path))
         {
            var nested = GetBlockReferenceIds(blockId);
            foreach(ObjectId id in nested)
               Visit(id, path);
         }
         path.Pop();
      }

      protected virtual bool VisitBlock(BlockTableRecord btr)
      {
         return true;
      }

      protected ObjectId GetReferencedBlockId(BlockReference blockref)
      {
         return ResolveBlockId(blockref);
      }

      protected virtual bool VisitBlockReference(ObjectId blockId, Stack<BlockReference> path)
      {
         return path.Count > 0 && IsBlockReference(path.Peek());
      }

      protected Dictionary<ObjectId, IEnumerable<ObjectId>> Map => map;

      private IEnumerable<ObjectId> GetBlockReferenceIds(ObjectId blockId)
      {
         IEnumerable<ObjectId> result;
         if(!map.TryGetValue(blockId, out result))
         {
            var btr = GetObject<BlockTableRecord>(blockId);
            if(VisitBlock(btr))
               result = GetNestedBlockRefIds(btr);
            else
               result = Enumerable.Empty<ObjectId>();
            map[blockId] = result;
         }
         return result;
      }

      List<ObjectId> GetNestedBlockRefIds(BlockTableRecord btr)
      {
         List<ObjectId> list = new List<ObjectId>();
         foreach(ObjectId id in btr.Cast<ObjectId>().Where(IsBlockRefId))
         {
            BlockReference blkref = GetObject<BlockReference>(id);
            if(IsBlockReference(blkref)) 
               list.Add(id);
         }
         return list;
      }

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
