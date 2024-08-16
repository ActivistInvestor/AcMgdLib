using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

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
         if(GetRejectMessage(options) == null)
            options.SetRejectMessage($"\nInvalid selection, {name} required,");
         options.AddAllowedClass(typeof(T), false);
         if(readOnly)
            options.AllowObjectOnLockedLayer = true;
         var result = ed.GetEntity(options);
         return result.IsFailed() ? ObjectId.Null : result.ObjectId;         
      }

      static object GetRejectMessage(PromptEntityOptions options)
      {
         return m_rejectMessage.GetValue(options);
      }

      static FieldInfo m_rejectMessage = typeof(PromptEntityOptions).GetField(
         "m_rejectMessage", BindingFlags.NonPublic | BindingFlags.Instance);

      public static string GetBlockName(this Editor editor, string message, string defaultValue = null, bool allowExisting = true)
      {
         Assert.IsNotNull(editor, nameof(editor));
         /// TODO: Reformat supplied message to include defaultValue
         if(string.IsNullOrEmpty(message))
            message = "\nBlock name: ";
         var pso = new PromptStringOptions(message);
         pso.AllowSpaces = true;
         if(!string.IsNullOrWhiteSpace(defaultValue))
         {
            pso.UseDefaultValue = true;
            pso.DefaultValue = defaultValue;
         }
         while(true)
         {
            var pr = editor.GetString(pso);
            if(pr.IsFailed())
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

      public static bool IsFailed(this PromptResult pr)
      {
         return pr.Status != PromptStatus.OK && pr.Status != PromptStatus.Keyword
            && pr.Status != PromptStatus.Other;
      }

      public static bool IsFailed(this PromptSelectionResult psr)
      {
         return psr.Status != PromptStatus.OK && psr.Status != PromptStatus.Keyword
            && psr.Status != PromptStatus.Other;
      }

      /// <summary>
      /// Gets multiple points from the user, optionally
      /// with each input point used as the basepoint for
      /// the next point, and an undo option.
      /// 
      /// This is a minimal implementation that lacks the
      /// use of any transient graphics to display lines
      /// between previously-entered points, etc. TODO.
      /// 
      /// If useBasePoint is true, an Undo option/keyword
      /// is enabled at each prompt for all but the initial 
      /// point, allowing the user to undo all previously-
      /// entered points, except for the initial point.
      /// </summary>
      /// <param name="editor">The Editor of the Active Document</param>
      /// <param name="useBasePoint">A value indicating if a rubber-
      /// band cursor from the most-recently entered point should
      /// be displayed.</param>
      /// <param name="firstPrompt">The prompt for the initial point</param>
      /// <param name="nextPrompt">The prompt for all subsequent points</param>
      /// <param name="func">A function taking a List<Point3d> and a Point3d, 
      /// that if supplied, must return a boolean indicating if the Point3d
      /// argument should be added to the list. If this function returns false, 
      /// the most-recently entered point is discarded and the user is prompted 
      /// again to supply another point.</param>
      /// <returns>An array of input points or null if the user cancels</returns>

      public static Point3d[] GetPoints(this Editor editor,
         bool useBasePoint = false,
         string firstPrompt = "\nFirst point: ",
         string nextPrompt = "\nNext point: ",
         Func<List<Point3d>, Point3d, bool> func = null)
      {
         Assert.IsNotNull(editor, nameof(editor));
         Assert.IsNotNullOrWhiteSpace(firstPrompt, nameof(firstPrompt));
         Assert.IsNotNullOrWhiteSpace(nextPrompt, nameof(nextPrompt));
         var ppo = new PromptPointOptions(firstPrompt);
         ppo.AllowNone = true;
         var ppr = editor.GetPoint(ppo);
         if(ppr.IsFailed())
            return null;
         List<Point3d> list = new List<Point3d>();
         list.Add(ppr.Value);
         ppo.Message = nextPrompt;
         ppo.AppendKeywordsToMessage = true;
         while(true)
         {
            if(useBasePoint)
            {
               ppo.UseBasePoint = true;
               ppo.BasePoint = list[list.Count - 1];
               ppo.Keywords.Clear();
               if(list.Count > 1)
                  ppo.Keywords.Add("Undo");
               //if(list.Count > 2 && !IsCollinear(list))
               //   ppo.Keywords.Add("Close");
            }
            ppr = editor.GetPoint(ppo);
            if(ppr.Status == PromptStatus.None)
               break;
            else if(ppr.Status == PromptStatus.Keyword && list.Count > 1)
            {
               list.RemoveAt(list.Count - 1);
               continue;
            }
            if(ppr.Status != PromptStatus.OK)
               return null;
            if(func == null || func(list, ppr.Value))
               list.Add(ppr.Value);
         }
         return list.Count > 0 ? list.ToArray() : null;
      }
   }

   /// <summary>
   /// TODO: to be returned by GetPoints()
   /// </summary>
   public class PromptPointsResult 
   {
      private PromptStatus status;
      List<Point3d> points;
      bool closed;

      public PromptPointsResult(PromptStatus ps, List<Point3d> points, bool closed = false)
      {
         this.status = ps;
         this.points = points ?? new List<Point3d>();
         this.closed = closed;
      }

      /// <summary>
      /// Indicates if the user chose "Close", if it was
      /// made available by the promptoptions.
      /// </summary>

      public bool Closed => closed;
      public PromptStatus Status => status;
      public IList<Point3d> Coordinates => points;
   }

   public static partial class AsyncEditorExtensions
   {
      static readonly DocumentCollection docs = Application.DocumentManager;

      public static PromptPointResult GetPointAsync(this Editor editor, string prompt)
      {
         Assert.IsNotNullOrWhiteSpace(prompt, nameof(prompt));
         return GetPointAsync(editor, new PromptPointOptions(prompt));
      }

      public static PromptPointResult GetPointAsync(this Editor editor, PromptPointOptions options)
      {
         AcConsole.Write("GetPointAsync()");
         Assert.IsNotNull(editor, nameof(editor));
         Assert.IsNotNull(options, nameof(options));
         if(!docs.IsApplicationContext)
            return editor.GetPoint(options);
         else
            return Invoke(editor, options).Result;

      }

      async static Task<PromptPointResult> Invoke(Editor ed, PromptPointOptions ppo)
      {
         PromptPointResult ppr = null;
         AcConsole.Write("Calling InvokeAsCommandAsync()");
         await docs.InvokeAsCommandAsync(delegate (Document doc)
         {
            AcConsole.Write("InvokeAsCommandAsync delegate");
            ppr = ed.GetPoint(ppo);
         });
         return ppr;
      }

   }
}
