/// WblockEvents.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Extensions;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

#if(!ACDB_ONLY)
using Autodesk.AutoCAD.ApplicationServices;
#endif

using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;

/// Notes:
/// 
/// A dependence on AcMgd/AcCoreMgd may be unavoidable,
/// and it is not entirely clear if there would ever be
/// a need for this class in a Database-only scenario 
/// such as a RealDwg host application or an ObjectDBX
/// client app.
/// 
/// The only dependence on AcMgd/AcCoreMgd that exists
/// at this point, is the diagnostic trace methods that 
/// write output to the command line.

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   public abstract class WblockCloneObserver : WblockCloneObserver<DBObject>
   {
      public WblockCloneObserver(Database db) : base(db)
      {
      }
   }

   /// <summary>
   /// This type derives from Overrule<T>, and constrains the 
   /// underlying overrule to only apply to instances of the 
   /// generic argument, or a type derived from it. 
   /// 
   /// Because this class derives from Overrule, derived types
   /// can get control when each object is cloned within the
   /// WBLOCK cloning operation, by simply overriding the base
   /// overrule's WblockClone() method.
   /// 
   /// The generic argument is only used to specify the target
   /// type of the base ObjectOverrule. 
   /// 
   /// </summary>
   /// <typeparam name="T">The Type to be overruled.</typeparam>

   public abstract class WblockCloneObserver<T> : ObjectOverrule<T> where T : DBObject
   {
      Database source;
      Database dest;
      Database from = null;
      ObjectId blockId = ObjectId.Null;
      Point3d insertionPoint;
      WblockType type = WblockType.None;
      IdMapping wblockObjectsMapping = null;     // passed in to some BeginWblockXxxxx events
      IdMapping deepCloneMapping = null;  // passed into BeginDeepClone/BeginDeepCloneTranslation
      bool disposed = false;
      bool beginWblockHandled = false;
      bool endWblockHandled = false;
      bool observing = false;
      bool aborted;
      int state = 0;
      bool isSaving;
      string filePath = null;
      bool overridesOnException;

      static bool trace = false;
      const int STATE_INIT = 0;
      const int STATE_WBLOCK_NOTICE = 1;
      const int STATE_DESTINATION_CREATED = 2;
      const int STATE_BEGIN_DEEPCLONE = 3;
      const int STATE_BEGIN_WBLOCK = 4;
      const int STATE_BEGIN_TRANSLATION = 5;
      const int STATE_END_DEEPCLONE = 6;
      const int STATE_END_WBLOCK = 7;
      const int STATE_DESTINATION_SAVED = 8;
      const int STATE_WBLOCK_OPERATION_ENDED = 9;

      public WblockCloneObserver(Database db)
         : base(false)
      {
         Assert.IsValid(db);
         this.source = db;
         this.source.DatabaseToBeDestroyed += sourceToBeDestroyed;
         overridesOnException = HasOverride(OnException);
         Observing = true;
      }

      static bool HasOverride(Delegate del)
      {
         return del.Method != del.Method.GetBaseDefinition();
      }

      protected Database Source
      {
         get
         {
            if(!source.IsValid())
               throw new InvalidOperationException("source database is null or disposed");
            return dest;
         }
      }

      protected Database Destination
      {
         get
         {
            if(!dest.IsValid())
               throw new InvalidOperationException("destination database is null or disposed");
            return dest;
         }
      }

      /// <summary>
      /// Indicates if the Database accessed through the
      /// Destination property is valid. If there is no valid
      /// destination database, the Destination property throws 
      /// an exception.
      /// </summary>

      public bool IsDestinationValid => dest.IsValid();

      /// <summary>
      /// Indicates if the operation was aborted.
      /// </summary>

      public bool IsAborted => aborted;

      /// <summary>
      /// Indicates the current state of the instance.
      /// See the STATE_XXXX constants for the range of
      /// possible values and their meanings.
      /// </summary>
      public int State => state;

      /// <summary>
      /// Indicates the type of the WBLOCK operation 
      /// currently in progress.
      /// </summary>
      public WblockType Type => type;

      /// <summary>
      /// This is the value of the From property 
      /// in the beginWblockXxxxxEventArgs instance,
      /// which should be the same as the value of
      /// the Source property.
      /// </summary>

      public Database From
      {
         get
         {
            CheckType(type != WblockType.None);
            return CheckNull(from);
         }
      }

      /// <summary>
      /// The ObjectId of the block that is being
      /// exported by WBLOCK when the WblockType
      /// is WblockType.Block. 
      /// </summary>

      public ObjectId BlockId
      {
         get
         {
            CheckType(type == WblockType.Block);
            return blockId;
         }
      }

      /// <summary>
      /// The InsertionPoint specified by the
      /// BeginWblockSelectedObjectsEventArgs.
      /// This property is only valid when the
      /// WblockType is SelectedObjects.
      /// </summary>
      
      public Point3d InsertionPoint
      {
         get
         {
            CheckType(type == WblockType.SelectedObjects);
            return insertionPoint;
         }
      }

      /// <summary>
      /// This property is only valid when WblockType
      /// is WblockType.Objects. In any other context,
      /// this property getter will throw an exception
      /// that stops the operation if the call originates 
      /// from an override of a virtual method of this 
      /// class, and is not handled there.
      /// </summary>

      public IdMapping WblockObjectsMapping => CheckNull(wblockObjectsMapping);

      public bool HasWblockMapping => wblockObjectsMapping != null
         && wblockObjectsMapping.Cast<IdPair>().Any();

      /// <summary>
      /// This is the IdMapping used in the deep clone
      /// operation within the WBLOCK operation. It is
      /// the value passed in the event arguments of the
      /// BeginDeepClone and BeginDeepCloneTranslation
      /// events.
      /// </summary>

      public IdMapping DeepCloneMapping => CheckNull(deepCloneMapping);

      /// <summary>
      /// If this returns false, any value returned by the 
      /// DeepCloneMapping property should not be accessed.
      /// </summary>

      public bool IsDeepCloneMappingValid(bool allowEmpty = true)
      {
         return deepCloneMapping != null
            && state >= STATE_BEGIN_DEEPCLONE
            && state <= STATE_END_WBLOCK
            && allowEmpty || deepCloneMapping.Cast<IdPair>().Any();
      }

      /// <summary>
      /// Provides a safe way to enumerate the DeepCloneMapping 
      /// without risking an exception. This method safeguards
      /// access to the IdMapping which can cause a failure if
      /// accessed too late in the process (which includes from
      /// the BeginSave event of the destination database).
      /// </summary>
      
      public IEnumerable<IdPair> IdPairs
      { 
         get
         {
            if(IsDeepCloneMappingValid(false))
               return deepCloneMapping.Cast<IdPair>();
            else
               return Enumerable.Empty<IdPair>();
         } 
      }

      /// <summary>
      /// Indicates if the destination is the clipboard.
      /// </summary>
      public bool IsClipboardTarget => false; // not sure how to do this yet.

      void CheckType(bool condition)
      {
         if(!condition)
            throw new InvalidOperationException("Property not valid for the current operation type");
      }

      /// <summary>
      /// Caveat Emptor:
      /// 
      /// Not calling ForceWblockDatabaseCopy() leads to
      /// complications that are difficult to resolve. If
      /// not called, most events do not fire, but the
      /// DatabaseConstructed event does fire, but only
      /// after the operation is completed, which wreaks
      /// havoc with the code logic used in this class.
      /// 
      /// So, to ensure consistent handling of all forms
      /// of WBLOCK, and to avoid immensely-convoluted code 
      /// logic, ForceWblockDatabaseCopy() is always called.
      /// 
      /// The WblockNotice event is the primary trigger
      /// that sets the wheels in motion to observe the
      /// entire operation. 
      /// </summary>

      void wblockNotice(object sender, WblockNoticeEventArgs e)
      {
         state = STATE_WBLOCK_NOTICE;
         Report(sender);
         Database.DatabaseConstructed += databaseConstructed;
         source.ForceWblockDatabaseCopy();
         try
         {
            OnOperationStarted(source);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnOperationStarted));
         }
         Observing = false;
      }

      void dispatch(System.Exception ex, [CallerMemberName] string caller = null)
      {
         
         Trace($"Unhandled exception in {caller ?? "unknown"}: {ex.ToString()}");
         if(!overridesOnException)
         {
            Reset(true);
            throw ex;
         }
         try
         {
            OnException(ex, caller);
         }
         catch(System.Exception ex2)
         {
            Reset(true);
            throw ex2;
         }
      }

      /// <summary>
      /// Overrides can choose to throw the exception
      /// or suppress it.
      /// </summary>
      /// <param name="ex"></param>
      
      protected virtual void OnException(System.Exception ex, string caller = "unknown")
      {
      }

      void databaseConstructed(object sender, EventArgs e)
      {
         state = STATE_DESTINATION_CREATED;
         Report(sender);
         Database.DatabaseConstructed -= databaseConstructed;
         dest = (Database)sender;
         dest.BeginDeepClone += beginDeepClone;
         dest.DatabaseToBeDestroyed += destinationToBeDestroyed;
         dest.BeginSave += beginSave;
         dest.SaveComplete += saveComplete;
      }

      private void saveComplete(object sender, DatabaseIOEventArgs e)
      {
         Report(sender);
         ReportMsg(sender, $"Filename = {Path.GetFileName(e.FileName)}");
         this.filePath = e.FileName;
         state = STATE_DESTINATION_SAVED;
         try
         {
            OnSaveComplete((Database)sender, e.FileName);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnSaveComplete));
         }
      }

      private void beginSave(object sender, DatabaseIOEventArgs e)
      {
         Report(sender);
         try
         {
            OnBeginSave((Database)sender, e.FileName);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnBeginSave));
         }
      }

      void sourceToBeDestroyed(object sender, EventArgs e)
      {
         this.source = null;
      }

      void destinationToBeDestroyed(object sender, EventArgs e)
      {
         Report(sender);
         OnOperationEnded(this.aborted);
         this.dest = null;
      }

      public bool Observing
      {
         get => observing;
         protected set
         {
            if(observing ^ value)
            {
               if(value)
                  source.WblockNotice += wblockNotice;
               else
                  source.WblockNotice -= wblockNotice;
               observing = value;
               OnObservingChanged(value);
            }
         }
      }

      /// <summary>
      /// Allows derived types to be notified when the
      /// observing state of the instance has changed.
      /// The observing state changes at the start and
      /// end of a WBLOCK operation, and is false for
      /// the duration of the operation.  This method
      /// allows a derived type to synchronize its state
      /// with the Observed property's value.
      /// </summary>
      /// <param name="value">The newly-assigned value 
      /// of the Observing property</param>
      
      protected virtual void OnObservingChanged(bool value)
      {
         ReportMsg($"value = {value}");
      }

      /// <summary>
      /// Only one BeginWblockXxxxx event will fire per
      /// WBlock operation, so they must be collectively
      /// added to and removed from the event source:
      /// </summary>

      void AddBeginWblockHandlers()
      {
         Assert.IsValid(dest);
         if(!beginWblockHandled)
         {
            beginWblockHandled = true;
            dest.BeginWblockBlock += beginWblockBlock;
            dest.BeginWblockEntireDatabase += beginWblockEntireDatabase;
            dest.BeginWblockSelectedObjects += beginWblockSelectedObjects;
            dest.BeginWblockObjects += beginWblockObjects;
         }
      }

      /// <summary>
      /// When any of the BeginWblockXxxxx events is fired, this
      /// will be called with addEndHandlers set to true, to add
      /// the WblockEnded/Aborted event handlers.
      /// </summary>
      /// <param name="addEndHandlers"></param>

      void RemoveBeginWblockHandlers(bool addEndHandlers = false)
      {
         Assert.IsValid(dest);
         if(beginWblockHandled)
         {
            beginWblockHandled = false;
            dest.BeginWblockBlock -= beginWblockBlock;
            dest.BeginWblockEntireDatabase -= beginWblockEntireDatabase;
            dest.BeginWblockSelectedObjects -= beginWblockSelectedObjects;
            dest.BeginWblockObjects -= beginWblockObjects;
            if(addEndHandlers && !endWblockHandled)
            {
               endWblockHandled = true;
               dest.WblockEnded += wblockEnded;
               dest.WblockAborted += wblockAborted;
            }
         }
      }

      void beginDeepClone(object sender, IdMappingEventArgs e)
      {
         state = STATE_BEGIN_DEEPCLONE;
         Report(sender);
         CheckSender(sender);
         Assert.IsValid(dest, false);
         this.deepCloneMapping = e.IdMapping;
         dest.BeginDeepClone -= beginDeepClone;
         dest.BeginDeepCloneTranslation += beginDeepCloneTranslation;
         dest.DeepCloneAborted += deepCloneAborted;
         dest.DeepCloneEnded += deepCloneEnded;
         AddBeginWblockHandlers();
         try
         {
            OnBeginDeepClone(e.IdMapping);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnBeginDeepClone));
         }
         Enabled = true;
      }

      void beginDeepCloneTranslation(object sender, IdMappingEventArgs e)
      {
         state = STATE_BEGIN_TRANSLATION;
         Report(sender);
         CheckSender(sender);
         dest.BeginDeepCloneTranslation -= beginDeepCloneTranslation;
         try
         {
            OnBeginDeepCloneTranslation(e.IdMapping);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnBeginDeepCloneTranslation));
         }
      }

      void deepCloneEnded(object sender, EventArgs e)
      {
         CheckSender(sender);
         DeepCloneEnded(false);
      }

      void deepCloneAborted(object sender, EventArgs e)
      {
         CheckSender(sender);
         aborted = true;
         DeepCloneEnded(true);
      }

      /// <summary>
      /// A common execution path for the
      /// deepCloneEnded and deepCloneAborted
      /// event handlers.
      /// </summary>
      /// <param name="aborted"></param>

      void DeepCloneEnded(bool aborted)
      {
         state = STATE_END_DEEPCLONE;
         ReportMsg($"aborted = {aborted}");
         dest.DeepCloneAborted -= deepCloneAborted;
         dest.DeepCloneEnded -= deepCloneEnded;
         base.Enabled = false;
         try
         {
            if(!IsDeepCloneMappingValid(true))
               throw new InvalidOperationException("Invalid deep clone mapping");
            OnDeepCloneEnded(deepCloneMapping, aborted);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnDeepCloneEnded));
         }
      }

      ///////////////////////////////////////////////////////
      /// Overridables
      /// 
      /// This type maps all deep clone and wblock clone-
      /// related events to corresponding virtual methods 
      /// that can be overriden in derived types. 
      /// 
      /// This design is used to support multiple layers of
      /// specialization in derived types. The only downside
      /// is that events are handed unconditionally, even if
      /// the consumer is not interested in them, but that is 
      /// not an issue since these are low-frequencey events.

      /// <summary>
      /// Signals the start of a WBLOCK operation (corresponds
      /// to the WblockNotice event). This method along with
      /// the OnOperationEnded() method, serve to define the 
      /// scope of the entire operation, allowing them to be
      /// used for initialization/finalization purposes.
      /// </summary>
      /// <param name="sourceDatabase">The source Database
      /// for the operation. This argument is the value of
      /// the Source property.</param>
      
      protected virtual void OnOperationStarted(Database sourceDatabase)
      {
         Report();
      }

      /// <summary>
      /// Corresponds to the BeginDeepClone event.
      /// </summary>
      /// <param name="map">The IdMapping to be used in
      /// the deep clone operation. This value is cached
      /// in the DeepCloneMapping property.</param>

      protected virtual void OnBeginDeepClone(IdMapping map)
      {
         Report();
      }

      /// <summary>
      /// Corresponds to the BeginDeepCloneTranslation event
      /// </summary>
      /// <param name="destination"></param>
      /// <param name="idMapping"></param>

      protected virtual void OnBeginDeepCloneTranslation(IdMapping idMapping)
      {
         Report();
      }

      /// <summary>
      /// Signals the end of the deep clone operation, 
      /// and corresponds to both the DeepCloneEnded 
      /// and DeepCloneAborted events.
      /// </summary>
      /// <param name="map">The IdMapping passed into the
      /// beginDeepClone event</param>
      /// <param name="aborted">A value indicating if the 
      /// operation was aborted.</param>

      protected virtual void OnDeepCloneEnded(IdMapping map, bool aborted = false)
      {
         Report();
      }

      /// <summary>
      /// From an override of this method, the event arguments can
      /// be accessed via properties of this type, and the Type of
      /// wblock operation that is starting is also exposed via the 
      /// Type property and as an argument to this method.
      /// </summary>

      protected virtual void OnBeginWblock(WblockType type)
      {
         ReportMsg($"WblockType: {type}");
      }

      /// <summary>
      /// Wraps both the WblockEnded and WblockAborted events.
      /// The argument indicates which handler for those two
      /// events called this.
      /// </summary>
      /// <param name="aborted">A value indicating if the 
      /// WBLOCK operation was aborted. Overrides of this
      /// event _must_ check this argument and not assume
      /// that the operation wasn't aborted.</param>

      protected virtual void OnWblockEnded(bool aborted = false)
      {
         Report();
      }

      /// <summary>
      /// This method can be overridden to get control when the
      /// instance is being reset. A reset puts the instance in
      /// the same state it had when it was first created.
      /// </summary>
      /// <param name="restart">True if observing subsequent
      /// WBLOCK operations in the Source database is enabled.</param>

      protected virtual void OnReset(bool restart)
      {
         Report();
      }

      /// <summary>
      /// Called when the WBLOCK operation has ended or
      /// was aborted.
      /// </summary>
      /// <param name="aborted"></param>
      
      protected virtual void OnWblockOperationEnded(bool aborted)
      {
         Report();
      }

      /// <summary>
      /// Called when the entire operation has ended,
      /// which happens when the destination database
      /// has been destroyed.
      /// </summary>
      /// <param name="aborted"></param>

      protected virtual void OnOperationEnded(bool aborted)
      {
         Report();
      }

      /// <summary>
      /// Called when the destination database is saved
      /// 
      /// Inmportant:
      /// 
      /// Do not attempt to access the IdMap property from
      /// this method.
      /// </summary>

      protected virtual void OnBeginSave(Database sender, string fileName)
      {
         isSaving = true;
         Report();
      }

      /// <summary>
      /// Called after the destination database has been saved.
      /// </summary>

      protected virtual void OnSaveComplete(Database sender, string fileName)
      {
         Report();
         isSaving = false;
      }

      /// <summary>
      /// Passing a value of true resets the instance to its
      /// initial state and causes it to begin listening for
      /// another WblockNotice event.
      /// 
      /// If false is passed, the instance will not observe 
      /// subsequent wblock operations.
      /// </summary>
      /// <param name="restart">True to continue observing
      /// subsequent WBLOCK operations</param>

      void Reset(bool restart = true)
      {
         state = STATE_INIT;
         Report();
         Assert.MustNotBeNullOrDisposed(source);
         OnReset(restart);
         Observing = restart;
         dest = null;
         deepCloneMapping = null;
         wblockObjectsMapping = null;
         from = null;
         blockId = ObjectId.Null;
         type = WblockType.None;
      }

      static T CheckNull<T>(T value, [CallerArgumentExpression("value")] string name = "Unspecified") where T : class
      {
         if(value is null)
            throw new NullReferenceException(name);
         return value;
      }

      void CheckIsValid()
      {
         if(!source.IsValid() && dest.IsValid())
            throw new InvalidOperationException("Invalid instance");
      }

      void CheckSender(object sender)
      {
         Database db = (Database)sender;
         Assert.MustNotBeNullOrDisposed(db);
         if(db != dest)
            throw new InvalidOperationException("Database mismatch");
      }

      /// <summary>
      /// BeginWblockXxxxx events. 
      /// 
      /// Only one of the following four events will be raised
      /// during a single WBLOCK operation. 
      /// 
      /// The OnBeginWblock() virtual method wraps all four of these 
      /// events, and specifies which type of operation is starting 
      /// using the WblockType enum argument.
      /// 
      /// Wrapping these events in a single virtual method relieves 
      /// the developer of the burden of having to explicitly manage 
      /// these events, and avoid the highly-convoluted, confusing, 
      /// and error-prone code logic required to do that.
      /// 
      /// This class manages these events and ensures they are added 
      /// to their source only when they are about to be raised and 
      /// removes all of them immediately after any one of them has
      /// been raised.
      /// </summary>
      /// <param name="sender">The Database event source</param>
      /// <param name="e">The event argument, which differs for each
      /// of the four events. The event arguments are cached by the
      /// instance and exposed through properties.</param>
      
      void beginWblockObjects(object sender, BeginWblockObjectsEventArgs e)
      {
         from = e.From;
         wblockObjectsMapping = e.IdMapping;
         BeginWblock(WblockType.Objects);
      }

      void beginWblockSelectedObjects(object sender, BeginWblockSelectedObjectsEventArgs e)
      {
         from = e.From;
         insertionPoint = e.InsertionPoint;
         BeginWblock(WblockType.SelectedObjects);
      }

      void beginWblockEntireDatabase(object sender, BeginWblockEntireDatabaseEventArgs e)
      {
         from = e.From;
         BeginWblock(WblockType.EntireDatabase);
      }

      void beginWblockBlock(object sender, BeginWblockBlockEventArgs e)
      {
         this.blockId = e.BlockId;
         this.from = e.From;
         BeginWblock(WblockType.Block);
      }

      /// Provides a shared/common path of execution
      /// from all of the above handlers.

      void BeginWblock(WblockType type)
      {
         /// Overrides of OnBeginWBlock() can set the 
         /// Enabled property to enable overruling. 
         /// 
         /// If true was passed as the second argument to 
         /// the constructor, overruling is automatically
         /// enabled when OnBeginWblock() returns.

         this.type = type;
         state = STATE_BEGIN_WBLOCK;
         ReportMsg($"Type = {type}");
         RemoveBeginWblockHandlers(true);
         try
         {
            OnBeginWblock(type);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnBeginWblock));
         }
         //if(overrulingEnabled)
         //   base.Enabled = true;
      }

      /// <summary>
      /// TODO: Need disposed checks throughout this type
      /// on members that use the destination database.
      /// </summary>
      
      /// <summary>
      /// Either of these is the last event to be raised,
      /// and is the only place where Reset() should be
      /// called from.
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>

      void wblockAborted(object sender, EventArgs e)
      {
         state = STATE_END_WBLOCK;
         Report(sender);
         try
         {
            OnWblockEnded(true);
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnWblockEnded));
         }
         WblockOperationEnded(true);
      }

      void wblockEnded(object sender, EventArgs e)
      {
         state = STATE_END_WBLOCK;
         Report(sender);
         try
         {
            OnWblockEnded();
         }
         catch(System.Exception ex)
         {
            dispatch(ex, nameof(OnWblockEnded));
         }
         WblockOperationEnded(false);
      }

      /// <summary>
      /// Overrulling is disabled here under all
      /// circumstances. If the second argument to 
      /// the constructor is true, it will be enabled
      /// the next time OnBeginWblock() is called.
      /// Otherwise, overruling will only be enabled
      /// if the Enabled property is set to true.
      /// 
      /// Derived types can override this behavior
      /// at any time, by simply setting the Enabled
      /// property to true or false, but they should
      /// not change that property from within any of
      /// the overridable virtual methods of the base
      /// ObjectOverrule class. Doing that will most-
      /// likely terminate AutoCAD.
      /// </summary>
      /// <param name="aborted"></param>

      void WblockOperationEnded(bool aborted)
      {
         state = STATE_WBLOCK_OPERATION_ENDED;
         Report();
         base.Enabled = false;
         RemoveWblockEndedHandlers();
         try
         {
            OnWblockOperationEnded(aborted);
         }
         catch(System.Exception ex)
         {
            dispatch(ex);
         }
         Reset();
      }

      void RemoveWblockEndedHandlers()
      {
         if(dest.IsValid())
         {
            dest.WblockEnded -= wblockEnded;
            dest.WblockAborted -= wblockAborted;
            endWblockHandled = false;
         }
      }

      protected override void Dispose(bool disposing)
      {
         base.Dispose(disposing);
         if(!disposed && disposing)
         {
            disposed = true;
            Reset(false);
         }
      }

      /// Diagnostic support

      [Conditional("DEBUG")]
      void ReportMsg(object sender, string other, [CallerMemberName] string caller = "(unknown)")
      {
         if(trace)
         {
            if(string.IsNullOrEmpty(other))
               other = "";
            else
               other = $"[{other}]";
            Database db = (Database)sender;
            Trace($"*** {caller} {db.Format()} ({state}) *** {other}");
         }
      }

      [Conditional("DEBUG")]
      void Report(object sender, [CallerMemberName] string caller = "(unknown)")
      {
         if(trace)
         {
            Database db = (Database)sender;
            Trace($"*** {caller} {db.Format()} ({state}) ***");
         }
      }

      [Conditional("DEBUG")]
      protected void Report([CallerMemberName] string caller = "(unknown)")
      {
         if(trace)
            Trace($"*** {caller} ({state}) ***");
      }

      [Conditional("DEBUG")]
      protected void ReportMsg(string msg, [CallerMemberName] string caller = "(unknown)")
      {
         if(trace)
         {
            if(!string.IsNullOrEmpty(msg))
               msg = $" [{msg}]";
            else
               msg = string.Empty;
            Trace($"*** {caller} ({state}) ***{msg}");
         }
      }

      protected static void Write(string fmt, params object[] args)
      {
         var doc = Application.DocumentManager.MdiActiveDocument;
         doc?.Editor.WriteMessage("\n" + fmt, args);
      }

      [Conditional("DEBUG")]
      protected static void Trace(string fmt, params object[] args)
      {
         if(trace)
         {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n" + fmt, args);
         }
      }

      public static bool TraceEnabled { get => trace; set => trace = value; }

   }

   public enum WblockType
   {
      None = 0,
      EntireDatabase = 1,
      Block = 2,
      Objects = 3,
      SelectedObjects = 4
   }


}

public static class WblockCloneObserverExtensions
{
   public static bool HasOverride(this Delegate del)
   {
      return del.Method.GetBaseDefinition() != del.Method;
   }

}

/*

Below is the trace output showing the events 
raised, in raised order. In this case, the trace
shows what happens when COPYCLIP is used.

   Command: COPYCLIP
   Select objects: Specify opposite corner: 6 found, 1 group
   Select objects:
   *** wblockNotice *** Database (0x1ffbe6ad210) [WblockSource1.dwg]
   *** databaseConstructed *** Database (0x1ffbe6a9e90) [(unnamed)]
   *** beginDeepClone *** Database (0x1ffbe6a9e90) [(unnamed)]
   *** OnBeginDeepClone ***
   *** beginWblockSelectedObjects *** Database (0x1ffbe6a9e90) [(unnamed)]
   *** OnBeginWblock *** [WblockType: SelectedObjects]
   *** WblockClone(): Line (0xA8)  => Line (0x72)
   *** WblockClone(): Line (0xA9)  => Line (0x73)
   *** WblockClone(): Line (0xAA)  => Line (0x74)
   *** WblockClone(): Line (0xAB)  => Line (0x75)
   *** WblockClone(): Line (0xAC)  => Line (0x76)
   *** WblockClone(): Circle (0xAD)  => Circle (0x77)
   *** beginDeepCloneTranslation *** Database (0x1ffbe6a9e90) [(unnamed)]
   *** OnBeginDeepCloneTranslation ***
   *** deepCloneEnded *** Database (0x1ffbe6a9e90) [(unnamed)]
   *** OnDeepCloneEnded ***
   *** wblockEnded *** Database (0x1ffbe6a9e90) [(unnamed)]
   *** OnWblockEnded ***
   *** OnOperationEnded ***
   *** OnReset ***
   *** databaseToBeDestroyed *** Database (0x1ffbe6a9e90) [(unnamed)]
   Command:

Points to make note of:

1. From the beginDeepClone handler and the corresponding
   OnBeginDeepClone() method, one cannot discern which 
   type of WBLOCK operation is underway, because the event 
   identifying that has not fired yet, so if the type of 
   WBLOCK operation matters to a derived type, it must 
   override the OnBeginWblock() method, which recieves the 
   type of WBLOCK as a parameter, or it can read the Type
   property *after* OnBeginWblock() has been called.

2. The OnWblockEnded() virtual method wraps both the 
   WblockEnded and WblockAborted events, and exposes
   the ended/aborted status as a boolean argument.

3. The OnDeepCloneEnded() virtual method wraps both the
   DeepCloneEnded and DeepCloneAborted events, exposing
   the ended/aborted status as a boolean argument. 
   Both OnWblockEnded() and OnDeepCloneEnded() overrides
   _must_ check the failed property, and should never
   assume the operation wasn't aborted.


State:

   *** wblockNotice (1) *** Database (0x2d6fa1ed880) [WblockSource1.dwg]
   *** databaseConstructed (2) *** Database (0x28e095f8070) [(unnamed)]
   *** beginDeepClone (3) *** Database (0x28e095f8070) [(unnamed)]
   *** OnBeginDeepClone (3) ***
   *** BeginWblock (4) *** [Type = SelectedObjects]
   *** OnBeginWblock (4) *** [WblockType: SelectedObjects]
   *** beginDeepCloneTranslation (5) *** Database (0x28e095f8070) [(unnamed)]
   *** OnBeginDeepCloneTranslation (5) ***
   *** DeepCloneEnded (6) *** [aborted = False]
   *** OnDeepCloneEnded (6) ***
   *** wblockEnded (7) *** Database (0x28e095f8070) [(unnamed)]
   *** OnWblockEnded (7) ***
   *** OperationEnded (8) ***
   *** Reset (0) ***
   *** OnReset (0) ***
   *** databaseToBeDestroyed (0) *** Database (0x28e095f8070) [(unnamed)]


*/

