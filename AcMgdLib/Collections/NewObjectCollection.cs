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
   ///    using(items = new NewObjectCollection(db.CurrentSpaceId))
   ///    {
   ///    }
   ///    
   /// </code>
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
   ///   XData and XDictionary filtering must not be used with
   ///   this overrule, because it only targets newly-appended
   ///   objects 7that typically have no XData or XDictionary
   ///   attached to them yet. Setting an XData or XDictionary
   ///   filter will disable custom filtering which is used to
   ///   filter out objects that are not newly-created and are
   ///   being appended to the database.
   ///    
   ///   In rare cases where there may be XData or an XDictionary
   ///   already on a new object, filering for it can be done
   ///   by overriding the IsApplicable() method in a derived 
   ///   type, and applying the filtering therein.
   ///    
   /// Advanced usage:
   /// 
   ///   This class exposes several virtual methods that can be
   ///   overridden by a derived type to operate on newly-created
   ///   objects and avoid the need to re-open them after whatever
   ///   operation created them has ended.
   ///   
   ///   The OnClosing() and OnClosed() methods are called just
   ///   before and just after each new object is closed, giving
   ///   a derived type the opportunity to operate on the newly-
   ///   created object as needed.
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
      /// </summary>
      /// <param name="ownerId">The ObjectId of the owner object to which
      /// new objects are appended. New objects appended to other owners
      /// are not collected. If this argument is ObjectId.Null, or is not
      /// provided, the BlockTableRecord representing the current space of 
      /// the current working database is used.</param>
      /// <param name="exactMatch">A value indicating if objects derived
      /// from the generic argument type are to be excluded. If this is
      /// true, only instances of the generic argument are collected and
      /// instances of derived types are not collected. The default for
      /// this argument is false. If the generic argument type is abstract,
      /// this property is ignored and is effectively-false</param>
      /// <param name="enabled">A value indicating if the instance is to
      /// be enabled to collect new objects upon construction</param>

      public NewObjectCollection(ObjectId ownerId = default(ObjectId),
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
      }

      static bool HasOverride(Delegate del)
      {
         Assert.IsNotNull(del, nameof(del));
         return del.Method != del.Method.GetBaseDefinition();
      }

      public ObjectId this[int index] => items[index];
      public int Count => items.Count;
      public bool Contains(ObjectId item) => items.Contains(item);

      /// <summary>
      /// This value is not validated. If ObjectId.Null
      /// is passed in, the current space of the current
      /// working database is used.
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
            {
               for(int i = items.Count - 1; i > -1; i--)
               {
                  if(!items[i].IsErased)
                     return items[i];
               }
               return ObjectId.Null;
            }
         }
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
         exact &= !typeof(T).IsAbstract;
         var predicate = GetPredicate<TType>(exact, IncludingErased);
         for(int i = items.Count - 1; i > -1; i--)
         {
            if(predicate(items[i]))
               return items[i];
         }
         return ObjectId.Null;
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
         if(!reverse)
            return items.Where(GetPredicate<TType>(exact, IncludingErased));
         else
            return Reverse.Where(GetPredicate<TType>(exact, IncludingErased));
      }

      IEnumerable<ObjectId> Reverse
      {
         get
         {
            for(int i = items.Count - 1; i > -1; i--)
            {
               yield return items[i];
            }
            
         }
      }

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
      /// Returns the sequence of elements with erased
      /// elements removed if the IncludingErased property
      /// is false.
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
      /// a new type from this type; override IsApplicable(), 
      /// and test whatever conditions are required.
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
      ///      public override IsApplicable(BlockReference subject)
      ///      {
      ///         return subject.Name == "WIDGET");
      ///      }
      ///   }
      ///   
      /// 
      /// </code>
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
      static Func<T, ObjectId> getBlockId = obj => Unsafe.As<Entity>(obj).BlockId;

      /// Delegate used for non-entity based types:
      static Func<T, ObjectId> getOwnerId = obj => obj.OwnerId;

      /// <summary>
      /// either of the above delegates is assigned to this 
      /// depending on whether T is an Entity or derived type.
      /// </summary>

      static readonly Func<T, ObjectId> GetOwnerId = isEntity ? getBlockId : getOwnerId;

      /// <summary>
      /// Called from ObjectOverrule.Close() 
      /// before base.Close() is called.
      /// </summary>
      /// <param name="obj"></param>

      protected virtual void OnClosing(T obj)
      {
      }

      /// <summary>
      /// Called from ObjectOverrule.Close() 
      /// after base.Close() is called.
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
      /// </summary>
      
      public void PurgeErased()
      {
         items.RemoveAll(item => item.IsErased);
      }

      /// <summary>
      /// Returns a value indicating if the instance contains
      /// one or more Ids of erased objects.
      /// </summary>
      
      public bool ContainsErased => items.Any(item => item.IsErased);

      /// <summary>
      /// If IncludingErased is false, only non-erased entries
      /// are copied to the destination.
      /// </summary>
      /// <param name="array"></param>
      /// <param name="clear"></param>
      /// <exception cref="ArgumentNullException"></exception>

      public void CopyTo(ObjectId[] array, bool clear = false)
      {
         if(array == null)
            throw new ArgumentNullException(nameof(array));
         if(!IncludingErased)
            PurgeErased();
         items.CopyTo(array);
         if(clear)
            Clear();
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

      /// <summary>
      /// TO BE DEPRECIATED
      /// 
      /// Enables the instance if not already enabled,
      /// and returns an IDisposable that when disposed
      /// will disable the instance.
      /// 
      /// If the instance is already enabled when this
      /// is called, an exception is raised.
      /// 
      /// A typical usage pattern for this method, is to
      /// call it before executing one or more commands,
      /// and then dispose the result after the command(s) 
      /// have completed, so that new objects created by 
      /// the command(s) can be captured.
      /// 
      /// </summary>
      /// <param name="clear">A value indicating if the
      /// instance should be cleared before it is enabled.
      /// </param>
      /// <returns>An IDisposable that when disposed will
      /// disable the instance.</returns>

      //public IDisposable Enable(bool clear = false)
      //{
      //   if(overrule.Enabled)
      //      throw new InvalidOperationException("Instance already enabled.");
      //   if(clear)
      //      this.Clear();
      //   overrule.Enabled = true;
      //   return DisposeAction.OnDispose(() => overrule.Enabled = false);
      //}

      static Func<ObjectId, bool> GetPredicate<TType>(bool exact = false, 
         bool includingErased = true) where TType : T
      {
         return RXClass<TType>.GetIdPredicate(exact, includingErased);
      }

      public IEnumerator<ObjectId> GetEnumerator()
      {
         /// IncludingErased controls if erased ids are enumerated:
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
