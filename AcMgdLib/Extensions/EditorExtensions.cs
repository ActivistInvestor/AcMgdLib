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

      //public static ObjectId GetEntity<T>(this Editor ed,
      //      string message,
      //      bool readOnly = true)
      //   where T : Entity
      //{
      //   return ed.GetEntity<T>(new PromptEntityOptions(message), readOnly);
      //}

      //public static ObjectId GetEntity<T>(this Editor ed,
      //      PromptEntityOptions options,
      //      bool readOnly = true)
      //   where T : Entity
      //{
      //   RXClass rxclass = RXObject.GetClass(typeof(T));
      //   string name = rxclass.DxfName;
      //   if(string.IsNullOrEmpty(name))
      //      name = rxclass.Name;
      //   if(GetRejectMessage(options) == null)
      //      options.SetRejectMessage($"\nInvalid selection, {name} required,");
      //   options.AddAllowedClass(typeof(T), false);
      //   if(readOnly)
      //      options.AllowObjectOnLockedLayer = true;
      //   var result = ed.GetEntity(options);
      //   return result.IsFailed() ? ObjectId.Null : result.ObjectId;         
      //}

      //static object GetRejectMessage(PromptEntityOptions options)
      //{
      //   return m_rejectMessage.GetValue(options);
      //}

      //static FieldInfo m_rejectMessage = typeof(PromptEntityOptions).GetField(
      //   "m_rejectMessage", BindingFlags.NonPublic | BindingFlags.Instance);

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
      
      public static bool IsNone(this PromptResult pr)
      {
         return pr.Status == PromptStatus.None;
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

   //public static partial class AsyncEditorExtensions
   //{
   //   static readonly DocumentCollection docs = Application.DocumentManager;

   //   public static PromptPointResult GetPointAsync(this Editor editor, string prompt)
   //   {
   //      Assert.IsNotNullOrWhiteSpace(prompt, nameof(prompt));
   //      return GetPointAsync(editor, new PromptPointOptions(prompt));
   //   }

   //   public static PromptPointResult GetPointAsync(this Editor editor, PromptPointOptions options)
   //   {
   //      AcConsole.Write("GetPointAsync()");
   //      Assert.IsNotNull(editor, nameof(editor));
   //      Assert.IsNotNull(options, nameof(options));
   //      if(!docs.IsApplicationContext)
   //         return editor.GetPoint(options);
   //      else
   //         return Invoke(editor, options).Result;

   //   }

   //   async static Task<PromptPointResult> Invoke(Editor ed, PromptPointOptions ppo)
   //   {
   //      PromptPointResult ppr = null;
   //      AcConsole.Write("Calling InvokeAsCommandAsync()");
   //      await docs.InvokeAsCommandAsync(delegate (Document doc)
   //      {
   //         AcConsole.Write("InvokeAsCommandAsync delegate");
   //         ppr = ed.GetPoint(ppo);
   //      });
   //      return ppr;
   //   }

   //}
}

namespace Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions
{
   using Autodesk.AutoCAD.EditorInput;
   using System;
   using System.Reflection;
   using System.Runtime.CompilerServices;
   using System.Runtime.InteropServices;

   public static partial class EditorInputExtensions
   {
      /// <summary>
      /// An extended overload of the Editor's GetEntity() method
      /// that uses a generic argument to constrain the selected 
      /// entity to instances of a type (and/or a derived type if 
      /// the exact argument is false), that also accepts a caller-
      /// supplied function that will be called to further validate 
      /// a successful selection.
      /// 
      /// The validate function overcomes a problem associated with
      /// repeatedly re-issing an input prompt when the method is
      /// used from a modal dialog box. Without using this method,
      /// repeated calls to GetEntity() from a modal dialog will
      /// cause the dialog to repeatedly disappear and reappear on
      /// each input prompt. This method allows validation of the
      /// selected entity from a modal dialog without causing the
      /// dialog to repeatedly show/hide. To accomplish this, the
      /// validation method can be supplied to fully-validate the
      /// user's response, and if necessary, retry input on failed
      /// validation any number of times, while the dialog remains
      /// hidden. When using a validation function, the function
      /// can display a failure message and return false, and the
      /// input prompt will be re-issued until the user cancels,
      /// or the validation function returns true. A modal dialog 
      /// will reappear after the call to this method returns.
      /// 
      /// An example that requires a user to select a closed,
      /// planar curve:
      /// 
      /// <code>
      /// 
      ///   public static ObjectId GetBoundary()
      ///   {
      ///      Document doc = Application.DocumentManager.MdiActiveDocument;
      ///      Editor ed = doc.Editor;
      ///      var rslt = ed.GetEntity<Curve>("\nSelect boundary: ", false, validate);
      ///      if(rslt.Status == PromptStatus.Ok)
      ///         return result.ObjectId;
      ///      else
      ///         return ObjectId.Null;
      ///         
      ///      // The validation method is called by GetEntity<T>,
      ///      // when an object is selected, and is passed the open
      ///      // entity. If the validation method returns false, the 
      ///      // input prompt is re-issued:
      ///      
      ///      bool validate(Curve crv, PromptEntityResult rslt, Editor ed)
      ///      {
      ///         if(!(curve.Closed && curve.IsPlanar))
      ///         {
      ///            ed.WriteMessage("\nInvalid selection," + 
      ///               " requires a closed, planar curve,");
      ///            return false;
      ///         }
      ///         return true;
      ///      }
      ///   }
      ///  
      ///       
      ///   
      /// </code>
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="editor"></param>
      /// <param name="message"></param>
      /// <param name="validate"></param>
      /// <returns></returns>

      public static PromptEntityResult GetEntity<T>(this Editor editor, string message,
         bool exact = false, Func<T, PromptEntityResult, Editor, bool> validate = null)
         where T : Entity
      {
         return GetEntity<T>(editor, new PromptEntityOptions(message), exact, validate);
      }

      public static PromptEntityResult GetEntity<T>(this Editor editor,
         PromptEntityOptions peo,
         bool exact = false,
         Func<T, PromptEntityResult, Editor, bool> validate = null) where T : Entity
      {
         if(editor == null)
            throw new ArgumentNullException(nameof(editor));
         if(peo == null)
            throw new ArgumentNullException(nameof(peo));
         peo.AllowNone = true;
         if(string.IsNullOrEmpty(GetRejectMessage(peo)))
            peo.SetRejectMessage($"\nInvalid selection,  requires {typeof(T).Name},");
         peo.AddAllowedClass(typeof(T), exact && !typeof(T).IsAbstract);
         if(validate == null)
            return editor.GetEntity(peo);
         using(UserInteractionThunk.Begin())
         {
            while(true)
            {
               var result = editor.GetEntity(peo);
               if(result.Status != PromptStatus.OK)
                  return result;
               using(var tr = new OpenCloseTransaction())
               {
                  T entity = tr.GetObject(result.ObjectId, OpenMode.ForRead) as T;
                  if(validate(entity, result, editor))
                     return result;
               }
            }
         }
      }

      static FieldInfo rejectMessageField = typeof(PromptEntityOptions)
         .GetField("m_rejectMessage", BindingFlags.Instance | BindingFlags.NonPublic);

      static string GetRejectMessage(PromptEntityOptions peo)
      {
         return rejectMessageField.GetValue(peo) as string;
      }

      /// <summary>
      /// Wraps EditorUserInteraction and does nothing if
      /// there is no active modal window.
      /// </summary>

      class UserInteractionThunk : IDisposable
      {
         EditorUserInteraction wrapped;

         public UserInteractionThunk()
         {
            IntPtr hwndActive = GetActiveModalWindow();
            if(hwndActive != IntPtr.Zero)
            {
               wrapped = Start(hwndActive);
            }
         }

         public static IDisposable Begin()
         {
            return new UserInteractionThunk();
         }

         public void Dispose()
         {
            if(wrapped != null)
            {
               wrapped.Dispose();
               wrapped = null;
            }
         }

         static EditorUserInteraction Start(IntPtr hWnd)
         {
            if(ctor == null)
               throw new ArgumentNullException("EditorUserInteraction(IntPtr) constructor not found");
            return (EditorUserInteraction)ctor.Invoke(new object[] { hWnd });
         }

         static ConstructorInfo ctor =
            typeof(EditorUserInteraction).GetConstructor(typeof(IntPtr));

         const uint GW_OWNER = 4;

         [DllImport("user32.dll")]
         private static extern IntPtr GetActiveWindow();

         [DllImport("user32.dll")]
         private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

         [DllImport("user32.dll")]
         private static extern bool IsWindowEnabled(IntPtr hWnd);

         /// <summary>
         /// I've come across various approaches to this problem, 
         /// and none (including this one) are completely foolproof.
         /// </summary>
         /// <returns>The handle of an active modal window, or
         /// IntPtr.Zero if no active modal window is found.</returns>

         static IntPtr GetActiveModalWindow()
         {
            IntPtr hwnd = GetActiveWindow();
            if(hwnd != IntPtr.Zero && hwnd != Application.MainWindow.Handle)
            {
               IntPtr hwndOwner = GetWindow(hwnd, GW_OWNER);
               if(hwndOwner != IntPtr.Zero && !IsWindowEnabled(hwndOwner))
                  return hwnd;
            }
            return IntPtr.Zero;
         }
      }

      /// <summary>
      /// 
      /// Prompts user to respond with Yes or No and
      /// returns a bool? indicating the response.
      /// 
      /// Returns null if user cancels and/or doesn't 
      /// provide a response.
      /// </summary>

      public static bool? GetBool(this Editor editor, string msg, bool defaultValue = false, string kwTrue = "Yes", string kwFalse = "No")
      {
         if(string.IsNullOrEmpty(kwTrue) || string.IsNullOrEmpty(kwFalse))
            throw new ArgumentException("true/false keywords must be non-empty strings");
         if(kwTrue.Equals(kwFalse, StringComparison.InvariantCultureIgnoreCase))
            throw new ArgumentException("true and false keywords must not be equal");
         PromptKeywordOptions pko = new PromptKeywordOptions(msg);
         pko.Keywords.Add(kwTrue);
         pko.Keywords.Add(kwFalse);
         pko.Keywords.Default = defaultValue ? kwTrue : kwFalse;
         var pr = editor.GetKeywords(pko);
         if(pr.Status != PromptStatus.OK)
            return null;
         return pr.StringResult == kwTrue;
      }

   }

   public static partial class TypeExtensions
   {
      public static ConstructorInfo GetConstructor(this Type type, params Type[] argTypes)
      {
         return type?.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public,
            null, argTypes, null);
      }
   }

}

