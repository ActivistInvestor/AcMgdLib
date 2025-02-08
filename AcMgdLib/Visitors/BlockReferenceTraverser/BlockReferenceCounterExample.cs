
/// BlockReferenceCounterExample.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the BlockReferenceCount class.
/// 
/// This example should display the number of instances
/// of every block the user sees, including those nested
/// in associative arrays.

using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcMgdLib.DatabaseServices;

namespace AcMgdLib.Visitors.Examples
{
   public static class BlockReferenceCounterExample
   {
      [CommandMethod("DEEPBCOUNT")]
      public static void CountEmAll()
      {
         try
         {
            ObjectId id = HostApplicationServices.WorkingDatabase.CurrentSpaceId;
            var traverser = new BlockReferenceCounter(id);
            traverser.Visit();
            var pairs = GetBlockNames(traverser.Count);
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            foreach(var pair in pairs)
            {
               editor.WriteMessage($"\n{pair.Key}:\t{pair.Value}");
            }
         }
         catch(System.Exception ex)
         {
            AcConsole.Write(ex.ToString());
         }
      }

      static IEnumerable<KeyValuePair<string, int>> GetBlockNames(
         Dictionary<ObjectId, int> map)
      {
         using(var tr = new OpenCloseTransaction())
         {
            foreach(var pair in map)
            {
               var btr = (BlockTableRecord)tr.GetObject(pair.Key, OpenMode.ForRead);
               yield return new KeyValuePair<string, int>(btr.Name, pair.Value);
            }
            tr.Commit();
         }
      }
   }

}
