
/// DatabaseExtensions.cs (partial)
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extensions to the Database class

using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcMgdLib.DatabaseServices
{
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
