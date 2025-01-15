using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

/// An example showing how to use an Overrule to operate
/// on layers added by the REFEDIT command.

namespace MyNamespace
{
   public class NewLayerOverrule : ObjectOverrule
   {
      public NewLayerOverrule()
      {
         AddOverrule(RXObject.GetClass(typeof(LayerTableRecord)), this, true);
      }

      static DocumentCollection docs = Application.DocumentManager;

      /// Disallow reentry into the Close() method, which can 
      /// happen if an API that tries to open the argument is 
      /// called from that method:

      bool reentered = false;

      public override void Close(DBObject obj)
      {
         var doc = docs.MdiActiveDocument;

         if(!reentered && obj.IsWriteEnabled && obj.IsNewObject
            && obj.IsReallyClosing && doc.Database == obj.Database
            && IsLongTransactionActive)
         {
            reentered = true;
            try
            {
               if(obj is LayerTableRecord layer)
               {
                  /// TODO: refedit is active,
                  /// Modify the new layer:
                  
                  layer.IsLocked = true;     // example
                  layer.IsFrozen = true;     // example
               }
            }
            catch(System.Exception ex)
            {
               doc.Editor.WriteMessage(
                  $"Exception in NewLayerOverrule.{nameof(Close)}(): {ex.Message}");
            }
            finally
            {
               reentered = false;
            }
            base.Close(obj);
         }
      }

      /// <summary>
      /// Don't want to act unconditionally,
      /// when a layer is added to the drawing,
      /// rather only when REFEDIT is active. 
      /// 
      /// The LongTransactionManager property is
      /// not documented anywhere that I know of:
      /// </summary>

      static bool IsLongTransactionActive => !Application.LongTransactionManager
         .CurrentLongTransactionFor(docs.MdiActiveDocument).IsNull;

      protected override void Dispose(bool disposing)
      {
         if(disposing)
            RemoveOverrule(RXObject.GetClass(typeof(LayerTableRecord)), this);
         base.Dispose(disposing);
      }

      [CommandMethod("NEWLAYEROVERRULE")]
      public static void NewLayerOverruleCommand()
      {
         if(overrule == null)
            overrule = new NewLayerOverrule();
         else
         {
            overrule.Dispose();
            overrule = null;
         }
      }

      static NewLayerOverrule overrule = null;
   }
}
