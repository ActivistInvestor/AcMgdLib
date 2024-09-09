/// DeepCloneOverruleExample.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Examples demonstrating the use of DeepCloneOverrule
/// and related/supporting APIs.

using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.Overrules.Examples
{
   public static class DeepCloneOverruleExampleCommands
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
      /// that follows it shows the same operation implemented 
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
         var per = ed.GetEntity<BlockReference>("\nSelect insertion: ");
         if(per.IsFailed())
            return;
         ObjectId[] idArray = psr.Value.GetObjectIds();
         using(var tr = doc.TransactionManager.StartTransaction())
         {
            /// The block reference that's used to define the
            /// position/orentation/scaling of the objects
            /// that are to be added to the block's definition:
            
            var blkref = (BlockReference)
               tr.GetObject(per.ObjectId, OpenMode.ForRead);

            /// The block to add the selected objects to:
            
            var btr = (BlockTableRecord)tr.GetObject(
               blkref.DynamicBlockTableRecord, OpenMode.ForRead);

            /// Avoiding operations that can cause a block to
            /// become self-referencing:
            /// 
            /// This note applies to any code that adds objects 
            /// to existing block definitions.
            /// 
            /// Adding objects to an existing block definition is
            /// an inherently-dangerous process that can silently
            /// corrupt drawing files.
            /// 
            /// It's critically-important that objects added to
            /// an existing block have no dependence on that block, 
            /// either direct or indirect. Adding objects that have
            /// a dependence on the block they're added to creates a
            /// cyclical reference, resulting in a self-referencing 
            /// block. 
            /// 
            /// The related, underlying managed and native APIs DO NOT 
            /// check for cyclical references when appending entities 
            /// to a block within the deep-clone process, meaning that 
            /// those APIs will allow the creation of self-referencing 
            /// blocks and offer no indication that it happened, which
            /// often results in the corruption not being discovered 
            /// until an audit is done or after a file is saved and then
            /// subsequently opened, at which point errors are reported.
            /// 
            /// This code will check for cyclical references in the 
            /// selected objects to be added to the block definition,
            /// and if any are found, will reject the selection:

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
            ed.WriteMessage($"\nAdded {idArray.Length} objects to block.");
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
      /// 3. Each selected Source object is opened for write in
      ///    a transaction, and its Erase() method is called to
      ///    erase it.
      /// 
      /// Rather than performing those three discrete steps, the
      /// version that follows uses the CopyTo() extension method, 
      /// passing it a delegate that receives each pair of Source 
      /// and clone objects, both of which are currently open when
      /// the delegate is called (the Source object is open for 
      /// read and the clone is open for write). 
      /// 
      /// The delegate then transforms the clone to the block's 
      /// model coordinate system, and then it erases the Source 
      /// object after upgrading its OpenMode to OpenMode.ForWrite. 
      /// 
      /// All of the required operations outlined above are done by 
      /// a single delegate with three lines of code, and without 
      /// the need to iterate over and open the originally-selected 
      /// objects, or the resulting clones.
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
         if(psr.IsFailed())
            return;
         var per = ed.GetEntity<BlockReference>("\nSelect block insertion: ");
         if(per.IsFailed())
            return;
         try
         {
            using(var tr = new DocumentTransaction())
            {
               var blkref = tr.GetObject<BlockReference>(per.ObjectId);
               ObjectId newOwnerId = blkref.DynamicBlockTableRecord;
               var newOwner = tr.GetObject<BlockTableRecord>(newOwnerId);
               ObjectId[] ids = psr.Value.GetObjectIds();

               /// Check for and reject selections that have
               /// a dependence on the selected block:

               if(newOwner.IsReferencedByAny(ids))
               {
                  ed.WriteMessage("\nInvalid: One or more selected objects"
                     + " are dependent on the selected block.");
                  tr.Commit();
                  return;
               }

               /// The code up to this point is nearly identical to the 
               /// corresponding code in the first example. What follows 
               /// is vastly-different.

               /// Get the transformation matrix required to transform
               /// the selected objects into the block's WCS:

               var transform = blkref.BlockTransform.Inverse();

               /// This function is called by the DeepCloneOverrule once 
               /// for each primary object that's cloned. It's passed 
               /// the source object and its clone. It erases the source 
               /// object and transforms the clone, all in one swell foop.

               void OnCloned(Entity source, Entity clone)
               {
                  source.UpgradeOpen();
                  source.Erase(true);
                  clone.TransformBy(transform);
               }

               /// The CopyTo() extension method acts as a wrapper for 
               /// the Database's DeepCloneObjects() method, that also
               /// manages the DeepCloneOverrule that will call the above
               /// OnCloned() function.
               /// 
               /// Although the CopyTo() method returns the IdMapping 
               /// representing the result of the deep clone operation, 
               /// it isn't needed in this case, because the OnCloned() 
               /// function above does all the work:

               ids.CopyTo<Entity>(newOwnerId, OnCloned);

               string name = newOwner.Name;
               Database db = newOwner.Database;
               tr.Commit();
               db.SetUndoRequiresRegen();
               ed.Regen();
               ed.WriteMessage($"\n{ids.Length} object(s) added to block {name}.");
            }
         }
         catch(System.Exception ex)
         {
            AcConsole.Write(ex.ToString());
         }
      }

      /// <summary>
      /// A no-frills emulation of the AutoCAD COPY command that
      /// uses the Copy() extension method to copy a selection of
      /// objects, translated by a specified displacement. 
      /// 
      /// The overload of the Copy() method used in this example 
      /// takes a Matrix3d argument, and transforms the clones by 
      /// the given matrix.
      /// 
      /// Note that in spite of the fact that the clones/copies 
      /// are translated, there is no use of a transaction at all 
      /// in this method, as all of the work is done by the Copy() 
      /// extension method.
      /// </summary>
      
      [CommandMethod("MYCOPY", CommandFlags.UsePickSet)]
      public static void MyCopyCommand()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var pso = new PromptSelectionOptions();
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = ed.GetSelection(pso);
         if(psr.IsFailed())
            return;
         var ppo = new PromptPointOptions("\nBase point: ");
         var ppr = ed.GetPoint(ppo);
         if(ppr.IsFailed())
            return;
         Point3d basePt = ppr.Value;
         ppo.BasePoint = basePt;
         ppo.UseBasePoint = true;
         ppo.Message = "\nDisplacement: ";
         ppr = ed.GetPoint(ppo);
         if(ppr.IsFailed())
            return;
         var ids = psr.Value.GetObjectIds();
         ids.Copy(Matrix3d.Displacement(basePt.GetVectorTo(ppr.Value)));
      }

      /// <summary>
      /// A no-frills emulation of one form of the WBLOCK command
      /// that uses the CopyTo() method to copy selected objects
      /// to the model space of a new DWG file, translated by a 
      /// specified displacement, and then saves the file to disk.
      /// 
      /// Note again, how each clone is transformed by the delegate
      /// passed to CopyTo(), avoiding the need to iteratively open 
      /// each of them after the fact, and eliminating the need for
      /// a Transaction:
      /// </summary>

      static string myDocuments = Environment.GetFolderPath(
         Environment.SpecialFolder.MyDocuments);
      static readonly string path = Path.Combine(myDocuments, "MYWBLOCK.dwg");

      [CommandMethod("MYWBLOCK", CommandFlags.UsePickSet)]
      public static void MyWblockCommand()
      {
         Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
         var pso = new PromptSelectionOptions();
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = ed.GetSelection(pso);
         if(psr.IsFailed())
            return;
         var ppr = ed.GetPoint("\nInsertion base point: ");
         if(ppr.IsFailed())
            return;
         Vector3d translate = ppr.Value.GetAsVector().Negate();
         Matrix3d transform = Matrix3d.Displacement(translate);
         var ids = psr.Value.GetObjectIds();
         using(var db = new Database(true, true))
         {
            var newOwnerId = db.GetModelSpaceBlockId();
            ids.CopyTo(newOwnerId, (source, clone) => clone.TransformBy(transform));
            db.SaveAs(path, true, DwgVersion.Current, null);
            ed.WriteMessage($"\n{psr.Value.Count} objects written to {path}");
         }
      }


   }

}
