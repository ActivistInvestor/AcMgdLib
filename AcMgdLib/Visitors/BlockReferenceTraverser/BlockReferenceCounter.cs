
/// BlockReferenceCounter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the BlockReferenceTraverser class.

using System.Collections.Generic;
using AcMgdLib.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcMgdLib.DatabaseServices
{
   public class BlockReferenceCounter : BlockReferenceTraverser
   {
      CountByMap<ObjectId> count = new CountByMap<ObjectId>();
      bool countDynamic;

      public BlockReferenceCounter(ObjectId blockId, bool countDynanic = true) 
         : base(blockId)
      {
         this.countDynamic = countDynanic;
      }

      protected override ObjectId GetBlockTableRecordId(BlockReference blockref)
      {
         return countDynamic ? blockref.DynamicBlockTableRecord : blockref.BlockTableRecord;
      }

      protected override bool VisitBlockReference(Stack<BlockReference> refs)
      {
         if(refs.Count > 0)
            count.Increment(refs.Peek().BlockTableRecord);
         return base.VisitBlockReference(refs);
      }

      public Dictionary<ObjectId, int> Count
      {
         get
         {
            if(count == null)
            {
               count = new CountByMap<ObjectId>();
               base.Visit();
            }
            return count;
         }
      }

   }

}
