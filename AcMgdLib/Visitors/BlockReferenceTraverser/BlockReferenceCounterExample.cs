
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
using Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions;
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
         pso.RejectObjectsFromNonCurrentSpace = true;
         editor.WriteMessage("\nSelect block references or ENTER for all,");
         var psr = editor.GetSelection(pso, (0, "INSERT"));
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
            var formatter = pairs.GetFormatter();
            foreach(var pair in pairs.OrderBy(p => p.Key)) 
               editor.WriteMessage("\n" + formatter(pair));
            var total = new KeyValuePair<string, int>("  Total:", pairs.Values.Sum());
            string txt = formatter(total);
            editor.WriteMessage($"\n{new string('-', txt.Length)}\n{txt}");
         }
         catch(System.Exception ex)
         {
            editor.WriteMessage(ex.ToString());
         }
      }

      public static Func<KeyValuePair<string, int>, string> GetFormatter(
         this Dictionary<string, int> data,
         string prefix = "\n",
         int margin = 3)
      {
         int maxKey = data.Keys.Max(key => key.Length) + margin;
         int maxVal = data.Values.Max().ToString().Length;
         return p => string.Format(
            "{0,-" + maxKey + "}{1," + maxVal + "}", p.Key, p.Value);
      }

   }

   public static partial class EditorInputExtensions
   {
      public static SelectionFilter GetFilter(this Editor ed,
         params (DxfCode code, object value)[] values)
      {
         return new SelectionFilter(
            values.Select(t => new TypedValue((short)t.code, t.value)).ToArray());
      }

      public static PromptSelectionResult GetSelection(this Editor ed,
         PromptSelectionOptions pso,
         params (DxfCode code, object value)[] values)
      {
         return ed.GetSelection(pso, GetFilter(ed, values));
      }
   }


}
