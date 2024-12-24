/// WblockCloneHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

/// This code may require C# 10.0

namespace AcMgdLib.AutoCAD.DatabaseServices
{

   /// <summary>
   /// WblockCloneHandler class:
   /// 
   /// Does all the grunt work required to allow 
   /// intervention in a wblock operation.
   /// 
   /// Usage requires one to derive a type from this 
   /// class, that overrides the OnWblockNotice() and 
   /// OnDeepCloneEnded() virtual methods, and within 
   /// those overrides, do whatever work is needed.
   /// 
   /// See the WblockGroupHandler class for an example
   /// showing the use of this type.
   /// 
   /// All overridable virtual methods are called in a
   /// try/catch that handles any exception thrown from
   /// an override, to prevent it from reaching the calling 
   /// event handler, because if that happens, AutoCAD's 
   /// managed API disables most database events, system-
   /// wide, for the rest of the session.
   /// 
   /// The IdMapping property should not be used after the
   /// OnDeepCloneEnded() virtual method is called, because 
   /// after that point it becomes unusable and can lead 
   /// to a failure if it is accessed.
   /// </summary>

   public abstract partial class WblockCloneHandler : IDisposable
   {
      Database sourceDb;
      Database destDb = null;
      IdMapping idMap = null;
      bool forceDatabaseCopy = false;
      bool observing = false;
      bool disposed = false;
      int state = 0;
      bool faulted = false;
      static DocumentCollection docs = Application.DocumentManager;
      IndexedHashSet<string> commands = null;

      public WblockCloneHandler(Database db, bool forceCopy = false)
      {
         if(db is null || db.IsDisposed)
            throw new ArgumentNullException(nameof(db));
         this.sourceDb = db;
         this.forceDatabaseCopy = forceCopy;
         Observing = true;
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
         get => forceDatabaseCopy;
         set => forceDatabaseCopy = value;
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
         set
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
      /// after the DeepCloneEnded event is raised.
      /// </summary>

      protected IdMapping IdMap
      {
         get
         {
            if(idMap is null || idMap.IsDisposed)
               throw new InvalidOperationException("null or invalid IdMapping");
            return idMap;
         }
      }

      protected Database Source
      {
         get
         {
            if(sourceDb is null || sourceDb.IsDisposed)
               throw new InvalidOperationException("source database is null or disposed");
            return sourceDb;
         }
      }

      protected Database Destination
      {
         get
         {
            if(destDb is null || destDb.IsDisposed)
               throw new InvalidOperationException("destination database is null or disposed");
            return destDb;
         }
      }

      protected bool IsFaulted => faulted;

      public IndexedHashSet<string> SupportedCommands =>
         commands ?? (commands = new IndexedHashSet<string>(StringComparer.OrdinalIgnoreCase));

      /// <summary>
      /// Indicates if the operation is supported for the
      /// active command in progress. If no supported commands
      /// have been added via the SupportedCommands[] property,
      /// the operation is enabled/supported in all commands.
      /// </summary>
      
      protected bool IsActiveCommandSupported
      {
         get
         {
            if(commands is null || commands.Count == 0)
               return true;
            Document doc = docs.MdiActiveDocument;
            return doc != null && commands.Contains(doc.CommandInProgress);
         }
      }

      protected virtual void Reset(bool enable = true)
      {
         destDb.DeepCloneEnded -= deepCloneEnded;
         destDb.DeepCloneAborted -= deepCloneAborted;
         this.idMap = null;
         this.destDb = null;
         Observing = enable && !faulted;
         state = Observing ? 1 : 0;
      }

      void wblockNotice(object sender, WblockNoticeEventArgs e)
      {
         Report();
         bool result = false;
         if(IsActiveCommandSupported)
         {
            if(Invoke(() => result = OnWblockNotice(sourceDb)) && result)
            {
               if(forceDatabaseCopy)
                  sourceDb.ForceWblockDatabaseCopy();
               Database.DatabaseConstructed += databaseConstructed;
               state = 2;
               Observing = false;
            }
         }
      }

      /// <summary>
      /// Derived types must override this and return a value 
      /// indicating if the operation should be handled. If
      /// the result is false, the operation is not handled
      /// and no other virtual methods will be called.
      /// </summary>
      /// <param name="sourceDb">The Database in which the
      /// WBLOCK operation is starting.</param>
      /// <returns>True to handle this WBLOCK operation,
      /// or false to ignore it.</returns>

      protected abstract bool OnWblockNotice(Database sourceDb);

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
         Database db = (Database)sender;
         db.BeginDeepClone -= beginDeepClone;
         if(db != destDb)
            return;
         this.idMap = e.IdMapping;
         if(Invoke(() => OnBeginDeepClone(destDb, IdMap)))
         {
            db.DeepCloneEnded += deepCloneEnded;
            db.DeepCloneAborted += deepCloneAborted;
            db.BeginDeepCloneTranslation += beginDeepCloneTranslation;
            state = 4;
         }
      }

      protected virtual void OnBeginDeepClone(Database destDb, IdMapping idMap)
      {
      }

      void beginDeepCloneTranslation(object sender, IdMappingEventArgs e)
      {
         Report();
         Database db = (Database)sender;
         db.BeginDeepCloneTranslation -= beginDeepCloneTranslation;
         if(db != destDb)
            return;
         if(this.IdMap != e.IdMapping)
            return;
         if(Invoke(() => OnBeginDeepCloneTranslation(e.IdMapping)))
            state = 5;
      }

      void deepCloneEnded(object sender, EventArgs e)
      {
         Report();
         deepCloneEnded(IdMap, false);
      }

      void deepCloneAborted(object sender, EventArgs e)
      {
         Report();
         deepCloneEnded(IdMap, true);
      }

      void deepCloneEnded(IdMapping map, bool aborted)
      {
         if(Invoke(() => OnDeepCloneEnded(map, aborted)))
            Reset(true);
      }

      protected virtual void OnBeginDeepCloneTranslation(IdMapping map)
      {
      }

      /// <summary>
      /// This method can be overridden in a derived type
      /// to perform whatever operations are required when
      /// the wblock clone operation has ended, before the
      /// destination database is written to storage. 
      /// 
      /// It is generally safe to operate on the destination 
      /// database from an override of this method.
      /// 
      /// This method wraps both the DeepCloneEnded and
      /// DeepCloneAborted events. The aborted argument 
      /// indicates if the operation was aborted. Hence,
      /// overrides of this method MUST check the argument
      /// before performing any operations and not assume 
      /// the operation successfully completed.
      /// 
      /// Keep in mind that there can be other applications,
      /// extensions, verticals, etc., that may also listen
      /// for the underlying notification that drives this 
      /// method (the DeepCloneEnded event), and may also be
      /// acting on the destination database when they receive 
      /// that notification. The order in which those other 
      /// observers are notified is completely undefined and 
      /// effectively-random.
      /// </summary>
      /// <param name="sender">The Database in which the deep
      /// clone operation is ending.</param>
      /// <param name="map">The IdMapping used in the operation</param>
      /// <param name="aborted">A value indicating if the operation
      /// was aborted</param>

      protected virtual void OnDeepCloneEnded(IdMapping map, bool aborted)
      {
      }

      /// <summary>
      /// If any virtual method invoked via this method throws
      /// an exception, no further notifications will be sent, 
      /// and the instance is disabled and will not respond to 
      /// subsequent wblock operations.
      /// 
      /// This is necessary because of how AutoCAD's deep clone
      /// and wblock clone events behave - if a handler of one
      /// of those events throws an exception, AutoCAD disables
      /// all most Database event notifications, system-wide, for 
      /// all managed applications.
      /// </summary>
      /// <param name="action"></param>
      /// <returns>A value indicating if the method invocation
      /// completed successfully.</returns>
      
      bool Invoke(Action action)
      {
         try
         {
            action();
            return true;
         }
         catch(System.Exception ex)
         {
            WriteMessage(ex.ToString());
            faulted = true;
            Reset(false);
            WriteMessage($"{GetType().Name} permanently disabled.");
            return false;
         }
      }

      /// <summary>
      /// Diagnostic support functions
      /// </summary>
      /// <param name="msg"></param>

      [Conditional("DEBUG")]
      protected static void Report([CallerMemberName] string msg = "(unknown)")
      {
      }

      protected internal static void WriteMessage(string fmt, params object[] args)
      {
         var doc = Application.DocumentManager.MdiActiveDocument;
         doc?.Editor.WriteMessage("\n" + fmt, args);
      }

      [Conditional("DEBUG")]
      protected static void DebugWrite(string fmt, params object[] args)
      {
         WriteMessage(fmt, args);
      }

      Database CheckSender(object sender)
      {
         if(sender is null)
            throw new ArgumentNullException(nameof(sender));
         Database db = (Database)sender;
         if(db != destDb)
            throw new InvalidOperationException("Wrong database");
         return db;
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

   /// <summary>
   /// Allows hashset manipulation using indexer semantics.
   /// 
   /// E.g.,
   /// 
   ///    this[key] = true;   // adds key to hashset
   ///    this[key] = false   // removes key from hashset
   ///    
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class IndexedHashSet<T> : HashSet<T>
   {
      public IndexedHashSet(IEqualityComparer<T> comparer = null)
         : base(comparer)
      {
      }

      public IndexedHashSet(IEnumerable<T> items, IEqualityComparer<T> comparer = null)
         : base(items, comparer) 
      {
      }

      public void AddRange(params T [] items)
      {
         this.UnionWith(items);
      }

      public bool this[T key]
      {
         get => Contains(key);
         set
         {
            if(value)
               AddRange(key);
            else
               Remove(key);
         }
      }
   }
}