/// PromptSelectionWithKeyword.cs  
/// 
/// ActivistInvestor / Tony TSource
/// 
/// Distributed under terms of the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Documents;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.EditorInput
{
   public static class PromptSelectionWithKeyword
   {
      public static PromptSelectionResult GetSelectionWithKeywords(this Editor editor, params string[] keywords)
      {
         string keyword = null;
         int i = 0;
         PromptSelectionOptions pso = new PromptSelectionOptions();
         pso.KeywordInput += OnKeywordInput;
         foreach(string s in keywords)
            pso.Keywords.Add(s);
         pso.Keywords.Add("Quit");
         pso.Keywords.Add("Change");
         var appContext = Application.DocumentManager.IsApplicationContext;
         AcConsole.WriteMessage($"\nIsApplicationContext = {appContext}");

         void OnKeywordInput(object sender, SelectionTextInputEventArgs e)
         {
            keyword = e.Input;
            editor.WriteMessage($"User entered keyword '{e.Input}'");
            if(e.Input == "Quit" || e.Input == "Change")
               throw new KeywordException(e.Input); // break out of loop

            /// The Quit and Change keywords must do things
            /// that cannot be done here, and so the call to
            /// GetSelection() must be exited and reentered
            /// in order for the actions for those keywords
            /// to be taken.
            /// 
            /// Any keywords other than Quit and Change that do
            /// not require exiting the call to GetSelection()
            /// should be handled here, rather than in the 
            /// catch{} block below.

            if(e.Input == "Second") 
            {
               AcConsole.TraceExpr(pso.Keywords.IsReadOnly);
               var kw = pso.Keywords.GetAt("NewKeyword");
               kw.Enabled ^= true;
               kw.Visible ^= true;
            }

            // Otherwise, Select Objects: prompt is re-issued
            // with the same keywords.
         }

         PromptSelectionResult result = null;

         while(true)
         {
            try
            {
               string suffix = pso.Keywords.GetDisplayString(true);
               pso.MessageForAdding = $"\nSelect objects or {suffix}";
               result = editor.GetSelection(pso);
            }
            catch(KeywordException ex)
            {
               editor.WriteMessage($"\nGetSelection() exited, keyword = '{ex.Keyword}'");
               if(ex.Keyword == "Quit")
               {
                  return null; // Can't return PromptSelectionResult
               }
               else if(ex.Keyword == "Change")
               {
                  /// Modify/add/remove keywords here
                  pso.Keywords.Add("NewKeyword");
               }
               continue;
            }
            return result;
         }
      }


      public class KeywordException : System.Exception
      {
         string keyword;
         public KeywordException(string keyword)
         {
            this.keyword = keyword;
         }

         public string Keyword => keyword;
      }

      [CommandMethod("SELECTWITHKEYWORDS")]
      public static void PromptSelectionWithKeywords()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor editor = doc.Editor;
         var psr = editor.GetSelectionWithKeywords("FIrst", "Second", "Third");
         if(psr == null)
            editor.WriteMessage("\nUser chose Quit");
         else
            editor.WriteMessage($"\nresult.Status = {psr.Status}");
      }

   }

   public abstract class PromptSelectionHandler
   {
      Editor editor;
      bool active = false;

      PromptSelectionResult result;
      PromptSelectionOptions options;
      SelectionFilter filter = null;
      string selectedKeyword;

      public PromptSelectionResult Result => result;

      public PromptSelectionOptions Options
      {
         get => options;
         set => options = value ?? new PromptSelectionOptions();
      }

      public PromptStatus Status => result?.Status ?? PromptStatus.None;

      public PromptSelectionHandler(Editor editor, PromptSelectionOptions options = null,
         SelectionFilter filter = null)
      {
         this.editor = editor;
         this.options = options ?? new PromptSelectionOptions();
      }

      public bool GetSelection()
      {
         if(active)
            throw new InvalidOperationException("Input is pending");
         if(Options.Keywords.Count > 0)
            Options.KeywordInput += keywordInput;
         while(true)
         {
            result = null;
            active = true;
            try
            {
               result = editor.GetSelection(this.options, this.filter);
               return result?.Status == PromptStatus.OK;
            }
            catch(KeywordException e)
            {
               selectedKeyword = e.Keyword;
               if(!QueryRetryInput(e.Keyword))
                  return this.result != null;
            }
            finally
            {
               active = false;
            }
         }
      }

      public string SelectedKeyword => selectedKeyword;

      private void keywordInput(object sender, SelectionTextInputEventArgs e)
      {
         selectedKeyword = e.Input;
         if(!OnKeywordInput(e.Input))
            throw new KeywordException(e.Input);
      }

      /// <summary>
      /// Return false to exit current GetSelection() call
      /// </summary>
      /// <param name="keyword"></param>
      /// <returns></returns>
      protected abstract bool OnKeywordInput(string keyword);

      /// <summary>
      /// Return true to retry input or false to exit,
      /// </summary>
      /// <param name="options"></param>
      /// <returns></returns>
      protected abstract bool QueryRetryInput(string keyWord);

      protected class KeywordException : System.Exception
      {
         string keyword;
         public KeywordException(string keyword)
         {
            this.keyword = keyword;
         }

         public string Keyword => keyword;
      }

   }

   enum PromptStatusEx
   {
      Cancel = -5002,
      None = 5000,
      Error = -5001,
      Keyword = -5005,
      OK = 5100,
      Modeless = 5027,
      Other = 5028,
      Exit = 5029 + 2048,  // Exit from loop that iteratively calls GetXxxxx() method.
      Retry = 5030 + 2048 // CanContinue loop that iteratively calls GetXxxxx() method.

   }



   public class KeywordList : IReadOnlyCollection<Keyword>
   {
      // ArrayList list;
      KeywordCollection keywords;

      static FieldInfo arrayListField =
         typeof(KeywordCollection).GetField("m_imp",
            BindingFlags.NonPublic | BindingFlags.Instance);

      public KeywordList(KeywordCollection collection)
      {
         if(collection == null)
            throw new ArgumentNullException(nameof(collection));
         // list = (ArrayList) arrayListField.IsEffectivelyVisible(collection);
         keywords = collection;
      }
      public int Count => keywords.Count;

      public Keyword this[string index]
      {
         get
         {
            if(string.IsNullOrWhiteSpace(index))
               throw new ArgumentException(nameof(index));
            int i = IndexOf(index);
            if(i < 0)
               throw new KeyNotFoundException(index);
            return keywords[i];
         }
      }

      public Keyword this[int index]
      {
         get
         {
            if(index < 0 || index >= keywords.Count)
               throw new IndexOutOfRangeException(index.ToString());
            return keywords[index];
         }
      }

      public void Add(string globalName)
      {
         Add(globalName, globalName, globalName, true, true);
      }

      public void Add(string globalName, 
         string localName, 
         string displayName, 
         bool visible = true, bool enabled = true)
      {
         keywords.Add(globalName, localName, displayName, visible, enabled);
      }

      public int IndexOf(string globalName)
      {
         if(string.IsNullOrWhiteSpace(globalName))
            throw new ArgumentException(nameof(globalName));
         for(int i = 0; i < keywords.Count; i++)
         {
            Keyword item = keywords[i];
            if(item.GlobalName.Equals(globalName, StringComparison.OrdinalIgnoreCase))
               return i;
         }
         return -1;
      }
      public IEnumerator<Keyword> GetEnumerator()
      {
         return keywords.Cast<Keyword>().GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }
   }
   public static class KeywordCollectionExtensions
   {
      /// <summary>
      /// Gets an existing Keyword by its GlobalName
      /// </summary>
      /// <param name="keywords"></param>
      /// <param name="globalName"></param>
      /// <returns></returns>
      public static Keyword GetAt(this KeywordCollection keywords, string globalName)
      {
         int cnt = keywords.Count;
         for(int i = 0; i < cnt; i++)
         {
            var item = keywords[i];
            if(item.GlobalName.Equals(globalName, StringComparison.OrdinalIgnoreCase))
               return item;
         }
         return null;
      }
   }

}
