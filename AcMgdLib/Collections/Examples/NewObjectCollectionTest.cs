/// NewObjectCollectionTests.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Ribbon;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Diagnostics.Extensions;
using System.Linq;

namespace AcMgdLib.Overrules.Examples
{
   /// <summary>
   /// This class enables an ObservableNewObjectCollection that
   /// targets the Line class. It tests that class's handling of
   /// newly-created objects that are subsequently erased within
   /// the operation that created them (or within any operation
   /// that occurs within the scope of the instance).
   /// 
   /// The handler of the CollectionChanged event enumerates all
   /// <em>non-erased</em>, newly-created objects and displays the
   /// ObjectId of each, along with the Count and NonErasedCount
   /// properties of the instance.
   /// 
   /// The steps required to test handling of erased, new objects
   /// is outlined below.
   /// </summary>

   public static class NewObjectCollectionTests
   {
      static DocumentCollection docs = Application.DocumentManager;

      static ObservableNewObjectCollection<Line> newLineObserver = null;

      /// <summary>
      /// Tests/demo's use of the ObservableNewObjectCollection 
      /// and its handling of newly-created objects that are
      /// subsequently erased within the scope of the instance.
      /// </summary>

      [CommandMethod("TESTNEWLINESOBSERVER")]
      public static void TestObservableNewObjectCollection()
      {
         string what = "enabled";
         var name = typeof(ObservableNewObjectCollection<Line>).CSharpName();
         if(newLineObserver == null)
         {
            newLineObserver = new ObservableNewObjectCollection<Line>();

            /// Add a handler to the CollectionChanged event,
            /// which will be raised when one or more Lines 
            /// have been added to the current space:

            newLineObserver.CollectionChanged += OnLinesAdded;

            /// This property specifies if the collection should 
            /// be cleared after each CollectionChanged notification.
            /// If this property is false, items remain in the
            /// collection until the Clear() method is called.

            newLineObserver.ClearOnNotify = true;

            /// Specify that notifications should be deferred 
            /// until the application is in a quiescent state.
            /// If this property is set to false, there will
            /// be a notification on the next Idle event that
            /// follows the addition of one or more objects.
            /// The idle event can be raised while commands are 
            /// active, if they prompt for input on the command 
            /// line. 
            /// 
            /// So, to defer notification until the command or 
            /// operation has ended, set this to true, and the
            /// notification is not sent until the Idle event
            /// fires, AND the drawing editor is quiescent.

            newLineObserver.NotifyOnQuiscent = true;
         }
         else
         {
            what = "disabled";
            newLineObserver.Dispose();
            newLineObserver = null;
         }
         AcConsole.Write($"{name} {what}.");
      }

      /// <summary>
      /// 
      /// Testing ObservableNewObjectCollection:
      /// 
      /// 1. Issue the TESTNEWOC command.
      /// 
      /// 2. Issue the LINE command.
      /// 
      /// 3. Draw exactly 6 line segments.
      /// 
      /// 4. At the "Specify next point or [Close/Undo]:" prompt,
      ///    Issue "Undo" twice, which reduces the number of line
      ///    segments to 4.
      ///    
      /// 5. Draw 3 additional line segments, bringing the total
      ///    number of visible line segments to 7.
      ///    
      /// 6. Press Enter to end the LINE command.
      /// 
      /// At this point, if you entered the exact number of
      /// line segments and issued Undo the specified number
      /// of times, you should see:
      /// 
      ///   CollectionChanged: AddedCount: 7  Count: 7  CountIncludingErased: 9
      ///   
      /// After that you should see each of the Line objects
      /// displayed along with the values of their IsErased 
      /// property.
      /// 
      /// The Undo subcommand issued during the LINE command
      /// resulted in erasing two line segments that were
      /// already added to the database. The objective of this
      /// test is to verify that those erased objects are not
      /// returned by any method that accesses elements of the 
      /// NewObjectCollection when its IncludingErased property
      /// is set to false.
      /// 
      /// Going further, try drawing a polyline containing any
      /// number of both straight and arc segments. Then, issue 
      /// the EXPLODE command to explode the polyline into lines 
      /// and arcs. The CollectionChanged event handler should be
      /// called and should report the Lines created by exploding 
      /// the polyline. The same operation can be performed on a
      /// block insertion, and should report the number of lines
      /// created by exploding it.
      /// 
      /// The test outlined above serves to verify that the 
      /// ObservableNewObjectCollection should account for all
      /// newly-created objects that are subsequently-erased 
      /// within the scope of the instance, and not enumerate
      /// or count them.
      /// 
      /// If you actually want erased, newly-created objects 
      /// included, you can set the IncludingErased property
      /// to true, but doing so is strongly ill-advised as it
      /// can create a lot of downstream confusion.
      /// 
      /// The handler of the CollectionChanged event:
      /// </summary>

      private static void OnLinesAdded(object sender, CollectionChangedEventArgs<Line> e)
      {
         AcConsole.Write($"\nCollectionChanged: AddedCount: {e.AddedCount}  "
            + $"Count: {e.Count}  CountIncludingErased: {e.Sender.CountIncludingErased}");

         /// Test the behavior of Enumerable.ToArray() on an
         /// instance with erased elements, to ensure that it
         /// produces the correct result. 
         /// 
         /// Because ToArray() uses the IEnumerator to get the 
         /// elements (rather than taking an optimized path that 
         /// it would if NewObjectCollection<T> implmented the 
         /// ICollection<T> interface), ToArray() should get the 
         /// same elements enumerated by the instance, which will
         /// depend on the value of the IncludingErased property.
         
         /// Verify that ToArray() is getting the expected values:

         var array = e.Sender.ToArray();
         AcConsole.Write($"Sender.ToArray().Length = {array.Length}");
         int cnt = e.Count;
         if(array.Length != cnt)
            AcConsole.Write($"ToArray().Length: {array.Length}, expected: {cnt}");

         using(var trans = new OpenCloseTransaction())
         {
            int i = 0;
            foreach(Line line in e.GetNewObjects(trans))
            {
               AcConsole.Write($"Line[{i++}] ({line.ObjectId.ToHexString()}) IsErased = {line.IsErased}");
            }
            trans.Commit();
         }
      }

      /// <summary>
      /// An second example that uses ObservableNewObjectCollection 
      /// to collect the ObjectIds of new layers added to the active 
      /// document.
      /// 
      /// Note that in this example, the Owner is the LayerTable,
      /// and the generic argument type is LayerTableRecord.
      /// </summary>

      static ObservableNewObjectCollection<LayerTableRecord> newLayerObserver = null;

      [CommandMethod("TESTNEWLAYEROC")]
      public static void TestNewLayerCollection()
      {
         string what = "enabled";
         string name = typeof(ObservableNewObjectCollection<LayerTableRecord>).CSharpName();

         if(newLayerObserver == null)
         {
            var ownerId = HostApplicationServices.WorkingDatabase.LayerTableId;
            newLayerObserver = new ObservableNewObjectCollection<LayerTableRecord>(ownerId);
            newLayerObserver.ClearOnNotify = true;
            newLayerObserver.NotifyOnQuiscent = true;
            newLayerObserver.CollectionChanged += OnLayersAdded;
         }
         else
         {
            what = "disabled";
            newLayerObserver.Dispose();
            newLayerObserver = null;
         }
         AcConsole.Write($"{name} {what}.");
      }

      /// <summary>
      /// Handler for the newLayerObserver's CollectionChanged event.
      /// 
      /// Note that an OpenCloseTransaction is required here, because 
      /// this event can fire within the context of a running command.
      /// </summary>

      static void OnLayersAdded(object sender, CollectionChangedEventArgs<LayerTableRecord> e)
      {
         using(var tr = new OpenCloseTransaction())
         {
            string names = string.Join(", ", e.GetNewObjects(tr).Select(layer => layer.Name));
            AcConsole.Write($"\nAdded {e.AddedCount} layer(s): {names}");
            tr.Commit();
         }
      }

      /// <summary>
      /// This example exercises transient use of NewObjectCollection 
      /// by using it to collect and select new objects created by the 
      /// EXPLODE command.
      /// </summary>

      [CommandMethod("EXPLODEANDSELECT")]
      public static void ExplodeAndSelect()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var per = ed.GetEntity("\nSelect object to explode: ");
         if(per.IsFailed())
            return;
         using(var newIds = new NewObjectCollection<Entity>())
         {
            ed.Command("._EXPLODE", per.ObjectId, "");
            if(newIds.NonErasedCount > 0)
               ed.SetImpliedSelection(newIds.ToArray());
         }
      }

      /// <summary>
      /// An ObservableNewObjectCollection that collects the ObjectIds
      /// of insertions of blocks having names that start with "DESK".
      /// 
      /// The example uses a DynamicBlockFilter to do the filtering and
      /// constrain the collection to only ids of insertions of blocks 
      /// having names starting with "DESK".
      /// 
      /// Testing this command can be simplified using this
      /// stock sample drawing file:
      /// 
      ///    Sample\Database Connectivity\Floor Plan Sample.dwg
      ///    
      /// The above file contains blocks having names matching
      /// the pattern used by the ObservableNewObjectCollection.
      /// 
      /// First enable the NewObjectCollection functionality using 
      /// the TESTNEWDESKOBSERVER command defined below, and then
      /// try inserting blocks matching the pattern (e.g., names 
      /// starting with "DESK"), and by copying existing objects 
      /// that include insertions of the target blocks. 
      /// 
      /// The filtering becomes obvious when copying existing
      /// objects. The instance will report the number of block
      /// references having names matching the pattern, and will
      /// make them the pickfirst selection set.
      /// </summary>

      static ObservableNewObjectCollection<BlockReference> newDesks;

      [CommandMethod("TESTNEWDESKOBSERVER")]
      public static void TestNewDeskOC()
      {
         string what = newDesks == null ? "enabled" : "disabled";
         string name = typeof(ObservableNewObjectCollection<BlockReference>).CSharpName();
         if(newDesks == null)
         {
            try
            {
               newDesks = new ObservableNewObjectCollection<BlockReference>(
                  new DynamicBlockFilter(btr => btr.Name.Matches("DESK*")));

               newDesks.CollectionChanged += OnDesksAdded;
            }
            catch(System.Exception ex)
            {
               AcConsole.Write(ex.ToString()); ;
            }
         }
         else
         {
            newDesks.Dispose();
            newDesks = null;
         }
         AcConsole.Write($"{name} {what}.");

         /// <summary>
         /// The CollectionAdded event handler for the newDesks
         /// ObservableNewObjectCollection. Displays the number
         /// of new objects matching the filter criteria, and 
         /// assigns the new objects to the pickfirst selection.
         /// </summary>
         /// <param name="sender"></param>
         /// <param name="e"></param>

         static void OnDesksAdded(object sender, CollectionChangedEventArgs<BlockReference> e)
         {
            AcConsole.Write($"\nCollectionChanged: AddedCount: {e.AddedCount}  "
               + $"Count: {e.Count}  CountIncludingErased: {e.Sender.CountIncludingErased}");
            AcConsole.Write($"Selected {e.AddedCount} new insertions of DESK* blocks.");
            if(e.AddedCount > 0)
               docs.MdiActiveDocument.Editor.SetImpliedSelection(
                  e.NewObjectIds.ToArray());
         }

      }

      /// <summary>
      /// 
      /// </summary>

      static ObservableNewObjectCollection<Curve> newCurveObserver;

      [CommandMethod("TESTNEWCURVEOBSERVER")]
      public static void TestNewCurveObserver()
      {
         string what = "enabled";
         string name = typeof(ObservableNewObjectCollection<Curve>).CSharpName();
         if(newCurveObserver == null)
         {
            newCurveObserver = 
               new ObservableNewObjectCollection<Curve>(typeof(BlockTableRecord));
            newCurveObserver.CollectionChanged += OnCurveAdded;
            newCurveObserver.ClearOnNotify = true;
            newCurveObserver.NotifyOnQuiscent = true;
         }
         else
         {
            what = "disabled";
            newCurveObserver.Dispose();
            newCurveObserver = null;
         }
         AcConsole.Write($"{name} {what}.");

         static void OnCurveAdded(object sender, CollectionChangedEventArgs<Curve> e)
         {
            try
            {
               AcConsole.Write($"\nCollectionChanged: AddedCount: {e.AddedCount}  "
                  + $"Count: {e.Count}  CountIncludingErased: {e.Sender.CountIncludingErased}");
               AcConsole.Write($"Selected {e.AddedCount} new Curve objects.");
               if(e.AddedCount > 0)
               {
                  ObjectId id = e.NewObjectIds.First();
                  string idstr = id.GetOwnerValue<DBObject, string>(obj => obj.ToIdString());
                  AcConsole.Write($"Owner: {idstr}");
               }
            }
            catch(System.Exception ex)
            {
               AcConsole.Write(ex.ToString());
            }
         }
      }
   }

}




