/// DatabaseExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// High-level APIs that help simplify and streamline 
/// development of managed AutoCAD extensions.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// This class exposes high-level extension methods 
   /// targeting the Database class, and are built on top
   /// of methods of the CollectionExtensions class.
   /// 
   /// These methods are intended to simplify application 
   /// development by allowing the developer to focus on 
   /// the implementation details of the application, rather 
   /// than the implementation details of common operations.
   /// 
   /// In addition to these extensions to the Database, the
   /// DBTransaction and DocTransaction classes expose most
   /// of these same methods as instance methods that allow
   /// them to be used without having to pass a Transaction
   /// argument.
   /// 
   /// See the DBObjectFilterExample.cs file for examples
   /// that use the instance methods of the DocTransaction 
   /// in lieu of these extension methods of the Database
   /// class.
   /// </summary>

   public static partial class DatabaseExtensions
   {
      /// <summary>
      /// Returns a sequence of entities from the given database's
      /// model space. The type of the generic argument is used
      /// to constrain the type of entity that is produced. By
      /// default, any entity that is an instance of the generic
      /// argument type, or whose type is derived from same, is
      /// included in the resulting enumeration. The exact argument
      /// can be set to true, to include only entities of the type
      /// of the generic argument, but exclude derived types.
      /// 
      /// For example, to get all BlockReference objects from the
      /// drawing's model space, but not Table objects (which are
      /// derived from BlockReference), use BlockReference as the 
      /// generic argument, and pass true for the exact argument.
      /// </summary>
      /// <typeparam name="T">The type of entity to return</typeparam>
      /// <param name="db">The target Database</param>
      /// 
      /// See the DBObjectExtensions.GetObjectsOfType() method for 
      /// the desription of all other parameters.
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<T> GetModelSpaceObjects<T>(this Database db,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         db.CheckTransaction(trans);
         return db.GetModelSpaceBlockId()
            .GetObject<BlockTableRecord>(trans)
            .GetObjects<T>(trans, mode, exact, openLocked);
      }

      /// <summary>
      /// Non-generic implementation of the above that gets all
      /// entities from model space:
      /// </summary>

      public static IEnumerable<Entity> GetModelSpaceEntities(this Database db,
         Transaction tr,
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false)
      {
         return GetModelSpaceObjects<Entity>(db, tr, mode, exact, openLocked);
      }

      /// <summary>
      /// Returns a sequence of entities from the given database's
      /// current space (which could be model space, a paper space 
      /// layout, or a block that is open in the block editor). 
      /// 
      /// The type of the generic argument is used to filter the types 
      /// of entities that are produced. The non-generic overload that
      /// follows returns all entities in the current space.
      /// </summary>
      /// <typeparam name="T">The type of entity to return</typeparam>
      /// <param name="db">The target Database</param>
      /// 
      /// See the GetObjectsOfType() method for a desription of 
      /// all other parameters.
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<T> GetObjects<T>(this Database db,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         db.CheckTransaction(trans);
         return db.CurrentSpaceId.GetObject<BlockTableRecord>(trans)
            .GetObjects<T>(trans, mode, exact, openLocked);
      }

      public static IFilteredEnumerable<T, TCriteria> GetObjects<T, TCriteria>(
            this Database db,
            Transaction trans,
            Expression<Func<T, ObjectId>> keySelector,
            Expression<Func<TCriteria, bool>> predicate)
         where T : Entity
         where TCriteria : DBObject
      {
         return GetObjects<T>(db, trans).
            WhereBy<T, TCriteria>(keySelector, predicate); 
      }

      /// <summary>
      /// Non-generic version of GetObjects<T>() that
      /// enumerates all entities in the current space.
      /// </summary>

      public static IEnumerable<Entity> GetEntities(this Database db,
         Transaction tr,
         OpenMode mode = OpenMode.ForRead,
         bool openLocked = false)
      {
         return GetObjects<Entity>(db, tr, mode, false, openLocked);
      }

      /// <summary>
      /// Returns a sequence containing entities from 
      /// <em>ALL</em> paper space layouts.
      /// </summary>
      /// <typeparam name="T">The type of objects to enumerate</typeparam>
      /// <param name="db">The Database to obtain the objects from</param>
      /// 
      /// See the GetObjectsOfType() method for a desription of 
      /// all other parameters.

      public static IEnumerable<T> GetPaperSpaceObjects<T>(this Database db,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openLocked = false) where T : Entity
      {
         db.CheckTransaction(trans);
         return db.GetLayoutBlocks(trans)
            .SelectMany(btr => btr.GetObjects<T>(trans, mode, exact, openLocked));
      }

      /// <summary>
      /// Opens and returns the Layout having the specified name.
      /// If a layout with the given name does not exist, this 
      /// method returns null if throwIfNotFound is false. 
      /// Otherwise, it throws a KeyNotFoundException.
      /// </summary>
      /// <param name="db">The Database to get the Layout from</param>
      /// <param name="name">The name of the layout</param>
      /// <returns>The requested Layout object or null if no 
      /// layout was found with the given name.</returns>

      public static Layout GetLayout(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         var layout = db.GetDictionaryObjects<Layout>(trans, OpenMode.ForRead)
            .FirstOrDefault(_layout => _layout.LayoutName.IsEqualTo(name));
         if(layout != null && mode == OpenMode.ForWrite)
            layout.UpgradeOpen();
         return layout;
      }

      /// <summary>
      /// Opens and returns an existing BlockTableRecord having 
      /// the given name.
      /// 
      /// If a block having the given name does not exist, a 
      /// KeyNotFoundException is thrown if the throwIfNotFound 
      /// argument is true. Otherwise this method returns null. 
      /// This method does not return erased entries.
      /// <remarks>
      /// Note that this method simply delegates to the generic
      /// GetRecord<T>() method.
      /// </remarks>
      /// </summary>
      /// <param name="db">The Database to get the result from</param>
      /// <param name="name">The name of the block</param>
      /// <param name="trans">The Transaction to use in the operation</param>
      /// <param name="mode">The OpenMode to open the BlockTableRecord as</param>
      /// <param name="throwIfNotFound">A value indicating if an exception
      /// should be thrown if a block with the given name does not exist.</param>
      /// <returns>The requested BlockTableRecord or null if a block with
      /// the given name does not exist.</returns>
      /// <exception cref="KeyNotFoundException">A block with the given
      /// name was not found</exception>

      public static BlockTableRecord GetBlock(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<BlockTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      /// <summary>
      /// The following GetXxxxx() methods operate in the same way as the
      /// GetBlock() method, but return other types of SymbolTableRecords.
      /// </summary>

      public static LayerTableRecord GetLayer(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<LayerTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      public static LinetypeTableRecord GetLinetype(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<LinetypeTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      /// <summary>
      /// This deviates from the other methods in this family.
      /// Instead of a meaningless name, it accepts a predicate 
      /// function can be used to more-accuately select an entry.
      /// </summary>

      public static ViewportTableRecord GetViewportTableRecord(this Database db,
         Func<ViewportTableRecord, bool> predicate,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<ViewportTableRecord>(db, predicate, trans, mode);
      }

      /// <summary>
      /// Gets a ViewportTableRecord by viewport number:
      /// </summary>

      public static ViewportTableRecord GetViewportTableRecord(this Database db, int vpnum,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return db.GetRecord<ViewportTableRecord>(vptr => vptr.Number == vpnum, trans, mode);
      }

      public static ViewTableRecord GetView(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<ViewTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      public static DimStyleTableRecord GetDimStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<DimStyleTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      public static RegAppTableRecord GetRegApp(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<RegAppTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      public static TextStyleTableRecord GetTextStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<TextStyleTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      public static UcsTableRecord GetUcs(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetRecord<UcsTableRecord>(db, name, trans, mode, throwIfNotFound);
      }

      /// <summary>
      /// This method can get a named SymbolTableRecord or 
      /// an entry in a standard/built-in DBDictionary based
      /// on the generic argument type.
      /// 
      /// For example, 
      /// 
      /// To get Group named "Group1", use:
      /// 
      ///    myDb.GetNamedObject<Group>("Group1", trans);
      ///    
      /// To get a LayerTableRecord:
      /// 
      ///    myDb.GetNamedObject<LayerTableRecord>("Layer1", trans);
      ///    
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="db"></param>
      /// <param name="key"></param>
      /// <param name="trans"></param>
      /// <param name="mode"></param>
      /// <param name="throwIfNotFound"></param>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>
      /// <exception cref="KeyNotFoundException"></exception>

      public static T GetNamedObject<T>(this Database db,
         string key,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = true) where T : DBObject
      {
         if(typeof(SymbolTableRecord).IsAssignableFrom(typeof(T)))
         {
            var func = SymbolTableAccessors.GetAccessor(typeof(T));
            SymbolTable table = func(db).GetObject<SymbolTable>(trans);
            ObjectId id = GetRecordId(table, key);
            if(id.IsNull)
            {
               if(throwIfNotFound)
                  throw new KeyNotFoundException(key);
               return null;
            }
            return trans.GetObject<T>(id, mode);
         }
         return GetDictionaryObject<T>(db, key, trans, mode, throwIfNotFound);
      }

      /// <summary>
      /// This method returns the elements of a SymbolTable
      /// or a built-in, predefined DBDictionary. 
      /// 
      /// The generic argument defines both the collection whose 
      /// elements are being requested, as well as the enumerated 
      /// element type.
      /// 
      /// It can return SymbolTableRecords, or DBObjects referenced
      /// by entries in a predefined DBDictionary.
      /// 
      /// For example:
      /// 
      /// To get all LayerTableRecords from the LayerTable:
      /// 
      ///   myDatabase.GetNamedObjects<LayerTableRecord>(trans);
      ///   
      /// To get all Groups from the Group dictionary:
      /// 
      ///   myDatabase.GetNamedObjects<Group>(trans);
      /// 
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="db"></param>
      /// <param name="trans"></param>
      /// <param name="mode"></param>
      /// <returns></returns>

      public static IEnumerable<T> GetNamedObjects<T>(this Database db,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         if(typeof(SymbolTableRecord).IsAssignableFrom(typeof(T)))
         {
            var func = SymbolTableAccessors.GetAccessor(typeof(T));
            Assert.IsNotNull(func, nameof(func));
            return func(db).GetObject<SymbolTable>(trans)
               .GetObjectsOfType<T>(trans, true, mode);
         }
         return GetDictionaryObjects<T>(db, trans, mode);
      }

      /// <summary>
      /// Worker for the above GetXxxx() methods:
      /// 
      /// Gets the non-erased SymbolTableRecord having the 
      /// given name, cast to the type of the generic argument.
      /// 
      /// The generic argument type defines both the type of 
      /// the SymbolTableRecord to get, and the SymbolTable to 
      /// get it from.
      /// 
      /// If a non-erased entry is not found with the given
      /// name, a KeyNotFoundException will be thrown if the
      /// throwIfNotFound method is true. Otherwise, null is
      /// returned. The default for throwIfNotFound is false.
      /// 
      /// </summary>
      /// <typeparam name="T">The Type of the SymbolTableRecord
      /// to retrieve, which also determines which SymbolTable the
      /// record is retrieved from.</typeparam>
      /// <param name="db">The Database to get the result from</param>
      /// <param name="name">The name of the record</param>
      /// <param name="trans">The Transaction to use in the operation</param>
      /// <param name="mode">The OpenMode to open the result in</param>
      /// <param name="throwIfNotFound">A value indicating if an exception
      /// should be thrown if a non-erased entry with the given name does 
      /// not exist.</param>
      /// <returns>The requested SymbolTableRecord or null if an entry with
      /// the given name does not exist.</returns>
      /// <exception cref="KeyNotFoundException">A non-erased entry with the 
      /// given name does not exist</exception>
      /// <exception cref="KeyNotFoundException"></exception>

      public static T GetRecord<T>(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false) where T : SymbolTableRecord
      {
         ObjectId id = GetRecordId(db.GetSymbolTable<T>(trans), name);
         if(!id.IsNull)
            return trans.GetObject<T>(id, mode);
         if(throwIfNotFound)
            throw new KeyNotFoundException(name);
         return null;
      }

      static ObjectId GetRecordId(SymbolTable table, string key)
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

      /// <summary>
      /// Gets the first SymbolTableRecord from the table defined
      /// by the generic argument that meets the criteria defined
      /// by the caller-supplied predicate. If no entry in the
      /// symbol table satisfies the predicate, this method will
      /// return null.
      /// </summary>
      /// <typeparam name="T">The Type of the SymbolTableRecord
      /// to retrieve, which also determines which SymbolTable the
      /// record is retrieved from.</typeparam>
      /// <param name="db">The Database to get the result from</param>
      /// <param name="predicate">A function that takes an instance
      /// of the generic argument type and returns a value indicating
      /// if the argument should be returned by this method.</param>
      /// <param name="trans">The Transaction to use in the operation</param>
      /// <param name="mode">The OpenMode to open the result in</param>
      /// <returns>The first entry in the symbol table that meets the 
      /// specified criteria, or null otherwise.</returns>

      public static T GetRecord<T>(this Database db,
         Func<T, bool> predicate,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead) where T : SymbolTableRecord
      {
         Assert.IsNotNull(predicate, nameof(predicate));
         db.CheckTransaction(trans);
         var result = db.GetSymbolTableRecords<T>(trans, OpenMode.ForRead)
            .FirstOrDefault(predicate);
         if(result != null && mode == OpenMode.ForWrite)
            result.UpgradeOpen();
         return result;
      }

      /// <summary>
      /// Gets the ObjectId of a Layout given its LayoutName
      /// </summary>

      public static ObjectId GetLayoutId(this Database db, string layoutName, bool throwIfNotFound = false)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         if(string.IsNullOrWhiteSpace(layoutName))
            throw new ArgumentException(nameof(layoutName));
         using(var tr = new ReadOnlyTransaction())
         {
            var layout = db.GetDictionaryObject<Layout>(
               tr, lo => lo.LayoutName.IsEqualTo(layoutName));
            if(layout != null)
               return layout.ObjectId;
            if(throwIfNotFound)
               throw new KeyNotFoundException(layoutName);
            return ObjectId.Null;
         }
      }

      /// <summary>
      /// Returns the ObjectId of an entry in a standard, predefined
      /// DBDictionary having the given key. The generic argument type 
      /// determines which dictionary is accessed to obtain the result.
      /// 
      /// For example, to get the ObjectId of a Group object having 
      /// the name "MyGroup" from the Group dictionary:
      /// 
      ///    ObjectId myGroupId = db.GetDictionaryEntryId<Group>("MyGroup");
      ///    
      /// The generic argument type is used to resolve which of the
      /// standard dictionaries the entry is retrieved from. Those
      /// are the dictionaries that have corresponding properties on
      /// the Database class (e.g., XxxxxDictionaryId).
      /// </summary>
      /// <typeparam name="T">The type of the DBObject whose ObjectId
      /// is to be returned, which determines which DBDictionary the 
      /// DBObject is obtained from</typeparam>
      /// <param name="db">The source Database</param>
      /// <param name="key">The key of the dictionary entry to retreive</param>
      /// <param name="throwIfNotFound">A value indicating if the method
      /// should throw a KeyNotFound exception if an entry with the specified
      /// key does not exist, if false, the method returns ObjectId.Null if an 
      /// entry with the specified key does not exist.</param>
      /// <returns>The DBDictionaryEntry's Value</returns>
      /// <exception cref="ArgumentException"></exception>

      public static ObjectId GetDictionaryEntryId<T>(this Database db, string key, bool throwIfNotFound = false)
         where T : DBObject
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         if(string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(nameof(key));
         using(var tr = new ReadOnlyTransaction())
         {
            var dict = DBDictionary<T>.GetObjectId(db).GetObject<DBDictionary>(tr);
            if(dict.Contains(key))
               return dict.GetAt(key);
            if(throwIfNotFound)
               throw new KeyNotFoundException(key);
            return ObjectId.Null;
         }
      }

      /// <summary>
      /// An overloaded variant of GetDictionaryEntryId() that 
      /// allows the caller to identify the dictionary object 
      /// to be returned using a supplied predicate function 
      /// rather than the entry's key.
      /// </summary>
      /// <typeparam name="T">The type of the DBObject that is
      /// to be returned, which determines which DBDictionary 
      /// the object is obtained from</typeparam>
      /// <param name="db">The source Database</param>
      /// <param name="predicate">A function that takes an instance
      /// of the generic argument and returns a value indicating if
      /// the argument should be returned by this method.</param>
      /// <param name="trans">The Transaction to use to open the result</param>
      /// <param name="mode">The OpenMode to use to open the result</param>
      /// <returns>The first object found that satisfies the predicate</returns>

      public static ObjectId GetDictionaryEntryId<T>(this Database db, Func<T, bool> predicate)
         where T : DBObject
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         Assert.IsNotNull(predicate, nameof(predicate));
         using(var tr = new ReadOnlyTransaction())
         {
            var result = GetDictionaryObject(db, tr, predicate);
            if(result != null)
               return result.ObjectId;
            else
               return ObjectId.Null;
         }
      }

      public static IEnumerable<ObjectId> GetDictionaryEntryIds<T>(
            this Database db,
            Func<T, bool> predicate) where T : DBObject
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         Assert.IsNotNull(predicate, nameof(predicate));
         using(var tr = new ReadOnlyTransaction())
         {
            foreach(var entry in DBDictionary<T>.GetDictionary(db, tr))
               yield return entry.Value;
         }
      }

      /// <summary>
      /// Opens and returns an entry's value from any standard
      /// DBDictionary given its key, as to the generic argument 
      /// type. 
      /// 
      /// The Generic argument determines which of the standard 
      /// DBDictionaries the entry is obtained from. 
      /// 
      /// This method is only usable on one of the standard 
      /// DBDictionaries, for which the Database class has a
      /// XxxxxDictionarId property (e.g., GroupDictionaryId,
      /// LayoutDictionaryId, etc.).
      /// </summary>
      /// <typeparam name="T">The type of the DBObject that is
      /// to be returned, which determines which DBDictionary 
      /// the object is obtained from</typeparam>
      /// <param name="db">The source Database</param>
      /// <param name="key">The key of the dictionary entry to retreive</param>
      /// <param name="trans">The Transaction to use to open the result</param>
      /// <param name="mode">The OpenMode to use to open the result</param>
      /// <param name="throwIfNotFound">A value indicating if the method
      /// should throw a KeyNotFound exception if an entry with the specified
      /// key does not exist. If false, the method returns null if an entry
      /// with the specified key does not exist.</param>
      /// <returns>The entry having the given key. If no entry is found 
      /// with the given key, an exception is raised if throwOnNotFound
      /// is true, or null is returned otherwise.</returns>
      /// <exception cref="KeyNotFoundException"></exception>

      public static T GetDictionaryObject<T>(this Database db,
         string key,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = true) where T : DBObject
      {
         ObjectId id = GetDictionaryEntryId<T>(db, key, throwIfNotFound);
         if(!id.IsNull)
            return id.GetObject<T>(trans, mode);
         if(throwIfNotFound)
            throw new KeyNotFoundException(key);
         return null;
      }

      /// <summary>
      /// An overloaded variant of GetDictionaryEntry() that 
      /// allows the caller to identify the dictionary object 
      /// to be returned using a supplied predicate function 
      /// rather than the entry's key.
      /// </summary>
      /// <typeparam name="T">The type of the DBObject that is
      /// to be returned, which determines which DBDictionary 
      /// the object is obtained from</typeparam>
      /// <param name="db">The source Database</param>
      /// <param name="predicate">A function that takes an instance
      /// of the generic argument and returns a value indicating if
      /// the argument should be returned by this method.</param>
      /// <param name="trans">The Transaction to use to open the result</param>
      /// <param name="mode">The OpenMode to use to open the result</param>
      /// <returns>The first object found that satisfies the predicate</returns>

      public static T GetDictionaryObject<T>(this Database db,
         Transaction trans,
         Func<T, bool> predicate,
         OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         Assert.IsNotNull(predicate, nameof(predicate));
         db.CheckTransaction(trans);
         var result = db.GetDictionaryObjects<T>(trans)
            .FirstOrDefault(predicate);
         if(result != null && mode == OpenMode.ForWrite)
            result.UpgradeOpen();
         return result;
      }

      /// <summary>
      /// Opens and enumerates all objects referenced by the values 
      /// in the specified standard dictionary that are instances of 
      /// the generic argument type.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="db"></param>
      /// <param name="trans"></param>
      /// <param name="mode"></param>
      /// <returns></returns>

      public static IEnumerable<T> GetDictionaryObjects<T>(this Database db,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         db.CheckTransaction(trans);
         return DBDictionary<T>.GetDictionary(db, trans).GetObjects<T>(trans, mode);
      }

      /// <summary>
      /// The following methods use GetDictionaryObject() to
      /// surface dedicated methods that open objects from each
      /// of the standard DBDictionaries.
      /// 
      /// They are merely wrappers for GetDictionaryObject()
      /// that make their result/purpose more obvoius, and 
      /// simplify usage.
      /// 
      /// Note that the documention for all methods in this
      /// family are atypical.
      /// </summary>
      /// <param name="db">The Database to retreive the result from</param>
      /// <param name="name">The name of the element to retreive.</param>
      /// <param name="trans">The Transaction used to open the result</param>
      /// <param name="mode">The OpenMode used to open the result</param>
      /// <param name="throwIfNotFound">A value indicating if this method
      /// should throw an exception if an item with the specified key or
      /// name is not found. If false, this method returns null.</param>
      /// <returns>The Group object having the specified name</returns>

      public static Group GetGroup(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<Group>(db, name, trans, mode, throwIfNotFound);
      }

      public static DataLink GetDataLink(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<DataLink>(db, name, trans, mode, throwIfNotFound);
      }

      public static DetailViewStyle GetDetailViewStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<DetailViewStyle>(db, name, trans, mode, throwIfNotFound);
      }

      public static SectionViewStyle GetSectionViewStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<SectionViewStyle>(db, name, trans, mode, throwIfNotFound);
      }

      public static MLeaderStyle GetMLeaderStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<MLeaderStyle>(db, name, trans, mode, throwIfNotFound);
      }

      public static TableStyle GetTableStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<TableStyle>(db, name, trans, mode, throwIfNotFound);
      }

      public static PlotSettings GetPlotSettings(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<PlotSettings>(db, name, trans, mode, throwIfNotFound);
      }

      public static DBVisualStyle GetVisualStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<DBVisualStyle>(db, name, trans, mode, throwIfNotFound);
      }

      public static Material GetMaterial(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<Material>(db, name, trans, mode, throwIfNotFound);
      }

      public static MlineStyle GetMlineStyle(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<MlineStyle>(db, name, trans, mode, throwIfNotFound);
      }

      /// <summary>
      /// This method retreives a Layout given its dictionary key,
      /// whereas the included GetLayout() method retrieves a layout
      /// by its LayoutName.
      /// </summary>

      public static Layout GetLayoutByKey(this Database db,
         string name,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool throwIfNotFound = false)
      {
         return GetDictionaryObject<Layout>(db, name, trans, mode, throwIfNotFound);
      }

      /// <summary>
      /// Enumerates all references to the given BlockTableRecord,
      /// including anonymous dynamic block references.
      /// 
      /// Note: This method enumerates all block references, including
      /// those that are inserted into non-layout blocks. See the included 
      /// ExceptNested() extension method for a means of enumerating only 
      /// block references inserted into layout blocks using this method.
      /// </summary>
      /// <param name="blockTableRecord"></param>
      /// <param name="trans"></param>
      /// <param name="mode"></param>
      /// <param name="exact"></param>
      /// <param name="openLocked"></param>
      /// <param name="directOnly"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="ArgumentException"></exception>

      public static IEnumerable<BlockReference> GetBlockReferences(
         this BlockTableRecord blockTableRecord,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool openLocked = false,
         bool directOnly = true)
      {
         Assert.IsNotNullOrDisposed(blockTableRecord, nameof(blockTableRecord));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         if(blockTableRecord.IsLayout)
            throw new ArgumentException("Invalid BlockTableRecord");
         ObjectIdCollection ids = blockTableRecord.GetBlockReferenceIds(directOnly, true);
         int cnt = ids.Count;
         for(int i = 0; i < cnt; i++)
         {
            yield return (BlockReference)trans.GetObject(ids[i], mode, false, openLocked);
         }
         if(!blockTableRecord.IsAnonymous && blockTableRecord.IsDynamicBlock)
         {
            ObjectIdCollection blockIds = blockTableRecord.GetAnonymousBlockIds();
            cnt = blockIds.Count;
            for(int i = 0; i < cnt; i++)
            {
               BlockTableRecord btr2 = blockIds[i].GetObject<BlockTableRecord>(trans);
               ids = btr2.GetBlockReferenceIds(directOnly, true);
               int cnt2 = ids.Count;
               for(int j = 0; j < cnt2; j++)
               {
                  yield return (BlockReference)trans.GetObject(ids[j], mode, false, openLocked);
               }
            }
         }
      }

      /// <summary>
      /// A filter method that can be applied to the sequence returned
      /// by GetBlockReferences() (or any sequence of Entity), that will 
      /// exclude all nested objects (e.g., those that are not directly 
      /// owned by a layout block).
      /// 
      /// Because GetBlockReferences() returns all insertions of the target
      /// block, it is not uncommon to require only those that are directly
      /// inserted into a layout (paper or model space), rather than those
      /// that are nested within other blocks. This method can be useful to
      /// filter out/exclude nested block references.
      /// 
      /// It's recommended that the source sequence be opened for read, and 
      /// the results of this method then be upgraded to OpenMode.ForWrite,
      /// which can be easily accomplished using the included UpgradeOpen() 
      /// extension method.
      /// 
      /// <code>
      /// 
      ///    using(var tr = new OpenCloseTransaction())
      ///    {
      ///       BlockTableRecord btr = // ...assign to a BlockTableRecord
      ///    
      ///       var blockrefs = btr.GetBlockReferences(tr).ExceptNested().UpgradeOpen();
      ///       
      ///       foreach(var blkref in blockrefs)
      ///       {
      ///          // only non-nested blkrefs will appear here,
      ///          // and will be write-enabled.
      ///       }
      ///    }
      ///    
      /// </code>
      /// 
      /// This method uses an overload of Enumerable.Where() included
      /// with this library that uses the DBObjectFilter class to do 
      /// 'relational' filtering of objects based on criteria obtained
      /// from a related object. In essence, this overload of Where()
      /// exposes the complete functionality of a simple DBObjectFilter 
      /// as an extension method that can in many cases be easier and 
      /// more intuitive to use.
      /// 
      /// The Where() overload requires two delegate arguments. The
      /// first delegate returns the ObjectId of the related object
      /// from which the query criteria is obtained (in this case,
      /// it returns the BlockId of its Etity argument). The second
      /// delegate is a predicate that will be applied to the related 
      /// objects to determine if the sequence element is included in
      /// the result (in this case, the related object the predicate
      /// is applied to is the BlockTableRecord whose ObjectId was
      /// returned by the first delegate).
      /// 
      /// </summary>
      /// <param name="source">An object that enumerates Entities</param>
      /// <returns>The subset of the source sequence consisting of only 
      /// elements that are directly owned by a layout block.</returns>

      public static IEnumerable<T> ExceptNested<T>(this IEnumerable<T> source)
         where T : Entity
      {
         return source.WhereBy<T, BlockTableRecord>(
            entity => entity.BlockId, 
            blockTableRecord => blockTableRecord.IsLayout);
      }

      /// <summary>
      /// Returns a value indicating if the specified block is
      /// a paper space layout block (not terribly efficient).
      /// </summary>
      /// <param name="btr"></param>
      /// <returns></returns>

      public static bool IsPaperSpaceBlock(this BlockTableRecord btr)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         Assert.IsNotNullOrDisposed(btr.Database, nameof(Database));
         return btr.IsLayout && btr.ObjectId !=
            SymbolUtilityServices.GetBlockModelSpaceId(btr.Database);
      }

      /// <summary>
      /// Filters a sequence of entities by owner block id
      /// </summary>

      public static IEnumerable<T> OwnedBy<T>(this IEnumerable<T> entities, ObjectId ownerId)
         where T: Entity
      {
         return entities.Where(ent => ent.BlockId == ownerId);
      }

      /// <summary>
      /// SymbolUtilityServices extensions
      /// </summary>

      public static ObjectId GetModelSpaceBlockId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetBlockModelSpaceId(db);
      }

      public static ObjectId GetBlockPaperSpaceId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetBlockPaperSpaceId(db);
      }

      public static ObjectId GetLinetypeByBlockId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetLinetypeByBlockId(db);
      }

      public static ObjectId GetLinetypeByLayerId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetLinetypeByLayerId(db);
      }

      public static ObjectId GetLinetypeContinuousId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetLinetypeContinuousId(db);
      }

      public static ObjectId GetRegAppAcadId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetRegAppAcadId(db);
      }

      public static ObjectId GetTextStyleStandardId(this Database db)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         return SymbolUtilityServices.GetTextStyleStandardId(db);
      }

      public static ObjectId GetLayerDefpointsId(this Database database)
      {
         Assert.IsNotNullOrDisposed(database, nameof(database));
         return SymbolUtilityServices.GetLayerDefpointsId(database);
      }

      public static ObjectId GetLayerZeroId(this Database database)
      {
         Assert.IsNotNullOrDisposed(database, nameof(database));
         return SymbolUtilityServices.GetLayerZeroId(database);
      }

      public static bool IsCompatibilityMode(this Database database)
      {
         Assert.IsNotNullOrDisposed(database, nameof(database));
         return SymbolUtilityServices.IsCompatibilityMode(database);
      }

      /// <summary>
      /// Constrains a sequence of Entity to only those 
      /// owned by the Layout having the given name.
      /// </summary>
      /// <typeparam name="T">The LayoutName of the layout
      /// containing the resulting subset of objects</typeparam>
      /// <param name="source">The input sequence of entities</param>
      /// <returns>The subset of the input sequence that are
      /// contained in the layout with the given name.</returns>

      public static IEnumerable<T> FromLayout<T>(this IEnumerable<T> source, string LayoutName)
         where T : Entity
      {
         Assert.IsNotNull(source, nameof(source));
         Assert.IsNotNullOrWhiteSpace(LayoutName, nameof(LayoutName));
         var db = source.TryGetDatabase();
         if(db == null)
            return Enumerable.Empty<T>();
         var layoutId = db.GetLayoutId(LayoutName);
         if(layoutId.IsNull)
            throw new ArgumentException($"Layout {LayoutName} not found.");
         var blockId = layoutId.Invoke<Layout, ObjectId>(layout => layout.BlockTableRecordId);
         return source.Where(e => e.BlockId == blockId);
      }

      /// <summary>
      /// Try to get the containing Database of a sequence of DBObjects
      /// </summary>

      public static Database TryGetDatabase(this IEnumerable<DBObject> objects,
         bool throwOnFailed = false)
      {
         Assert.IsNotNull(objects, nameof(objects));
         Database db = objects.FirstOrDefault()?.Database;
         AcRx.ErrorStatus.NoDatabase.ThrowIf(throwOnFailed && db == null);
         return db;
      }

      /// <summary>
      /// Gets the sequence of ObjectIds representing the 
      /// BlockTableRecords of each Layout.
      /// </summary>
      /// <param name="database">The Database to operate on</param>
      /// <param name="includingModelSpace">A value indicating if
      /// the Model space layout's block Id should be included</param>
      /// <returns>A sequence of ObjectIds representing the 
      /// BlockTableRecords of each layout block</returns>

      public static IEnumerable<ObjectId> GetLayoutBlockIds(this Database database,
         bool includingModelSpace = false)
      {
         using(var trans = new ReadOnlyTransaction())
         {
            foreach(Layout layout in database.GetDictionaryObjects<Layout>(trans))
            {
               if(includingModelSpace || !layout.ModelType)
                  yield return layout.BlockTableRecordId;
            }
         }
      }

      /// <summary>
      /// Opens and returns a sequence of Layout BlockTableRecords
      /// from the given Database.
      /// </summary>
      /// <param name="includingModelSpace">A value indicating if
      /// the Model space BlockTableRecord should be included.</param>
      /// 
      /// See the GetObjectsOfType() method for a desription of 
      /// all other parameters.
      /// <returns>An object that enumerates the BlockTableRecord
      /// for each layout.</returns>

      public static IEnumerable<BlockTableRecord> GetLayoutBlocks(this Database database,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool includingModelSpace = false)
      {
         return GetLayoutBlockIds(database, includingModelSpace)
            .GetObjects<BlockTableRecord>(trans, mode, true, false);
      }

      /// <summary>
      /// Returns a sequence containing all BlockReferences in
      /// the given Database whose names match the given wildcard
      /// pattern, excluding references to blocks that are:
      /// 
      ///   Non-dynamic Anonymous blocks
      ///   Layouts 
      ///   External references/overlays
      ///   Blocks from external references/overlays.
      /// 
      /// Anonymous dynamic block references will be included if 
      /// the dynamic block definition's name matches the pattern.
      /// 
      /// A caller-supplied predicate can be provided that can
      /// override the above described constraints, if needed.
      /// 
      /// Caller-provided predicates are typically used to do
      /// filtering of blocks based on the presence of certain
      /// application-managed XData or DBDictionary content. 
      /// 
      /// A caller-provided predicate can constraint the result
      /// to the default conditions, in addition whatever other
      /// conditions it imposes using the IsUserBlock() method
      /// included in this library, which is what this method
      /// uses by default to constrain the result.
      /// </summary>
      /// <param name="db">The Database</param>
      /// <param name="pattern">A wcmatch-style pattern that
      /// matches the name of one or more blocks</param>
      /// <param name="trans">The transaction to use in the
      /// operation.</param>
      /// <param name="mode">The OpenMode to open the 
      /// BlockReferences in</param>
      /// <param name="predicate">An optional function that 
      /// takes a BlockTableRecord as an argument and
      /// returns a value indicating if references to the
      /// BlockTableRecord should be included. If provided,
      /// this function overrides the built-in conditions
      /// outlined above. If not provided, the conditions 
      /// outlined above are imposed.</param>
      /// <returns>A sequence containing all matching
      /// BlockReference objects.</returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<BlockReference> GetBlockReferences(this Database db,
         string pattern,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         Func<BlockTableRecord, bool> predicate = null)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         Assert.IsNotNullOrWhiteSpace(pattern, nameof(pattern));
         predicate = predicate ?? IsUserBlock;
         return db.GetSymbolTableRecords<BlockTableRecord>(trans)
            .Where(btr => predicate(btr) && btr.Name.Matches(pattern))
            .SelectMany(btr => btr.GetBlockReferences(trans, mode));
      }

      static bool IsUserBlock(this BlockTableRecord btr) => 
         !(btr.IsAnonymous
            || btr.IsLayout
            || btr.IsFromExternalReference
            || btr.IsFromOverlayReference
            || btr.IsDependent);
      
      /// <summary>
      /// Get all AttributeReferences with the given tag from
      /// every insertion of the given BlockTableRecord.
      /// </summary>

      public static IEnumerable<AttributeReference> GetAttributeReferences(
         this BlockTableRecord btr,
         Transaction tr,
         string tag)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         Assert.IsNotNullOrDisposed(tr, nameof(tr));
         string s = tag.ToUpper();
         foreach(var blkref in btr.GetBlockReferences(tr))
         {
            var attref = blkref.GetAttributes(tr).FirstOrDefault(a => a.Tag.ToUpper() == s);
            if(attref != null)
               yield return attref;
         }
      }

      /// <summary>
      /// Get AttributeReferences from the given block reference (lazy).
      /// Can enumerate AttributeReferences of database resident and
      /// non-database resident BlockReferences.
      /// </summary>

      public static IEnumerable<AttributeReference> GetAttributes(this BlockReference blkref, Transaction tr, OpenMode mode = OpenMode.ForRead)
      {
         Assert.IsNotNullOrDisposed(blkref, nameof(blkref));
         Assert.IsNotNullOrDisposed(tr, nameof(tr));
         var objects = blkref.AttributeCollection.Cast<object>();
         object first = blkref.AttributeCollection.TryGetFirst();
         if(first != null)
         {
            if(first is AttributeReference)
            {
               foreach(AttributeReference attref in blkref.AttributeCollection)
                  yield return attref;
            }
            else
            {
               foreach(ObjectId id in blkref.AttributeCollection)
                  yield return (AttributeReference)tr.GetObject(id, mode, false, false);
            }
         }
      }

      /// <summary>
      /// Returns a Dictionary<string, AttributeReference> containinng
      /// all AttributeReferences for the given block reference, keyed
      /// to each AttributeReference's Tag.
      /// </summary>
      /// <param name="blkref"></param>
      /// <param name="tr"></param>
      /// <param name="mode"></param>
      /// <returns></returns>

      public static Dictionary<string, AttributeReference> GetAllAttributes(
         this BlockReference blkref,
         Transaction tr,
         OpenMode mode = OpenMode.ForRead)
      {
         return blkref.GetAttributes(tr, mode)
            .ToDictionary(att => att.Tag.ToUpper(), att => att);
      }

      public static Dictionary<string, string> GetAttributeValues(
         this BlockReference blkref,
         Transaction tr)
      {
         return blkref.GetAttributes(tr)
            .ToDictionary(att => att.Tag.ToUpper(), att => att.TextString);
      }
   }

}



