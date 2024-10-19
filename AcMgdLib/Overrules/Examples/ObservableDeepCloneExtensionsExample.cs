/// ObservableDeepCloneExtensionsExample.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Examples showing the use of the CopyObjects() extension
/// methods from ObservableDeepCloneExtension.cs.


using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class ObservableDeepCloneExtensionsExample
   {
      /// <summary>
      /// Example:
      /// 
      /// A rudimentry emulation of the COPY command, 
      /// sans dragging support.
      /// 
      /// With the help of the included extension methods, the
      /// operation of cloning the selection and transforming 
      /// the clones is done in a single line of code.
      /// </summary>

      [CommandMethod("MYCOPY")]
      public static void MyCopy()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Database db = doc.Database;
         Editor ed = doc.Editor;
         var pso = new PromptSelectionOptions();
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = ed.GetSelection(pso);
         if(psr.Status != PromptStatus.OK)
            return;
         var ppo = new PromptPointOptions("\nBasepoint: ");
         var ppr = ed.GetPoint(ppo);
         if(ppr.Status != PromptStatus.OK)
            return;
         ppo.Message = "\nDisplacment: ";
         Point3d from = ppr.Value;
         ppo.BasePoint = from;
         ppo.UseBasePoint = true;
         ppr = ed.GetPoint(ppo);
         if(ppr.Status != PromptStatus.OK)
            return;
         var xform = Matrix3d.Displacement(from.GetVectorTo(ppr.Value));
         var ids = psr.Value.GetObjectIds();
         ids.CopyObjects<Entity>((source, clone) => clone.TransformBy(xform));
      }
   }
}
