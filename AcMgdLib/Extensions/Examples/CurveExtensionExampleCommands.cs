
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.EditorExtensions;
using Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;

/// CurveExtensionExampleCommands.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Curve class.

namespace AcMgdLib.Extensions.Examples
{
   public static class CurveExtensionExampleCommands
   {

      [CommandMethod("TESTGETFRAGMENTS")]
      public static void GetFragmentsTest()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var per = ed.GetEntity<Curve>("\nSelect a curve: ");
         if(per.IsFailed())
            return;
         var points = ed.GetPoints(true);
         if(points == null)
            return;
         using(var tr = new DocumentTransaction())
         {
            Curve curve = tr.GetObject<Curve>(per.ObjectId);
            using(var fragments = curve.GetFragmentsAt(points, true, true))
            {
               if(fragments.Count > 0)
               {
                  ed.SetImpliedSelection(tr.Append(fragments).ToArray());
               }
            }
            tr.Commit();
         }
      }

   }
}
