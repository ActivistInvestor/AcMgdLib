
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

using System;
using System.Collections.Generic;
using System.Linq;
using AcMgdLib.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.Visitors.Examples
{
   public static class BlockReferenceCounterExample
   {
      [CommandMethod("DEEPBCOUNT")]
      public static void DeepBCount()
      {
         try
         {
            ObjectId id = HostApplicationServices.WorkingDatabase.CurrentSpaceId;
            var counter = new BlockReferenceCounter(id);
            counter.Visit();
            var pairs = GetBlockNames(counter.Count);
            var editor = Application.DocumentManager.MdiActiveDocument.Editor;
            var format = pairs.GetFormatter();
            foreach(var pair in pairs.OrderBy(p => p.Key))
            {
               editor.WriteMessage($"\n{format(pair)}");
            }
            var total = new KeyValuePair<string, int>("  Total:", pairs.Values.Sum());
            string totstr = format(total);
            string rule = new string('-', totstr.Length);
            editor.WriteMessage($"\n{rule}\n");
            editor.WriteMessage(totstr);
         }
         catch(System.Exception ex)
         {
            AcConsole.Write(ex.ToString());
         }
      }

      static Dictionary<string, int> GetBlockNames(Dictionary<ObjectId, int> map,
         bool includingAnonymous = false)
      {
         using(var tr = new OpenCloseTransaction())
         {
            Dictionary<string, int> result = new Dictionary<string, int>();
            foreach(var pair in map)
            {
               var btr = (BlockTableRecord)tr.GetObject(pair.Key, OpenMode.ForRead);
               if(includingAnonymous || !btr.IsAnonymous)
                  result[btr.Name] = pair.Value;
            }
            tr.Commit();
            return result;
         }
      }

      public static Func<KeyValuePair<string, int>, string> GetFormatter(
         this Dictionary<string, int> data,
         string prefix = "\n",
         int padding = 3)
      {
         int maxKey = data.Keys.Max(key => key.Length) + padding;
         int maxVal = data.Values.Max().ToString().Length;
         return p => string.Format(
            "{0,-" + maxKey + "}{1," + maxVal + "}", p.Key, p.Value);
      }
   }


}
