/// DatabaseTransaction.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Expressions.Predicates;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

/// Note: This file is intentionally kept free of any
/// dependence on AcMgd/AcCoreMgd.

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A specialization of Autodesk.AutoCAD.DatabaseServices.Transaction
   /// 
   /// This class encapsulates both a Database and a Transaction. 
   /// 
   /// Instance members mirroring methods of the DatabaseExtensions 
   /// class are included in this class and can be used to perform a 
   /// variety of operations. 
   /// 
   /// The methods that mirror the methods of the DatabaseExtensions 
   /// class are instance methods that can be invoked on an instance of 
   /// this type or a derived type. That allows these methods to be 
   /// called without the need to pass a Transaction or a Database as 
   /// arguments.
   /// 
   /// Current working database management:
   /// 
   /// If the Database argument's AutoDelete property is true (meaning 
   /// it is not a Database that is open in the Editor), the constructor 
   /// will set it to the current working database, and will restore the 
   /// previous working database when the instance is disposed.
   /// 
   /// An optional argument to the constructor can be supplied to
   /// prevent changing the current working database.
   /// 
   /// When allowing an instance to change the current working database
   /// to the database argument passed to the constructor, the previous
   /// working database must not be destroyed or deleted during the life
   /// of the instance.
   /// 
   /// If a DatabaseTransaction is used in a loop to iteratively
   /// process .DWG files that are opened via ReadDwgFile(), each 
   /// instance must be disposed before the next instance is created. 
   /// 
   /// Allowing multiple instances of a DatabaseTransaction to exist 
   /// concurrently can result in the current working database being
   /// set to an invalid or disposed Database when the last instance 
   /// is disposed.
   /// 
   /// </summary>

   public class DatabaseTransaction : Transaction
   {
      Database database;
      BlockTableRecord currentSpace = null;
      TransactionManager manager = null;   
      Database prevWorkingDb = null;
      static Dictionary<Database, DatabaseTransaction> transactions =
         new Dictionary<Database, DatabaseTransaction>();

      /// <summary>
      /// Creates and starts a DatabaseTransaction. 
      /// </summary>
      /// <param name="database">The Database in which to start the 
      /// transaction. All Database-specific operations performed by 
      /// an instance of this class will use this argument.</param>
      /// <param name="asWorkingDatabase">A value indicating if the given
      /// Database should be made the current working database for the life 
      /// of the transaction. This argument is only applicable to databases 
      /// that are created via the Database's new() constructor, and does
      /// not apply to Databases that are open in the AutoCAD editor, or 
      /// to databases associated with a Document. 
      /// 
      /// If at the time the instance is created, the current 
      /// working database is not open in the drawing editor, 
      /// that current working database is not changed.</param>

      public DatabaseTransaction(Database database, bool asWorkingDatabase = true)
         : base(new IntPtr(-1), false)
      {
         Assert.IsNotNullOrDisposed(database, nameof(database));
         if(transactions.ContainsKey(database))
            throw new InvalidOperationException("Database already owned by a DatabaseTransaction");
         transactions.Add(database, this);
         this.database = database;
         this.manager = database.TransactionManager;
         var curDb = WorkingDatabase;
         /// Currently, the current working database is not checked
         /// for validity, or if it is an AutoDeleting database verses
         /// one open in the drawing editor. This may need to change.
         if(asWorkingDatabase && database.AutoDelete && curDb != database)
            prevWorkingDb = SetWorkingDatabase(database);
         manager.StartTransaction().ReplaceWith(this);
      }

      /// <summary>
      /// True if this transaction is associated with 
      /// a database that's open in the drawing editor.
      /// </summary>
      public bool IsDocTransaction { get; private set; }

      /// <summary>
      /// The value of the wrapped Database's AutoDelete property
      /// </summary>
      public bool IsAutoDelete => database?.AutoDelete ?? false;

      /// <summary>
      /// This is only intended to be called from the
      /// constructor of DocumentTransaction. 
      /// 
      /// The derived type starts the transaction.
      /// </summary>

      protected DatabaseTransaction(Database db, TransactionManager mgr)
         : base(new IntPtr(-1), false)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         Assert.IsNotNullOrDisposed(mgr, nameof(mgr));
         if(db.AutoDelete)
            throw new InvalidOperationException("Database is not open in the drawing editor");
         if(transactions.ContainsKey(db))
            throw new InvalidOperationException("Database already owned by a DatabaseTransaction");
         this.database = db;
         this.manager = mgr;
         transactions.Add(db, this);
         IsDocTransaction = true;
      }

      /// <summary>
      /// Complications: Automatically restoring the previous working database
      /// after changing it to the encapsulated database requires that the
      /// previous working database be validated as not having been disposed.
      /// If it has, it can't be restored to the current working database
      /// without leading to a catastrophic failure.
      /// 
      /// Restoration of the previous working database to the database of the
      /// Active Document is trivial, but introduces a dependence on AcMgd.dll
      /// and AcCoreMgd.dll, which this file avoids.
      /// </summary>
      /// <param name="disposing"></param>
      /// <exception cref="InvalidOperationException"></exception>
      
      protected override void Dispose(bool disposing)
      {
         if(!transactions.Remove(this.database))
            Debug.WriteLine("Error: Database not found in map!");
         if(disposing)
         {
            if(prevWorkingDb != null && prevWorkingDb != WorkingDatabase)
            {
               Database temp = prevWorkingDb;
               prevWorkingDb = null;
               if(!temp.IsDisposed)
               {
                  SetWorkingDatabase(temp);
               }
               else
               {
                  base.Dispose();
                  TryDisposeDatabase();
                  throw new InvalidOperationException("Cannot restore previous working database.");
               }
            }
            if(this.AutoDelete && this.IsReadOnly)
               base.Commit();
         }
         base.Dispose(disposing);
         if(disposing)
            TryDisposeDatabase();
      }

      bool TryDisposeDatabase()
      {
         bool result = OwnsDatabase && database != null && !database.IsDisposed
               && database.AutoDelete && database != WorkingDatabase;
         if(result) 
            database.Dispose();
         return result;
      }

      public Database Database => database;

      public bool IsCurrentWorkingDatabase => this.database == WorkingDatabase;

      /// <summary>
      /// Opens a DWG file and returns a DatabaseTransaction
      /// representing the opened file.
      /// 
      /// When this method is used to open a DWG file, the Database 
      /// representing the opened DWG file is owned and managed by 
      /// the returned DatabaseTransaction instance. When the instance
      /// of the DatabaseTransaction is disposed, the database will be
      /// disposed along with it. Hence, if changes to the database are
      /// to be saved, that must happen before the DatabaseTransaction 
      /// is disposed. If changes to the Database are saved, they should 
      /// not be saved until after the DatabaseTransaction is commited.
      /// 
      ///   using(var trans = DatabaseTransaction.Open("SomeFile.dwg"))
      ///   {
      ///       // Modify the database here...
      ///       
      ///       trans.Commit();  // commit the changes
      ///       
      ///       // after commiting the transaction, 
      ///       // save changes back to disk:
      ///       
      ///       trans.Database.SaveAs(...)  // Save the changes
      ///   }
      ///   
      ///   // Once this point is reached, the DatabaseTransaction
      ///   // and the Database it encapsulates has been disposed.
      ///   
      /// To prevent a Database that was created internally by a call
      /// to Open() from being disposed when the transaction that wraps
      /// it is disposed, one can call the Detach() method.
      /// 
      /// </summary>
      /// <param name="path"></param>
      /// <param name="mode"></param>
      /// <param name="asWorkingDatabase"></param>
      /// <returns></returns>
      
      public static DatabaseTransaction Open(string path, FileOpenMode mode, 
         bool asWorkingDatabase = true)
      {
         Database db = new Database(false, true);
         try
         {
            db.ReadDwgFile(path, mode, true, null);
            var result = new DatabaseTransaction(db, asWorkingDatabase);
            result.OwnsDatabase = true;
            return result;
         }
         catch
         {
            db.Dispose();
            throw;
         }
      }

      public Database Detach()
      {
         this.OwnsDatabase = false;
         return this.database;
      }

      /// Indicates if the instance is responsible for
      /// managing the life of the encapsulated Database.
      /// If this property is true, the encapsulated
      /// Database will be disposed when the instance
      /// is disposed.

      public bool OwnsDatabase { get; private set; }

      /// <summary>
      /// Can be set to true, to prevent the transaction
      /// from aborting, which has high overhead. Use only
      /// when the database, document, and editor state has
      /// not been altered in any way (including sysvars,
      /// view changes, etc.).
      /// </summary>

      public bool IsReadOnly { get; set; }

      OpenMode openMode = OpenMode.ForRead;
      public OpenMode OpenMode
      {
         get => openMode;
         set
         {
            if(value != OpenMode.ForRead || value != OpenMode.ForWrite)
               throw new ArgumentException("Invalid OpenMode");
            if(this.IsReadOnly && value == OpenMode.ForWrite)
               throw new ArgumentException("Instance is read-only");
            this.openMode = value;
         }
      }

      public override void Abort()
      {
         if(IsReadOnly)
            base.Commit();
         else
            base.Abort();
      }

      ~DatabaseTransaction()
      {
         if(!IsDisposed)
            Debug.WriteLine($"\nFailed to dispose {nameof(DatabaseTransaction)}");
      }

      public override TransactionManager TransactionManager => manager;

      static Database SetWorkingDatabase(Database db)
      {
         Database current = HostApplicationServices.WorkingDatabase;
         if(current != db)
            HostApplicationServices.WorkingDatabase = db;
         return current;
      }

      static Database WorkingDatabase => HostApplicationServices.WorkingDatabase;
      //{
      //   get => HostApplicationServices.WorkingDatabase;
      //   set => HostApplicationServices.WorkingDatabase = value;
      //}

      /// Database-oriented Operations
      /// 
      /// The following methods operate on the encapsulated Database.

      /// <summary>
      /// Appends the entity to the given BlockTableRecord or
      /// the current space BlockTableRecord if the owner is 
      /// not supplied or is null.
      /// </summary>
      /// <param name="entity">The Entity to be appended to the
      /// specified owner or the current space block</param>
      /// <param name="owner">The BlockTableRecord to append the 
      /// given Entity to. The argument must be write-enabled.</param>
      /// <returns></returns>

      public ObjectId Append(Entity entity, BlockTableRecord owner = null)
      {
         CheckDisposed();
         owner = owner ?? CurrentSpaceForWrite;
         Assert.IsNotNullOrDisposed(entity, nameof(entity));
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         AcRx.ErrorStatus.NotOpenForWrite.ThrowIf(!owner.IsWriteEnabled);
         owner.AppendEntity(entity);
         Add(entity);
         return entity.ObjectId;
      }

      /// <summary>
      /// Appends the sequence of entities to the current space block.
      /// </summary>
      /// <remarks>
      /// If disposeOnError is true (the default), and an exception 
      /// occurs before all entities were appended to the current space, 
      /// this method attempts to dispose any entities from the input 
      /// sequence that haven't been appended to the current space.
      /// </remarks>
      /// <param name="entities">The sequence of entities to append to
      /// the current space</param>
      /// <param name="disposeOnFail">A value indicating if entities
      /// from the source sequence that have not been appended to the
      /// owner should be disposed if an exception occurs before all of
      /// the entities were appended (default: true).</param>
      /// <returns>An ObjectIdCollection containing the ObjectIds of
      /// all entities appended to the owner BlockTableRecord</returns>

      public ObjectIdCollection Append(IEnumerable<Entity> entities, bool disposeOnFail = true)
      {
         return Append(entities, CurrentSpaceForWrite, disposeOnFail);
      }

      /// <summary>
      /// Appends the sequence of entities to the specified owner 
      /// BlockTableRecord. The specified owner BlockTableRecord must 
      /// be open for write.
      /// <remarks>
      /// If disposeOnError is true (the default), and an exception 
      /// occurs before all entities were appended to the owner block, 
      /// this method attempts to dispose any entities from the input 
      /// sequence that haven't been appended to the owner block.
      /// </remarks>
      /// <param name="entities">The sequence of entities to append to
      /// the owner BlockTableRecord</param>
      /// <param name="owner">The owner BlockTableRecord to append the entities to</param>
      /// <param name="disposeOnFail">A value indicating if entities
      /// from the source sequence that have not been appended to the
      /// owner should be disposed if an exception occurs (default: true).</param>
      /// <returns>An ObjectIdCollection containing the ObjectIds of
      /// all entities appended to the owner BlockTableRecord</returns>
      /// </summary>

      public ObjectIdCollection Append(IEnumerable<Entity> entities, BlockTableRecord owner, bool disposeOnFail = true)
      {
         CheckDisposed();
         Assert.IsNotNull(entities, nameof(entities));
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         AcRx.ErrorStatus.NotOpenForWrite.ThrowIf(!owner.IsWriteEnabled);
         ObjectIdCollection result = new ObjectIdCollection();
         using(var e = entities.GetEnumerator())
         {
            try
            {
               while(e.MoveNext())
               {
                  var entity = e.Current;
                  Assert.IsNotNullOrDisposed(entity, nameof(entity));
                  owner.AppendEntity(entity);
                  Add(entity);
                  result.Add(entity.ObjectId);
               }
               return result;
            }
            catch(System.Exception ex)
            {
               if(disposeOnFail)
               {
                  while(e.MoveNext())
                     e.Current?.Dispose();
               }
               throw ex;
            }
         }
      }

      /// <summary>
      /// Appends the entities in the DBObjectCollection to the specified
      /// owner BlockTableRecord. The owner argument must be open for write.
      /// 
      /// If the dispose argument is true, the entire contents of the given
      /// DBObjectCollection (including non-Entity based objects) will be 
      /// disposed after all entities have been appended.
      /// 
      /// If an exception occurs before all entities in the DBObjectCollection
      /// have been appended to the owner, the entities that were not appended 
      /// will be disposed.
      /// </summary>
      /// <param name="entities">The DBObjectCollection containing the entities
      /// to be appended to the owner block</param>
      /// <param name="owner">The owner BlockTableRecord to append the entities to</param>
      /// <param name="dispose">A value indicating if all elements in the source
      /// DBObjectCollection should be disposed upon successful completion of the
      /// operation.</param>
      /// <returns></returns>

      public ObjectIdCollection Append(DBObjectCollection entities, BlockTableRecord owner, bool dispose = true)
      {
         IDisposable disposer = dispose ? entities.EnsureDispose(false) : null;
         using(disposer)
         {
            return Append(entities.OfType<Entity>(), owner, true);
         }
      }

      /// <summary>
      /// Overload of the above that appends the entities to the
      /// current space.
      /// </summary>

      public ObjectIdCollection Append(DBObjectCollection entities, bool dispose = true)
      {
         return Append(entities, CurrentSpaceForWrite, dispose);
      }

      protected BlockTableRecord CurrentSpaceForWrite
      {
         get
         {
            if(currentSpace == null || currentSpace.IsDisposed || currentSpace.ObjectId != CurrentSpaceId)
               currentSpace = GetObject<BlockTableRecord>(CurrentSpaceId, OpenMode.ForWrite);
            else if(!currentSpace.IsWriteEnabled)
               currentSpace.UpgradeOpen();
            return currentSpace;
         }
      }

      protected static void TryUpgradeOpen(DBObject target)
      {
         Assert.IsNotNullOrDisposed(target, nameof(target));   
         if(!target.IsWriteEnabled)
            target.UpgradeOpen();
      }

      /// <summary>
      /// This method can be used to upgrade the open mode of
      /// objects to OpenMode.ForWrite, with support for upgrading
      /// entities on locked layers (the DBObject.UpgradeOpen() 
      /// method doesn't support upgrading objects on locked layers).
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="source"></param>
      /// <param name="upgradeOnLockedLayers"></param>
      /// <returns></returns>
      
      public IEnumerable<T> UpgradeOpen<T>(IEnumerable<T> source, bool upgradeOnLockedLayers = true) where T: DBObject
      {
         Assert.IsNotNull(source, nameof(source));
         foreach(T entity in source)
         {
            if(!entity.IsWriteEnabled)
            {
               if(upgradeOnLockedLayers)
                  GetObject(entity.ObjectId, OpenMode.ForWrite, false, upgradeOnLockedLayers);
               else
                  entity.UpgradeOpen();
            }
            yield return entity;
         }
      }

      /// <summary>
      /// A strongly-typed verion of GetObject() that
      /// merely allows the caller to avoid an explicit
      /// cast to the desired type. Requires that the
      /// given ObjectId reference a DBObject of the
      /// generic argument type or a derived type.
      /// </summary>

      public T GetObject<T>(ObjectId id, OpenMode mode = OpenMode.ForRead, bool openErased = false, bool openOnLockedLayer = false) where T:DBObject
      {
         CheckDisposed();
         return (T) base.GetObject(id, mode, openErased, openOnLockedLayer);
      }

      /// <summary>
      /// A strongly-typed version of GetObject() that 
      /// checks the runtime class of its argument.
      /// </summary>

      public T GetObjectChecked<T>(ObjectId id, OpenMode mode = OpenMode.ForRead, bool openErased = false, bool openOnLockedLayer = false) where T : DBObject
      {
         CheckDisposed();
         AcRx.ErrorStatus.WrongObjectType.Requires<T>(id);
         return (T)base.GetObject(id, mode, openErased, openOnLockedLayer);
      }

      /// <summary>
      /// An indexer that can be used to open 
      /// DBObjects in the mode specified by the 
      /// OpenMode property, which defaults to 
      /// OpenMode.ForRead. 
      /// 
      /// Using the indexer to open objects for 
      /// write is not recommended.
      /// 
      /// If OpenMode is set to write, entities on
      /// locked layers are opened for write if the
      /// ForceOpenOnLockedLayers property is true
      /// (it is false by default).
      /// 
      /// Opening erased objects is not supported.
      /// </summary>
      /// <param name="key">The ObjectId of the DBObject to open</param>
      /// <returns>The opened DBObject</returns>

      public DBObject this[ObjectId key]
      {
         get
         {
            return base.GetObject(key, openMode, false, ForceOpenOnLockedLayers);
         }
      }

      public bool ForceOpenOnLockedLayers { get; set; }

      /// <summary>
      /// Shorthand method for AddNewlyCreatedDBObject()
      /// </summary>
      /// <param name="operand"></param>

      public void Add(DBObject entity)
      {
         base.AddNewlyCreatedDBObject(entity, true);
      }

      public static implicit operator Database(DatabaseTransaction operand)
      {
         return operand?.database ?? throw new ArgumentNullException(nameof(operand));
      }

      void CheckDisposed()
      {
         if(this.IsDisposed || !this.AutoDelete || this.database == null || this.database.IsDisposed)
            throw new InvalidOperationException("Transaction or Database was ended or disposed.");
      }

      /// <summary>
      /// Used to get the Database instance with checks
      /// made for a disposed instance or Database:
      /// </summary>

      protected Database ThisDb 
      { 
         get 
         {
            CheckDisposed();
            return this.database; 
         } 
      }

      /// What follows are replications of most methods of the
      /// DatabaseExtensions class, expressed as instance methods
      /// of this type, that pass the encapsulated Database and 
      /// the instance as the Database and Transaction arguments
      /// respectively. Making these methods instance methods of
      /// this class allows them to be called without having to
      /// pass a Transaction or a Database argument, serving to 
      /// simplify their use.
      /// 
      /// See the docs for methods of the DatabaseExtensions class
      /// for more information on these APIs. The main difference
      /// between these methods and the equivalent methods of the
      /// DatabaseExtensions class, is that all methods of this type
      /// replace the Database as the invocation target with the 
      /// instance of this type, and omit all Transaction arguments.
      ///
      /// The Database and Transaction arguments required by the
      /// extension version of this method are implicit here:
      
      public IEnumerable<T> GetModelSpaceObjects<T>(
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         return ThisDb.GetModelSpaceObjects<T>(this, mode, exact, openLocked);
      }

      /// <summary>
      /// Opens objects for read, and filters them implicitly
      /// using the provided filtering criteria. See the
      /// DBObjectFilter class and the Where<T, TCriteria>()
      /// extension method for details on the use of this
      /// overload.
      /// 
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <typeparam name="TCriteria"></typeparam>
      /// <param name="keySelector"></param>
      /// <param name="predicate"></param>
      /// <returns></returns>

      public IFilteredEnumerable<T, TCriteria> GetModelSpaceObjects<T, TCriteria>(
            Expression<Func<T, ObjectId>> keySelector,
            Expression<Func<TCriteria, bool>> predicate)
         where T : Entity 
         where TCriteria: DBObject
      {
         Assert.IsNotNull(keySelector, nameof(keySelector));
         Assert.IsNotNull(predicate, nameof(predicate));
         return ThisDb.GetModelSpaceObjects<T>(this, OpenMode.ForRead, false, false)
            .WhereBy<T, TCriteria>(keySelector, predicate);
      }

      /// <summary>
      /// Opens model space objects of the specified generic
      /// argument type for read, filtered by the specified 
      /// IFilter<T>. 
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="filter">An object that implements IFilter<T>,
      /// (such as DBObjectFilter), that is used to constrain the
      /// elements produced by this method.</param>
      /// <returns>A sequence of entities that 
      /// satisfy the filter criteria</returns>

      public IEnumerable<T> GetModelSpaceObjects<T>(IFilter<T> filter) where T:Entity
      {
         Assert.IsNotNull(filter, nameof(filter));
         return ThisDb.GetModelSpaceObjects<T>(this, OpenMode.ForRead, false, false)
            .Where(filter.Predicate);
      }

      public IEnumerable<Entity> GetModelSpaceEntities(
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false)
      {
         return ThisDb.GetModelSpaceObjects<Entity>(this, mode, exact, openLocked);
      }

      /// <summary>
      /// Returns a sequence of entities from the current space 
      /// (which could be model space, a paper space layout, or 
      /// a block that is open in the block editor). 
      /// 
      /// The type of the generic argument is used to filter the 
      /// types of entities that are produced. The non-generic 
      /// overload that follows returns all entities in the current 
      /// space.
      /// </summary>

      public IEnumerable<T> GetObjects<T>(
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         return ThisDb.GetObjects<T>(this, mode, exact, openLocked);
      }

      public IEnumerable<T> GetObjects<T, TCriteria>(
         Expression<Func<T, ObjectId>> keySelector,
         Expression<Func<TCriteria, bool>> predicate)
         where T : Entity
         where TCriteria : DBObject
      {
         return ThisDb.GetObjects<T>(this)
            .WhereBy<T, TCriteria>(keySelector, predicate);
      }

      public IEnumerable<T> GetObjects<T>(IFilter<T> filter) where T : Entity
      {
         Assert.IsNotNull(filter, nameof(filter));
         return ThisDb.GetObjects<T>(this, OpenMode.ForRead, false, false)
            .Where(filter.Predicate);
      }


      public IEnumerable<Entity> GetEntities(
         OpenMode mode = OpenMode.ForRead,
         bool openLocked = false)
      {
         return ThisDb.GetObjects<Entity>(this, mode, false, openLocked);
      }

      public IEnumerable<T> GetPaperSpaceObjects<T>(
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         return ThisDb.GetPaperSpaceObjects<T>(this, mode, exact, openLocked);
      }

      public Layout GetLayout(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetLayout(name, this, mode, throwIfNotFound);
      }

      public BlockTableRecord GetBlock(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<BlockTableRecord>(name, this, mode, throwIfNotFound);
      }

      public BlockTableRecord GetModelSpaceBlock(OpenMode mode = OpenMode.ForRead)
      {
         return GetObject<BlockTableRecord>(ThisDb.GetModelSpaceBlockId(), mode);
      }

      public BlockTableRecord GetCurrentSpaceBlock(OpenMode mode = OpenMode.ForRead)
      {
         return GetObject<BlockTableRecord>(ThisDb.CurrentSpaceId, mode);
      }

      public LayerTableRecord GetLayer(string name, 
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<LayerTableRecord>(name, this, mode, throwIfNotFound);
      }

      public LinetypeTableRecord GetLinetype(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<LinetypeTableRecord>(name, this, mode, throwIfNotFound);
      }

      public ViewportTableRecord GetViewportTableRecord(Func<ViewportTableRecord, bool> predicate,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<ViewportTableRecord>(predicate, this, mode);
      }

      public ViewportTableRecord GetViewportTableRecord(int vpnum,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<ViewportTableRecord>(vptr => vptr.Number == vpnum, this, mode);
      }

      public ViewTableRecord GetView(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<ViewTableRecord>(name, this, mode, throwIfNotFound);
      }

      public DimStyleTableRecord GetDimStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<DimStyleTableRecord>(name, this, mode, throwIfNotFound);
      }

      public RegAppTableRecord GetRegApp(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<RegAppTableRecord>(name, this, mode, throwIfNotFound);
      }

      public TextStyleTableRecord GetTextStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<TextStyleTableRecord>(name, this, mode, throwIfNotFound);
      }

      public UcsTableRecord GetUcs(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetRecord<UcsTableRecord>(name, this, mode, throwIfNotFound);
      }

      public T GetNamedObject<T>(string key,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = true) where T : DBObject
      {
         return ThisDb.GetNamedObject<T>(key, this, mode, throwIfNotFound);
      }

      public IEnumerable<T> GetNamedObjects<T>(OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         return ThisDb.GetNamedObjects<T>(this, mode);
      }

      public T GetRecord<T>(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false) where T : SymbolTableRecord
      {
         return ThisDb.GetRecord<T>(name, this, mode, throwIfNotFound);
      }

      public ObjectId GetRecordId(SymbolTable table, string key)
      {
         if(table.Has(key))
         {
            try
            {
               return table[key];
            }
            catch(AcRx.Exception)
            {
            }
         }
         return ObjectId.Null;
      }

      public T GetRecord<T>(Func<T, bool> predicate,
         OpenMode mode = OpenMode.ForRead) where T : SymbolTableRecord
      {
         return ThisDb.GetRecord<T>(predicate, this, mode);
      }

      public ObjectId GetLayoutId(string layoutName, bool throwIfNotFound = false)
      {
         return ThisDb.GetLayoutId(layoutName, throwIfNotFound);
      }

      public ObjectId GetDictionaryEntryId<T>(string key, bool throwIfNotFound = false)
         where T : DBObject
      {
         return ThisDb.GetDictionaryEntryId<T>(key, throwIfNotFound);
      }

      public ObjectId GetDictionaryEntryId<T>(Func<T, bool> predicate)
         where T : DBObject
      {
         return ThisDb.GetDictionaryEntryId<T>(predicate);
      }

      public IEnumerable<ObjectId> GetDictionaryEntryIds<T>(Func<T, bool> predicate) 
         where T : DBObject
      {
         return ThisDb.GetDictionaryEntryIds<T>(predicate);
      }

      public T GetDictionaryObject<T>(string key,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = true) where T : DBObject
      {
         return ThisDb.GetDictionaryObject<T>(key, this, mode, throwIfNotFound);
      }

      public T GetDictionaryObject<T>(Func<T, bool> predicate,
         OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         return ThisDb.GetDictionaryObject<T>(this, predicate, mode);
      }

      public IEnumerable<T> GetDictionaryObjects<T>(OpenMode mode = OpenMode.ForRead) 
         where T : DBObject
      {
         return ThisDb.GetDictionaryObjects<T>(this, mode);
      }

      public Group GetGroup(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetGroup(name, this, mode, throwIfNotFound);
      }

      public DataLink GetDataLink(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetDataLink(name, this, mode, throwIfNotFound);
      }

      public DetailViewStyle GetDetailViewStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetDetailViewStyle(name, this, mode, throwIfNotFound);
      }

      public SectionViewStyle GetSectionViewStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetSectionViewStyle(name, this, mode, throwIfNotFound);
      }

      public MLeaderStyle GetMLeaderStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetMLeaderStyle(name, this, mode, throwIfNotFound);
      }

      public TableStyle GetTableStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetTableStyle(name, this, mode, throwIfNotFound);
      }

      public PlotSettings GetPlotSettings(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetPlotSettings(name, this, mode, throwIfNotFound);
      }

      public DBVisualStyle GetVisualStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetVisualStyle(name, this, mode, throwIfNotFound);
      }

      public Material GetMaterial(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetMaterial(name, this, mode, throwIfNotFound);
      }

      public MlineStyle GetMlineStyle(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetMlineStyle(name, this, mode, throwIfNotFound);
      }

      public Layout GetLayoutByKey(string name,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return ThisDb.GetLayoutByKey(name, this, mode, throwIfNotFound);
      }

      public IEnumerable<BlockTableRecord> GetLayoutBlocks(
         OpenMode mode = OpenMode.ForRead,
         bool includingModelSpace = false)
      {
         return ThisDb.GetLayoutBlocks(this, mode, includingModelSpace);
      }

      public IEnumerable<BlockTableRecord> GetLayoutBlocks(params string[] names)
      {
         return GetLayoutBlockIds(names).GetObjects<BlockTableRecord>(this);
      }

      public IEnumerable<BlockTableRecord> GetLayoutBlocksMatching(string pattern)
      {
         return GetLayoutBlockIdsMatching(pattern).GetObjects<BlockTableRecord>(this);
      }

      public IEnumerable<ObjectId> GetLayoutBlockIds(params string[] layoutNames)
      {
         return ThisDb.GetLayoutBlockIds(layoutNames);
      }

      public IEnumerable<ObjectId> GetLayoutBlockIdsMatching(string pattern)
      {
         return ThisDb.GetLayoutBlockIdsMatching(pattern);
      }

      public IEnumerable<BlockReference> GetBlockReferences(string pattern,
         OpenMode mode = OpenMode.ForRead,
         Func<BlockTableRecord, bool> predicate = null)
      {
         return ThisDb.GetBlockReferences(pattern, this, mode, predicate);
      }

      /// <summary>
      /// The current space and model space BlockTableRecords
      /// open for read:
      /// </summary>
      public BlockTableRecord CurrentSpace => GetCurrentSpaceBlock();
      public BlockTableRecord ModelSpace => GetModelSpaceBlock();

      /// <summary>
      /// SymbolTables expressed as Properties (opened for read)
      /// </summary>

      public BlockTable BlockTable => GetObject<BlockTable>(ThisDb.BlockTableId);
      public LayerTable LayerTable => GetObject<LayerTable>(ThisDb.LayerTableId);
      public LinetypeTable LinetypeTable => GetObject<LinetypeTable>(ThisDb.LinetypeTableId);
      public ViewportTable ViewportTable => GetObject<ViewportTable>(ThisDb.ViewportTableId);
      public ViewTable ViewTable => GetObject<ViewTable>(ThisDb.ViewTableId);
      public DimStyleTable DimStyleTable => GetObject<DimStyleTable>(ThisDb.DimStyleTableId);
      public RegAppTable RegAppTable => GetObject<RegAppTable>(ThisDb.RegAppTableId);
      public TextStyleTable TextStyleTable => GetObject<TextStyleTable>(ThisDb.TextStyleTableId);
      public UcsTable UcsTable => GetObject<UcsTable>(ThisDb.UcsTableId);

      /// <summary>
      /// Built-in/Predefined Dictionaries expressed as Properties (opened for read)
      /// </summary>
      public DBDictionary GroupDictionary => GetObject<DBDictionary>(ThisDb.GroupDictionaryId);
      public DBDictionary DataLinkDictionary => GetObject<DBDictionary>(ThisDb.DataLinkDictionaryId);
      public DBDictionary DetailViewStyleDictionary => GetObject<DBDictionary>(ThisDb.DetailViewStyleDictionaryId);
      public DBDictionary SectionViewStyleDictionary => GetObject<DBDictionary>(ThisDb.SectionViewStyleDictionaryId);
      public DBDictionary MLeaderStyleDictionary => GetObject<DBDictionary>(ThisDb.MLeaderStyleDictionaryId);
      public DBDictionary TableStyleDictionary => GetObject<DBDictionary>(ThisDb.TableStyleDictionaryId);
      public DBDictionary PlotSettingsDictionary => GetObject<DBDictionary>(ThisDb.PlotSettingsDictionaryId);
      public DBDictionary VisualStyleDictionary => GetObject<DBDictionary>(ThisDb.VisualStyleDictionaryId);
      public DBDictionary MaterialDictionary => GetObject<DBDictionary>(ThisDb.MaterialDictionaryId);
      public DBDictionary LayoutDictionary => GetObject<DBDictionary>(ThisDb.LayoutDictionaryId);
      public DBDictionary MLStyleDictionary => GetObject<DBDictionary>(ThisDb.MLStyleDictionaryId);
      public DBDictionary NamedObjectsDictionary => GetObject<DBDictionary>(ThisDb.NamedObjectsDictionaryId);


      /// SymbolUtilityServices methods expressed as instance properties:

      public ObjectId CurrentSpaceId => ThisDb.CurrentSpaceId;
      public ObjectId ModelSpaceBlockId => 
         SymbolUtilityServices.GetBlockModelSpaceId(ThisDb);
      public ObjectId PaperSpaceBlockId => 
         SymbolUtilityServices.GetBlockPaperSpaceId(ThisDb);
      public ObjectId ByBlockLinetype => ThisDb.ByBlockLinetype;
      public ObjectId ByLayerLinetype => ThisDb.ByLayerLinetype;
      public ObjectId ContinuousLinetype => ThisDb.ContinuousLinetype;
      public ObjectId RegAppAcadId => 
         SymbolUtilityServices.GetRegAppAcadId(ThisDb);
      public ObjectId TextStyleStandardId => 
         SymbolUtilityServices.GetTextStyleStandardId(ThisDb);
      public ObjectId LayerDefpointsId => 
         SymbolUtilityServices.GetLayerDefpointsId(ThisDb);
      public ObjectId LayerZeroId => ThisDb.LayerZero;
      public bool IsCompatibilityMode => 
         SymbolUtilityServices.IsCompatibilityMode(ThisDb);


   }

}




