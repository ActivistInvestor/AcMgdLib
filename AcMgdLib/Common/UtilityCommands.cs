/// UtilityCommands.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using AcMgdLib.Interop.Examples;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using Autodesk.AutoCAD.Runtime.NativeInterop;

namespace AcMgdLib.Utility
{
   public static class UtilityCommands
   {
      /// <summary>
      /// Displays a simple dump of the managed properties
      /// of a selected object. 
      /// </summary>
      
      [CommandMethod("MGDUMP", CommandFlags.UsePickSet | CommandFlags.Redraw)]
      public static void MgDump()
      {
         using(var trans = new DocumentTransaction(true, true))
         {
            var ss = trans.Editor.SelectImplied();
            if(ss.Status == PromptStatus.OK && ss.Value?.Count == 1)
            {
               trans[ss.Value[0].ObjectId].Dump();
               trans.Editor.SetImpliedSelection(ss.Value.GetObjectIds());
               return;
            }
            var peo = new PromptEntityOptions("\nSelect an object: ");
            peo.AllowObjectOnLockedLayer = true;
            while(true)
            {
               var per = trans.Editor.GetEntity(peo);
               if(per.Status != PromptStatus.OK)
                  return;
               trans[per.ObjectId].Dump();
            }
         }
      }

      /// <summary>
      /// Uses DwgDataList to dump the output of an entity's
      /// DwgOut() implementation.
      /// </summary>

      [CommandMethod("DWGDUMP", CommandFlags.UsePickSet | CommandFlags.Redraw)]
      public static void DwgDump()
      {
         using(var trans = new DocumentTransaction(true, true))
         {
            var ss = trans.Editor.SelectImplied();
            if(ss.Status == PromptStatus.OK && ss.Value?.Count == 1)
            {
               trans[ss.Value[0].ObjectId].DwgDump();
               trans.Editor.SetImpliedSelection(ss.Value.GetObjectIds());
               return;
            }
            var peo = new PromptEntityOptions("\nSelect an object: ");
            peo.AllowObjectOnLockedLayer = true;
            while(true)
            {
               var per = trans.Editor.GetEntity(peo);
               if(per.Status != PromptStatus.OK)
                  return;
               trans[per.ObjectId].DwgDump();
            }
         }
      }

      /// <summary>
      /// Uses acdbEntGet() to display a DXF dump of a
      /// selected object.
      /// </summary>

      [CommandMethod("DXFDUMP", CommandFlags.UsePickSet | CommandFlags.Redraw)]
      public static void DxfDump()
      {
         using(var trans = new DocumentTransaction(true, true))
         {
            var ed = trans.Editor;
            var ss = trans.Editor.SelectImplied();
            if(ss.Status == PromptStatus.OK && ss.Value?.Count > 0)
            {
               foreach(ObjectId id in ss.Value.GetObjectIds())
               {
                  TypedValueList tvList = id.EntGet();
                  ed.WriteMessage("\n" + tvList.ToString<short>("\n"));
                  ed.WriteMessage("\n\n");
               }
               trans.Editor.SetImpliedSelection(ss.Value.GetObjectIds());
               return;
            }
            var peo = new PromptEntityOptions("\nSelect an object: ");
            peo.AllowObjectOnLockedLayer = true;
            while(true)
            {
               var per = trans.Editor.GetEntity(peo);
               if(per.Status != PromptStatus.OK)
                  return;
               TypedValueList tvList = per.ObjectId.EntGet();
               ed.WriteMessage("\n" + tvList.ToString<short>("\n"));
            }
         }
      }

      /// <summary>
      /// List the names of *all* groups in a drawing file,
      /// along with their count, and selectable, anonymous 
      /// and erased status.
      /// 
      /// Using this command reveals that when inserting
      /// a file containing groups into a drawing, empty
      /// anonymous groups are created. 
      /// 
      /// It also shows that those empty groups are removed 
      /// when the file is saved (or subsequently-reopened).
      /// 
      /// Note that this command is dependent on AcMgdLib, 
      /// and will not work without it.
      /// </summary>

      [CommandMethod("LISTGR")]
      public static void ListGroups()
      {
         using(var tr = new DocumentTransaction(true, true))
         {
            foreach(var gr in tr.GetNamedObjects<Group>())
            {
               AcConsole.Write($"{gr.Name}  Count: {gr.NumEntities}  " +
                  $"Selectable: {gr.Selectable}  " +
                  $"Anonymous: {gr.IsAnonymous}  " +
                  $"Erased: {gr.IsErased}");
               gr.GetXDataForApplication("ACMGDLIB_GROUPDATA")?.Dump();
            }
         }
      }


   }
}
