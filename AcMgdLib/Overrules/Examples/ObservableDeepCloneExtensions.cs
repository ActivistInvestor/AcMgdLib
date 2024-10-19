/// ObservableDeepCloneExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods supporting deep clone operations 
/// that are observed by an ObjectOverrule. The observing
/// ObjectOverrule invokes a user-defined action on each
/// pair of source/clone objects, allowing either or both
/// of same to be acted on within the deep clone operation.
/// 
/// Revisions:
/// 
/// Renamed all extension methods to "CopyObjects()" to
/// reflect their (roughly) functional equivalence to the 
/// ActiveX API's AcadDatabase.CopyObjects() method.
/// 
/// Example code moved to ObservableDeepCloneExtensionsExample.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class ObservableDeepCloneExtensions
   {
      /// <summary>
      /// Extension methods that supplement the Database's class'
      /// DeepCloneObjects() method, adding support for operating
      /// on each source/clone pair at the point when each clone 
      /// is created and added to its owner, while it is open for
      /// write and able to be modified.
      /// 
      /// Using this method, one can completely avoid additional
      /// post-processing steps performed after DeepCloneObjects()
      /// returns, that typically involve starting a Transaction,
      /// iterating over the IdMapping, and opening each clone to 
      /// perform additional operations on them. 
      /// 
      /// By operating on clones at the point where they are created
      /// within the deep clone process, this method can be far-more 
      /// efficient than using the stock DeepCloneObjects() method 
      /// followed by code that post-processes the clones using the 
      /// IdMapping and a transaction.
      /// 
      /// The performance advantage achieved through the use of this
      /// method in lieu of calling DeepCloneObjects(), mostly comes
      /// from the fact that the delegate that's passed to this method 
      /// is called and passed the source object and its clone, which
      /// are already open within the deep-clone operation. 
      /// 
      /// In addition to being able to operate on the clone, the
      /// delegate passed to this method can also operate on the 
      /// source object by upgrading it to OpenMode.ForWrite.
      /// 
      /// A basic usage example is included in the file:
      /// 
      ///   ObservableDeepCloneExtensionsExample.cs
      ///    
      /// As can be seen in the example, after the entities have been 
      /// cloned, there's no need for additional code that starts a 
      /// transaction; iterates over the IdMapping; opens each clone 
      /// and transforms it; etc. Instead of that, the delegate that's
      /// passed to the CopyObjects() method does everything needed,
      /// effectively-reducing the task of cloning the objects, and
      /// transforming the clones to a single line of code:
      /// 
      ///   ids.CopyObjects((source, clone) => clone.TransformBy(xform));
      ///   
      /// Because both the source and clone are already open when 
      /// the delegate is called and passed to them, we can completely 
      /// avoid the significant overhead associated with iteratively
      /// opening each clone in a transaction and modifying it, after 
      /// the deep clone operation ends.
      /// 
      /// Implementation Notes:
      /// 
      /// The generic argument does not constrain the type(s) of
      /// objects that are cloned. It only constraints what subset
      /// of those objects are passed to the action delegate. If
      /// for example, the generic argument is BlockReference, all
      /// objects in the input collection are cloned regardless of
      /// their type, but the action delegate will only be called 
      /// for those that are block references.
      /// 
      /// Primary verses non-primary delegate invocation:
      /// 
      /// By default, the delegate passed to CopyObjects() is only
      /// called for primary clones. That behavior is controlled by
      /// the optional primaryOnly argument passed to the core API.
      /// If one wishes to act on non-primary clones (which could
      /// be any object that is cloned as part of cloning a primary
      /// object - such as an extension dictionary or Xrecord), the 
      /// primarOnly argument can be set to false, and the generic
      /// argument type can be set to the type of object which the
      /// caller wants to operate on, or one of its base types.
      /// 
      /// Targeting Sub-entity and owned object types:
      /// 
      /// If the generic argument is any sub-entity type such as 
      /// AttributeReference, Vertex, etc., the primaryOnly argument 
      /// is ignored and is effectively-false. This is due to the 
      /// fact that sub-entities are always passed with the isPrimary 
      /// argument set to false when their owner object is a primary 
      /// clone.
      /// 
      /// The same condition applies to generic argument types
      /// that can be owned by primary clones. For example, an
      /// extension dictionary (DBDictionary) will never be a
      /// primary clone. In the case of non-subentity types that
      /// are owned by a cloned object, the primaryOnly argument
      /// must be set to false in order for an action that targets
      /// that type to be called. Between the constraint imposed
      /// by the generic argument and the primaryOnly property,
      /// a caller can target a specific type of owned object in
      /// a deep clone operation.
      ///   
      /// Overloading:
      /// 
      /// There are a number of overloads of CopyObjects() that are 
      /// divided into two groups. One group targets the Database 
      /// object (like the DeepCloneObjects() method), and another 
      /// group targets ObjectIdCollection or IEnumerable<ObjectId>. 
      /// 
      /// This latter group supports modular use of these APIs in 
      /// scenarios where there may not be a Database to operate on 
      /// directly. The only difference between these two groups is 
      /// that the latter obtains the Database from the collection 
      /// elements.
      /// 
      /// Collection argument conventions (either ObjectIdCollection 
      /// or IEnumerable<ObjectId>):
      /// 
      /// The same rules that apply to DeepCloneObjects() apply to the
      /// collections which this method operates on: All elements in 
      /// the collection must have the same owner object.
      /// 
      /// Implied owner:
      /// 
      /// Overloads that do not take an OwnerId property will clone the 
      /// objects to their current owner. If an overload that takes an 
      /// OwnerId property receives ObjectId.Null in that argument, the
      /// behavior is the same as calling an overload taking no ownerId.
      /// 
      /// The simplest overloads are also the most commonly-used ones,
      /// which are those that accept only an ownerid and an action, or 
      /// only an action argument.
      /// 
      /// There are also overloads provided that accept a Matrix3d, and
      /// implicitly transform the clones by the given matrix.
      /// 
      /// Note that arguments are documented only for the primary API,
      /// and the argument documentation for all other overloads can be 
      /// inferred from same.
      /// </summary>
      /// 
      /// 
      /// <summary>
      /// The core CopyObjects() method.
      /// 
      /// <typeparam name="T">The type of the DBObject that is
      /// passed to the action. Only instances of the this argument
      /// type are passed to the action. All other types of objects
      /// that are also cloned are not passed to the action.</typeparam>
      /// <param name="db">The Database containing the objects to be
      /// deep cloned.</param>
      /// <param name="ids">The ObjectIdCollection containing the 
      /// objects to be cloned. All objects must have the same owner.</param>
      /// <param name="ownerId">The ObjectId of the destination owner
      /// object which the clones are to be added to, or ObjectId.Null
      /// to add the clones to the owner of the source objects.</param>
      /// <param name="action">A delegate that takes two instances of
      /// the generic argument type, the first being a source object
      /// and the second being the clone of it. The source object is 
      /// open for read, and the clone is open for write.</param>
      /// <param name="primaryOnly">A value indicating if the action
      /// should be invoked on all source/clone pairs, or only primary 
      /// source/clone pairs.</param>
      /// <returns>An IdMapping instance representing the result of 
      /// the operation</returns>

      public static IdMapping CopyObjects<T>(this Database db,
         ObjectIdCollection ids,
         ObjectId ownerId,
         Action<T, T> action,
         bool primaryOnly = true) where T : DBObject
      {
         if(db == null)
            throw new ArgumentNullException(nameof(db));
         return DeepClone(db, ids, ownerId, action, primaryOnly);
      }

      /// <summary>
      /// Overload of above method taking an IEnumerable<ObjectId>
      /// in lieu of ObjectIdCollection:
      /// </summary>

      public static IdMapping CopyObjects<T>(this Database db,
         IEnumerable<ObjectId> ids,
         ObjectId ownerId,
         Action<T, T> action,
         bool primaryOnly = true) where T : DBObject
      {
         return CopyObjects(db, ToCollection(ids), ownerId, action, primaryOnly);
      }

      /// <summary>
      /// An overloaded version of the above that deep 
      /// clones entities to the same owner space. The
      /// ObjectIdCollection argument must contain only
      /// ObjectIds of entities or a derived type.
      /// </summary>

      public static IdMapping CopyObjects<T>(this Database db,
         ObjectIdCollection ids,
         Action<T, T> action) where T : Entity
      {
         return DeepClone(db, ids, ObjectId.Null, action, true);
      }

      public static IdMapping CopyObjects<T>(this Database db,
         IEnumerable<ObjectId> ids,
         Action<T, T> action) where T : Entity
      {
         return CopyObjects(db, ToCollection(ids), action);
      }

      /// <summary>
      /// An overload that operates only on entities, and 
      /// implicitly transforms the clones using the supplied
      /// transformation matrix.
      /// </summary>
      /// <param name="ids">The ObjectIdCollection containing
      /// the ObjectIds of the entities to be copied. All elements
      /// must have the same owner.</param>
      /// <param name="xform">A Matrix3d that describes the
      /// transformation to be applied to each clone. If this 
      /// argument is the identity matrix, no transformation
      /// is performed.</param>

      public static IdMapping CopyObjects(this Database db,
         ObjectIdCollection ids,
         Matrix3d xform)
      {
         Action<Entity, Entity> action = null;
         if(!xform.IsEqualTo(Matrix3d.Identity))
            action = (source, clone) => clone.TransformBy(xform);
         return CopyObjects(db, ids, action);
      }

      public static IdMapping CopyObjects(this Database db,
         IEnumerable<ObjectId> ids,
         Matrix3d xform)
      {
         return CopyObjects(db, ToCollection(ids), xform);
      }

      /// <summary>
      /// What follows are versions of the above extension methods 
      /// targeting ObjectIdCollection and IEnumerable<ObjectId>.
      /// 
      /// Their main difference is that they are invoked on a given
      /// collection and obtain the database from the collection.
      /// </summary>
      /// <typeparam name="T">The type of the DBObject that is to be
      /// passed to the action. Only instances of this type will be
      /// passed to the action.</typeparam>
      /// <param name="ids">The ObjectIdCollection containing the 
      /// objects to be cloned. All objects must have the same owner.</param>
      /// <param name="ownerId">The ObjectId of the destination owner
      /// object which the clones are to be added to, or ObjectId.Null
      /// to clone the objects to their current owner.</param>
      /// <param name="primaryOnly">A value indicating if the action
      /// should be invoked on all source/clone pairs, or only those
      /// that are primary source/clone pairs.</param>
      /// <param name="action">A delegate that takes two instances of
      /// the generic argument type, the first being the source object
      /// and the second being the clone of it. The source object is 
      /// open for read, and the clone is open for write.</param>
      /// <returns>An IdMapping instance representing the result of 
      /// the operation</returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="AcRx.Exception"></exception>

      public static IdMapping CopyObjects<T>(this ObjectIdCollection ids,
         ObjectId ownerId,
         Action<T, T> action,
         bool primaryOnly = true) where T : DBObject
      {
         return DeepClone(null, ids, ownerId, action, primaryOnly);
      }

      public static IdMapping CopyObjects<T>(this IEnumerable<ObjectId> ids,
         ObjectId ownerId,
         Action<T, T> action,
         bool primaryOnly = true) where T : DBObject
      {
         return CopyObjects(ToCollection(ids), ownerId, action, primaryOnly);
      }

      /// <summary>
      /// Clones the objects represented by the collection to the
      /// same owner space they current reside in, and invokes the
      /// given action on each source/clone pair. The ids argument
      /// must reference entities or a derived type.
      /// </summary>

      public static IdMapping CopyObjects<T>(this ObjectIdCollection ids,
         Action<T, T> action) where T : Entity
      {
         return CopyObjects(ids, ObjectId.Null, action, true);
      }

      public static IdMapping CopyObjects<T>(this IEnumerable<ObjectId> ids,
         Action<T, T> action) where T : Entity
      {
         return CopyObjects(ToCollection(ids), ObjectId.Null, action);
      }

      /// <summary>
      /// An extension method targeting ObjectIdCollection, 
      /// that simplifies copying entities to the same owner 
      /// space, and transforming the copies.
      /// </summary>
      /// <param name="ids">An ObjectIdCollection containing the
      /// ids of the entities to be copied. All elements must be 
      /// references to Entity or a derived type, and must have
      /// the same owner.</param>
      /// <param name="xform">The transformation matrix to be 
      /// applied to the clones of the input entities. If this
      /// argument is the Identity matrix no transformation is
      /// performed.</param>
      /// <returns>An IdMapping representing the result of the
      /// operation</returns>

      public static IdMapping CopyObjects(this ObjectIdCollection ids, Matrix3d xform)
      {
         Validate<Entity>(ids);
         Action<Entity, Entity> action = null;

         /// If the caller passed the Identity matrix, there's
         /// no transformation, and no need for the Overrule,
         /// which is not used if we pass a null action:

         if(!xform.IsEqualTo(Matrix3d.Identity))
            action = (source, clone) => clone.TransformBy(xform);

         return CopyObjects(ids, ObjectId.Null, action, true);
      }

      /// <summary>
      /// An overload of the above method that targets IEnumerable<ObjectId>.
      /// </summary>

      public static IdMapping CopyObjects(this IEnumerable<ObjectId> ids, Matrix3d xform)
      {
         return CopyObjects(ToCollection(ids), xform);
      }

      /// <summary>
      /// Worker for all CopyObjects() overloads:
      /// </summary>

      static IdMapping DeepClone<T>(Database db, ObjectIdCollection ids, ObjectId ownerId, Action<T, T> action, bool primaryOnly = true) where T : DBObject
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         IdMapping map = new IdMapping();
         if(ids.Count == 0)
            return map;
         if(ownerId.IsNull)
            ownerId = GetOwnerId(ids);
         if(ownerId.IsNull)
            throw new AcRx.Exception(AcRx.ErrorStatus.InvalidOwnerObject);
         db = db ?? ownerId.Database;
         if(db == null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NoDatabase);
         if(ownerId.Database != db || ids[0].Database != db)
            throw new AcRx.Exception(AcRx.ErrorStatus.WrongDatabase);
         DeepCloneObjectsOverrule<T> overrule = null;
         if(action != null)
            overrule = new DeepCloneObjectsOverrule<T>(ownerId, primaryOnly, action);
         using(overrule)
         {
            db.DeepCloneObjects(ids, ownerId, map, false);
         }
         return map;
      }

      static ObjectId GetOwnerId(ObjectIdCollection ids)
      {
         if(ids != null && ids.Count > 0 && !ids[0].IsNull)
         {
            using(DBObject obj = ids[0].Open(OpenMode.ForRead))
            {
               return obj is Entity ent ? ent.BlockId : obj.OwnerId;
            }
         }
         throw new AcRx.Exception(AcRx.ErrorStatus.InvalidObjectId);
      }

      static ObjectIdCollection ToCollection(this IEnumerable<ObjectId> ids)
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         return new ObjectIdCollection(ids as ObjectId[] ?? ids.ToArray());
      }

      static readonly RXClass entityClass = RXObject.GetClass(typeof(Entity));

      public static void Validate(this ObjectIdCollection ids)
      {
         Validate<DBObject>(ids);
      }

      public static void Validate<T>(this ObjectIdCollection ids) where T : DBObject
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         bool checkType = typeof(T) != typeof(DBObject);
         RXClass rxclass = RXObject.GetClass(typeof(T));
         for(int i = 0; i < ids.Count; i++)
         {
            ObjectId id = ids[i];
            if(id.IsNull)
               throw new AcRx.Exception(AcRx.ErrorStatus.NullObjectId);
            if(checkType && !id.ObjectClass.IsDerivedFrom(rxclass))
               throw new AcRx.Exception(AcRx.ErrorStatus.WrongObjectType);
         }
      }
   }

   /// <summary>
   /// This class is used by the CopyObjects() extension methods,
   /// but can also be used directly when calling DeepCloneObjects().
   /// Create the instance just before calling DeepCloneObjects(),
   /// and dispose it immediately after that method returns. Supply
   /// an Action that will be called for each instance of the generic
   /// argument that is cloned:
   /// 
   /// <code>
   /// 
   ///  A minimal example that will transform each cloned entity:
   ///  
   ///  Database db = HostApplicationServices.WorkingDatabase;
   ///  Matrix3d xform = Matrix3d.Displacment(new Vector3d(1, 1, 1));
   ///  ObjectIdCollection ids = /// add objects to be cloned
   ///  var owner = db.CurrentSpaceId;
   ///  Action<Entity, Entity> action = (src, clone) => clone.TransformBy(xform);
   ///  IdMapping map = new IdMapping();
   ///  using(new DeepCloneObjectsOverrule<Entity>(owner, true, action))
   ///  {
   ///     db.DeepCloneObjects(ids, owner, map, false);
   ///  }
   ///    
   /// 
   /// </code>
   /// </summary>
   /// <typeparam name="T">The Type of DBObject that is to
   /// be overruled and acted on</typeparam>
   
   public class DeepCloneObjectsOverrule<T> : ObjectOverrule where T : DBObject
   {
      static readonly RXClass targetClass = RXObject.GetClass(typeof(T));
      static readonly Func<DBObject, ObjectId, bool> IsOwnedBy;
      Action<T, T> cloneAction;
      ObjectId ownerId;
      bool primaryOnly = true;
      bool disposed = false;

      static DeepCloneObjectsOverrule()
      {
         if(typeof(Entity).IsAssignableFrom(typeof(T)))
            IsOwnedBy = (obj, ownerId) => Unsafe.As<Entity>(obj).BlockId == ownerId;
         else
            IsOwnedBy = (obj, ownerId) => obj.OwnerId == ownerId;
      }

      public DeepCloneObjectsOverrule(ObjectId ownerId, bool primaryOnly, Action<T, T> cloneAction)
      {
         this.cloneAction = cloneAction;
         this.ownerId = ownerId;
         if(IsSubEntityTarget)
            this.primaryOnly = false;
         else
            this.primaryOnly = primaryOnly;
         AddOverrule(targetClass, this, true);
      }

      /// <summary>
      /// DeepClone() is never passed a sub-entity (AttributeReference, 
      /// Vertex, etc) with the isPrimary argument set to true when the 
      /// owner is being cloned, so if the generic argument type is the 
      /// type of a sub-entity, primaryOnly must be false, otherwise the 
      /// action will not be invoked on the DeepClone() arguments.
      /// </summary>
      
      static bool IsSubEntityTarget
      {
         get
         {
            return typeof(T) == typeof(AttributeReference)
               || typeof(T) == typeof(Vertex)
               || typeof(T) == typeof(Vertex2d);
         }
      }

      public override DBObject DeepClone(DBObject obj, DBObject owner, IdMapping idMap, bool isPrimary)
      {
         var result = base.DeepClone(obj, owner, idMap, isPrimary);
         if((isPrimary || !primaryOnly)
            && (ownerId.IsNull || IsOwnedBy(obj, ownerId))
            && obj is T source && result is T clone)
         {
            OnCloned(source, clone);
         }
         return result;
      }

      protected virtual void OnCloned(T source, T clone)
      {
         cloneAction?.Invoke(source, clone);
      }

      protected override void Dispose(bool disposing)
      {
         if(!disposed && disposing)
         {
            disposed = true;
            RemoveOverrule(targetClass, this);
         }
         base.Dispose(disposing);
      }

   }
}
