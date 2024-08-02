/// NewObjectCollection.cs
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Collections;
using System.Collections.Generic;
using System.Extensions;
using System.Linq;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A specialization of ObjectOverrule<T> that marshals a
   /// collection containing the ObjectIds of newly-created 
   /// objects of a specified type or derived type.
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
   /// Common usage pattern:
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
   ///   objects that typically have no XData or XDictionary
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
      IEnumerable<ObjectId> OfType<TType>(bool exact = false) where TType : T;
      public ObjectId Last { get; }
      public ObjectId OwnerId { get; set; }
      public bool Contains(ObjectId item);
      ObjectId LastOfType<TType>(bool exact = false) where TType : T;
      public void Clear();
   }

   /// <summary>
   /// TODO: implement OwnerId filtering
   /// </summary>

   public class NewObjectCollection<T> : DisposableBase, INewObjectCollection<T> where T : DBObject
   {
      OrderedSet<ObjectId> items = new OrderedSet<ObjectId>();
      bool exactMatch = false;
      ObjectId ownerId = ObjectId.Null;
      Overrule overrule = null;

      public NewObjectCollection(ObjectId ownerId = default(ObjectId),
            bool enabled = true,
            bool exactMatch = false)

      {
         overrule = new Overrule(this, enabled);
         this.exactMatch = exactMatch && !typeof(T).IsAbstract;
         this.ownerId = ownerId;

         /// Constrains this overrule to operate only on new 
         /// objects that are being appended to a database:
         /// See the IsApplicable() override, which is called
         /// when a custom filter is set:

         overrule.SetCustomFilter();
      }

      public ObjectId this[int index] => items[index];
      public int Count => items.Count;
      public bool Contains(ObjectId item) => items.Contains(item);

      /// <summary>
      /// This value is not validated. If ToObjectId.Null
      /// is passed in, there is no owner filtering.
      /// </summary>

      public ObjectId OwnerId { get => ownerId; set => ownerId = value; }

      // If the generic argument type is abstract,
      // this will always return true, because there
      // cannot be an instance of an abstract type.
      //
      // This is useful, for example, to specifically
      // target BlockReferences but not Tables (which
      // are derived from BlockReference).

      public bool ExactMatch
      {
         get => exactMatch && !typeof(T).IsAbstract;
         set => exactMatch = value;
      }

      /// <summary>
      /// Returns the last element in the collection or 
      /// ToObjectId.Null if the collection is empty.
      /// </summary>

      public ObjectId Last
      {
         get
         {
            return items.Count > 0 ? items[items.Count - 1] : ObjectId.Null;
         }
      }

      /// <summary>
      /// Returns the last element of the specified type, or 
      /// ToObjectId.Null if the collection is empty or does not
      /// contain any elements of the specified type.
      /// </summary>
      /// <typeparam name="TType">The type of the item to 
      /// retrieve. Must be the generic argument type, or
      /// a type derived from the generic argument type</typeparam>
      /// <param name="exact">Indicates if types derived from 
      /// the generic argument type are to be included in the
      /// search. If the TType argument type is abstract, this
      /// value is ignored, and is effectively false</param>
      /// <returns>The last element of the specified type, or 
      /// ToObjectId.Null if no matching element exists</returns>

      public ObjectId LastOfType<TType>(bool exact = false) where TType : T
      {
         if(typeof(TType) == typeof(T))
            return Last;
         exact &= !typeof(T).IsAbstract;
         var predicate = GetPredicate<TType>(exact);
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
      /// </summary>
      /// <typeparam name="TType">The type to retrieve</typeparam>
      /// <param name="exact">a value indicating if derived
      /// types should be retrieved or not</param>
      /// <returns>A sequence of ObjectIds</returns>

      public IEnumerable<ObjectId> OfType<TType>(bool exact = false) where TType : T
      {
         return items.Where(GetPredicate<TType>(exact));
      }

      /// Type filtering functionality is currently a 'TODO',
      /// and is currently not implemented. To implement custom
      /// filtering by type or by any other criteria, derive a 
      /// new type from this type and override IsApplicable, 
      /// supermessaging the base type's method first and if it 
      /// returns true, test additional conditions as required.
      /// 
      /// An example that filters for block references of a
      /// specific name:
      /// 
      /// <code>
      /// 
      ///   public class MyNewWidgetOverrule : NewObjectOverrule<BlockReference>
      ///   {
      ///      /// Act only if the subject is an insertion of block "WIDGET":
      ///      
      ///      public override IsApplicable(RXObject subject)
      ///      {
      ///         return base.IsApplicable(subject) &&
      ///           (subject is BlockReference br && br.Name == "WIDGET");
      ///      }
      ///   }
      ///   
      /// 
      /// </code>

      bool IsApplicable(T obj)
      {
         return (ownerId.IsNull || GetOwnerId(obj) == ownerId)
            && !ExactMatch || obj.GetType() == typeof(T);
      }

      /// <summary>
      /// This avoids the overhead of testing
      /// non-Entity based types
      /// </summary>

      static bool isEntity = typeof(Entity).IsAssignableFrom(typeof(T));

      /// Delegate used for entity-based types
      static Func<T, ObjectId> getBlockId = obj =>
         (obj is Entity ent) ? ent.BlockId : obj.OwnerId;

      /// Delegate used for non-entity based types:
      static Func<T, ObjectId> getOwnerId = obj => obj.OwnerId;

      /// <summary>
      /// either of the above delegates is assigned to this 
      /// depending on whether T is an Entity or derived type.
      /// </summary>

      static Func<T, ObjectId> GetOwnerId = isEntity ? getBlockId : getOwnerId;

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
      /// Failing to do this can result in 
      /// significant memory usage.
      /// </summary>

      public void Clear()
      {
         items.Clear();
      }

      public void CopyTo(ObjectId[] array, bool clear = false)
      {
         if(array == null)
            throw new ArgumentNullException(nameof(array));
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

      public IDisposable Enable(bool clear = false)
      {
         if(overrule.Enabled)
            throw new InvalidOperationException("Instance already enabled.");
         if(clear)
            this.Clear();
         overrule.Enabled = true;
         return DisposeAction.OnDispose(() => overrule.Enabled = false);
      }

      static Func<ObjectId, bool> GetPredicate<TType>(bool exact = false) where TType : T
      {
         return RXClass<TType>.GetIdPredicate(exact);
      }

      public IEnumerator<ObjectId> GetEnumerator()
      {
         return items.GetEnumerator();
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
         public Overrule(NewObjectCollection<T> owner, bool enabled = true)
            : base(enabled)
         {
            this.owner = owner;
         }

         NewObjectCollection<T> owner;

         public override void Close(DBObject obj)
         {
            T subject = (T)obj;
            bool flag = obj.IsNewObject;
            if(flag)
               owner.OnClosing(subject);
            base.Close(obj);
            if(flag)
               owner.OnClosed(subject);
            ObjectId id = obj.ObjectId;
            if(flag && !id.IsNull)
               owner.Add(id);
         }

         public override bool IsApplicable(RXObject subject)
         {
            return subject is T obj && obj.IsNewObject && owner.IsApplicable(obj);
         }
      }
   }
}
