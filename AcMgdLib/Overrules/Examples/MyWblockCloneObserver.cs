/// MyWblockCloneObserver.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using System;

namespace AcMgdLib.Common.Examples
{

   /// <summary>
   /// An example concrete implementation of WblockCloneObserver
   /// that demontrates the use of ObjectOverrule methods to get
   /// control when each object is cloned.
   /// 
   /// The class does nothing other than display information
   /// about each call to the WblockClone() override.
   /// </summary>

   public class MyWblockCloneObserver : WblockCloneObserver
   {
      /// <summary>
      /// Enables tracing of Overrule methods
      /// </summary>
      static bool trace = false;

      public static bool OverruleTrace {get => trace; set => trace = value;}

      public MyWblockCloneObserver(Database db) : base(db)
      {
      }

      protected override void OnDeepCloneEnded(IdMapping map, bool aborted = false)
      {
         /// Testing unhandled exceptions: If an
         /// exception is not handled in this method,
         /// there are no further notifications, not
         /// even DeepCloneAborted or WblockAborted
         /// 
         /// Trace("Testing throw from OnDeepCloneEnded");
         /// throw new NotSupportedException();

         // map.CopyGroups();
         base.OnDeepCloneEnded(map, aborted);
      }

      protected override void OnWblockEnded(bool aborted = false)
      {
         base.OnWblockEnded(aborted);
         Trace("Testing throw from OnWblockEnded");
         AcConsole.StackTrace();
         throw new NotSupportedException();
      }


      protected override void OnException(System.Exception ex, string caller = "unknown")
      {
         ReportMsg(ex.ToIdString());
         // throw ex; // This causes the exception to be handled.
      }

      /// <summary>
      /// ObjectOverrule overrides for demonstration purposes.
      /// 
      /// Because WblockCloneObserver is an ObjectOverrule, the
      /// virtual methods of that type can be overridden in any
      /// derived type. 
      /// 
      /// Notes:
      /// 
      /// The base type will detect if any ObjectOverrule virtual 
      /// methods are overridden in any derived type, and if not,
      /// the overrule is not enabled and imposes no overhead.
      /// 
      /// Enabling/disabling overruling. 
      /// 
      /// If a derived type overrides any of the virtual methods 
      /// of the ObjectOverrule class, overruling is automatically
      /// enabled when the beginDeepClone notification is received,
      /// and is automatically disabled when the wblockEnded (or 
      /// wblockAborted) notification is received.
      /// 7
      /// Manually Enabling/Disabling overruling
      /// 
      /// While derived types are free to manually enable or disable
      /// overruling, it is strongly advised that they do not do so.
      /// It is also critically-important that the Enabled property
      /// of this class not be assigned to from within any override
      /// of an ObjectOverrule virtual method (such as the two that
      /// are overridden below). If Enabled is set to false within
      /// either of these two methods, it will most-likely terminate
      /// AutoCAD.
      /// </summary>

      public override DBObject DeepClone(DBObject src, DBObject owner, IdMapping idMap, bool isPrimary)
      {
         var clone = base.DeepClone(src, owner, idMap, isPrimary);
         TraceClone("Deep", src, clone, owner, idMap, isPrimary);
         return clone;
      }

      public override DBObject WblockClone(DBObject src, RXObject owner, IdMapping idMap, bool isPrimary)
      {
         var clone = base.WblockClone(src, owner, idMap, isPrimary);
         if(owner is DBObject ownerObj)
            TraceClone("Wblock", src, clone, ownerObj, idMap, isPrimary);
         return clone;
      }

      void TraceClone(string text, DBObject src, DBObject clone, DBObject owner, IdMapping idMap, bool isPrimary)
      {
         if(trace)
            Write($"***   {text}Clone(): {src.Format()} => {clone.Format()}" +
               $" Owner: {owner.Format()} isPrimary = {isPrimary}");
      }

   }

   public static class TestMyWblockCloneObserver
   {
      /// <summary>
      /// This command enables the MyWblockCloneObserver 
      /// class in all documents.
      /// </summary>

      [CommandMethod("WBC")]
      public static void TestCommand()
      {
         DocData<MyWblockCloneObserver>.Initialize(
            doc => new MyWblockCloneObserver(doc.Database));

         Application.DocumentManager.MdiActiveDocument
            .Editor.WriteMessage($"{nameof(MyWblockCloneObserver)} enabled.");
         WbcTrace();
      }

      [CommandMethod("WBCOFF")]
      public static void WbcOff()
      {
         DocData<MyWblockCloneObserver>.Uninitialize();
         Application.DocumentManager.MdiActiveDocument
            .Editor.WriteMessage($"{nameof(MyWblockCloneObserver)} disabled.");
      }

      [CommandMethod("WBCT")]
      public static void WbcTrace()
      {
         AcConsole.WriteLine($"{nameof(MyWblockCloneObserver)}.Trace = {MyWblockCloneObserver.TraceEnabled ^= true}");
      }

      [CommandMethod("WBCOV")]
      public static void WbcOverruleTrace()
      {
         AcConsole.WriteLine($"{nameof(MyWblockCloneObserver)}.OverruleTrace = {MyWblockCloneObserver.OverruleTrace ^= true}");
      }

   }


}