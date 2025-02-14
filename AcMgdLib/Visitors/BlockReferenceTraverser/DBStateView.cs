
/// DatabaseState.cs (partial)
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Helper classes for the Database class (excerpted)

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcMgdLib.DatabaseServices
{
   /// <summary>
   /// A class that's used to detect if a
   /// Database has been modified between
   /// two points in time.
   /// </summary>

   public class DBStateView
   {
      Database db;
      int last;

      public DBStateView(Database db)
      {
         this.db = db;
         last = -1; 
      }

      public bool IsModified
      {
         get
         {
            var curDb = HostApplicationServices.WorkingDatabase;
            bool flag = curDb != db;
            if(flag)
               HostApplicationServices.WorkingDatabase = db;
            try
            {
               int previous = last;
               last = (int)Application.GetSystemVariable("ENTMODS");
               return previous != last;
            }
            finally
            {
               if(flag)
                  HostApplicationServices.WorkingDatabase = curDb;
            }
         }
      }
   }

}
