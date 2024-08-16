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
   /// commands are targeted, and how they are started.
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
   ///       foreach(ObjectId newId in newItems)
   ///       {
   ///          // Operate on the ObjectId of each object
   ///          // created by the EXPLODE command here...
   ///       }
   ///    }
   ///    
   /// </code>
   /// Capturing newly-created objects created by user-initiated 
   /// operations.
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
   /// XData and XDictionary filtering must not be used with
   /// this overrule, because it only targets newly-appended
   /// objects that typically have no XData or XDictionary
   /// attached to them yet. Setting an XData or XDictionary
   /// filter will disable custom filtering which is used to
   /// filter out objects that are not newly-created.
   ///  
   /// In rare cases where there may be XData or an XDictionary
   /// already on a new object, filering for it can be done
   /// by overriding the IsApplicable() method in a derived 
   /// type, and applying the filtering therein.
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
   /// The NonErasedCount property always returns the count of 
   /// non-erased objects, regardless of the current value of 
   /// the IncludingErased property.
   /// 
   /// The CountIncludingErased property always returns the count
   /// of all elements, including erased elements, regardless of
   /// the value of the IncludingErased property.
   /// 
   /// The ContainsErased property returns a value indicating if 
   /// the instance contains at least one erased element.
   /// 
   /// It is strongly-recommended that IncludingErased be left to
   /// false, because if it is true, the various properties and
   /// methods noted above return or count erased objects, which 
   /// can lead to massive downstream confusion and bugs.
   /// 
   /// The decision to take this approach in the design of this
   /// class is rooted largely in the idea that erased objects
   /// should be treated as non-existent.
   ///   
   /// </remarks>
   /// <typeparam name="T">The Targeted Type</typeparam>

   /// This interface is provided for the purpose of exposing a
   /// managed instance of NewObjectIdCollection as an aggregate 
   /// through a property of another type, and where restricted 
   /// access is necessary.

   public interface INewObjectCollection<T> : IReadOnlyList<ObjectId>, IDisposable where T: DBObject
   {
      IEnumerable<ObjectId> OfType<TType>(bool exact = false, bool reverse = false) where TType : T;
      ObjectId Last { get; }
      bool ExactMatch { get; set; }
      bool IncludingErased { get; set; }
      int NonErasedCount { get; }
      ObjectId OwnerId { get; set; }
      bool Contains(ObjectId item);
      void Clear();
      ObjectId LastOfType<TType>(bool exact = false) where TType : T;
   }

   public abstract class NewObjectCollection : DisposableBase
   {
   }

   public class NewObjectCollection<T> : DisposableBase, INewObjectCollection<T> where T : DBObject
   {
      List<ObjectId> items = new List<ObjectId>();
      Overrule overrule = null;

      /// <summary>
      /// Creates an instance of a NewObjectCollection that by-default
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
         overrule = new Overrule(this, enabled);
         overrule.noIsApplicableOverride = !HasOverride(IsApplicable);
         overrule.overridesOnClosing = HasOverride(OnClosing);
         overrule.overridesOnClosed = HasOverride(OnClosed);
         overrule.exactMatch = exactMatch && !typeof(T).IsAbstract;
         if(ownerId.IsNull)
         {
            Database workingDatabase = HostApplicationServices.WorkingDatabase;
            AcRx.ErrorStatus.NoDatabase.ThrowIf(workingDatabase == null);
            ownerId = workingDatabase.CurrentSpaceId;
         }
         overrule.ownerId = ownerId;
         this.IncludingErased = includingErased;
      }

      static bool HasOverride(Delegate del)
      {
         Assert.IsNotNull(del, nameof(del));
         return del.Method != del.Method.GetBaseDefinition();
      }

      /// <summary>
      /// Always returns non-erased elements, regardless
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
      
      public bool Contains(ObjectId item) => items.Contains(item);

      public Database Database => OwnerId.Database;

      /// <summary>
      /// This value is not validated. If ObjectId.Null
      /// is passed in, the current space of the current
      /// working database is used.
      /// 
      /// Note: Setting this property clears the instance.
      /// </summary>

      public ObjectId OwnerId
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

      // If the generic argument type is abstract,
      // this will always return false, because there
      // cannot be an instance of an abstract type.
      //
      // This is useful for example, to specifically
      // target BlockReferences but not Tables (which
      // are derived from BlockReference).

      public bool ExactMatch
      {
         get => overrule.exactMatch;
         set
         {
            overrule.exactMatch = value && !typeof(T).IsAbstract;
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
      /// that return one or more items from the instance.
      /// 
      /// The IncludingErased property controls that behavior.
      /// The default value of this property is false. 
      /// 
      /// The following behaviors describe the effect of this
      /// property's value:
      /// 
      /// The Count property <em>always includes erased objects</em>.
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
      /// </summary>

      public bool IncludingErased
      {
         get; set;
      }

      /// <summary>
      /// Returns the last element in the collection or 
      /// ObjectId.Null if the collection is empty. If
      /// the IncludingErased property is false, this
      /// method returns the last non-erased element in
      /// the collection.
      /// </summary>

      public ObjectId Last
      {
         get
         {
            if(IncludingErased)
               return items.Count > 0 ? items[items.Count - 1] : ObjectId.Null;
            else
               return LastWhere(id => !id.IsErased);
         }
      }

      ObjectId LastWhere(Func<ObjectId, bool> predicate)
      {
         for(int i = items.Count - 1; i > -1; i--)
         {
            if(predicate(items[i]))
               return items[i];
         }
         return ObjectId.Null;
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
         if(typeof(TType) == typeof(T))
            return Last;
         return ReverseWhere(GetPredicate<TType>(exact, IncludingErased)).FirstOrDefault();
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
         var predicate = GetPredicate<TType>(exact, IncludingErased);
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
      
      public int NonErasedCount
      { 
         get
         {
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
      /// Returns the total number elements including erased
      /// elements, regardless of the value of IncludingErased.
      /// </summary>
      
      public int CountIncludingErased => items.Count;

      /// <summary>
      /// Returns the sequence of elements with erased
      /// elements removed if the IncludingErased property
      /// is false. The enumerator for the instance uses
      /// this property.
      /// </summary>
      
      IEnumerable<ObjectId> Items
      {
         get
         {
            if(IncludingErased)
               return items;
            else
               return items.Where(item => !item.IsErased);
         }
      }

      /// <summary>
      /// Enables custom filtering in derived types.
      /// 
      /// To implement custom filtering by any criteria, derive 
      /// a new type from this type; override IsApplicable(), and
      /// in the override, test whatever conditions are required.
      /// 
      /// If derived directly from NewObjectCollection<T>, 
      /// there's no need to supermessage the base method,
      /// as it returns true by default.
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
      /// matching a specified pattern, that can include references
      /// to anonymous, dynamic blocks:
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

      protected virtual bool IsApplicable(T obj)
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
      /// before the argument is closed.. 
      /// 
      /// The ObjectId of the argument is not
      /// valid from within this override.
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
      /// from this override.
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
      /// memory usage if the instance is not transient.
      /// 
      /// In the ObservableNewObjectOverrule derived type,
      /// the ClearOnNotify property automates clearing
      /// the instance.
      /// </summary>

      public virtual void Clear()
      {
         items.Clear();
      }

      /// <summary>
      /// Removes the ObjectIds of erased objects from
      /// the collection. This method must not be called
      /// while enumerating the instance, or using any
      /// method that enumerates elements.
      /// 
      /// TODO: further testing is needed to validate the
      /// use of this method while notifying observers of
      /// derived types. Enforced restrictions on when this 
      /// method can be called will most-likely be needed.
      /// </summary>
      
      public void PurgeErased()
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
            for(int i = 0; i < items.Count; i++)
               if(items[i].IsErased)
                  return true;
            return false;
         }
      }
      /// <summary>
      /// This method enables/disables collection of new
      /// objects without having to dispose the instance.
      /// </summary>
      
      public bool Enabled
      {
         get => overrule.Enabled;
         set => overrule.Enabled = value;
      }

      /// <summary>
      /// This copies all elements to the given array, including
      /// erased elements.
      /// </summary>
      /// <param name="array">The array to copy the elements to.
      /// The argument must contain at least Count elements.</param>
      /// <param name="clear">A value indicating if the instance
      /// should be cleared after the copy operation is completed.</param>
      /// <exception cref="ArgumentNullException"></exception>

      public void CopyTo(ObjectId[] array, bool clear = false)
      {
         Assert.IsNotNull(array, nameof(array));
         items.CopyTo(array);
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
      
      public void NonErasedCopyTo(ObjectId[] array, int start = 0)
      {
         int i = start;
         foreach(ObjectId id in items.Where(item => !item.IsErased))
            array[i++] = id;

      }

      /// <summary>
      /// Overrides of this can selectively add the
      /// argument to the collection.
      /// </summary>
      /// <param name="id"></param>
      
      protected virtual void Add(ObjectId id)
      {
         items.Add(id);
      }

      static Func<ObjectId, bool> GetPredicate<TType>(bool exact = false, 
         bool includingErased = true) where TType : T
      {
         return RXClass<TType>.GetIdPredicate(exact, includingErased);
      }

      /// <summary>
      /// The IncludingErased property controls if 
      /// erased ids are included or not:
      /// </summary>
      /// <returns></returns>
      
      public IEnumerator<ObjectId> GetEnumerator()
      {
         return Items.GetEnumerator();
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
         internal bool exactMatch = false;
         internal ObjectId ownerId;
         internal bool overridesOnClosed;
         internal bool overridesOnClosing;
         internal bool noIsApplicableOverride;

         public Overrule(NewObjectCollection<T> owner, bool enabled = true)
            : base(enabled)
         {
            this.owner = owner;
            SetCustomFilter();
         }

         public override void Close(DBObject obj)
         {
            T subject = (T)obj;
            bool flag = obj.IsNewObject;
            if(flag && overridesOnClosing)
               owner.OnClosing(subject);
            base.Close(obj);
            if(flag && overridesOnClosed)
               owner.OnClosed(subject);
            ObjectId id = obj.ObjectId;
            if(flag && !id.IsNull)
               owner.Add(id);
         }

         public override bool IsApplicable(RXObject subject)
         {
            return subject is T obj && (obj.IsNewObject
               && ownerId == GetOwnerId(obj)
               && !exactMatch || subject.GetType() == typeof(T)
               && noIsApplicableOverride || owner.IsApplicable(obj));
         }
      }
   }
}
