using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace Ac2025Project
{
   public static class UtilityCommands
   {
      /// <summary>
      /// Displays a simple dump of the managed properties
      /// of a selected object. 
      /// </summary>
      
      [CommandMethod("MGDUMP", CommandFlags.UsePickSet | CommandFlags.Redraw)]
      public static void Dump()
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

   }
}
