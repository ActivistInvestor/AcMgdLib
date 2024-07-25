/// EntityExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Entity class.

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A specialization of ObjectOverrule that provides an entry
   /// point for operating on cloned objects at the point when
   /// the objects are cloned. This class can eliminate the need
   /// to subsequently open newly-created clones to perform various
   /// operations on them. The constructor accepts one of two types
   /// of delegate that will be called at the point when an object
   /// has been cloned, and it is a primary clone. 
   /// 
   /// The delegates are called before the clone has been added to
   /// to an owner or to the Database.
   /// 
   /// Note that the generic argument type can be any type, but 
   /// this overrule will only be called for cloned objects that 
   /// are instances of the generic argument type, regardless of 
   /// what other type(s) of objects are also cloned.
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class DeepCloneOverrule<T> : ObjectOverrule<T> where T : DBObject
   {
      // This handler can return null to delegate to default clone:
      Func<T, DBObject, DBObject, DBObject> handler;
      // This handler can access the source and clone
      // but cannot replace the clone with something else.
      Action<T, T> action;
      ObjectId ownerId;

      /// <summary>
      /// Accepts an Action<T, T> that is passed each source object 
      /// and the clone of the source object. The action can perform 
      /// operations on both the source object and the clone of it.
      /// 
      /// The source object will be open for read, and the clone will
      /// be open for write. If the openmode of the source object is
      /// upgraded to OpenMode.ForWrite via UpgradeOpen(), it should
      /// downgraded to OpenMode.ForRead after write operations have
      /// completed, using DowngradeOpen(). This is necessary because
      /// the source object may not be transaction-resident.
      /// </summary>
      /// <param name="action">The delegate that accepts the source
      /// object and its clone of the given generic argument type.
      /// If objects that are not of the generic argument type are 
      /// also cloned, this method is not called for those objects.
      /// </param>
      /// <param name="ownerId">The ObjectId of the owner which the
      /// clones are to belong to. To clone the objects to the same
      /// owner as the source objects, pass ObjectId.Null or do not
      /// specify a value for this argument.</param>

      /// Used when objects are being cloned to the same owner.
      
      public DeepCloneOverrule(Action<T, T> action)
         : this(ObjectId.Null, action)
      {
      }

      // Used when objects are being cloned to a different owner:

      public DeepCloneOverrule(ObjectId ownerId, Action<T, T> action)
         : base(true)
      {
         Assert.IsNotNull(action, nameof(action));
         this.action = action;
         this.ownerId = ownerId;
      }

      /// <summary>
      /// This constructor is currently not used and is reserved
      /// for future enhancements.
      /// </summary>
      /// <param name="handler"></param>
      /// <param name="ownerId"></param>

      DeepCloneOverrule(Func<T, DBObject, DBObject, DBObject> handler, ObjectId ownerId = default(ObjectId))
      {
         Assert.IsNotNull(handler, nameof(handler));
         this.handler = handler;
         this.ownerId = ownerId;
         AddOverrule(rxclass, this, true);
      }

      public override DBObject DeepClone(DBObject source, DBObject owner, IdMapping idMap, bool isPrimary)
      {
         DBObject cloned = base.DeepClone(source, owner, idMap, isPrimary);
         if(isPrimary && ownerId.IsNull || owner.ObjectId == ownerId)
         {
            if(source is T target)
            {
               if(handler != null)
                  return handler(target, owner, cloned) ?? cloned;
               else if(action != null && cloned is T clone)
                  action(target, clone);
            }
         }
         return cloned;
      }
   }

   public static class DeepCloneOverruleExtensions
   {
      /// <summary>
      /// Extension methods targeting ObjectIdCollection that 
      /// perform a deep-clone of the collection elements to 
      /// the same or a different owner, and optionally give
      /// the caller a means to operate on each source object
      /// and its respective clone, within the context of the 
      /// deep clone operation.
      /// 
      /// An overload is provided that accepts an Action as an
      /// argument, which will be called and passed each source
      /// object and its respective clone. The supplied action 
      /// can operate on the source and the clone in whatever
      /// manner it chooses.
      /// 
      /// See the AddToBlockExamples class for example use of
      /// the CopyTo() extension methods.
      /// </summary>
      /// <param name="source"></param>
      /// <param name="ownerId"></param>
      /// <param name="transform"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>

      public static IdMapping Copy(this ObjectIdCollection source, Matrix3d transform = default(Matrix3d))
      {
         return CopyTo(source, ObjectId.Null, transform);
      }

      public static IdMapping Copy(this IEnumerable<ObjectId> source, Matrix3d transform = default(Matrix3d))
      {
         Assert.IsNotNull(source);
         return CopyTo(new ObjectIdCollection(source.AsArray()), ObjectId.Null, transform);
      }


      /// <summary>
      /// Clones the objects referenced by the source collection,
      /// and optionally applies a transformation to each clone.
      /// </summary>
      /// <param name="source"></param>
      /// <param name="ownerId"></param>
      /// <param name="transform"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>

      public static IdMapping CopyTo(this ObjectIdCollection source, ObjectId ownerId = default(ObjectId), Matrix3d transform = default(Matrix3d))
      {
         Assert.IsNotNull(source, "source");
         if(source.Count == 0)
            throw new ArgumentException("Empty collection");
         ObjectId first = source[0];
         AcRx.ErrorStatus.NullObjectId.ThrowIf(first.IsNull);
         if(ownerId.IsNull)
            ownerId = first.GetValue<ObjectId>(obj => obj.OwnerId);
         else
            AcRx.ErrorStatus.WrongDatabase.ThrowIf(first.Database != ownerId.Database);
         IdMapping result = new IdMapping();
         DeepCloneOverrule<Entity> overrule = null;
         if(transform != default(Matrix3d))
            overrule = new DeepCloneOverrule<Entity>((s, e) => e.TransformBy(transform));
         using(overrule)
         {
            ownerId.Database.DeepCloneObjects(source, ownerId, result, false);
         }
         return result;
      }

      public static IdMapping CopyTo(this IEnumerable<ObjectId> source, ObjectId ownerId = default(ObjectId), Matrix3d transform = default(Matrix3d))
      {
         Assert.IsNotNull(source, "source");
         return CopyTo(new ObjectIdCollection(source.AsArray()), ownerId, transform);
      }

      /// <summary>
      /// Deep-clones the objects referenced by the given ObjectIdCollection
      /// to the specified owner, or to the same owner as the source objects,
      /// and invokes the action on each pair of source and clone objects as
      /// each source object is cloned.
      /// </summary>
      /// <typeparam name="T">The type of DBObject to apply the action to.
      /// Any clones that are not instances of this type do not have the
      /// action applied to them.</typeparam>
      /// <param name="source">The ObjectIdCollection that references the
      /// objects to be cloned.</param>
      /// <param name="ownerId">The ObjectId of the new owner</param>
      /// <param name="action">A delegate that takes two instances of the
      /// generic argument. The first argument is the source object and the
      /// second argument is the clone of the source object. The source
      /// object is open for read, and the clone is open for write.</param>
      /// <returns>An IdMapping representing the result of the operation.</returns>
      /// <exception cref="ArgumentException"></exception>

      public static IdMapping CopyTo<T>(this ObjectIdCollection source, ObjectId ownerId, Action<T, T> action)
         where T : DBObject
      {
         Assert.IsNotNull(source, "source");
         Assert.IsNotNull(action, nameof(action));
         if(source.Count == 0)
            return new IdMapping();
         ObjectId first = source[0];
         if(ownerId.IsNull)
            ownerId = first.GetValue<ObjectId>(obj => obj.OwnerId);
         else
            AcRx.ErrorStatus.WrongDatabase.ThrowIf(first.Database != ownerId.Database);
         IdMapping result = new IdMapping();
         using(new DeepCloneOverrule<T>(ownerId, action))
         {
            ownerId.Database.DeepCloneObjects(source, ownerId, result, false);
         }
         return result;
      }

      public static IdMapping CopyTo<T>(this IEnumerable<ObjectId> source, ObjectId ownerId, Action<T, T> action)
         where T : DBObject
      {
         Assert.IsNotNull(source, nameof(source));
         return CopyTo<T>(new ObjectIdCollection(source.AsArray()), ownerId, action);
      }
   }
}
