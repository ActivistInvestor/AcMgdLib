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
   /// ObjectId of each, along with the Count and NonEasedCount
   /// properties of the instance.
   /// 
   /// The steps required to test handling of erased, new objects
   /// is outlined below.
   /// </summary>

   public static class NewObjectCollectionTest
   {
      static ObservableNewObjectCollection<Line> newLineObserver = null;

      /// <summary>
      /// Tests/demo's use of the ObservableNewObjectCollection 
      /// and its handling of newly-created objects that are
      /// subsequently erased within the scope of the instance.
      /// </summary>

      [CommandMethod("TESTNEWOC")]
      public static void TestObservableNewObjectCollection()
      {
         string what = "enabled";
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
            /// be a notification on each Idle event, which can
            /// be raised while commands are active, when they
            /// prompt for input on the command line. 
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
         AcConsole.Write($"NewLineObserver {what}.");
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
      ///   CollectionChanged: AddedCount: 7 Count: 9 NonErasedCount = 7
      ///   
      /// After that you should see each of the 7 Line objects
      /// displayed along with the value of their IsErased property.
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
            + $"Count: {e.Count}  NonErasedCount: {e.NonErasedCount}");

         using(var trans = new DocumentTransaction())
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
         AcConsole.Write($"NewLayerObserver {what}.");
      }

      /// <summary>
      /// Handler for the CollectionChanged event:
      /// </summary>

      static void OnLayersAdded(object sender, CollectionChangedEventArgs<LayerTableRecord> e)
      {
         using(var tr = new DocumentTransaction())
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
      public static void TestNewObjectCollection()
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
   }
}




