/// WblockCloneHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using System;
using System.Diagnostics;
using System.Diagnostics.Extensions;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;

/// This code requires C# 10.0

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   /// <summary>
   /// WblockCloneHandler class:
   /// 
   /// Does all the grunt work required to allow 
   /// intervention in a wblock clone operation.
   /// 
   /// Usage requires one to derive a type from this 
   /// class, that overrides the OnDeepCloneEnded()
   /// virtual method, and within that override, do 
   /// whatever work is needed.
   /// 
   /// See the WblockGroupHandler class for an example
   /// showing the use of this type.
   /// </summary>

   public abstract class WblockCloneHandler
   {
      Database sourceDb;
      Database destDb = null;
      IdMapping idMap = null;

      public WblockCloneHandler(Database db) 
      { 
         this.sourceDb = db;
         db.WblockNotice += wblockNotice;
      }

      /// <summary>
      /// Caveat emptor: The IdMapping is not usable
      /// from a handler of the BeginSave event, and 
      /// will terminate AutoCAD if accessed from that 
      /// context. The IdMapping must not be accessed 
      /// after the DeepCloneEnded event was raised.
      /// </summary>

      protected IdMapping IdMap
      {
         get
         {
            if(idMap == null || idMap.IsDisposed)
               throw new InvalidOperationException("null or invalid IdMapping");
            return idMap;
         }
      }

      protected Database Source
      {
         get
         {
            if(sourceDb == null || sourceDb.IsDisposed)
               throw new InvalidOperationException("source database is null or disposed");
            return destDb;
         }
      }

      protected Database Destination
      {
         get
         {
            if(destDb == null || destDb.IsDisposed)
               throw new InvalidOperationException("destination database is null or disposed");
            return destDb;
         }
      }

      protected virtual void Reset()
      {
         this.idMap = null;
         this.destDb = null;
         sourceDb.WblockNotice += wblockNotice;
      }

      void wblockNotice(object sender, WblockNoticeEventArgs e)
      {
         Report();
         Database.DatabaseConstructed += databaseConstructed;
         // Added again in DeepCloneEnded/Aborted, to avoid reentry:
         sourceDb.WblockNotice -= wblockNotice;
      }

      void databaseConstructed(object sender, EventArgs e)
      {
         Report();
         Database.DatabaseConstructed -= databaseConstructed;
         destDb = (Database)sender;
         destDb.BeginDeepClone += beginDeepClone;
      }

      void beginDeepClone(object sender, IdMappingEventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender, destDb);
         this.idMap = e.IdMapping;
         db.BeginDeepClone -= beginDeepClone;
         db.DeepCloneEnded += deepCloneEnded;
         db.DeepCloneAborted += deepCloneAborted;
         db.BeginDeepCloneTranslation += beginDeepCloneTranslation;
      }

      void beginDeepCloneTranslation(object sender, IdMappingEventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender, destDb);
         db.BeginDeepCloneTranslation -= beginDeepCloneTranslation;
         if(this.IdMap != e.IdMapping)
            return;
         OnBeginDeepCloneTranslation(e.IdMapping);
      }

      void deepCloneEnded(object sender, EventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender, destDb);
         db.DeepCloneEnded -= deepCloneEnded;
         OnDeepCloneEnded(db);
         Reset();
      }

      void deepCloneAborted(object sender, EventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender, destDb);
         db.DeepCloneAborted -= deepCloneAborted;
         OnDeepCloneAborted(db);
         Reset();
      }

      protected virtual void OnBeginDeepCloneTranslation(IdMapping map)
      {
      }

      protected virtual void OnDeepCloneAborted(Database sender)
      {
      }

      /// <summary>
      /// This method must be overridden in a derived type
      /// to perform whatever operations are required when
      /// the wblock clone operation has ended.
      /// </summary>
      /// <param name="sender"></param>

      protected abstract void OnDeepCloneEnded(Database sender);

      [Conditional("DEBUG")]
      protected static void Report([CallerMemberName] string msg = "(unknown)")
      {
         DebugWrite($"*** {msg} ***");
      }

      protected static void WriteMessage(string fmt, params object[] args)
      {
         var doc = Application.DocumentManager.MdiActiveDocument;
         doc?.Editor.WriteMessage("\n" + fmt, args);
      }

      [Conditional("DEBUG")]
      protected static void DebugWrite(string fmt, params object[] args)
      {
         var doc = Application.DocumentManager.MdiActiveDocument;
         doc?.Editor.WriteMessage("\n" + fmt, args);
      }

      static void CheckSender(object sender, Database expected)
      {
         if((Database) sender != expected)
            throw new InvalidOperationException("Wrong database");
      }

   }


}