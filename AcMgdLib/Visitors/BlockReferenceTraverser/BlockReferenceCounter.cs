
/// BlockReferenceCounter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the BlockReferenceTraverser class.

using System;
using System.Collections.Generic;
using System.Linq;
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

   public class BlockReferenceCounter : BlockReferenceVisitor
   {
      CountMap<ObjectId> count;
      DBStateView state;
      int entmods = -1;

      public BlockReferenceCounter(ObjectId blockId, bool resolveDynamic = true)
         : base(blockId, resolveDynamic)
      {
         state = new DBStateView(blockId.Database);
      }

      public BlockReferenceCounter(IEnumerable<ObjectId> ids, bool resolveDynamic = true)
         : base(ids, resolveDynamic)
      {
         if(ids == null || !ids.Any())
            throw new ArgumentException("null or empty sequence");
         state = new DBStateView(ids.First().Database);
      }

      protected override bool VisitBlockReference(
         Stack<BlockReference> path,
         BlockTableRecord block)
      {
         bool result = base.VisitBlockReference(path, block);
         if(result)
            count += block.ObjectId;
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
            CheckVisiting(false);
            if(count == null || state.IsModified)
            {
               base.Visit();
            }
            return count;
         }
      }

      public Dictionary<string, int> CountWithNames(bool includingAnonymous = false)
      {
         var cnt = Count;
         Dictionary<string, int> result = new Dictionary<string, int>();
         if(cnt.Count > 0)
         {
            using(var tr = new OpenCloseTransaction())
            {
               foreach(var pair in cnt)
               {
                  var btr = (BlockTableRecord)tr.GetObject(pair.Key, OpenMode.ForRead);
                  if(includingAnonymous || !btr.IsAnonymous)
                     result[btr.Name] = pair.Value;
               }
               tr.Commit();
            }
         }
         return result;
      }

   }

}
