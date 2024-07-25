/// DeepCloneOverruleExample.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Examples demonstrating the use of DeepCloneOverrule
/// and related/supporting APIs.

using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class AddToBlockExamples
   {
      /// <summary>
      /// This example prompts for a selection of objects 
      /// and a block reference, and adds the selected 
      /// objects to the selected block reference's block 
      /// definition and then erases the selected objects.
      /// 
      /// The objects added to the block's definition have
      /// a constant spatial relationship to the selected
      /// block reference, or to put it another way, the
      /// objects added to the block have the same position,
      /// orientation and scale, relative to the selected
      /// block insertion.
      /// 
      /// The example also shows how to check the selection
      /// for cyclical references and disallow adding objects 
      /// to the block's definition that would cause it to 
      /// become self-referencing.
      /// 
      /// The first example uses mostly 'bare-metal' or built-
      /// in APIs to perform the operation. A second example 
      /// that follows shows the same operation implemented 
      /// with the help of APIs included in this library.
      /// </summary>

      [CommandMethod("ADDTOBLOCK1", CommandFlags.NoBlockEditor | CommandFlags.UsePickSet)]
      public static void ExampleAddToBlock1()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         PromptSelectionOptions pso = new PromptSelectionOptions();
         pso.RejectObjectsOnLockedLayers = true;
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = ed.GetSelection(pso);
         if(psr.Status != PromptStatus.OK)
            return;
         var insert = ed.GetEntity<BlockReference>("\nSelect insertion: ");
         if(insert.IsNull)
            return;
         ObjectId[] idArray = psr.Value.GetObjectIds();
         using(var tr = doc.TransactionManager.StartTransaction())
         {
            /// The block reference that's used to define the
            /// position/orentation/scaling of the objects
            /// that are to be added to the block's definition:
            
            var blkref = (BlockReference)
               tr.GetObject(insert, OpenMode.ForRead);

            /// The block to add the selected objects to:
            
            var btr = (BlockTableRecord)tr.GetObject(
               blkref.DynamicBlockTableRecord, OpenMode.ForRead);

            /// Check for and reject selections containing objects 
            /// that would result in a self-referencing block.
            /// This code uses an API from this library:

            if(btr.IsReferencedByAny(idArray))
            {
               ed.WriteMessage("\nInvalid selection: One or more "
                  + "selected objects reference the selected block.");
               tr.Commit();
               return;
            }
            Database db = btr.Database;
            IdMapping map = new IdMapping();

            /// Clone the selected objects to the BlockTableRecord:
            
            db.DeepCloneObjects(new ObjectIdCollection(idArray), btr.ObjectId, map, false);

            /// Get the ObjectIds of the clones:
            
            var cloneIds = map.Cast<IdPair>()
               .Where(p => p.IsPrimary && p.IsCloned)
               .Select(p => p.Value);

            /// Open the clones for write and transform them 
            /// to the BlockTableRecord's WCS:
            
            var transform = blkref.BlockTransform.Inverse();
            foreach(var id in cloneIds)
            {
               Entity clone = (Entity)tr.GetObject(id, OpenMode.ForWrite);
               clone.TransformBy(transform);
            }
            
            /// Open the originally-selected objects for
            /// write and erase them:
            
            foreach(var id in idArray)
            {
               Entity source = (Entity) tr.GetObject(id, OpenMode.ForWrite);
               source.Erase(true);
            }

            tr.Commit();
            ed.Regen();
            CoreUtils.SetUndoRequiresRegen(db);
            ed.WriteMessage($"\nAdded {idArray.Length} items to block.");
         }
      }


      /// <summary>
      /// Leveraging the CopyTo() extension method and the 
      /// DeepCloneOverrule:
      /// 
      /// The example above uses the more-conventional approach 
      /// to achieve its objective, which includes the following 
      /// discrete steps:
      /// 
      /// 1. DeepCloneObjects() is called to clone the selected
      ///    objects to the BlockTableRecord.
      ///    
      /// 2. The IdMapping is used to access the clones that
      ///    were created in step 1, and each clone is opened
      ///    for write in a transaction, and transformed into
      ///    the WCS of the block definition space.
      ///    
      /// 3. Each selected source object is opened for write in
      ///    a transaction, and its Erase() method is called to
      ///    erase it.
      /// 
      /// Rather than performing those three discrete steps, the
      /// version that follows uses the CopyTo() extension method, 
      /// passing it a delegate that receives each pair of source 
      /// and clone objects, both of which are currently open when
      /// the delegate is called (the source object is open for 
      /// read and the clone is open for write). 
      /// 
      /// The delegate then transforms the clone to the block's 
      /// model coordinate system, and then it erases the source 
      /// object after upgrading its OpenMode to OpenMode.ForWrite. 
      /// 
      /// All of the required operations are performed by a single 
      /// delegate with three lines of code, and without the need 
      /// to iterate over and open the originally-selected objects 
      /// or the resulting clones.
      /// </summary>
      
      [CommandMethod("ADDTOBLOCK2", CommandFlags.NoBlockEditor | CommandFlags.UsePickSet)]
      public static void ExampleAddToBlock2()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         PromptSelectionOptions pso = new PromptSelectionOptions();
         pso.RejectObjectsOnLockedLayers = true;
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = ed.GetSelection(pso);
         if(psr.Status != PromptStatus.OK)
            return;
         var idBlkRef = ed.GetEntity<BlockReference>("\nSelect insertion: ");
         if(idBlkRef.IsNull)
            return;
         using(var tr = new DocumentTransaction())
         {
            var blkref = tr.GetObject<BlockReference>(idBlkRef);

            var btr = tr.GetObject<BlockTableRecord>(
               blkref.DynamicBlockTableRecord);

            /// This applies to any code that adds objects to 
            /// existing blocks: It's critically-important that 
            /// objects that are added to an existing block have 
            /// no dependence on that block, either direct or 
            /// indirect. 
            /// 
            /// Adding an object that has a dependence on the 
            /// block its added to creates a cyclical reference 
            /// that results in a self-referencing block. 
            /// 
            /// The underlying managed and native APIs DO NOT check
            /// dependencies when adding objects to a block within
            /// the deep-clone process, meaning that it allows the
            /// creation of self-referencing blocks and gives no
            /// indication that it happened, until an audit is done.
            /// 
            /// This code checks its arguments to detect if any of 
            /// them have a dependence on the block and if so, will
            /// reject the selection:

            ObjectId[] ids = psr.Value.GetObjectIds();

            if(ids.HasDependenceOn(btr))
            {
               ed.WriteMessage("\nInvalid: One or more selected objects"
                  + " are dependent on the selected block.");
               tr.Commit();
               return;
            }

            /// The above code is nearly identical to the corresponding
            /// code from the first example. What follows is vastly-
            /// different.

            /// Get the transformation matrix required to transform
            /// the selected objects into the block's WCS:

            var transform = blkref.BlockTransform.Inverse();

            /// This method is called by the DeepCloneOverrule for each
            /// object that's cloned. It's passed each source object and
            /// its clone. It erases the source object and transforms the
            /// clone, all in one swell foop.

            void OnCloned(Entity source, Entity clone)
            {
               source.UpgradeOpen();
               source.Erase(true);
               clone.TransformBy(transform);
            }

            /// This extension method serves as a wrapper for the
            /// Database's DeepCloneObjects() method, that also
            /// manages the DeepCloneOverrule that calls the above
            /// OnCloned() method, allowing the programmer to avoid 
            /// having to deal directly with the DeepCloneOverrule 
            /// class. 

            ids.CopyTo<Entity>(btr.ObjectId, OnCloned);

            tr.Commit();
            ed.Regen();
            btr.Database.SetUndoRequiresRegen();
            ed.WriteMessage($"\nAdded {ids.Length} items to block.");
         }
      }
   }

}
