/// WblockCloneHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;

/// This code requires C# 10.0

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   /// <summary>
   /// WblockCloneHandler class:
   /// 
   /// Does all the grunt work required to allow 
   /// intervention in a wblock operation.
   /// 
   /// Usage requires one to derive a type from this 
   /// class, that overrides the OnDeepCloneEnded()
   /// virtual method, and within that override, do 
   /// whatever work is needed.
   /// 
   /// See the WblockGroupHandler class for an example
   /// showing the use of this type.
   /// </summary>

   public abstract class WblockCloneHandler : IDisposable
   {
      Database sourceDb;
      Database destDb = null;
      IdMapping idMap = null;
      bool forceCopy = false;
      bool observing = false;
      bool disposed = false;
      int state = 0;

      public WblockCloneHandler(Database db, bool forceCopy = false)
      { 
         this.sourceDb = db;
         this.forceCopy = forceCopy;
         Observing = true;
         state = 1;
      }

      /// <summary>
      /// Required to intervene during a wblock of
      /// the entire database. If this is false, there
      /// is no copy of the database created, and the
      /// operation instead is more akin to a SaveAs() 
      /// operation performed on the source Database,
      /// and in that case, OnEndDeepClone() and other
      /// virtual methods of this type are NOT called.
      /// 
      /// This option has no affect on the objects and 
      /// block forms of WBLOCK.
      /// 
      /// If you do not need to intervene during a full
      /// WBLOCK of the entire file, leave this property
      /// set to false, as it can ential a significant
      /// amount of overhead and memory consumption.
      /// </summary>
      
      public bool ForceDatabaseCopy
      {
         get => forceCopy;
         set => forceCopy = value;
      }

      /// <summary>
      /// A 'master' switch that enables listening for
      /// WBLOCK operations. This switch is turned off
      /// for the duration of such operations, and then
      /// turned back on when the operation completes.
      /// </summary>

      public bool Observing
      {
         get => observing;
         protected set
         {
            if(observing ^ value)
            {
               if(value)
                  sourceDb.WblockNotice += wblockNotice;
               else
                  sourceDb.WblockNotice -= wblockNotice;
               observing = value;
            }
         }
      }

      /// <summary>
      /// Caveat emptor: The IdMapping is not usable
      /// from a handler of the BeginSave event, and 
      /// will terminate AutoCAD if accessed from that 
      /// context. The IdMapping should not be accessed 
      /// after a DeepCloneEnded event is raised.
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
         destDb.DeepCloneEnded -= deepCloneEnded;
         destDb.DeepCloneAborted -= deepCloneAborted;
         this.idMap = null;
         this.destDb = null;
         Observing = true;
         state = 1;
      }

      void wblockNotice(object sender, WblockNoticeEventArgs e)
      {
         Report();
         if(forceCopy)
            sourceDb.ForceWblockDatabaseCopy();
         Database.DatabaseConstructed += databaseConstructed;
         state = 2;
         Observing = false;
      }

      void databaseConstructed(object sender, EventArgs e)
      {
         Report();
         Database.DatabaseConstructed -= databaseConstructed;
         destDb = (Database)sender;
         destDb.BeginDeepClone += beginDeepClone;
         state = 3;
      }

      void beginDeepClone(object sender, IdMappingEventArgs e)
      {
         Report();
         CheckSender(sender);
         Database db = (Database)sender;
         this.idMap = e.IdMapping;
         db.BeginDeepClone -= beginDeepClone;
         db.DeepCloneEnded += deepCloneEnded;
         db.DeepCloneAborted += deepCloneAborted;
         db.BeginDeepCloneTranslation += beginDeepCloneTranslation;
         state = 4;
      }

      void beginDeepCloneTranslation(object sender, IdMappingEventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender);
         db.BeginDeepCloneTranslation -= beginDeepCloneTranslation;
         if(this.IdMap != e.IdMapping)
            return;
         OnBeginDeepCloneTranslation(e.IdMapping);
      }

      void deepCloneEnded(object sender, EventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender);
         OnDeepCloneEnded(db, IdMap);
         Reset();
      }

      void deepCloneAborted(object sender, EventArgs e)
      {
         Report();
         Database db = (Database)sender;
         CheckSender(sender);
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
      /// the wblock clone operation has ended, before the
      /// destination database is written to storage. 
      /// 
      /// It is generally safe to operate on the destination 
      /// database from an override of this method.
      /// 
      /// Keep in mind that there can be other applications,
      /// extensions, verticals, etc., that may also listen
      /// for the underlying notification that drives this 
      /// method (the DeepCloneEnded event), and may also be
      /// acting on the destination database when they receive 
      /// that notification. The order in which those other 
      /// observers are notified is completely undefined and 
      /// effectively-random.
      /// 
      /// </summary>
      /// <param name="sender">The Database in which the deep
      /// clone operation is ending.</param>

      protected abstract void OnDeepCloneEnded(Database sender, IdMapping map);

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

      void CheckSender(object sender)
      {
         if((Database) sender != destDb)
            throw new InvalidOperationException("Wrong database");
      }

      public void Dispose()
      {
         if(!disposed)
         {
            disposed = true;
            try
            {
               Observing = false;
            }
            catch 
            { 
            }
         }
      }
   }


}