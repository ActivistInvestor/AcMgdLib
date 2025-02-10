
/// BlockReferenceCounter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the BlockReferenceTraverser class.

using System;
using System.Collections.Generic;
using AcMgdLib.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
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
      CountMap<ObjectId> count = new CountMap<ObjectId>();
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
               count = new CountMap<ObjectId>();
               base.Visit();
            }
            return count;
         }
      }
   }


   public static partial class DatabaseExtensions
   {
      static int lastEntMod = -1;

      /// <summary>
      /// Used to detect if the database has changed 
      /// between two consecutive calls to this method.
      /// 
      /// The first call to this method always returns
      /// true. 
      /// </summary>
      /// <returns></returns>

      public static bool IsModified(this Database db)
      {
         int last = lastEntMod;
         lastEntMod = GetEntMods(db);
         return last != lastEntMod;
      }
      
      static int GetEntMods(Database db)
      {
         using(new WorkingDatabase(db))
         {
            return (int)Application.GetSystemVariable("ENTMODS");
         }
      }

      class WorkingDatabase : IDisposable
      {
         Database previous = null;
         public WorkingDatabase(Database db)
         {
            var current = HostApplicationServices.WorkingDatabase;
            if(current != db)
            {
               HostApplicationServices.WorkingDatabase = db;
               previous = current;
            }
         }

         public void Dispose()
         {
            if(previous != null)
            {
               HostApplicationServices.WorkingDatabase = previous;
               previous = null;
            }
         }
      }
   }

}
