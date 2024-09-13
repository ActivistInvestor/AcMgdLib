
/// MyEntityVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the EntityVisitor class.

using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.EditorInputExtensions;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.Visitors.Examples
{
   /// <summary>
   /// Implementing the Visitor pattern using
   /// System.Dynamic. 
   /// 
   /// Unlike the traditional visitor pattern, where
   /// there is a 'big switch' construct to dispatch
   /// calls to overridden type-specific methods, the 
   /// EntityVisitor class uses System.Dynamic to
   /// dispatch calls to one of several overloads of a 
   /// single method (the Visit() method). 
   /// 
   /// So, instead of virtual methods like VisitLine(), 
   /// VisitCircle(), VisitPolyline(), and so on, this 
   /// implementation of the pattern uses a single method 
   /// (The Visit() method), which can be overloaded with 
   /// different argument types, where the overload having 
   /// an argument type that most-closely matches the
   /// runtime type of the argument will be called by the
   /// Dynamic Language Runtime, while the alternative way
   /// of doing this using the tradional visitor pattern
   /// involves significant work (depending on the number
   /// of types supported), and more-importantly, can only 
   /// support types that are known at compile-time. 
   /// 
   /// With the dynamic approach, types that are not known 
   /// at compile-time can be supported in derived types 
   /// created after-the-fact, where those types are known. 
   /// 
   /// So for example, if an AutoCAD vertical has types that 
   /// are derived from Entity, a user of the EntityVisitor
   /// can reference the assmemby containing those types, and 
   /// implement a class derived from EntityVisitor that
   /// can include overloaded versions of Visit() taking those 
   /// vertical-specific, Entity-based argument types, and do 
   /// that without the need to alter the EntityVisitor base 
   /// type to add support for the previously-unknown types. 
   /// 
   /// That works, because dynamic overload resolution is
   /// a purely-runtime affair, and is what allows types
   /// that were not known when the base type was built,
   /// to be supported by types derived from it where the
   /// types are known.
   /// 
   /// There is a performance penalty associated with 
   /// the use of Dynamic, but the benfits of dynamic
   /// overload resolution outweigh that performance
   /// penalty.
   /// 
   /// An example specialization of <see cref="EntityVisitor"/>
   /// 
   /// This example displays the type and Handle of every 
   /// Line, Polyline, and other Curve-based entity, along 
   /// with AttributeDefinitions, that are contained in the 
   /// definitions of every block that is inserted into the 
   /// current space. It also displays the stack of owner 
   /// blocks containing the block currently being visited.
   /// 
   /// This example was tested on the following sample
   /// drawing file:
   /// 
   ///   Samples\Database Connectivity\Floor Plan Sample.dwg
   /// 
   /// Because this sample was used to test/debug various
   /// aspects of the EntityVisitor class, it contains 
   /// some 'baggage' related to displaying output for 
   /// testing/debugging purposes, and therefore doesn't
   /// serve as a good example of how the EntityVisitor
   /// class can be leveraged.
   /// 
   /// See the HightlightCirclesVisitor class, along with
   /// the DeepExplodeVisitorCommands class for better and
   /// easier to understand examples that better-illustrate
   /// how EntityVisitor helps simplify what may otherwise
   /// be moderately-complex operations.
   /// </summary>

   public class MyEntityVisitor : EntityVisitor
   {
      int blockrefCount = 0;

      /// <summary>
      /// Example: 
      /// 
      /// Implement handling of Line entities:
      /// 
      /// The argument type determines what 
      /// type(s) of objects are passed to 
      /// the method:
      /// </summary>

      public void Visit(Line line)
      {
         Report(line);
      }

      /// For testing optimization, define a few
      /// additional handlers for types that may
      /// not exist at all, or in great quantity.

      public void Visit(DBText text)
      {
         Report(text);
      }

      /// <summary>
      /// Example: 
      /// 
      /// Implement handling of AttributeDefinitions:
      /// </summary>

      public void Visit(AttributeDefinition attdef)
      {
         Report(attdef);
      }

      /// <summary>
      /// Example:
      /// 
      /// Implement handling of AttributeReferences
      /// </summary>
      /// <param name="attref"></param>
      
      public void Visit(AttributeReference attref)
      { 
         Report(attref); 
      }

      /// <summary>
      /// Handles all instances of Curve that are NOT
      /// handled by other overrloads defined in this
      /// class (e.g., Line, Polyline, and Circle).
      /// </summary>

      public void Visit(Curve curve)
      {
         Report(curve, " (As Curve)");
      }

      /// <summary>
      /// The VisitBlockReference() method of the base type can
      /// be overridden to control if the argument's definition
      /// is visited or not. If overridden and the base method
      /// is not supermessaged, the contents of the block that's
      /// referenced by the argument will not be visited. Hence, 
      /// this override provides a means to achieve more-granular 
      /// control over what blocks are visited.
      /// 
      /// In contextual mode, a BlockReference can be visited 
      /// conditionally, based on its containers.
      /// 
      /// This example only displays the name of the block that
      /// is being visited, and unconditionally allows visiting
      /// all blocks by always supermessaging the base method.
      /// 
      /// If the user's intention is to visit all blocks, then
      /// this method need not be overridden, other than for the 
      /// purpose of establishing or maintaining scope or state, 
      /// displaying messages, etc., as is done in this example. 
      /// </summary>
      
      protected override void VisitBlockReference(BlockReference blkref)
      {
         /// This just displays some information about 
         /// the method call and its argument on the
         /// console:
         
         TraceVisitBlockReference(blkref);
      }

      void TraceVisitBlockReference(BlockReference blkref)
      {
         var name = GetBlockDisplayName(blkref);
         string msg = "";
         if(this.Contextual && Path.Any())
         {
            string owners = $"{string.Join(" > ", Path)}";
            msg = $"blockref {name} ({blkref.Handle.Format()}) (in {owners})";
         }
         else
         {
            msg = $"blockref {name} ({blkref.Handle.Format()})";
         }
         Trace($"Visiting {msg}");
         ++blockrefCount;
         base.VisitBlockReference(blkref);
         Trace($"Visited {msg}");
      }

      protected string GetBlockDisplayName(BlockReference blkref)
      {
         if(blkref.IsDynamicBlock)
            return $"{blkref.Name} ({GetEffectiveName(blkref)})";
         else
            return blkref.Name;
      }

      internal void Report(Entity entity, string fmt = "")
      {
         if(Verbose)
         {
            string msg = "";
            if(fmt.Contains("{0}") && !fmt.Contains("{1"))
               msg = TryFormat(fmt, entity.ToIdString());
            else
               msg = $"{entity.ToIdString()} {fmt}";
            Trace($"Visiting {msg}");
         }
      }

      static string TryFormat(string fmt, params object[] args)
      {
         try
         {
            return string.Format(fmt, args);
         }
         catch(FormatException e)
         {
            return $"{e.Message})";
         }
      }

      internal void Trace(string msg, bool indent = true)
      {
         if(Verbose)
         {
            if(indent)
               AcConsole.Write($"{new string(' ', Path.Count * 3)}{msg}");
            else
               AcConsole.Write($"{msg}");
         }
      }

      public bool Verbose { get; set; }
   }

   public static class MyEntityVisitorCommands
   {
      static bool verbose = true;
      static bool contextual = true;

      [CommandMethod("MYENTITYVISITOR")]
      public static void TestMyEntityVisitor()
      {
         var doc = Application.DocumentManager.MdiActiveDocument;
         var ed = doc.Editor;
         var per = ed.GetEntity<BlockReference>(
            "\nSelect a block reference, or ENTER for all: ");
         if(per.IsFailed(true))
            return;
         ObjectId id = per.ObjectId.IsNull ?
            doc.Database.CurrentSpaceId : per.ObjectId;
         var res = ed.GetBool("\nContextual Visit?", contextual);
         if(res == null)
            return;
         contextual = res.Value;
         res = ed.GetBool("\nVerbose?", verbose);
         if(res == null)
            return;
         verbose = res.Value;
         try
         {
            using(var tr = new DocumentTransaction())
            {
               MyEntityVisitor visitor = new MyEntityVisitor();
               visitor.Contextual = contextual;
               visitor.Verbose = verbose;
               visitor.VisitAttributes = true;
               visitor.Visit(id, tr, true);
               AcConsole.Write($"Visited {visitor.Count} blocks.");
               AcConsole.Write($"Visited {visitor.VisitedCount} entities");
               AcConsole.Write($"Elapsed time (ms): {visitor.Elapsed}");
               tr.Commit();
            }
         }
         catch(System.Exception ex)
         {
            AcConsole.Write(ex.ToString());
         }
      }
   }
}
