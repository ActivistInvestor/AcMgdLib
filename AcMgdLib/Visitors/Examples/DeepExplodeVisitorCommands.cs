﻿
/// DeepExplodeVisitorCommands.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Examples showing the use of the EntityVisitor 
/// and DeepExplodeVisitor classes

using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.Visitors.Examples
{
   public static class DeepExplodeVisitorCommands
   {
      /// <summary>
      /// A command that recursively explodes all Curve
      /// objects nested within a selected block insertion,
      /// or all block insertions in the current space.
      /// 
      /// If the block insertion's definition contains one
      /// or more insertions of other blocks, the operation
      /// will explode all curves from the nested references
      /// as well, to any depth.
      /// 
      /// This code does not modify any existing objects. It
      /// simply copies existing entities and transforms them
      /// to the current space in the drawing editor.
      /// 
      /// While the term 'EXPLODE' is used throught this code,
      /// what the code actually does is what AutoCAD's NCOPY
      /// command does, except that this code operates on all
      /// or a range of nested entities of a given type.
      /// 
      /// In real-world applications, one might use the 
      /// DeepExplodeVisitor class to conditionally copy 
      /// nested objects based on a broad range of criteria
      /// such as what container block(s) they're nested in;
      /// they depth they are nested at; or their geometric 
      /// traits and/or spatial relationship with one or more 
      /// other parameters.
      /// </summary>

      [CommandMethod("DEEPEXPLODECURVES", CommandFlags.Redraw)]
      public static void DeepExplodeCurvesCommand()
      {
         DeepExplode<Curve>("curve(s)");
      }

      /// <summary>
      /// A version of the above command that operates
      /// on all entities rather than just Curves:
      /// </summary>

      [CommandMethod("DEEPEXPLODE", CommandFlags.Redraw)]
      public static void DeepExplodeCommand()
      {
         DeepExplode<Entity>();
      }

      /// <summary>
      /// Another variant of the above command that 
      /// operates on Line entities:
      /// </summary>
      
      [CommandMethod("DEEPEXPLODELINES", CommandFlags.Redraw)]
      public static void DeepExplodeLinesCommand()
      {
         DeepExplode<Line>("line(s)");
      }

      /// <summary>
      /// Does the actual work for all of the above commands:
      /// </summary>

      static void DeepExplode<T>(string what = "object(s)") where T : Entity
      {
         using(var tr = new DocumentTransaction())
         {
            var per = tr.Editor.GetEntity<BlockReference>(
               "\nSelect a block reference, or ENTER for all: ");
            if(per.IsFailed() && !per.IsNone())
               return;
            var visitor = new DeepExplodeVisitor<T>();
            var id = per.ObjectId.IsNull ? tr.CurrentSpaceId : per.ObjectId;
            visitor.Visit(id, tr, true);
            int count = visitor.Result.Count;
            AcConsole.Write($"\nExploded {count} {what}");
            tr.Commit();
            if(count > 0)
               tr.Editor.SetImpliedSelection(visitor.Result.ToArray());
         }
      }


   }

}