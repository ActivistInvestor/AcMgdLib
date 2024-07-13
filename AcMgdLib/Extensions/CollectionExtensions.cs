/// CollectionExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// A collection of old helper APIs that provide 
/// support for accessing/querying the contents 
/// of AutoCAD Databases using LINQ.
/// 
/// A few changes have been made along the way, since 
/// this library was first written (which happened over
/// the period of several years). 
/// 
/// Some of those revisions require C# 7.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// Notes: Many of the extension methods included in
   /// this code base do not deal gracefully with attempts 
   /// to open entities that reside on locked layers for 
   /// write. 
   /// 
   /// So, in adherence to the best practice of not opening
   /// DBObjects for write unless it is predetermined that
   /// they will be modified, most of the methods included
   /// herein will by-default, open objects for read, and
   /// require a caller to subsequently upgrade their open
   /// state to write as needed.
   /// 
   /// The recommended practice for Entities is to open them
   /// for read, and then determine if they can or should be 
   /// upgraded to OpenMode.ForWrite, based on whether the 
   /// referenced layer is locked, and/or specifics of the 
   /// particular use case, and then upgrade their open state
   /// to OpenMode.ForWrite.
   /// 
   /// The included UpgradeOpen<T>() method can be used 
   /// with a transaction to forcibly-upgrade an entity's 
   /// open mode to write, even if the entity resides on 
   /// a locked layer. The DBObject.UpgradeOpen() method
   /// does't support upgrading an object's open state to 
   /// write if the object is on a locked layer, and will 
   /// throw an exception in that case. The UpgradeOpen()
   /// extension method included herein circumvents that
   /// problem using a Transaction.
   /// 
   /// Erased objects:
   /// 
   /// These extensions do not provide options for opening
   /// erased objects in most cases, as doing that is not a
   /// routine operation performed by application code, and
   /// is done only in rare, highly-specialized use cases.
   /// 
   /// No Linq Library Code:
   /// 
   /// While this library is designed to serve Linq-based
   /// applications, it specifically avoids the use of Linq
   /// internally (but not entirely), because Linq does not
   /// perform as well as language constructs like for(), 
   /// foreach(), etc.
   /// </summary>

   public static partial class CollectionExtensions
   {
      /// <summary>
      /// Internal worker to which members of the       
      /// GetObjects() family of extension methods 
      /// internally delegate to.
      /// 
      /// Notes:
      /// 
      /// This method is not public, because it can be
      /// invoked on any IEnumerable, which cannot be
      /// allowed, as only certain types that implement
      /// that interface enumerate ObjectIds (this goes
      /// way back to Autodesk's failure to update those 
      /// types to the generic IEnumerable<T> interface
      /// after it was introduced in .NET 2.0).
      /// 
      /// To avoid unnecessary unboxing, the core method
      /// that most of the public GetObjects() extension
      /// methods delegates to goes to great lengths to
      /// to coerce its IEnumerable input to a strongly-
      /// typed collection or enumerable, allowing it to 
      /// avoid the expensive unboxing that is inherent 
      /// to the IEnumerable interface.
      /// 
      /// The following parameter documentation for this 
      /// core method applies to all public GetObjects() 
      /// extension methods that ultimately delegate to 
      /// this method.
      /// </summary>
      /// <typeparam name="T">The type of DBObject to enumerate</typeparam>
      /// that appear in the source</typeparam>
      /// <param name="source">An object that enumerates ObjectIds</param>
      /// <param name="trans">The transaction to use in the operation</param>
      /// <param name="mode">The OpenMode to open objects with</param>
      /// <param name="exact">A value indicating if enumerated objects must 
      /// be the exact type of the non-abstract generic argument (true), or 
      /// can be any type derived from the generic argument (false)</param>
      /// <param name="openLocked">A value indicating if entities on locked
      /// layers should be opened for write.</param>
      /// <returns>A sequence of opened DBObjects</returns>
      /// <exception cref="ArgumentNullException"></exception>

      static IEnumerable<T> GetObjectsCore<T>(
         this IEnumerable source,
         Transaction trans,
         OpenMode mode,
         bool exact = false,
         bool openErased = false,
         bool openLocked = false) where T : DBObject
      {
         source.TryCheckTransaction(trans);
         openLocked &= mode == OpenMode.ForWrite;
         bool flag = typeof(T) == typeof(DBObject);
         Func<ObjectId, bool> predicate = flag ? id => true : RXClass<T>.GetIdPredicate(exact);

         /// The following code goes to great extents to
         /// take an optimized path, based on whether the 
         /// source has an indexer and/or is a strongly-
         /// typed IEnumerable, and only falls back to 
         /// enumerating a non-generic IEnumerable if all 
         /// else fails.

         if(source is ObjectIdCollection ids)
         {
            int len = ids.Count;
            for(int i = 0; i < len; i++)
            {
               ObjectId id = ids[i];
               if(flag || predicate(id))
               {
                  yield return (T)trans.GetObject(id, mode, openErased, openLocked);
               }
            }
         }
         else if(source is ObjectId[] array)
         {
            int len = array.Length;
            for(int i = 0; i < len; i++)
            {
               ObjectId id = array[i];
               if(flag || predicate(id))
               {
                  yield return (T)trans.GetObject(id, mode, openErased, openLocked);
               }
            }
         }
         else if(source is IList<ObjectId> list)
         {
            int len = list.Count;
            for(int i = 0; i < len; i++)
            {
               ObjectId id = list[i];
               if(flag || predicate(id))
               {
                  yield return (T)trans.GetObject(id, mode, openErased, openLocked);
               }
            }
         }
         else if(source is IEnumerable<ObjectId> enumerable)
         {
            foreach(ObjectId id in enumerable)
            {
               if(flag || predicate(id))
               {
                  yield return (T)trans.GetObject(id, mode, openErased, openLocked);
               }
            }
         }
         else  // fallback to least-preferred path with greatest overhead (unboxing)
         {
            /// Optimize for getting all entities from a BlockTableRecord:
            if(source is BlockTableRecord btr && typeof(T) == typeof(Entity))
            {
               foreach(ObjectId id in btr)
               {
                  yield return (T)trans.GetObject(id, mode, openErased, openLocked);
               }
            }
            else
            {
               foreach(ObjectId id in source)
               {
                  if(flag || predicate(id))
                  {
                     yield return (T)trans.GetObject(id, mode, openErased, openLocked);
                  }
               }
            }
         }
      }

      /// Public GetObjects<T>() overloads:

      /// <summary>
      /// BlockTableRecord:
      /// 
      /// Enumerates all or a subset of the entities in a
      /// block's definition. The generic argument type
      /// is used to both constrain the resulting items
      /// to only those that are instances of the generic
      /// argument, and to cast the enumerated elements to
      /// that same type. To get all entities in a block's
      /// definition, use Entity as the generic argument.
      /// 
      /// Testing of each element's type is done against the
      /// runtime class of each source ObjectId to avoid the
      /// needless creation of managed wrappers for elements
      /// that are not enumerated.
      /// </summary>
      /// <typeparam name="T">Entity or any type derived from 
      /// Entity</typeparam>
      /// <param name="source">The BlockTableRecord from
      /// which to retrieve the entities from.</param>
      /// 
      /// See the GetObjectsCore() method for a desription 
      /// of all other parameters.

      public static IEnumerable<T> GetObjects<T>(this BlockTableRecord source,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         return GetObjectsCore<T>(source, trans, mode, exact, false, openLocked);
      }

      /// <summary>
      /// ObjectIdCollection:
      /// 
      /// A version of GetObjects() targeting ObjectIdCollection,
      /// that enumerates a subset of DBObjects represented by the 
      /// ObjectIds in the source collection. 
      /// 
      /// Only the subset of elements representing instances of the 
      /// generic argument are enumerated.
      /// </summary>
      /// <param name="source">The ObjectIdCollection containing the
      /// ObjectIds of the objects to be opened and returned</param>
      /// 
      /// See the GetObjectsCore() method for a desription of 
      /// all other parameters.

      public static IEnumerable<T> GetObjects<T>(this ObjectIdCollection source,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead,
            bool exact = false,
            bool openLocked = false)
         where T : DBObject
      {
         return GetObjectsCore<T>(source, trans, mode, exact, false, openLocked);
      }

      /// <summary>
      /// Can be used in lieu of GetObjects() when it can be 
      /// assumed that all elements in the ObjectIdCollection 
      /// represent an Entity or a type derived from same, and
      /// the enumerated objects should be of that type.
      /// </summary>

      public static IEnumerable<T> GetEntities<T>(this ObjectIdCollection source,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool openLocked = false) where T : Entity
      {
         return GetObjectsCore<T>(source, trans, mode, false, openLocked);
      }

      /// <summary>
      /// IEnumerable<ObjectId>:
      /// 
      /// A version of GetObjects() targeting IEnumerable<ObjectId>
      /// that enumerates the DBObjects represented by the ObjectIds
      /// in the source sequence represeting instances of the generic 
      /// argument. This method applies to arrays of ObjectId as well.
      /// </summary>
      /// <parm name="ids">The sequence of ObjectIds representing
      /// the entities that are to be opened and returned</parm>
      /// 
      /// See the GetObjectsCore() method for a description 
      /// of all other parameters.

      public static IEnumerable<T> GetObjects<T>(this IEnumerable<ObjectId> source,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead,
            bool exact = false,
            bool openLocked = false) where T : DBObject
      {
         return GetObjectsCore<T>(source, trans, mode, exact, false, openLocked);
      }

      /// <summary>
      /// The GetEntities() variant of the above that can be used 
      /// when it can be assumed that all elements in the source 
      /// sequence represent an Entity or a type derived from same, 
      /// and the type of the resulting sequence is Entity.
      /// </summary>

      public static IEnumerable<T> GetEntities<T>(this IEnumerable<ObjectId> source,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool openLocked = false) where T : Entity
      {
         return GetObjectsCore<T>(source, trans, mode, false, false, openLocked);
      }

      /// <summary>
      /// SymbolTable:
      /// 
      /// An overload of GetObjects() that targets SymbolTables,
      /// and enumerates their SymbolTableRecords.
      /// 
      /// To include erased entries in the result, invoke this
      /// method on the value of a SymbolTable's IncludingErased 
      /// property.
      /// </summary>
      /// <param name="source">The SymbolTable whose contents are
      /// to be opened and enumerated</param>
      /// 
      /// See the GetObjectsCore() method for a description 
      /// of all other parameters.

      public static IEnumerable<T> GetObjects<T>(this SymbolTable source,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead) where T : SymbolTableRecord
      {
         Assert.IsNotNullOrDisposed(source, nameof(source));
         return GetObjectsCore<T>(source, trans, mode, false, true, false);
      }

      /// An internal-use-only version of GetObjects<T> that
      /// targets IEnumerable:
      
      internal static IEnumerable<T> GetObjectsOfType<T>(this IEnumerable source,
         Transaction trans,
         bool exact = false,
         OpenMode mode = OpenMode.ForRead,
         bool openErased = false,
         bool openLocked = false) where T: DBObject
      {
         return GetObjectsCore<T>(source, trans, mode, exact, openErased, openLocked);
      }

      /// <summary>
      /// Gets and opens an object of the specified generic
      /// argument type, having the given key from the target
      /// DBObject's extension dictionary. 
      /// 
      /// If <paramref name="create"/> is true, it applies to 
      /// <em>both</em> the extension dictionary and the entry.
      /// E.g., an owner with no existing extension dictionary 
      /// will have a new extension dictionary added, along with 
      /// a new instance of the entry type (T) added to that.
      /// 
      /// The type used as the generic argument must have a
      /// public, parameterless constructor. See the overload
      /// below for a way to use this method without requiring
      /// the generic argument to have a public, parameterless
      /// constructor.
      /// 
      /// Prior to comitting the transaction, callers can check 
      /// the IsNewObject property of the result to determine if 
      /// it was created and added by this API or already existed.
      /// </summary>
      /// <param name="owner">The DBObject to get the value from</param>
      /// <param name="key">The dictionary key of the requested value</param>
      /// <param name="create">A value indicating if a new entry 
      /// should be created and added to the extension dictionary 
      /// if an entry with the given key does not exist.</param>
      /// <returns>The requested value</returns>

      public static T GetDictionaryObject<T>(this DBObject owner,
            string key,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead,
            bool create = false) where T : DBObject, new()
      {
         return GetDictionaryObject<T>(owner, key, trans,
            (create ? () => new T() : (Func<T>)null), mode);
      }

      /// <summary>
      /// Overloaded version of GetDictionaryObject() that
      /// differs from the above in the following way:
      /// 
      /// A caller-supplied delegate is used to create a
      /// new entry, which means the generic argument does 
      /// not require a public parameterless constructor.
      /// 
      /// The delegate can create and initialize the new
      /// entry as needed, not requiring that to be done
      /// after-the-fact.
      /// </summary>

      public static T GetDictionaryObject<T>(this DBObject owner,
         string key,
         Transaction trans,
         Func<T> factory,
         OpenMode mode = OpenMode.ForRead)  where T : DBObject
      {
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         Assert.IsNotNullOrWhiteSpace(key, nameof(key));
         bool create = factory != null;
         ObjectId xDictId = owner.ExtensionDictionary;
         bool exists = xDictId.IsA<DBDictionary>();
         if(!exists && factory == null)
            return null;
         var dict = owner.GetExtensionDictionary(trans, OpenMode.ForRead, create);
         if(dict == null)
            throw new InvalidOperationException("Failed to get or create extension dictionary");
         if(exists && dict.Contains(key))
         {
            ObjectId id = dict.GetAt(key);
            if(!id.IsA<T>())
               throw new ArgumentException(
                  $"Value at key {key} is not an instance of {typeof(T).Name}");
            return trans.GetObject<T>(id, mode);
         }
         T result = null;
         if(factory != null)
         {
            result = factory();
            dict.UpgradeOpen();
            dict.SetAt(key, result);
            trans.AddNewlyCreatedDBObject(result, true);
         }
         return result;
      }

      /// <summary>
      /// Opens and returns an existing Xrecord having the
      /// given key from the owner's extension dictionary, 
      /// or creates a new extension dictionary (if one does 
      /// not exist), and adds a new Xrecord to it.
      /// 
      /// Objects to be added to an extension dictionary must 
      /// have a public, parameterless constructor.
      /// 
      /// Important: If create is true, this value applies to 
      /// <em>both</em> the extension dictionary and the xrecord. 
      /// E.g., an object with no existing extension dictionary 
      /// will have a new extension dictionary added, along with 
      /// a new Xrecord added to that.
      /// 
      /// If <paramref name="create"/> is false and the owner has 
      /// no existing extension dictionary, this method returns
      /// null without adding an extension dictionary.
      /// </summary>
      /// <param name="owner">The DBObject to get the result from</param>
      /// <param name="key">The key of the result</param>
      /// <param name="trans">The transaction to use to open the result</param>
      /// <param name="mode">The OpenMode to use to open the result</param>
      /// <param name="create">A value indicating if a new
      /// entry should be created and added to the dictionary if an
      /// existing item with the given key does not exist.</param>
      /// <returns>A new or existing xrecord having the given key.</returns>

      public static Xrecord GetXrecord(this DBObject owner,
            string key,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead,
            bool create = false)
      {
         return owner.GetDictionaryObject<Xrecord>(key, trans, mode, create);
      }

      /// <summary>
      /// Opens and returns the owner's extension dictionary, and 
      /// optionally adds and returns a new extension dictionary if
      /// one does not exist.
      /// </summary>
      /// <param name="owner">The DBObject to get the result from</param>
      /// <param name="trans">The transaction to use to open the result</param>
      /// <param name="mode">The OpenMode to use to open the result</param>
      /// <param name="create">A value indicating if a new
      /// extension dictionary should be created and added to the owner
      /// if an existing extension dictionary does not exist.</param>
      /// <returns>The owner's extension dictionary or null if one
      /// does not exist and <paramref name="create"/> is false.</returns>

      public static DBDictionary GetExtensionDictionary(this DBObject owner, Transaction trans, OpenMode mode = OpenMode.ForRead, bool create = false)
      {
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         owner.TryCheckTransaction(trans);
         ObjectId id = owner.ExtensionDictionary;
         if(id.IsNull && create)
         {
            if(!owner.IsWriteEnabled)
               owner.UpgradeOpen();
            owner.CreateExtensionDictionary();
            id = owner.ExtensionDictionary;
         }
         return id.IsA<DBDictionary>() ? trans.GetObject<DBDictionary>(id, mode) : null;
      }

      /// <summary>
      /// Returns a sequence of SymbolTableRecord-based types
      /// from a SymbolTable in the given Database, where the 
      /// SymbolTable whose elements are returned is determined 
      /// by the generic argument type. 
      /// 
      /// For example, to get LayerTableRecords from the layer 
      /// table, specify LayerTableRecord as the generic argument. 
      /// To get LineTypeTableRecords from the LinetypeTable, use
      /// LinetypeTableRecord as the generic argument, etc. The
      /// underlying API will determine which SymbolTable should
      /// be accessed based on the generic argument.
      /// 
      /// To include erased SymbolTableRecords in the result, invoke 
      /// this method on the value of the SymbolTable's IncludingErased 
      /// property.
      /// </summary>
      /// <typeparam name="T">The type of SymbolTableRecord to be
      /// returned, which also determines which SymbolTable is to 
      /// have its entries retieved. The generic argument must be 
      /// a concrete type derived from the SymbolTableRecord type.</typeparam>
      /// <param name="db">The Database to access</param>
      /// <param name="trans">The transaction to use for the operation</param>
      /// <param name="mode">The OpenMode to open resulting objects in
      /// (default: OpenMode.ForRead)</param>
      /// <returns>A sequence of SymbolTableRecord-based elements</returns>

      public static IEnumerable<T> GetSymbolTableRecords<T>(this Database db,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead) where T : SymbolTableRecord
      {
         return db.GetSymbolTable<T>(trans)
            .GetObjectsCore<T>(trans, mode, false, true);
      }

      /// <summary>
      /// An overload of GetObjects() that targets DBDictionary.
      /// This method opens and enumerates the objects referenced
      /// by each dictionary entry's value, that are of the type
      /// of the generic argument or a derived type. As such, it
      /// can return all dictionary objects, or a subset thereof.
      /// 
      /// For example, to retrieve only Xrecords from a DBDictionary,
      /// and ignore any other type of object, use:
      /// 
      ///    someDBDictionary.GetObjects<Xrecord>(....)
      ///    
      /// </summary>
      /// <typeparam name="T">The type of the elements to retrieve</typeparam>
      /// <param name="source">The DBDictionary whose elements
      /// are to be retrieved</param>
      /// 
      /// See the GetObjectsCore() method for a description 
      /// of all other parameters.

      public static IEnumerable<T> GetObjects<T>(this DBDictionary source,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         Assert.IsNotNullOrDisposed(source, nameof(source));
         return source.Cast<DictionaryEntry>()
            .Select(e => e.Value)
            .GetObjectsCore<T>(trans, mode, false, true);
      }

      /// <summary>
      /// Gets the ObjectIds of all values in the given DBDictionary
      /// that reference objects of the type of the generic argument.
      /// 
      /// To get erased entries, invoke this method on the result of 
      /// the DBDictionary's IncludingErased property.
      /// </summary>
      /// <param name="source">The DBDictionary to access</param>
      /// <param name="includingErased">A value indicating if erased
      /// elements should be included.</param>
      /// <returns>An object that enumerates the ObjectIds of the
      /// entry values in the DBDictionary</returns>

      public static IEnumerable<ObjectId> GetObjectIds<T>(this DBDictionary source, bool includingErased = false)
         where T : DBObject
      {
         Assert.IsNotNullOrDisposed(source, nameof(source));
         Func<ObjectId, bool> func = RXClass<T>.MatchId;
         foreach(DBDictionaryEntry e in source)
         {
            if(func(e.Value))
               yield return e.Value;
         }
      }

      /// <summary>
      /// Upgrades the OpenMode of a sequence of DBObjects to
      /// OpenMode.ForWrite. If a Transaction is provided, the
      /// objects are upgraded using the Transaction, otherwise
      /// the objects are upgraded using UpgradeOpen(). When a
      /// transaction is provided, the objects are upgraded to
      /// OpenMode.ForWrite <em>even if they are entities that 
      /// reside on locked layers</em>.
      /// </summary>
      /// <typeparam name="T">The type of the elements in the
      /// output and resulting sequences</typeparam>
      /// <param name="source">The input sequence of DBObjects</param>
      /// <param name="trans">The transaction to use in the operation.
      /// If a Transaction is provided, the objects will be upgraded
      /// to OpenMode.ForWrite even if they are entities residing on
      /// a locked layer</param>
      /// <returns>The input sequence upgraded to OpenMode.ForWrite</returns>

      public static IEnumerable<T> UpgradeOpen<T>(this IEnumerable<T> source,
         Transaction trans = null,
         bool openOnLockedLayers = true) where T : DBObject
      {
         Assert.IsNotNull(source, nameof(source));
         if(trans != null)
         {
            Assert.IsNotNullOrDisposed(trans, nameof(trans));
            foreach(T obj in source)
            {
               Assert.IsNotNullOrDisposed(obj, nameof(obj));
               if(!obj.IsWriteEnabled)
                  trans.GetObject(obj.ObjectId, OpenMode.ForWrite, false, openOnLockedLayers);
               yield return obj;
            }
         }
         else
         {
            foreach(T obj in source)
            {
               Assert.IsNotNullOrDisposed(obj, nameof(obj));
               if(!obj.IsWriteEnabled)
                  obj.UpgradeOpen();
               yield return obj;
            }
         }
      }

      //internal static void CheckTransaction(this Database db, Transaction trans)
      //{
      //   if(db == null || db.IsDisposed)
      //      throw new ArgumentNullException(nameof(db));
      //   if(trans == null || trans.IsDisposed)
      //      throw new ArgumentNullException(nameof(trans));
      //   if(trans is OpenCloseTransaction)
      //      return;
      //   if(trans.GetType() != typeof(Transaction))
      //      return;   // can't perform this check without pulling in AcMgd/AcCoreMgd
      //   if(trans.TransactionManager != db.TransactionManager)
      //      throw new ArgumentException("Transaction not from this Database");
      //}

      //internal static void TryCheckTransaction(object source, Transaction trans)
      //{
      //   Assert.IsNotNull(source, nameof(source));
      //   Assert.IsNotNullOrDisposed(trans, nameof(trans));
      //   if(trans is OpenCloseTransaction)
      //      return;
      //   if(trans.GetType() != typeof(Transaction))
      //      return; // can't perform check without pulling in AcMgd/AcCoreMgd
      //   if(source is DBObject obj && obj.Database is Database db
      //         && trans.TransactionManager != db.TransactionManager)
      //      throw new ArgumentException("Transaction not from this Database");
      //}


   }

}



