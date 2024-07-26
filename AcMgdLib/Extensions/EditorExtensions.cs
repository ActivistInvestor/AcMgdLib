using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autodesk.AutoCAD.EditorInput.Extensions
{
   public static partial class EditorExtensions
   {

      /// <summary>
      /// Helper extension methods for getting a
      /// specific type of object from the user:
      /// </summary>

      public static ObjectId GetEntity<T>(this Editor ed,
            string message,
            bool readOnly = true)
         where T : Entity
      {
         return ed.GetEntity<T>(new PromptEntityOptions(message), readOnly);
      }

      public static ObjectId GetEntity<T>(this Editor ed,
            PromptEntityOptions options,
            bool readOnly = true)
         where T : Entity
      {
         RXClass rxclass = RXObject.GetClass(typeof(T));
         string name = rxclass.DxfName;
         if(string.IsNullOrEmpty(name))
            name = rxclass.Name;
         options.SetRejectMessage($"\nInvalid selection, {name} required,");
         options.AddAllowedClass(typeof(T), false);
         if(readOnly)
            options.AllowObjectOnLockedLayer = true;
         var result = ed.GetEntity(options);
         return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
      }

      public static string GetBlockName(this Editor editor, string message, string defaultValue = null, bool allowExisting = true)
      {
         Assert.IsNotNull(editor, nameof(editor));
         /// TODO: Reformat supplied message to include defaultValue
         if(string.IsNullOrEmpty(message))
            message = "\nBlock name: ";
         PromptStringOptions pso = new PromptStringOptions(message);
         pso.AllowSpaces = true;
         if(!string.IsNullOrWhiteSpace(defaultValue))
         {
            pso.UseDefaultValue = true;
            pso.DefaultValue = defaultValue;
         }
         while(true)
         {
            PromptResult pr = editor.GetString(pso);
            if(pr.Status != PromptStatus.OK)
               return null;
            if(string.IsNullOrWhiteSpace(pr.StringResult))
               return defaultValue;
            Database db = editor.Document.Database;
            using(var tr = new DatabaseTransaction(db, true))
            {
               tr.IsReadOnly = true;
               if(!SymbolUtilities.TryValidateSymbolName(pr.StringResult))
               {
                  editor.WriteMessage("\nInvalid block name,");
                  continue;
               }
               if(!allowExisting && tr.BlockTable.Contains(pr.StringResult))
               {
                  editor.WriteMessage("\nA block with the specified name exists,");
                  continue;
               }
               return pr.StringResult;
            }
         }
      }
   }
}
