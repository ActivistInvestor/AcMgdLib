﻿/// CollectionExtensions.cs  
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
using System.Diagnostics.Extensions;
using System.Extensions;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Expressions.Predicates;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// Notes: 
   /// 
   /// Many of the extension methods included in this code 
   /// base do not deal gracefully with attempts to open 
   /// entities that reside on locked layers for write. 
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
   /// is only needed in rare, highly-specialized use cases.
   /// 
   /// "Linq-less" implementation:
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
      /// methods delegate to goes to great lengths to
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
      /// that appear in the List</typeparam>
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
         bool openLocked = false,
         Expression<Func<ObjectId, bool>> expression = null) where T : DBObject
      {
         source.TryCheckTransaction(trans);
         openLocked &= mode == OpenMode.ForWrite;
         bool flag = typeof(T) == typeof(DBObject);
         Expression<Func<ObjectId, bool>> expr = null;
         if(!flag)
            expr = RXClass<T>.GetMatchExpression(exact);
         if(expression != null)
         {
            expr = expr != null ? expr.And(expression) : expression;
         }
         Func<ObjectId, bool> predicate = expr != null ? expr.Compile() : null;
         flag = predicate == null;

         /// The following code goes to great extents to
         /// find an optimized path, based on whether the 
         /// List has an indexer and/or is a strongly-
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
                  yield return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openLocked));
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
                  yield return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openLocked));
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
                  yield return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openLocked));
               }
            }
         }
         else if(source is IEnumerable<ObjectId> enumerable)
         {
            foreach(ObjectId id in enumerable)
            {
               if(flag || predicate(id))
               {
                  yield return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openLocked));
               }
            }
         }
         else  // fallback to least-preferred path with greatest overhead (unboxing)
         {
            /// Optimized path for all entities from a BlockTableRecord:
            if(source is BlockTableRecord btr && typeof(T) == typeof(Entity))
            {
               foreach(ObjectId id in btr)
               {
                  if(flag || predicate(id))
                     yield return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openLocked));
               }
            }
            else
            {
               foreach(ObjectId id in source)
               {
                  if(flag || predicate(id))
                  {
                     yield return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openLocked));
                  }
               }
            }
         }
      }

      /// Public GetObjects() overloads:

      /// <summary>
      /// BlockTableRecord:
      /// 
      /// Enumerates all or a subset of the entities in a
      /// block's definition. The generic argument type
      /// is used to both constrain the resulting list
      /// to only those that are instances of the generic
      /// argument, and to cast the enumerated elements to
      /// that same type. To get all entities in a block's
      /// definition, use Entity as the generic argument.
      /// 
      /// Testing of each element's type is done against the
      /// runtime class of each List ObjectId to avoid the
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
      /// ObjectIds in the List collection. 
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
      /// in the List sequence represeting instances of the generic 
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
      /// when it can be assumed that all elements in the List 
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
      /// <param name="treatElementsAsHard">The value to assign to 
      /// a newly-created DBDictionary's TreatElementsAsHard property.
      /// This argument applies only to DBDictionaries created by
      /// this method.</param>
      /// <param name="create">A value indicating if a new entry 
      /// should be created and added to the extension dictionary 
      /// if an entry with the given key does not exist. If this
      /// argument is false, and no existing entry having the given
      /// key exists, this method returns null, and does not raise
      /// an exception.</param>
      /// <returns>The requested value or null</returns>

      public static T GetDictionaryValue<T>(this DBObject owner,
            string key,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead,
            bool create = false,
            bool treatElementsAsHard = false) where T : DBObject, new()
      {
         return GetDictionaryValue<T>(owner, key, trans,
            mode, (create ? (arg) => new T() : (Func<DBObject, T>)null), treatElementsAsHard);
      }

      /// <summary>
      /// Overloaded version of GetDictionaryValue() that
      /// differs from the above in the following way:
      /// 
      /// A caller-supplied delegate is used to create a
      /// new entry, which means the generic argument does 
      /// not require a public parameterless constructor.
      /// 
      /// If the factory argument is provided, this method 
      /// always creates a new extension dictionary and entry 
      /// if either or both do not exist. If the factory
      /// argument is not provided, this method will return
      /// null if there is no extension dictionary, or there
      /// is not existing entry having the given key.
      /// 
      /// The factory delegate is passed the owner parameter 
      /// to this method, which it can use it to create and 
      /// initialize the dictionary entry's value, if needed.
      /// </summary>

      public static T GetDictionaryValue<T>(this DBObject owner,
         string key,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         Func<DBObject, T> factory = null,
         bool treatElementsAsHard = false) where T : DBObject
      {
         owner.CheckTransaction(trans);
         Assert.IsNotNullOrWhiteSpace(key, nameof(key));
         bool create = factory != null;
         ObjectId xDictId = owner.ExtensionDictionary;
         bool exists = !xDictId.IsNull;
         if(!exists && factory == null)
            return null;
         var dict = owner.GetExtensionDictionary(trans, OpenMode.ForRead, create);
         if(dict == null)
            throw new InvalidOperationException("Failed to get or create extension dictionary");
         if(dict.IsNewObject)
            dict.TreatElementsAsHard = treatElementsAsHard;
         if(exists && dict.Contains(key))
         {
            ObjectId id = dict.GetAt(key);
            return trans.GetObjectChecked<T>(id, mode);
         }
         T result = null;
         if(factory != null)
         {
            result = factory(owner);
            if(!dict.IsWriteEnabled)
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
      /// null without adding an extension dictionary or Xrecord.
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
            bool create = false,
            bool treatElementsAsHard = false)
      {
         return owner.GetDictionaryValue<Xrecord>(key, trans, 
            mode, create, treatElementsAsHard);
      }

      /// <summary>
      /// Returns the value of an existing XRecord's Data property, 
      /// or null if the XRecord doesn't exist.
      /// </summary>
      /// <param name="owner">The owning DBObject</param>
      /// <param name="key">The key of the Xrecord within the owner's extension dictionary</param>
      /// <returns>A ResultBuffer containing the XRecord's data or 
      /// null if the Xrecord does not exist.</returns>

      public static ResultBuffer GetXRecordData(this DBObject owner, string key)
      {
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         using(var tr = new ReadOnlyTransaction())
         {
            return owner.GetXrecord(key, tr)?.Data;
         }
      }

      /// <summary>
      /// Assigns the given List to the XRecord having the
      /// specified key, within the owner's extension dictionary.
      /// If the Xrecord does not exist, it will be created and
      /// added to the extension dictionary. 
      /// 
      /// If the extension dictionary does not exist, it will be 
      /// created.
      /// 
      /// If the TypedValue argument array contains one or more
      /// elements that are ObjectIds, a newly-created XRecord's 
      /// XlateReferences property is set to true.
      /// </summary>
      /// <param name="owner"></param>
      /// <param name="key"></param>
      /// <param name="trans"></param>
      /// <param name="typedValues"></param>
      /// <returns>A value indicating if a new Xrecord was created
      /// (true) or an existing Xrecord was modified (false).</returns>
      
      public static bool SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params TypedValue[] typedValues)
      {
         bool xlate = typedValues.Any(tv => tv.Value is ObjectId);
         return SetXRecordData(owner, key, trans, xlate, xlate, typedValues);
      }

      public static bool SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         ResultBuffer buffer)
      {
         Assert.IsNotNull(buffer, nameof(buffer));
         bool xlate = buffer.Cast<TypedValue>().Any(tv => tv.Value is ObjectId);
         return SetXRecordData(owner, key, trans, xlate, xlate, buffer);
      }

      public static bool SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         bool treatElementsAsHard,          // applies to newly-created DBDictionaries only
         bool xlateReferences,              // applies to newly-created Xrecords only
         params TypedValue[] typedValues)
      {
         Assert.IsNotNull(typedValues, nameof(typedValues));
         return SetXRecordData(owner, key, trans, treatElementsAsHard, xlateReferences, new ResultBuffer(typedValues));
      }

      /// <summary>
      /// Sets the Data of an Xrecord having the given key within
      /// the extension dictionary of the given owner object.
      /// 
      /// If the extension dictionary and/or Xrecord doesn't exist, 
      /// they will be created. 
      /// </summary>
      /// <param name="owner">The owner DBObject</param>
      /// <param name="key">The key of the Xrecord</param>
      /// <param name="trans">The Transaction to use for the operation</param>
      /// <param name="treatElementsAsHard">A value to assign to newly-created 
      /// extension dictionaries TreatElementsAsHard property - Only applies to 
      /// newly-created extension dictionaries.</param>
      /// <param name="xlateReferences">The value to assign to newly-created
      /// Xrecords XlateReferences property - Only applies to newly-created
      /// Xrecords.</param>
      /// <param name="buffer">The ResultBuffer containing the data to assign
      /// to the Xrecord.</param>
      /// <returns></returns>

      public static bool SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         bool treatElementsAsHard,          // applies to newly-created DBDictionaries only
         bool xlateReferences,              // applies to newly-created Xrecords only
         ResultBuffer buffer)
      {
         Assert.IsNotNullOrWhiteSpace(key, nameof(key));
         owner.CheckTransaction(trans);
         Xrecord xrecord = owner.GetXrecord(key, trans, OpenMode.ForWrite, true, treatElementsAsHard);
         if(xrecord.IsNewObject)
            xrecord.XlateReferences = xlateReferences;
         xrecord.Data = buffer;
         return xrecord.IsNewObject;
      }

      /// <summary>
      /// Removes the entry having the specified key 
      /// from a DBObject's extension dictionary.
      /// </summary>
      /// <param name="owner"></param>
      /// <param name="key"></param>
      /// <param name="trans"></param>
      /// <returns></returns>

      public static bool RemoveDictionaryEntry(this DBObject owner,
         string key,
         Transaction trans)
      {
         owner.TryCheckTransaction(trans);
         Assert.IsNotNullOrWhiteSpace(key, nameof(key));
         DBDictionary xdict = owner.GetExtensionDictionary(trans, OpenMode.ForWrite, false);
         if(xdict != null && xdict.Contains(key))
         {
            ObjectId id = xdict.GetAt(key);
            xdict.Remove(id);
            DBObject obj = trans.GetObject(id, OpenMode.ForWrite);
            obj.Erase();
            return true;
         }
         return false;
      }

      /// <summary>
      /// Assigns a value to the given DBObject's 
      /// extension dictionary for the given key,
      /// and optionally removes and erases any
      /// existing entry having the specified key.
      /// </summary>
      /// <param name="owner">The DBObject whose extension
      /// dictionary is to be assigned to</param>
      /// <param name="key">The key to assign the value to</param>
      /// <param name="newValue">The value to assign to the specified key</param>
      /// <param name="trans">The Transaction to use to perform the operation</param>
      /// <param name="replace">A value indicating if an existing
      /// entry with the specified key should be replaced with the
      /// new value. If this value is false and an entry exists with
      /// the specified key, an exception is thrown.</param>
      /// <returns></returns>

      public static void SetDictionaryEntry(this DBObject owner,
         string key,
         DBObject newValue,
         Transaction trans, 
         bool replace = true)
      {
         owner.TryCheckTransaction(trans);
         Assert.IsNotNullOrWhiteSpace(key, nameof(key));
         AcRx.ErrorStatus.AlreadyInDB.ThrowIf(!newValue.ObjectId.IsNull);
         var xdict = owner.GetOrCreateExtensionDictionary(trans, OpenMode.ForWrite);
         if(xdict.Contains(key))
         {
            if(!replace)
               throw new ArgumentException($"Dictionary key exists: {key}");
            ObjectId id = xdict.GetAt(key);
            xdict.Remove(id);
            DBObject obj = trans.GetObject(id, OpenMode.ForWrite);
            obj.Erase();
         }
         xdict.SetAt(key, newValue);
         trans.AddNewlyCreatedDBObject(newValue, true);
      }

      /// <summary>
      /// This works like SetXRecordData() except that if there
      /// is an existing Xrecord, the arguments are appended to
      /// the existing Xrecord's data. 
      /// 
      /// If no existing XRecord exists, the behavior is identical
      /// to SetXRecordData().
      /// </summary>
      /// <param name="owner"></param>
      /// <param name="key"></param>
      /// <param name="trans"></param>
      /// <param name="typedValues"></param>

      public static void AppendToXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         bool treatElementsAsHard,          // applies to newly-created DBDictionaries only
         bool xlateReferences,              // applies to newly-created Xrecords only
         params TypedValue[] typedValues)
      {
         Assert.IsNotNullOrWhiteSpace(key, nameof(key));
         owner.CheckTransaction(trans);
         Xrecord xrecord = owner.GetXrecord(key, trans, OpenMode.ForWrite, treatElementsAsHard);
         if(!xrecord.IsNewObject)
         {
            var data = xrecord.Data;
            foreach(var tv in typedValues)
               data.Add(tv);
            xrecord.Data = data;
         }
         else
         {
            xrecord.XlateReferences = xlateReferences;
            xrecord.Data = new ResultBuffer(typedValues);
         }
      }

      /// <summary>
      /// Overload of above that sets xlateReferences and
      /// treatElementsAsHard arguments to true if the data 
      /// contains one or more ObjectIds.
      /// </summary>

      public static void AppendToXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params TypedValue[] typedValues)
      {
         bool xlate = typedValues.Any(tv => tv.Value is ObjectId);
         AppendToXRecordData(owner, key, trans, xlate, xlate, typedValues);
      }

      /// <summary>
      /// Overloads of SetXRecordData() and AppendToXRecordData() 
      /// that accept any of these parameter types:
      /// 
      ///    params ValueTuple<int, object>[]
      ///    params ValueTuple<short, object>[]
      ///    params ValueTuple<DxfCode, object>[]
      ///    params ValueTuple<LispDataType, object>[]
      ///    
      /// </summary>

      public static void SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params (int, object)[] values)
      {
         bool xlate = values.Any(tv => tv.Item2 is ObjectId);
         SetXRecordData(owner, key, trans, xlate, xlate, values.ToTypedValues());
      }

      public static void SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params (DxfCode, object)[] values)
      {
         bool xlate = values.Any(tv => tv.Item2 is ObjectId);
         SetXRecordData(owner, key, trans, xlate, xlate, values.ToTypedValues());
      }

      public static void SetXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params (LispDataType, object)[] values)
      {
         bool xlate = values.Any(tv => tv.Item2 is ObjectId);
         SetXRecordData(owner, key, trans, xlate, xlate, values.ToTypedValues());
      }

      public static void AppendToXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params (int, object)[] values)
      {
         bool xlate = values.Any(tv => tv.Item2 is ObjectId);
         AppendToXRecordData(owner, key, trans, xlate, xlate, values.ToTypedValues());
      }

      public static void AppendToXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params (DxfCode, object)[] values)
      {
         bool xlate = values.Any(tv => tv.Item2 is ObjectId);
         AppendToXRecordData(owner, key, trans, xlate, xlate, values.ToTypedValues());
      }

      public static void AppendToXRecordData(this DBObject owner,
         string key,
         Transaction trans,
         params (LispDataType, object)[] values)
      {
         bool xlate = values.Any(tv => tv.Item2 is ObjectId);
         AppendToXRecordData(owner, key, trans, xlate, xlate, values.ToTypedValues());
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

      public static DBDictionary GetExtensionDictionary(this DBObject owner, 
         Transaction trans, 
         OpenMode mode = OpenMode.ForRead, bool create = false)
      {
         owner.TryCheckTransaction(trans);
         ObjectId id = owner.ExtensionDictionary;
         if(id.IsNull && create)
         {
            if(!owner.IsWriteEnabled)
               owner.UpgradeOpen();
            owner.CreateExtensionDictionary();
            id = owner.ExtensionDictionary;
            if(!owner.IsTransactionResident)
               owner.DowngradeOpen();
         }
         if(id.IsNull)
         {
            if(create)
               throw new InvalidOperationException("Failed to get/create extension dictionary");
            return null;
         }
         return trans.GetObject<DBDictionary>(id, mode);
      }

      /// <summary>
      /// Like GetExtensionDictionary() with implicit
      /// creation of non-existing dictionary, mainly for
      /// making the intent of the calling code explicit.
      /// </summary>

      public static DBDictionary GetOrCreateExtensionDictionary(this DBObject owner,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead)
      {
         return GetExtensionDictionary(owner, trans, mode, true);
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
      /// <typeparam name="T">The type of SymbolTableRecords to be
      /// returned, which also determines which SymbolTable is to 
      /// have its entries retieved. The generic argument must be 
      /// a concrete type derived from the SymbolTableRecord type.</typeparam>
      /// <param name="db">The Database to access</param>
      /// <param name="trans">The transaction to use for the operation</param>
      /// <param name="mode">The OpenMode to open resulting objects in
      /// (default: OpenMode.ForRead)</param>
      /// <returns>A sequence of SymbolTableRecord-based objects</returns>

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
         OpenMode mode = OpenMode.ForRead,
         bool openErased = false) where T : DBObject
      {
         Assert.IsNotNullOrDisposed(source, nameof(source));
         return source.Cast<DictionaryEntry>()
            .Select(e => e.Value)
            .GetObjectsCore<T>(trans, mode, openErased, true);
      }

      /// <summary>
      /// Gets the ObjectIds of all List in the given DBDictionary
      /// that reference objects of the type of the generic argument.
      /// 
      /// To get erased entries, invoke this method on the result of 
      /// the DBDictionary's IncludingErased property.
      /// </summary>
      /// <param name="source">The DBDictionary to access</param>
      /// <param name="includingErased">A value indicating if erased
      /// elements should be included.</param>
      /// <returns>An object that enumerates the ObjectIds of the
      /// entry List in the DBDictionary</returns>

      public static IEnumerable<ObjectId> GetObjectIds<T>(this DBDictionary source, bool includingErased = false)
         where T : DBObject
      {
         Assert.IsNotNullOrDisposed(source, nameof(source));
         Func<ObjectId, bool> func = RXClass<T>.GetIdPredicate(false, includingErased);
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

      /// <summary>
      /// Helper extension method for ObjectIdCollection that
      /// returns the last occurrence of an element of the given 
      /// ObjectIdCollection that is an instance of the given type, 
      /// or ObjectId.Null if no element of the given type exists 
      /// in the collection.
      /// 
      /// E.g., get the ObjectId of the last non-erased PolyLine 
      /// in an ObjectIdCollection:
      /// 
      ///    var lastPlineId = myIdCollection.Last<Polyline>();
      ///    
      /// </summary>
      /// <typeparam name="T">The type of object being requested</typeparam>
      /// <param name="ids">The ObjectIdCollection to query</param>
      /// <param name="exactMatch">A value indicating if types that
      /// are derived from the generic argument type are to be
      /// returned.</param>
      /// <returns>The ObjectId of the last occurrence of an element
      /// whose type is the requested type, or ObjectId.Null if no 
      /// element of the requested type exists in the collection.</returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static ObjectId Last<T>(
            this ObjectIdCollection ids,
            bool exactMatch = false)
         where T : DBObject
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         if(ids.Count > 0)
         {
            var predicate = RXClass<T>.GetIdPredicate(exactMatch);
            for(int i = ids.Count - 1; i > -1; i--)
            {
               if(predicate(ids[i]))
                  return ids[i];
            }
         }
         return ObjectId.Null;
      }

      /// <summary>
      /// An overload of Last<T>() that allows the caller
      /// to obtain the nth-from-last occurence of a given
      /// type. 
      /// 
      /// The index argument specifies the reverse-index of
      /// the subset of elements of the requested type, with 
      /// a value of 0 representing the last occurrence of 
      /// the specified type.
      /// 
      /// For example, to return the last occurrence specify
      /// an index of 0. To return the next-to-last occurence,
      /// specify an index of 1, and so on.
      /// 
      /// Repeated use of this method on the same argument,
      /// with a different index argument is not recommended,
      /// as it is easier and far-more efficient to collect
      /// all the elements of a desired type, and reverse their
      /// order. See the ReverseOfType<T>() method for that.
      /// 
      /// See the ReversOfType<T>() extension method for that
      /// solution.
      /// 
      /// This returns the next-to-last Polyline in an
      /// ObjectIdCollection:
      /// 
      ///    ids.Last<Polyline>(1);
      ///   
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="ids"></param>
      /// <param name="index">The relative offset from the
      /// end of the collection of the subset of elements of 
      /// the given type.
      /// etc.</param>
      /// <param name="exactMatch"></param>
      /// <param name="includingErased"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static ObjectId Last<T>(
            this ObjectIdCollection ids,
            int index,
            bool exactMatch = false)
         where T : DBObject
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         if(index < 0 || index >= ids.Count)
            throw new IndexOutOfRangeException(nameof(index));
         if(ids.Count > 0)
         {
            int found = 0;
            var predicate = RXClass<T>.GetIdPredicate(exactMatch);
            for(int i = ids.Count - 1; i > -1; i--)
            {
               if(predicate(ids[i]) && index == found++)
                  return ids[i];
            }
         }
         return ObjectId.Null;
      }

      /// <summary>
      /// Returns a sequence that produces a subset of the 
      /// ObjectIdCollection consisting of elements that
      /// represent instances of the generic argument type.
      /// 
      /// If the exact argument is true and the given type
      /// is not abstract, types derived from the given type 
      /// are not included.
      /// </summary>
      /// <typeparam name="T">The type whose ObjectIds are to 
      /// be included in the result.</typeparam>
      /// <param name="ids">The ObjectIdCollection to query</param>
      /// <param name="exactMatch">A value indicating if types that
      /// are derived from the generic argument type are to be
      /// excluded.</param>
      /// <param name="reverse">A value indicating if the resulting
      /// elements should be enumerated in reverse-order relative to 
      /// the source sequence</param>
      /// <param name="includingErased">A value indicating if
      /// erased ObjectIds should be included.</param>
      /// <returns>A sequence of ObjectIds representing objects 
      /// that are instances of the given generic argument  type, 
      /// or instances of objects derived from same</returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static IEnumerable<ObjectId> OfType<T>(
            this ObjectIdCollection ids,
            bool exactMatch = false,
            bool reverse = false) where T : DBObject
      {
         Assert.IsNotNull(ids, nameof(ids));
         var predicate = RXClass<T>.GetIdPredicate(exactMatch);
         return reverse ? ReverseIterate() : Iterate();

         IEnumerable<ObjectId> Iterate()
         {
            if(ids == null)
               throw new ArgumentNullException(nameof(ids));
            if(ids.Count > 0)
            {
               for(int i = 0; i < ids.Count; i++)
               {
                  if(predicate(ids[i]))
                     yield return ids[i];
               }
            }
         }

         IEnumerable<ObjectId> ReverseIterate()
         {
            if(ids == null)
               throw new ArgumentNullException(nameof(ids));
            if(ids.Count > 0)
            {
               for(int i = ids.Count - 1; i > -1; i--)
               {
                  if(predicate(ids[i]))
                     yield return ids[i];
               }
            }
         }
      }

      /// <summary>
      /// A ToArray() method for ObjectIdCollection and
      /// Point3dCollection, that help de-clutter application 
      /// code, and avoid a wasteful Cast<T>().ToArray().
      /// </summary>
      /// <param name="ids">The ObjectIdCollection to convert 
      /// to an array</param>
      /// <returns>An array of ObjectId[] containing the elements 
      /// of ObjectIdCollection</returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static ObjectId[] ToArray(this ObjectIdCollection ids)
      {
         return ToArray<ObjectId>(ids);
      }

      public static Point3d[] ToArray(this Point3dCollection ids)
      {
         return ToArray<Point3d>(ids);
      }

      public static void AddRange(this ObjectIdCollection ids, IEnumerable<ObjectId> items)
      {
         Assert.IsNotNullOrDisposed(ids, nameof(ids));
         Assert.IsNotNull(items, nameof(items));
         if(items is ObjectId[] array)
         {
            var span = array.AsSpan();
            for(int i = 0; i < span.Length; i++)
               ids.Add(span[i]);
         }
         else 
         {
            foreach(ObjectId id in items)
               ids.Add(id);
         }
      }

      /// <summary>
      /// ToArray() for non-generic ICollection types.
      /// 
      /// Requires the element type to be explicitly 
      /// passed as the generic argument.
      /// </summary>

      public static T[] ToArray<T>(this ICollection collection)
      {
         if(collection == null)
            throw new ArgumentNullException(nameof(collection));
         T[] array = new T[collection.Count];
         collection.CopyTo(array, 0);
         return array;
      }

      /// <summary>
      /// Like ToArray() except that it returns its argument
      /// if it is an array of the specified type.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="collection"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static T[] AsArray<T>(this ICollection collection)
      {
         if(collection == null)
            throw new ArgumentNullException(nameof(collection));
         if(collection is T[] array)
            return array;
         return ToArray<T>(collection);
      }


   }

}



