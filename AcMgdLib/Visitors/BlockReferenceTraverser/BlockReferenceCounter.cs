
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
   /// <summary>
   /// This class counts block references, and is consistent
   /// with the count provided by AutoCAD's COUNT UI.
   /// 
   /// See the BlockReferenceCounterExample.cs file for an
   /// exammple showing its use.
   /// </summary>

   public class BlockReferenceCounter : BlockReferenceTraverser
   {
      CountMap<ObjectId> count;
      int entmods = -1;

      public BlockReferenceCounter(ObjectId blockId, bool resolveDynamic = true)
         : base(blockId, resolveDynamic)
      {
      }

      protected override bool VisitBlockReference(ObjectId blockId, Stack<BlockReference> path)
      {
         bool result = base.VisitBlockReference(blockId, path);
         if(result)
            count += blockId;
         return result;
      }

      public override void Clear()
      {
         base.Clear();
         count = new CountMap<ObjectId>();
      }

      public Dictionary<ObjectId, int> Count
      {
         get
         {
            if(count == null || this.Database.IsModified())
            {
               base.Visit();
            }
            return count;
         }
      }
   }

}
