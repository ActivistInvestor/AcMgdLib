using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace CopyBlockExample
{
   public static class CopyBlockExampleCommands
   {

      /// <summary>
      /// Copies an existing block and gives the new copy the specified name.
      /// </summary>

      public static class CopyBlockExample
      {
         [CommandMethod("BCOPY")]
         public static void CopyBlockCommand()
         {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            ed.WriteMessage("\nEntering BCOPY command.");
            Database db = doc.Database;
            var btrId = ed.GetEntity<BlockReference>("\nSelect block reference: ");
            if(btrId.IsNull)
               return;
            string newName = ed.GetBlockName("\nNew block name: ", null, false);
            if(string.IsNullOrWhiteSpace(newName))
               return;
            try
            {
               using(var tr = new DocumentTransaction())
               {
                  if(tr.BlockTable.Contains(newName))
                  {
                     ed.WriteMessage("\nA block with the specified name already exists.");
                     tr.Commit();
                     return;
                  }
                  var blockref = tr.GetObject<BlockReference>(btrId);
                  ObjectId id = blockref.DynamicBlockTableRecord;
                  BlockTableRecord btr = tr.GetObject<BlockTableRecord>(id);
                  string name = btr.Name;
                  ObjectId cloneId = btr.Copy(newName);
                  btr = tr.GetObject<BlockTableRecord>(cloneId);
                  ed.WriteMessage("\nBlock [{0}] copied to [{1}].", name, btr.Name);
                  tr.Commit();
               }
            }
            catch(System.Exception ex)
            {
               ed.WriteMessage("\n\n{0}", ex.ToString());
               // ed.WriteMessage("\nOperation failed: {0}", ex.Message);
            }
         }

      }
   }
}






