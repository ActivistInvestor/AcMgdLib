
/// HighlightCirclesVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the EntityVisitor class.

using Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Runtime;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace AcMgdLib.Visitors.Examples
{
   /// <summary>
   /// EntityVisitor Example
   /// 
   /// This example specialization of EntityVisitor 
   /// will highlight every circle nested within a 
   /// block reference and all nested block references.
   /// 
   /// This class uses the PushHighLight() method of
   /// base type to highlight entities. That method
   /// takes care of the grunt work of composing the
   /// FullSubentityPath argument that's required by 
   /// the Entity's PushHighLight() method.
   /// 
   /// </summary>

   public class HighlightCirclesVisitor : EntityVisitor<Circle>
   {
      protected override void Initialize(ObjectId id)
      {
         count = 0;
         base.Initialize(id);
      }

      public void Visit(Circle circle)
      {
         PushHighlight(circle);
         ++count;
      }

      int count = 0;

      public int HighlightedCount => count;
   }

   public static class HighlightCirclesVisitorCommands
   {
      [CommandMethod("HIGHLIGHTCIRCLES")]
      public static void HighlightCirclesCommand()
      {
         using(var tr = new DocumentTransaction())
         {
            tr.IsReadOnly = true;
            var per = tr.Editor.GetEntity<BlockReference>(
               "\nSelect a block reference, or ENTER for all: ");
            if(per.IsFailed(true))
               return;
            var visitor = new HighlightCirclesVisitor();
            var id = per.ObjectId.IsNull ? tr.CurrentSpaceId : per.ObjectId;
            visitor.Visit(id, tr, true);
            AcConsole.Write($"Highlighted {visitor.HighlightedCount} Circle(s)");
            tr.Commit();
         }

      }
   }
}
