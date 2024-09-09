/// NewObjectCollection.cs
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Extensions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Utility;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A class encapsulating a specialization of ObjectOverrule
   /// that marshals a collection containing the ObjectIds of 
   /// newly-created objects of a specified type or derived type.
   /// 
   /// This class was designed to automate a common task, which 
   /// is to identify objects that were appended to the database 
   /// by a command or other complex operation when the command 
   /// or operation ends, and then operate on the newly-added 
   /// objects in some way.
   /// 
   /// This class should not be active on a 'full-time' basis,
   /// as it stores the ObjectIds of every object added to the
   /// database (of the type specified as the generic argument),
   /// and for that reason could consume significant amounts of
   /// memory over an extended period of activity.
   /// </summary>
   /// 
   /// <remarks>
   /// 
   /// Common usage patterns:
   /// 
   /// This class can be used in several ways, depending on what
   /// commands or operations are targeted, and how they are started.
   /// 
   /// In cases where a targeted command or sequence of commands
   /// is being executed by managed code, such as by calling the
   /// Editor's Command() method, usage is fairly simple. You just 
   /// create an instance of NewObjectCollection prior to executing 
   /// the command(s), and after their execution has completed, you 
   /// access the collection's elements and operate on them and then
   /// dispose the NewObjectCollection:
   /// 
   /// <code>
   /// 
   ///    Document doc = Application.DocumentManager.MdiActiveDocument;
   ///    Database db = doc.Database;
   ///    
   ///    using(newItems = new NewObjectCollection<Entity>())
   ///    {
   ///       doc.Editor.Command("._EXPLODE", ....);
   ///       
   ///       using(var trans = new OpenCloseTransaction())
   ///       {
   ///          foreach(ObjectId id in newItems)
   ///          {
   ///             var newEntity = (Entity) trans.GetObject(id, OpenMode.ForRead);
   ///             
   ///             // Operate on each new Entity created
   ///             // by the EXPLODE command.
   ///          }
   ///          trans.Commit();
   ///       }
   ///    }
   ///    
   /// </code>
   /// Capturing newly-created objects created by user-initiated 
   /// operations or commands.
   /// 
   /// Usually, a handler is added to the CommandWillStart event,
   /// and in that handler, an instance of a NewObjectCollection
   /// is either created, or an existing instance is enabled, so
   /// that it begins collecting the ObjectIds of newly-created 
   /// objects. 
   /// 
   /// At that same point, additional handlers are added to the
   /// CommandEnded/Cancelled/Failed events, one of which will
   /// be raised when the conmmand ends. The handlers for these
   /// three events typically perform some or all of these steps:
   /// 
   ///   1. Collect the ObjectIds of newly-created objects, 
   ///      and process them as needed.
   ///      
   ///   2. Disable or Dispose the NewObjectCollection so that
   ///      it does not continue collecting new objects.
   ///      
   ///   3. Remove the event handlers for the CommandEnded/
   ///      Cancelled/Failed events to avoid any further event 
   ///      handling until a CommandWillStart handler is called
   ///      again to signal the start of a command-of-interest.
   ///      
   /// Custom filtering
   /// 
   /// The NewObjectCollection class supports custom filtering
   /// that can be achieved in several ways. First, a delegate
   /// that takes an instance of the generic argument type and
   /// returns a bool can be passed to the constructor, and it
   /// will be used to constrain the objects that are added to
   /// the selection, to be only those for which the delegate
   /// returns true.
   /// 
   /// A second way of custom filtering can be achieved through
   /// derived types. A type derived from NewObjectCollection can 
   /// override the IsApplicable() method that returns a value
   /// indicating if the argument can be added to the collection.
   /// 
   /// XData and XDictionary filtering cannot be reliably used 
   /// with this class, because it only targets newly-appended
   /// objects that will typically have no XData or XDictionary
   /// attached to them yet, unless they are clones of existing 
   /// objects having XData or an XDictionary. 
   /// 
   /// In those cases where there may be XData or an XDictionary
   /// already on a new object, filering for that can be done 
   /// using either of the two basic ways of filtering outlined 
   /// above.
   ///    
   /// Advanced usage:
   /// 
   /// This class exposes several virtual methods that can be
   /// overridden by a derived type to operate on newly-created
   /// objects and avoid the need to re-open them after whatever
   /// operation created them has ended.
   /// 
   /// The OnClosing() and OnClosed() methods are called just
   /// before and just after each new object is closed, giving
   /// a derived type the opportunity to operate on the newly-
   /// created object as needed.
   /// 
   /// While this class supports these methods, the overhead
   /// of collecting new objects cannot be justified if the
   /// only reason for deriving from this class is to handle
   /// the OnClosing()/OnClosed() overrides. For only that
   /// purpose, one should instead use the NewObjectOverrule 
   /// class, which is specifically designed for that purpose.
   ///   
   /// Erased objects:
   /// 
   /// It is not uncommon for newly-created objects that have
   /// been added to a database/owner to be subsequently erased
   /// within the operation that created and added the objects.
   /// 
   /// Example:
   /// 
   /// The LINE command adds each line segment to the owning
   /// space block and database as they are drawn. If the user
   /// issues the Undo subcommand to undo creation of one or
   /// more line segments, those line segments remain in the
   /// database, and are merely erased. 
   /// 
   /// Because this class adds all newly-created objects to an
   /// instance, and does not track their erased status, the
   /// instance can contain the ObjectIds of erased objects.
   /// 
   /// The IncludingErased property controls how this class
   /// behaves WRT erased objects. If it is false (the default)
   /// erased objects are not enumerated by the instance, and
   /// not included in any other operations that yield, return,
   /// or count elements, which includes the Last property;
   /// the LastOfType() method; and so on.
   /// 
   /// The Count property returns the count of only non-erased 
   /// elements if the IncludingErased property is false. If
   /// IncludingErased is true, the Count property includes the
   /// count of erased objects.
   /// 
   /// Members not governed by IncludingErased:
   /// 
   /// The CountIncludingErased property always returns the count
   /// of all elements, including erased elements, regardless of
   /// the value of the IncludingErased property.
   /// 
   /// The NonErasedCount property always returns the count of 
   /// non-erased objects, regardless of the current value of 
   /// the IncludingErased property.
   /// 
   /// The ContainsErased property returns a value indicating if 
   /// the instance contains at least one erased element.
   /// 
   /// The indexer always returns erased elements regardless of
   /// the value of IncludingErased.
   /// 
   /// Recommended usage pattern:
   /// 
   /// It is strongly-recommended that IncludingErased be left 
   /// to false, because if it is true, the various members noted
   /// above will return or count erased objects, which can lead 
   /// to massive downstream confusion and bugs.
   /// 
   /// The decision to take this approach in the design of this
   /// class is rooted largely in the idea that for most purposes
   /// served by this class, erased objects should be treated as 
   /// if they don't exist.
   ///   
   /// </remarks>
   /// <typeparam name="T">The Targeted Type</typeparam>

   /// These interfaces exist for the purpose of exposing a
   /// managed instance of NewObjectIdCollection as an aggregate 
   /// through a property of another type, and where restricted 
   /// access is necessary.

   public interface INewObjectCollection : IDisposable
   {
      ObjectId Last { get; }
      bool ExactMatch { get; set; }
      bool IncludingErased { get; set; }
      int NonErasedCount { get; }
      int CountIncludingErased { get; }
      ObjectId OwnerId { get; set; }
      bool Contains(ObjectId item);
      public Type NonSpecificOwnerType { get; set; }
      void Clear();
   }

   public interface INewObjectCollection<T> : INewObjectCollection, IReadOnlyList<ObjectId> where T: DBObject
   {
      IEnumerable<ObjectId> OfType<TType>(bool exact = false, bool reverse = false) where TType : T;
      ObjectId LastOfType<TType>(bool exact = false) where TType : T;
   }

   public abstract class NewObjectCollection : DisposableBase, INewObjectCollection
   {
      public abstract ObjectId Last { get; }
      public abstract bool ExactMatch { get; set; }
      public abstract bool IncludingErased { get; set; }
      public abstract int NonErasedCount { get; }
      public abstract int CountIncludingErased { get; }
      public abstract ObjectId OwnerId { get; set; }
      public abstract Type NonSpecificOwnerType { get; set; }

      public abstract void Clear();
      public abstract bool Contains(ObjectId item);

      protected static bool HasOverride(Delegate del)
      {
         Assert.IsNotNull(del, nameof(del));
         return del.Method != del.Method.GetBaseDefinition();
      }
   }

   /// <summary>
   /// DEVNOTE: This class implements IReadOnlyList<T>, which in-turn
   /// inherits IEnumerable<T>. Implementing ICollection<T> on this 
   /// class is problematic, due to Linq's ToArray() method using the 
   /// Count property and CopyTo() method to optimize conversion to an 
   /// array.
   /// 
   /// In this case, the Count property may not return the total number
   /// of elements in the collection (if the IncludingErased property is
   /// false, it counts only non-erased elements). This would cause a
   /// problem with ToArray() because it uses the ICollection<T>.Count 
   /// property to get the number of elements in the array it allocates, 
   /// and then uses CopyTo() to copy the elements to that array. While 
   /// the Count property will never exceed the actual number of elements 
   /// in the underlying list, it will underflow if the list contains any
   /// erased elements and the IncludingErased property is false, causing
   /// ToArray() to allocate more elements than are copied by CopyTo().
   /// 
   /// Because this type is inherently read-only, the only advantage to
   /// implementing ICollection<T> is to allow ToArray() and ToList() to
   /// use an optimized path. However, the complications of implementing
   /// ICollection<T> outweight the benefits, and because this type will
   /// typically not contain a very large number of elements, there is
   /// little-to-no benefit to implementing ICollection<T>.
   /// 
   /// </summary>
   /// <typeparam name="T">The type represented by collection elements</typeparam>

   //public class NewObjectCollection<T> : NewObjectCollection<DBObject, T>, INewObjectCollection<T> where T : DBObject
   //{
   //}

   public class NewObjectCollection<T> : NewObjectCollection, INewObjectCollection<T> 
      where T : DBObject
   {
      protected readonly List<ObjectId> items = new List<ObjectId>();
      Overrule overrule = null;
      bool includingErased = false;
      Func<T, bool> filterPredicate = null;
      // Type ownerType = null;

      /// <summary>
      /// Creates an instance of a NewObjectCollection that by-default,
      /// will immediately begin collecting newly-created objects that
      /// are appended to the specified owner, or the current space of
      /// the current working database if no owner is specified.
      /// 
      /// Note that this class does not require the OwnerId to be the
      /// ObjectId of a BlockTableRecord. It can be any type of DBObject
      /// that owns other objects (for example, a SymbolTable).
      /// 
      /// Because this class is restricted to operating on objects owned 
      /// by a specific owner, it is both owner- and database-specific.
      /// 
      /// </summary>
      /// <param name="ownerId">The ObjectId of the owner object to which
      /// new objects are appended. New objects appended to other owners
      /// are not collected. If this argument is ObjectId.Null or is not
      /// provided, the BlockTableRecord representing the current space of 
      /// the current working database is used.</param>
      /// <param name="includingErased">Specifies if erased objects should
      /// be enumerated and returned by members of this type that return
      /// new Objects or ObjectIds of new objects. If this value is false, 
      /// only non-erased new objects are accessable through members of 
      /// this type. The default value of this property is false.</param>
      /// <param name="exactMatch">A value indicating if objects derived
      /// from the generic argument type are to be excluded. If this is
      /// true, only instances of the generic argument are collected and
      /// instances of derived types are not collected. The default for
      /// this argument is false. If the generic argument type is abstract,
      /// this property is ignored and is effectively-false</param>
      /// <param name="enabled">A value indicating if the instance is to
      /// be enabled to collect new objects upon construction</param>

      public NewObjectCollection(ObjectId ownerId = default(ObjectId),
            bool includingErased = false,
            bool exactMatch = false,
            bool enabled = true)

      {
         Initialize(ownerId, null, null, enabled, includingErased, exactMatch);
      }

      /// <summary>
      /// Overloaded constructor that accepts a Func<T, bool> that is 
      /// used constrain items added to the collection. This function 
      /// can be used in lieu of deriving from this type and overriding 
      /// the IsApplicable() method.
      /// </summary>
      /// <param name="predicate">A function that takes an instance
      /// of the targeted type and returns a value indicating if its
      /// argument should be added to the collection.</param>

      public NewObjectCollection(Func<T, bool> predicate,
         ObjectId ownerId = default(ObjectId),
         bool includingErased = false,
         bool exactMatch = false,
         bool enabled = true)
      {
         Assert.IsNotNull(predicate, nameof(predicate));
         Initialize(ownerId, null, predicate, enabled, includingErased, exactMatch);
      }

      /// <summary>
      /// Non-specific owner filtering.
      /// 
      /// This constructor creates an instance that uses a 
      /// non-specific owner filter, that collects all new 
      /// objects that are owned by any object of a specified 
      /// type.
      /// 
      /// That means:
      /// 
      /// <em>The collection can contain objects with different 
      /// owners, that can be in different Databases.</em>
      /// 
      /// ownerType must be a <em>non-abstract type derived
      /// from DBObject</em>. 
      /// 
      /// Typically, ownerType would be a concrete type, such 
      /// as a type derived from SymbolTable, DBDictionary, or 
      /// BlockTableRecord.
      /// 
      /// Sub-entity filtering by owner type:
      /// 
      /// If the ownerType is BlockReference or Polyline3d, 
      /// it will not constrain the collection to only owned 
      /// AttributeReference or Vertex3d entities, because the
      /// NewObjectOverrule uses the BlockId property for all
      /// Entity-based types, rather than the OwnerId property.
      /// 
      /// To collect only AttributeReferences in any space, 
      /// the ownerType should be BlockTableRecord. Ditto for
      /// Vertex and Vertex3d entities, and any other type of
      /// sub-entity that's owned by another Entity-based type.
      /// 
      /// To collect extension dictionaries, specify the type
      /// of owner of the desired extension dictionaries to be
      /// collected, which can be DBObject (for all extension 
      /// dictionaries) or a more-specific type of object such
      /// as Entity.
      /// 
      /// Currently, the ability to filter by any aspect of an
      /// owner object aside from its type is not implemented, 
      /// but may be added in future revisions of this type.
      /// Currently, this can be done using a predicate filter
      /// that can open and examine owner objects, and use them
      /// as filtering criteria. One should be careful if using
      /// such an approach, as it can add noticable overhead to
      /// operations that can create many instances of targeted
      /// objects. Note that the DBObjectFilter class can also
      /// be useful for caching owner object query results, by
      /// eliminating the need to open/examine the same owner
      /// object multiple/many times.
      /// 
      /// To collect Xrecords, you can specify DBDictionary 
      /// as the owner type, but you cannot specify the type 
      /// of the object that owns the DBDictionary. This may
      /// also change in a future version which may add the 
      /// ability to search the entire ownership hierarchy of 
      /// a targeted object.
      /// </summary>
      /// <param name="ownerClass"></param>
      /// <param name="predicate"></param>
      /// <param name="includingErased"></param>
      /// <param name="exactMatch"></param>
      /// <param name="enabled"></param>

      public NewObjectCollection(Type ownerType, 
         Func<T, bool> predicate,
         bool includingErased = false,
         bool exactMatch = false,
         bool enabled = true)
      {
         Assert.IsNotNull(ownerType, nameof(ownerType));
         Initialize(ObjectId.Null, ownerType, null, enabled, includingErased, exactMatch);
      }

      void Initialize(ObjectId ownerId, Type ownerType, Func<T, bool> filter, bool enabled, bool includingErased, bool exactMatch)
      {
         if(ownerType != null)
         {
            if(ownerType.IsAbstract)
               throw new ArgumentException("Invalid owner type (cannot be abstract)");
            if(!typeof(DBObject).IsAssignableFrom(ownerType))
               throw new ArgumentException("Invalid owner type (must be derived from DBObject)");
         }
         this.includingErased = includingErased;

         /// If OwnerId is null and no owner type was 
         /// provided, use the current space of the 
         /// current working database:
         if(ownerId.IsNull && ownerType == null)
         {
            Database workingDatabase = HostApplicationServices.WorkingDatabase;
            AcRx.ErrorStatus.NoDatabase.ThrowIf(workingDatabase == null);
            ownerId = workingDatabase.CurrentSpaceId;
         }
         overrule = new Overrule(this, enabled);
         // this.ownerType = ownerType;
         overrule.ownerId = ownerId;
         overrule.OwnerType = ownerType;
         overrule.noIsApplicableOverride = !HasOverride(IsApplicable);
         overrule.overridesOnClosing = HasOverride(OnClosing);
         overrule.overridesOnClosed = HasOverride(OnClosed);
         overrule.exactMatch = exactMatch && !typeof(T).IsAbstract;
         overrule.filter = filter; 
      }

      /// <summary>
      /// Assigning a value to this property causes the
      /// instance to constrain the collection by owners
      /// of the given type, rather than a specific owner,
      /// and the collection can contain elements owned by
      /// multiple/different owners, but they must all be
      /// of the same type. Most-commonly, the owner type 
      /// may be BlockTableRecord, or a type derived from
      /// SymbolTable, or DBDictionary.
      /// 
      /// The OwnerType is always an exact match and must
      /// be a concrete type, which for example, means that 
      /// SymbolTable is not a valid owner type, it must be 
      /// a concrete type that is derived from SymbolTable, 
      /// such as LayerTable, BlockTable, etc.
      /// 
      /// The value assigned to this type must be the type
      /// of the owner of elegible elements. For example,
      /// if the element type is Entity, the value of this 
      /// property must be typeof(BlockTableRecord).
      /// 
      /// If the element type is LayerTableRecord, the value
      /// assigned to this type must be LayerTable.
      /// 
      /// If this value is assigned to null, the instance
      /// reverts to filtering by a specific owner, which is
      /// the owner whose ObjectId was specified in a call
      /// to the constructor, or the BlockTableRecord of the
      /// current space at the point when the instance was
      /// created.
      /// 
      /// 
      /// </summary>
      
      public override Type NonSpecificOwnerType
      {
         get { return overrule.OwnerType; }
         set 
         { 
            Assert.IsAssignableTo<DBObject>(value, nameof(value));
            overrule.OwnerType = value; 
         }
      }

      /// <summary>
      /// Caveat Emptor:
      /// 
      /// This ALWAYS returns non-erased elements, regardless
      /// of the value of IncludingErased.
      /// </summary>
      
      public ObjectId this[int index] => items[index];

      /// <summary>
      /// Excludes erased elements in the result if
      /// the IncludingErased property is false.
      /// 
      /// To get the count of all elements including
      /// erased elements unconditionally, use the
      /// CountIncludingErased property.
      /// 
      /// To get the count of only non-erased elements
      /// unconditionally, use the NonErasedCount property.
      /// </summary>

      public int Count
      {
         get
         {
            if(IncludingErased)
               return items.Count;
            int result = 0;
            for(int i = 0; i < items.Count; i++)
            {
               if(!items[i].IsErased)
                  ++result;
            }
            return result;
         }
      }

      /// <summary>
      /// Aways searches erased objects.
      /// </summary>
      
      public override bool Contains(ObjectId item) => items.Contains(item);

      public Database Database => OwnerId.Database;

      /// <summary>
      /// This value is not validated. If ObjectId.Null
      /// is passed in, the current space of the current
      /// working database is used.
      /// 
      /// Note: Setting this property clears the instance.
      /// </summary>

      public override ObjectId OwnerId
      {
         get => overrule.ownerId;
         set
         {
            if(overrule.ownerId != value)
            {
               if(value.IsNull)
               {
                  AcRx.ErrorStatus.NoDatabase.ThrowIf(HostApplicationServices.WorkingDatabase == null);
                  value = HostApplicationServices.WorkingDatabase.CurrentSpaceId;
               }
               AcRx.ErrorStatus.NullObjectId.ThrowIf(value.IsNull);
               overrule.ownerId = value;
               Clear();
            }
         }
      }

      /// <summary>
      /// Specifies if targeted objects must be instances of
      /// the non-abstract generic argument type, or can be
      /// types derived from same. True operates only on 
      /// instances of the generic argument type, and false
      /// operates on instances of the generic argument type
      /// and any type derived from same.
      /// 
      /// If the generic argument type is abstract, this will 
      /// return false, because there cannot be an instance of 
      /// an abstract type.
      ///
      /// This is useful for example, to specifically target 
      /// BlockReferences but not Tables (which are derived 
      /// from BlockReference).
      /// 
      /// Setting the value of this property clears the instance.
      /// </summary>

      public override bool ExactMatch
      {
         get => overrule.exactMatch;
         set
         {
            if(value ^ overrule.exactMatch)
            {
               overrule.exactMatch = value && !typeof(T).IsAbstract;
               Clear();
            }
         }
      }

      /// <summary>
      /// Objects added to a database/owner can subsequently 
      /// be erased (and unerased) within the same operation 
      /// that added them.
      /// 
      /// For example, when using the LINE command, the user
      /// can undo the creation of individual segments, all 
      /// the way back to the start of the command. 
      /// 
      /// The undone segments remain in the database as erased 
      /// objects, and their ObjectIds remain in an instance 
      /// of this type after having been added to it when the
      /// object was initially added.
      /// 
      /// This type currently does not attempt to track the
      /// erasure of objects whose Ids have been added to the 
      /// instance (which can have a very-high cost, and be 
      /// very complicated when you consider that an object 
      /// can be repeatedly erased and unerased), so there must 
      /// be a means of filtering the Ids of erased objects out 
      /// of any enumeration of the instance or other operations 
      /// that return one or more elements from the instance.
      /// 
      /// The IncludingErased property controls that behavior.
      /// The default value of this property is false. 
      /// 
      /// The following behaviors describe the effect of this
      /// property's value:
      /// 
      /// The Count property returns the count of all objects
      /// if IncludingErased is true, or the count of non-erased
      /// objects otherwise.
      /// 
      /// The NonErasedCount property unconditionally excludes 
      /// erased objects, but has a cost (the collection must be 
      /// traversed to count non-erased objects).
      /// 
      /// The indexer <em>always returns erased objects</em>,
      /// regardless of the value of IncludingErased, making 
      /// the caller responsible for checking the erased status 
      /// of each returned value.
      /// 
      /// If the instance is enumerated via foreach() or via a
      /// Linq operation, erased elements are included only if
      /// the IncludingErased property is true.
      /// 
      /// Methods returning ObjectIds (such as the Last property 
      /// and LastOfType() method) return erased objects only if 
      /// IncludingErased is true. Hence, the Last property will
      /// return the last non-erased object if IncludingErased is
      /// false.
      /// 
      /// Setting the value of this property clears the instance.
      /// </summary>

      public override bool IncludingErased
      {
         get => includingErased;
         set
         {
            if(includingErased ^ value)
            {
               includingErased = value;
               Clear();
            }
         }
      }

      /// <summary>
      /// Returns the last element in the collection or 
      /// ObjectId.Null if the collection is empty. If
      /// the IncludingErased property is false, this
      /// method returns the last non-erased element in
      /// the collection.
      /// 
      /// Note: The result of this property is dependent
      /// on the IncludingErased and ExactMatch properties.
      /// </summary>

      public override ObjectId Last
      {
         get
         {
            if(IncludingErased)
               return items.Count > 0 ? items[items.Count - 1] : ObjectId.Null;
            else
               return LastOfType<T>(ExactMatch);
         }
      }

      ObjectId LastWhere(Func<ObjectId, bool> predicate)
      {
         return ReverseWhere(predicate).FirstOrDefault();
      }

      /// <summary>
      /// Returns the last element of the specified type, or 
      /// ObjectId.Null if the collection is empty or does not
      /// contain any elements of the specified type.
      /// 
      /// If the IncludingErased property is false, this method
      /// returns the last non-erased occurrence of the given Type.
      /// </summary>
      /// <typeparam name="TType">The type of the item to 
      /// retrieve. Must be the generic argument type, or
      /// a type derived from the generic argument type</typeparam>
      /// <param name="exact">Indicates if types derived from 
      /// the generic argument type are to be included in the
      /// search. If the TType argument type is abstract, this
      /// value is ignored, and is effectively-false</param>
      /// <returns>The last element of the specified type, or 
      /// ObjectId.Null if no matching element exists</returns>

      public ObjectId LastOfType<TType>(bool exact = false) where TType : T
      {
         return ReverseWhere(GetIdPredicate<TType>(exact, IncludingErased)).FirstOrDefault();
      }

      /// <summary>
      /// Returns a sequence of ObjectIds that reference
      /// DBObjects of the specified type.
      /// 
      /// If the IncludingErased property is false, the
      /// ObjectIds of erased obejects are not included.
      /// </summary>
      /// <typeparam name="TType">The type to retrieve</typeparam>
      /// <param name="exact">a value indicating if derived
      /// types should be retrieved or not</param>
      /// <param name="reverse">A value indicating if the
      /// sequence should be enumerated in reverse order.</param>
      /// <returns>A sequence of ObjectIds</returns>

      public IEnumerable<ObjectId> OfType<TType>(bool exact = false, bool reverse = false) where TType : T
      {
         var predicate = GetIdPredicate<TType>(exact, IncludingErased);
         if(!reverse)
            return items.Where(predicate);
         else
            return ReverseWhere(predicate);
      }

      IEnumerable<ObjectId> ReverseWhere(Func<ObjectId, bool> predicate)
      {
         for(int i = items.Count - 1; i > -1; i--)
         {
            if(predicate(items[i]))
               yield return items[i];
         }
      }

      /// <summary>
      /// Returns the number of non-erased elements,
      /// regardless of the value of IncludingErased.
      /// </summary>
      
      public override int NonErasedCount
      { 
         get
         {
            int result = 0;
            var span = items.AsSpan();
            for(int i = 0; i < span.Length; i++)
            {
               if(!span[i].IsErased)
                  ++result;
            }
            return result;
         }
      }

      /// <summary>
      /// Returns the total number of elements including erased
      /// elements, regardless of the value of IncludingErased.
      /// </summary>
      
      public override int CountIncludingErased => items.Count;

      /// <summary>
      /// Returns an object that enumerates all elements,
      /// or only non-erased elements depending on the value
      /// of the IncludingErased property.
      /// </summary>
      
      IEnumerable<ObjectId> FilteredItems
      {
         get
         {
            if(IncludingErased)
               return items;
            else
               return items.Where(NotErased);
         }
      }

      /// using this in lieu of 'id => !id.IsErased',
      /// avoids allocation of a new delegate.

      static bool NotErased(ObjectId id)
      {
         return !id.IsErased;
      }

      /// <summary>
      /// Enables custom filtering in derived types.
      /// 
      /// To implement custom filtering by any criteria, derive 
      /// a new type from this type; override IsApplicable(), and
      /// in the override, test whatever conditions are required
      /// and return a value indicating if the argument should be
      /// added to the collection.
      /// 
      /// If derived directly from NewObjectCollection<T>, 
      /// there's no need to supermessage the base method,
      /// as it always returns true.
      /// 
      /// An example that filters for block references of 
      /// a specific name:
      /// 
      /// <code>
      /// 
      ///   public class MyNewWidgetCollection : NewObjectCollection<BlockReference>
      ///   {
      ///      /// Collect only new insertions of the block "WIDGET".
      ///      
      ///      public override IsApplicable(BlockReference blockReference)
      ///      {
      ///         return blockReference.Name == "WIDGET";
      ///      }
      ///   }
      /// 
      /// </code>
      /// 
      /// A more-advanced example that filters for block references
      /// having names matching a specified pattern, that can include 
      /// references to anonymous, dynamic blocks:
      /// 
      ///   public class MyNewDoorCollection : NewObjectCollection<BlockReference>
      ///   {
      ///      static DynamicBlockFilter filter = new DynamicBlockFilter(
      ///         btr => btr.Name.Matches("DOOR*"));
      ///            
      ///      /// Collect only new insertions of blocks
      ///      /// whose names start with "DOOR":
      ///      
      ///      public override IsApplicable(BlockReference blockReference)
      ///      {
      ///         return filter.IsMatch(blockReference);
      ///      }
      ///   }
      /// 
      /// </summary>

      protected virtual bool IsApplicable(T subject)
      {
         return true;
      }

      /// <summary>
      /// This avoids the overhead of testing
      /// non-Entity based types
      /// </summary>

      static bool isEntity = typeof(Entity).IsAssignableFrom(typeof(T));

      /// Delegate used for entity-based types
      static readonly Func<T, ObjectId> getBlockId = obj => Unsafe.As<Entity>(obj).BlockId;

      /// Delegate used for non-entity based types:
      static readonly Func<T, ObjectId> getOwnerId = obj => obj.OwnerId;

      /// <summary>
      /// either of the above delegates is assigned to this 
      /// depending on whether T is Entity or a derived type.
      /// </summary>

      static readonly Func<T, ObjectId> GetOwnerId = isEntity ? getBlockId : getOwnerId;

      /// <summary>
      /// Called from ObjectOverrule.Close() 
      /// before the argument is closed.
      /// 
      /// The ObjectId of the argument is available
      /// and valid from this override.
      /// 
      /// In this override, the argument is open
      /// for write and can be modified by the
      /// override.
      /// 
      /// This class is optimized to avoid calling
      /// this method if it has not been overridden.
      /// </summary>
      /// <param name="obj"></param>

      protected virtual void OnClosing(T obj)
      {
      }

      /// <summary>
      /// Called from ObjectOverrule.Close() 
      /// after the argument is closed.
      /// 
      /// The ObjectId of the argument is available
      /// and valid from this override.
      /// 
      /// In this override, the argument has already
      /// been closed, and is effectively read-only.
      /// 
      /// This class is optimized to avoid calling
      /// this method if it has not been overridden.
      /// </summary>
      /// <param name="subject"></param>

      protected virtual void OnClosed(T subject)
      {
      }

      /// <summary>
      /// It's highly-recommended that the instance be
      /// cleared as soon as the contents are no longer
      /// needed. 
      /// 
      /// Failing to do this can result in significant 
      /// memory usage if the instance is not transient,
      /// and may also cause downstream confusion.
      /// 
      /// In the ObservableNewObjectOverrule derived type,
      /// the ClearOnNotify property automates clearing
      /// the instance each time an observer is notified
      /// that objects were added.
      /// </summary>

      public override void Clear()
      {
         items.Clear();
      }

      /// <summary>
      /// Removes the ObjectIds of erased objects from
      /// the collection. This method must not be called
      /// while enumerating the instance, or using any
      /// method that enumerates elements.
      /// 
      /// REVISED: Changed access from protected to private.
      /// Further testing is needed to validate the use of 
      /// this method while notifying observers of derived 
      /// types. Enforced restrictions on when this method 
      /// can be called will most-likely be needed.
      /// </summary>
      
      void PurgeErased()
      {
         items.RemoveAll(item => item.IsErased);
      }

      /// <summary>
      /// Returns a value indicating if the instance contains
      /// one or more Ids of erased objects.
      /// </summary>
      
      public bool ContainsErased
      {
         get
         {
            var span = items.AsSpan();
            for(int i = 0; i < span.Length; i++)
               if(span[i].IsErased)
                  return true;
            return false;
         }
      }

      /// <summary>
      /// This copies all elements to the given array, including
      /// erased elements.
      /// </summary>
      /// <param name="array">The array to copy the elements to.
      /// The argument must contain at least CountIncludingErased 
      /// elements.</param>
      /// <param name="clear">A value indicating if the instance
      /// should be cleared after the copy operation is completed.</param>
      /// <exception cref="ArgumentNullException"></exception>

      public void CopyTo(ObjectId[] array, bool clear = false)
      {
         Assert.IsNotNull(array, nameof(array));
         if(array.Length < items.Count)
            throw new InvalidOperationException("Insufficient array size");
         items.AsSpan().CopyTo(array);
         if(clear)
            Clear();
      }

      /// <summary>
      /// Caveat Emptor:
      /// 
      /// This API does not check the size of the array argument.
      /// The caller must use NonErasedCount to determine the number
      /// of elements that will be copied.
      /// </summary>
      /// <param name="array"></param>
      /// <param name="start"></param>
      /// <exception cref="InvalidOperationException"></exception>
      
      public void NonErasedCopyTo(ObjectId[] array)
      {
         List<ObjectId> list = new List<ObjectId>(items.Count);
         foreach(ObjectId id in items.Where(item => !item.IsErased))
            list.Add(id);
         list.TrimExcess();
         var span = list.AsSpan();
         span.CopyTo(array);
      }

      /// <summary>
      /// Overrides of this can selectively add the
      /// argument to the collection. Overrides must
      /// return a value indicating if the argument
      /// was added.
      /// 
      /// DEVNOTE: This API was designed to accomodate
      /// either a list or HashSet-based container as the 
      /// internal storage medium.
      /// </summary>
      /// <param name="id">The ObjectId to add to the collection</param>
      /// <returns>A boolean value indicating if the 
      /// argument was added to the collection.</returns>
      
      protected virtual bool Add(ObjectId id)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         int cnt = items.Count;
         items.Add(id);
         return items.Count > cnt;
      }

      static Func<ObjectId, bool> GetIdPredicate<TType>(bool exact = false, 
         bool includingErased = true) where TType : T
      {
         return RXClass<TType>.GetIdPredicate(exact, includingErased);
      }

      /// <summary>
      /// The IncludingErased property controls if 
      /// erased ids are enumerated or not:
      /// </summary>
      /// <returns></returns>
      
      public IEnumerator<ObjectId> GetEnumerator()
      {
         return FilteredItems.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      protected override void Dispose(bool disposing)
      {
         if(overrule != null)
         {
            overrule.Dispose();
            overrule = null;
         }
      }

      class Overrule : ObjectOverrule<T>
      {
         NewObjectCollection<T> owner;
         /// <summary>
         /// If assigned, there is no specific owner
         /// and any owner that is an instance of the
         /// ownerClass matches.
         /// </summary>
         RXClass ownerClass;
         Type ownerType = null;
         internal bool exactMatch = false;
         internal ObjectId ownerId;
         internal bool overridesOnClosed;
         internal bool overridesOnClosing;
         internal bool noIsApplicableOverride;
         internal Func<T, bool> filter;

         public Overrule(NewObjectCollection<T> owner, bool enabled = true)
            : base(enabled)
         {
            this.owner = owner;
         }

         public Type OwnerType
         {
            get
            {
               return ownerType;
            }
            set
            {
               if(value != null)
               {
                  if(value.IsAbstract)
                     throw new ArgumentException("Invalid owner type (must not be abstract");
                  if(!typeof(DBObject).IsAssignableFrom(value))
                     throw new ArgumentException("Invalid owner type (must be derived from DBObject)");
               }
               ownerType = value;
               ownerClass = value != null ? RXClass.GetClass(value) : null;
            }
         }

         public override void Close(DBObject obj)
         {
            try
            {
               if(obj is T subject 
                  && subject.IsNewObject 
                  && subject.IsWriteEnabled 
                  && IsApplicable(subject))
               {
                  if(overridesOnClosing)
                     owner.OnClosing(subject);
                  base.Close(obj);
                  if(overridesOnClosed)
                     owner.OnClosed(subject);
                  owner.Add(obj.ObjectId);
               }
            }
            catch(System.Exception ex)
            {
               AcConsole.Write(ex.ToString());
            }
         }

         /// <summary>
         /// Performs filtering of objects based on one of two
         /// criteria. If the ownerClass is not null, then the
         /// filtering is by the type of the owner, and any owner
         /// of the specified type matches, allowing elements with
         /// different owners.
         /// 
         /// If ownerClass is null, filtering is by specific owner
         /// that is identified by the ownerId field.
         /// </summary>
         
         bool MatchOwner(T subject)
         {
            if(ownerClass != null)  // Non-specific owner 
            {
               return ownerClass == GetOwnerId(subject).ObjectClass; // exact match only
            }
            else // Specific owner
            {
               return ownerId == GetOwnerId(subject);
            }
         }

         public bool IsApplicable(T subject)
         {
            return (!exactMatch || subject.GetType() == typeof(T))
               && MatchOwner(subject)
               && (noIsApplicableOverride || owner.IsApplicable(subject))
               && (filter == null || filter(subject));
         }

      }
   }

}
