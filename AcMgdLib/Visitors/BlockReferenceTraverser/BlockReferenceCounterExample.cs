
/// BlockReferenceCounterExample.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using AcMgdLib.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.Visitors.Examples
{
   public static class BlockReferenceCounterExample
   {
      /// <summary>
      /// An example demonstrating the use of the 
      /// BlockReferenceCounter class.
      /// 
      /// This example should display the number of 
      /// instances of every block the user sees, 
      /// including those within associative arrays.
      /// 
      /// Its results are consistent with what 
      /// is reported by AutoCAD's COUNT pallete,
      /// exclusive of error detection.
      /// </summary>

      [CommandMethod("MGDCOUNT")]
      public static void MgdCount()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         var editor = doc.Editor;
         PromptSelectionOptions pso = new PromptSelectionOptions();
         TypedValue[] array = new TypedValue[] { (new TypedValue(0, "INSERT")) };
         SelectionFilter filt = new SelectionFilter(array);
         pso.RejectObjectsFromNonCurrentSpace = true;
         editor.WriteMessage("\nSelect block references or press ENTER for all,");
         var psr = editor.GetSelection(pso, new SelectionFilter(array));
         ObjectId[] selection = null;
         if(psr.Status == PromptStatus.OK && psr.Value.Count > 0)
         {
            selection = psr.Value.GetObjectIds();
         }
         else if(psr.Status != PromptStatus.Error)
         {
            return;
         }
         try
         {
            BlockReferenceCounter counter;
            if(selection != null)
               counter = new BlockReferenceCounter(selection);
            else
               counter = new BlockReferenceCounter(doc.Database.CurrentSpaceId);
            var pairs = counter.CountWithNames();
            var format = pairs.GetFormatter();
            foreach(var pair in pairs.OrderBy(p => p.Key))
            {
               editor.WriteMessage($"\n{format(pair)}");
            }
            var total = new KeyValuePair<string, int>("  Total:", pairs.Values.Sum());
            string totaltext = format(total);
            string lines = new string('-', totaltext.Length);
            editor.WriteMessage($"\n{lines}\n");
            editor.WriteMessage(totaltext);
         }
         catch(System.Exception ex)
         {
            editor.WriteMessage(ex.ToString());
         }
      }

      /// <summary>
      /// Helper for formatting console output
      /// </summary>

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

      public static Func<KeyValuePair<string, double>, string> GetFormatter(
         this Dictionary<string, double> data,
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
