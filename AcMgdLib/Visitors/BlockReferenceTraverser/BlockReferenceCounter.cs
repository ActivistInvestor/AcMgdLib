
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
using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.DatabaseServices
{
   /// <summary>
   /// This class counts block references, and is consistent
   /// with the count provided by AutoCAD's COUNT UI.
   /// 
   /// See the BlockReferenceCounterExample.cs file for an
   /// exammple showing its use.
   /// 
   /// Revisions:
   /// 
   /// 2.13.25:
   /// 
   /// The resolveDynamic parameter and property has been
   /// removed from this class, because accurate counting 
   /// of blocks requires that the definitions of anonymous
   /// dynamic blocks be visited, to accomodate cases where
   /// there can be a variable number of nested inserions
   /// of blocks in each anonymous variation's definition
   /// (e.g., parameterized arrays).
   /// </summary>

   public class BlockReferenceCounter : BlockReferenceVisitor
   {
      CountMap<ObjectId> count;
      DBStateView state;

      /// <summary>
      /// Counts all BlockReferences nested in the BlockTableRecord
      /// whose ObjectId is passed as the argument. The argument can
      /// be the ObjectId of a layout block, or the Id of any block 
      /// in the block table.
      /// 
      /// The result contains values for non-dynamic anonymous block
      /// references, but not anonymous dynamic block references, as
      /// they always resolve to the dynamic block definition.
      /// </summary>
      
      public BlockReferenceCounter(ObjectId blockId)
         : base(blockId) 
      {
         state = new DBStateView(blockId.Database);
      }

      public BlockReferenceCounter(IEnumerable<ObjectId> ids)
         : base(ids)
      {
         if(ids == null || !ids.Any())
            throw new ArgumentException("null or empty sequence");
         state = new DBStateView(ids.First().Database);
      }

      /// <summary>
      /// This override visits the definitions of anonymous
      /// dynamic blocks, but treats insertions of them as
      /// insertions of the dynamic block definition.
      /// </summary>
      /// <param name="blockref"></param>
      /// <param name="block"></param>
      /// <param name="containers"></param>
      /// <returns></returns>
      
      protected override bool VisitBlockReference(
         BlockReference blockref,
         BlockTableRecord block, 
         Stack<BlockReference> containers)
      {
         bool result = !block.IsFromExternalReference;
         if(result)
            count += blockref.DynamicBlockTableRecord;
         return result;
      }

      public override void BeginVisit()
      {
         base.BeginVisit();
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
         var counts = Count;
         Dictionary<string, int> result = new Dictionary<string, int>();
         if(counts.Count > 0)
         {
            using(var tr = new OpenCloseTransaction())
            {
               foreach(var pair in counts)
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
