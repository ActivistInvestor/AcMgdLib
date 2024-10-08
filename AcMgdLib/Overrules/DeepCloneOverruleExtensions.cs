﻿/// DeepCloneOverruleExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods supporting deep clone/wblock clone
/// operations that are observed by the DeepCloneOverrule
/// class.

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Utility;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class DeepCloneOverruleExtensions
   {
      /// <summary>
      /// Extension methods targeting ObjectIdCollection and
      /// IEnumerable<ObjectId>, that perform a deep clone or
      /// Wblock clone of the collection elements to the same 
      /// or a different owner, and optionally give the caller 
      /// the means to operate on each Source object and its 
      /// respective clone, within the context of the clone 
      /// operation.
      /// 
      /// An overload is provided that accepts an Action as an
      /// argument, which will be called and passed each Source
      /// object and its respective clone. The supplied action 
      /// can operate on the Source and the clone in whatever
      /// manner it chooses.
      /// 
      /// Additional overloads accept a transformation matrix 
      /// argument, and will transform the clones by the given
      /// matrix.
      /// 
      /// See the AddToBlockExamples class for example use of
      /// the Copy() and CopyTo() extension methods.
      /// </summary>
      /// <param name="source"></param>
      /// <param name="ownerId"></param>
      /// <param name="transform"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>

      /// Copies the Source objects to the same owner space
      /// and applys the specified transformation to the clones:
      
      public static IdMapping Copy(this ObjectIdCollection source, Matrix3d transform = default(Matrix3d))
      {
         return CopyTo(source, ObjectId.Null, transform);
      }

      /// <summary>
      /// Overload of the above Copy() method that accepts an 
      /// IEnumerable<ObjectId> in lieu of an ObjectIdCollection:
      /// </summary>
      /// <param name="source"></param>
      /// <param name="transform"></param>
      /// <returns></returns>
      
      public static IdMapping Copy(this IEnumerable<ObjectId> source, Matrix3d transform = default(Matrix3d))
      {
         Assert.IsNotNull(source, nameof(source));
         return CopyTo(new ObjectIdCollection(source.AsArray()), ObjectId.Null, transform);
      }


      /// <summary>
      /// Clones the objects referenced by the Source collection
      /// to the owner having the given ownerId, and optionally 
      /// applys a transformation to each clone.
      /// </summary>
      /// <param name="source">The Source objects to be copied</param>
      /// <param name="ownerId">The ObjectId of the new owner 
      /// object which the Source objects are to be copied to.
      /// If this value is ObjectId.Null, the Source objects are 
      /// copied to their current owner.</param>
      /// <param name="transform">An optional transformation matrix
      /// to apply to the copied objects.</param>
      /// <returns>An IdMapping representing the result of the
      /// clone operation.</returns>
      /// <exception cref="ArgumentException"></exception>

      public static IdMapping CopyTo(this ObjectIdCollection source, 
         ObjectId ownerId, 
         Matrix3d transform = default(Matrix3d))
      {
         Action<Entity, Entity> action = null;
         if(!transform.IsEqualTo(default(Matrix3d)))
            action = (src, clone) => clone.TransformBy(transform);
         return CopyTo<Entity>(source, ownerId, action);
      }

      /// <summary>
      /// Copies the selection to the specified owner, and invokes
      /// the specified delegate on each Source/clone pair. These 
      /// methods are intended to be used to clone entities, hence
      /// the delegate must take Entity arguments.
      /// </summary>
      /// <param name="source">An ObjectIdCollection or an
      /// IEnumerable<ObjectId> containing the ObjectIds of
      /// the objects to be copied.</param>
      /// <param name="ownerId">The ObjectId of the new owner
      /// of the copies.</param>
      /// <param name="action">A delegate that takes two Entity
      /// arguments, the first being the Source entity that was
      /// cloned, and the second being the clone of the Source
      /// entity.</param>
      /// <returns></returns>
      
      public static IdMapping CopyTo(this ObjectIdCollection source,
         ObjectId ownerId,
         Action<Entity, Entity> action)
      {
         return CopyTo<Entity>(source, ownerId, action);
      }

      public static IdMapping CopyTo(this IEnumerable<ObjectId> source,
         ObjectId ownerId,
         Action<Entity, Entity> action)
      {
         return CopyTo<Entity>(source, ownerId, action);
      }

      /// <summary>
      /// Overload of the above method taking an IEnumerable<ObjectId> 
      /// in lieu of an ObjectIdCollection.
      /// </summary>
      /// <param name="source"></param>
      /// <param name="ownerId"></param>
      /// <param name="transform"></param>
      /// <returns></returns>

      public static IdMapping CopyTo(this IEnumerable<ObjectId> source, 
         ObjectId ownerId, 
         Matrix3d transform = default(Matrix3d))
      {
         Assert.IsNotNull(source, nameof(source));
         return CopyTo(new ObjectIdCollection(source.AsArray()), ownerId, transform);
      }

      /// <summary>
      /// All of the above Copy()/CopyTo() overloads delegate to this 
      /// core method.
      /// 
      /// Deep-clones or wblock-clones the objects referenced by the given 
      /// ObjectIdCollection to the specified owner, or to their current 
      /// owner, and invokes the supplied action on each pair of Source and 
      /// clone objects as each Source object is cloned.
      /// </summary>
      /// <typeparam name="T">The type of DBObject to apply the action to.
      /// Any Source/clone pairs that are not instances of this type will 
      /// not have the action applied to them.</typeparam>
      /// <param name="source">The ObjectIdCollection that references the
      /// objects to be cloned. DeepExplode DBObjects referenced by the elements 
      /// must have the same owner.</param>
      /// <param name="ownerId">The ObjectId of the DBObject which will be
      /// the owner of the clones. If this argument is ObjectId.Null, the 
      /// clones will have the same owner as the Source objects. If this
      /// argument is the ObjectId of an object in a Database other than 
      /// the one containing the Source objects, a Wblock clone operation 
      /// is performed using DuplicateRecordCloning.Ignore</param>
      /// <param name="drc">The DuplicateRecordCloning to use when the 
      /// destination is a different Database</param>
      /// <param name="action">A delegate that takes two instances of the
      /// generic argument. The first argument is the Source object and the
      /// second argument is the clone of the Source object. The Source
      /// object is open for read, and the clone is open for write.</param>
      /// <returns>An IdMapping representing the result of the operation.</returns>
      /// <exception cref="ArgumentException"></exception>

      public static IdMapping CopyTo<T>(this ObjectIdCollection source,
            ObjectId ownerId,
            Action<T, T> action = null)
         where T : DBObject
      {
         return CopyTo<T>(source, ownerId, DuplicateRecordCloning.Ignore, action);
      }

      /// <summary>
      /// The core API that most of the other apis in this class
      /// delegate to. This API is typically not called from the
      /// outside, but can be.
      /// </summary>
      /// <typeparam name="T">The type of the source/clone objects
      /// to be operated on by the specified action.</typeparam>
      /// <param name="source">The source ObjectIdCollection</param>
      /// <param name="ownerId">The ObjectId of the destination owner</param>
      /// <param name="drc">The DuplicateRecordCloning value that is
      /// forwarded to a call to WblockCloneObjects()</param>
      /// <param name="action">The action that is called and passed
      /// each source and its clone immediately after the point at
      /// which the clone was added to the destination owner/database.</param>
      /// <returns>An IdMapping instance representing the result of the operation</returns>

      public static IdMapping CopyTo<T>(this ObjectIdCollection source,
            ObjectId ownerId,
            DuplicateRecordCloning drc = DuplicateRecordCloning.Ignore,
            Action<T, T> action = null)
         where T : DBObject
      {
         Assert.IsNotNullOrDisposed(source, nameof(source));
         if(source.Count == 0)
            return new IdMapping();
         if(ownerId.IsNull)
            ownerId = source.TryGetOwnerId();
         AcRx.ErrorStatus.InvalidOwnerObject.ThrowIf(ownerId.IsNull);
         Database db = source[0].Database;
         bool wblock = ownerId.Database != db;
         if(wblock)
            Assert.IsNotNullOrDisposed(db, nameof(db));
         IdMapping result = new IdMapping();
         DeepCloneOverrule<T> overrule = null;
         if(action != null)
            overrule = new DeepCloneOverrule<T>(ownerId, action);
         using(overrule)
         {
            if(wblock)
               db.WblockCloneObjects(source, ownerId, result, drc, false);
            else
               db.DeepCloneObjects(source, ownerId, result, false);
         }
         return result;
      }

      /// <summary>
      /// An overload of the above method that accepts an
      /// IEnumerable<ObjectId> in lieu of an ObjectIdCollection.
      /// </summary>

      public static IdMapping CopyTo<T>(this IEnumerable<ObjectId> source,
         ObjectId ownerId,
         Action<T, T> action = null) where T : DBObject
      {
         Assert.IsNotNull(source, nameof(source));
         return CopyTo<T>(new ObjectIdCollection(source.AsArray()), ownerId, action);
      }

   }
}
