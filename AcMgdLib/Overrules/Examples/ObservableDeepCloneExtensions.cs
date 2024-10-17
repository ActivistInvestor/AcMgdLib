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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class ObservableDeepCloneExtensions
   {
      /// <summary>
      /// An extension method that complements the Database's
      /// DeepCloneObjects() method, adding support for operating
      /// on each source/clone pair at the point when each clone 
      /// is created and added to its owner, while it is open for
      /// write and able to be modified.
      /// 
      /// Using this method, one can completely avoid additional
      /// post-processing steps that typically involve iterating 
      /// over the IdMapping and opening each clone to perform 
      /// additional operations on them. 
      /// 
      /// That allows this method to be far more efficient than 
      /// using the stock DeepCloneObjects() method followed by a 
      /// post-processing loop that operates on all of the clones. 
      /// 
      /// The performance advantage of this method largely comes
      /// from the fact that the delegate passed to this method 
      /// is called while the source and its clone are still open 
      /// within the deep-clone operation. 
      /// 
      /// In addition to being able to operate on the clone, the
      /// delegate passed to this method can also operate on the 
      /// source object by upgrading it to OpenMode.ForWrite.
      /// 
      /// Included MyCopy() basic example method is included below.
      /// As can be seen in the example, after the entities have been 
      /// cloned, there's no need to start a transaction; iterate 
      /// over an IdMapping and open each clone to transform it. 
      /// The delegate used by the CopyObjects() method does that, 
      /// effectively-reducing the task of transforming the clones 
      /// to a single line of code.
      ///   
      /// </summary>
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
      /// <param name="primaryOnly">A value indicating if the action
      /// should be invoked on all source/clone pairs, or only primary 
      /// source/clone pairs.</param>
      /// <param name="action">A delegate that takes two instances of
      /// the generic argument type, the first being the source object
      /// and the second being the clone of it. The source object is 
      /// open for read, and the clone is open for write.</param>
      /// <returns>An IdMapping instance representing the result of 
      /// the operation</returns>

      public static IdMapping CopyObjects<T>(this Database db, 
         ObjectIdCollection ids, 
         ObjectId ownerId,
         bool primaryOnly, 
         Action<T, T> action) where T : DBObject
      {
         if(db == null)
            throw new ArgumentNullException(nameof(db));
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         if(ids.Count == 0)
            return new IdMapping();
         if(ownerId.IsNull)
            ownerId = GetOwnerId(ids[0]);
         if(db != ids[0].Database || db != ownerId.Database)
            throw new AcRx.Exception(AcRx.ErrorStatus.WrongDatabase);
         return DeepClone(db, ids, ownerId, primaryOnly, action);
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
         AssertAreAllEntities(ids);
         if(db == null)
            throw new ArgumentNullException(nameof(db));
         if(ids.Count == 0)
            return new IdMapping();
         ObjectId ownerId = GetOwnerId(ids[0]);
         if(ownerId.Database != db)
            throw new AcRx.Exception(AcRx.ErrorStatus.WrongDatabase);
         return DeepClone<T>(db, ids, ownerId, true, action);
      }

      /// <summary>
      /// A Copy() extension method that operates only on entities, 
      /// and implicitly transforms the clones, using the supplied
      /// transformation matrix.
      /// </summary>
      /// <param name="ids">The ObjectIdCollection containing
      /// the ObjectIds of the entities to be copied. All elements
      /// must have the same owner.</param>
      /// <param name="xform">The Matrix3d describing the
      /// transformation to be applied to each cloned entity</param>

      public static IdMapping CopyObjects(this Database db,
         ObjectIdCollection ids,
         Matrix3d xform) 
      {
         Action<Entity, Entity> action = null;
         if(!xform.IsEqualTo(Matrix3d.Identity))
            action = (source, clone) => clone.TransformBy(xform);
         return CopyObjects<Entity>(db, ids, action);
      }


      /// <summary>
      /// An extension method that targets ObjectIdCollection.
      /// </summary>
      /// <typeparam name="T">The type of the DBObject that is
      /// passed to the action. Only instances of the this argument
      /// type are passed to the action. All other types of objects
      /// that are also cloned are not passed to the action.</typeparam>
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
         bool primaryOnly,
         Action<T, T> action) where T : DBObject
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         if(ids.Count == 0)
            return new IdMapping();
         if(ownerId.IsNull)
            ownerId = GetOwnerId(ids[0]);
         Database db = ownerId.Database;
         if(db == null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NoDatabase);
         if(db != ids[0].Database)
            throw new AcRx.Exception(AcRx.ErrorStatus.WrongDatabase);
         return DeepClone<T>(db, ids, ownerId, primaryOnly, action);
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
         AssertAreAllEntities(ids);
         Action<Entity, Entity> action = null;

         /// If the caller passed the Identity matrix, there's
         /// no transformation, and no need for the Overrule,
         /// which is not used if we pass a null action:

         if(!xform.IsEqualTo(Matrix3d.Identity))
            action = (source, clone) => clone.TransformBy(xform);
         
         return CopyObjects<Entity>(ids, ObjectId.Null, true, action);
      }

      /// <summary>
      /// An overload of the above method that targets IEnumerable<ObjectId>.
      /// </summary>
      
      public static IdMapping CopyObjects(this IEnumerable<ObjectId> ids, Matrix3d xform)
      {
         return CopyObjects(new ObjectIdCollection(
            ids as ObjectId[] ?? ids.ToArray()), xform);
      }

      /// <summary>
      /// Worker for all CopyObjects() overloads:
      /// </summary>

      static IdMapping DeepClone<T>(Database db, ObjectIdCollection ids, ObjectId ownerId, bool primaryOnly, Action<T, T> action) where T : DBObject
      {
         IdMapping map = new IdMapping();
         Overrule<T> overrule = null;
         if(action != null)
            overrule = new Overrule<T>(ownerId, primaryOnly, action);
         using(overrule)
         {
            db.DeepCloneObjects(ids, ownerId, map, false);
         }
         return map;
      }

      static ObjectId GetOwnerId(ObjectId id)
      {
         using(DBObject obj = id.Open(OpenMode.ForRead))
         {
            return obj is Entity ent ? ent.BlockId : obj.OwnerId;
         }
      }

      static readonly RXClass entityClass = RXObject.GetClass(typeof(Entity));

      //static Cache<RXClass, bool> entityClasses
      //   = new Cache<RXClass, bool>(c => c.IsDerivedFrom(entityClass));

      static void AssertAreAllEntities(this ObjectIdCollection ids)
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         for(int i = 0; i < ids.Count; i++)
         {
            ObjectId id = ids[i];
            if(id.IsNull)
               throw new AcRx.Exception(AcRx.ErrorStatus.NullObjectId);
            //if(!entityClasses[id.ObjectClass])
            //   throw new AcRx.Exception(AcRx.ErrorStatus.NotAnEntity);
            if(!id.ObjectClass.IsDerivedFrom(entityClass))
               throw new AcRx.Exception(AcRx.ErrorStatus.NotAnEntity);
         }
      }

      class Overrule<T> : ObjectOverrule where T : DBObject
      {
         static readonly RXClass targetClass = RXObject.GetClass(typeof(T));
         static readonly Func<DBObject, ObjectId, bool> IsOwnedBy;
         Action<T, T> OnCloned;
         ObjectId ownerId;
         bool primaryOnly = true;
         bool disposed = false;

         static Overrule()
         {
            if(typeof(Entity).IsAssignableFrom(typeof(T)))
               IsOwnedBy = (obj, ownerId) => Unsafe.As<Entity>(obj).BlockId == ownerId;
            else
               IsOwnedBy = (obj, ownerId) => obj.OwnerId == ownerId;
         }

         public Overrule(ObjectId ownerId, bool primaryOnly, Action<T, T> onCloned)
         {
            this.OnCloned = onCloned;
            this.ownerId = ownerId;
            AddOverrule(targetClass, this, true);
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

   public static class ObservableDeepCloneExtensionsExample
   {
      /// <summary>
      /// Example:
      /// 
      /// A rudimentry emulation of the COPY command, 
      /// sans dragging support.
      /// 
      /// With the help of the included extension methods, the
      /// operation of cloning the selection and transforming 
      /// the clones is done in a single line of code.
      /// </summary>

      [CommandMethod("MYCOPY")]
      public static void MyCopy()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Database db = doc.Database;
         Editor ed = doc.Editor;
         var pso = new PromptSelectionOptions();
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = ed.GetSelection();
         if(psr.Status != PromptStatus.OK)
            return;
         var ppo = new PromptPointOptions("\nBasepoint: ");
         var ppr = ed.GetPoint(ppo);
         if(ppr.Status != PromptStatus.OK)
            return;
         ppo.Message = "\nDisplacment: ";
         Point3d from = ppr.Value;
         ppo.BasePoint = from;
         ppo.UseBasePoint = true;
         ppr = ed.GetPoint(ppo);
         if(ppr.Status != PromptStatus.OK)
            return;
         psr.Value.GetObjectIds().CopyObjects(
            Matrix3d.Displacement(from.GetVectorTo(ppr.Value)));
      }
   }
}
