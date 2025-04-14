using System.Collections.Generic;
using System.Linq;
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

/// LongTransactionExtensions.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

public static class LongTransactionExtensions
{
   static DocumentCollection docs = Application.DocumentManager;

   static Document GetDocument(Database db, bool throwIfNotFound)
   {
      if(db is null)
         throw new ArgumentNullException(nameof(db));
      var result = docs.Cast<Document>().FirstOrDefault(doc => doc.Database == db);
      if(result is null && throwIfNotFound)
         throw new ArgumentException("No document");
      return result;
   }

   /// <summary>
   /// Takes an ObjectId and returns a value indicating if
   /// the object represented by the id is part of the current
   /// workset, or false if there is no current workset.
   /// </summary>
   /// <param name="doc"></param>
   /// <param name="id"></param>
   /// <returns></returns>

   public static bool WorkSetHas(this Document doc, ObjectId id)
   {
      if(doc != null)
      {
         if(id.Database != doc.Database)
            return false;
         ObjectId ltrid = Application.LongTransactionManager.CurrentLongTransactionFor(doc);
         if(!ltrid.IsNull)
         {
            using(var tr = new OpenCloseTransaction())
            {
               try
               {
                  var longtrans = (LongTransaction)tr.GetObject(ltrid, OpenMode.ForRead);
                  return longtrans.WorkSetHas(id, true);
               }
               finally
               {
                  tr.Commit();
               }
            }
         }
      }
      return false;
   }

   /// <summary>
   /// Takes a sequence of ObjectIds, and filters out those 
   /// that are not in the current workset. If there is no 
   /// current workset, this returns an empty sequence.
   /// </summary>

   public static IEnumerable<ObjectId> InWorkSet(this IEnumerable<ObjectId> ids)
   {
      if(ids is null)
         throw new ArgumentNullException(nameof(ids));
      if(!ids.Any())
         return ids;
      Database db = ids.First().Database;
      Document doc = GetDocument(db, true);
      ObjectId ltrid = Application.LongTransactionManager.CurrentLongTransactionFor(doc);
      if(!ltrid.IsNull)
      {
         using(var tr = new OpenCloseTransaction())
         {
            try
            {
               var longtrans = (LongTransaction)tr.GetObject(ltrid, OpenMode.ForRead);
               return ids.Where(id => longtrans.WorkSetHas(id, true)).ToArray();
            }
            finally
            {
               tr.Commit();
            }
         }
      }
      else
      {
         return Enumerable.Empty<ObjectId>();
      }
   }

   /// <summary>
   /// Works like the Editor's GetSelection() method, except that
   /// if there is a current workset, the selection is constrained
   /// to objects in the workset. If there's no current workset, 
   /// the behavior is identical to the Editor's GetSelection()
   /// method.
   /// </summary>
   /// <param name="ed"></param>
   /// <param name="pso"></param>
   /// <returns></returns>

   public static PromptSelectionResult GetWorkSetSelection(this Editor ed,
      PromptSelectionOptions pso = null,
      SelectionFilter filter = null)
   {
      pso = pso ?? new PromptSelectionOptions();
      var ltrId = Application.LongTransactionManager.CurrentLongTransactionFor(ed.Document);
      bool hasWorkset = !ltrId.IsNull;
      Transaction tr = null;
      LongTransaction ltr = null;
      if(hasWorkset)
      {
         tr = new OpenCloseTransaction();
         ltr = (LongTransaction)tr.GetObject(ltrId, OpenMode.ForRead);
         ed.SelectionAdded += selectionAdded;
      }
      try
      {
         return ed.GetSelection(pso, filter);
      }
      finally
      {
         if(hasWorkset)
         {
            ed.SelectionAdded -= selectionAdded;
            tr.Commit();
         }
      }

      void selectionAdded(object sender, SelectionAddedEventArgs e)
      {
         int removed = 0;
         var ids = e.AddedObjects.GetObjectIds();
         for(int i = 0; i < ids.Length; i++)
         {
            if(!ltr.WorkSetHas(ids[i], true))
            {
               e.Remove(i);
               ++removed;
            }
         }
         if(removed > 0)
         {
            ed.WriteMessage($"\n{removed} object(s) not in workset\n");
         }
      }
   }
}

public static class LongTransactionExtensionsExamples
{
   /// <summary>
   /// Document.WorkSetHas() usage example:
   /// </summary>
   [CommandMethod("WORKSETHAS")]
   public static void IsObjectInWorkSetExample()
   {
      Document doc = Application.DocumentManager.MdiActiveDocument;
      var per = doc.Editor.GetEntity("\nSelect object: ");
      if(per.Status != PromptStatus.OK)
         return;
      bool res = doc.WorkSetHas(per.ObjectId);
      string what = res ? "" : "not ";
      doc.Editor.WriteMessage($"Selected object is {what}in workset.");
   }

   /// <summary>
   /// SelectFromWorkSet() usage example:
   /// </summary>

   [CommandMethod("SELECTFROMWORKSET", CommandFlags.Redraw)]
   public static void GetWorkSetSelectionExample()
   {
      Document doc = Application.DocumentManager.MdiActiveDocument;
      Editor ed = doc.Editor;
      var psr = ed.GetWorkSetSelection();
      if(psr.Status == PromptStatus.OK)
         ed.SetImpliedSelection(psr.Value.GetObjectIds());
   }
}


