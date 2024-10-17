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

using System;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class ObservableDeepCloneExtensions
   {
      /// <summary>
      /// An extension method that overloads the Database's
      /// DeepCloneObjects() method, adding support for operating
      /// on each source/clone pair as each clone is created and
      /// added to its owner.
      /// 
      /// Using this method one can completely avoid additional
      /// post-processing steps that involve iterating over the 
      /// IdMapping and opening each clone to perform additional 
      /// operations on them.
      /// 
      /// This method can be far more efficient than the native
      /// DeepCloneObjects() method, mainly because the delegate 
      /// passed to this method is called while the source and its
      /// clone are still open within the deep-clone operation.
      /// 
      /// Included below is a basic example, the MyCopyCommand() 
      /// method. As can be seen in the example, there's no need 
      /// to start a transaction; iterate over an IdMapping; or
      /// open each clone to transform it. The delegate passed to
      /// the DeepCloneObjects<T>() method does that, effectively-
      /// reducing the task of transforming the clones to a single 
      /// line of code.
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
      /// object which the clones are to be added to.</param>
      /// <param name="primaryOnly">A value indicating if the action
      /// should be invoked on all source/clone pairs, or only those
      /// that are primary source/clone pairs.</param>
      /// <param name="action">A delegate that takes two instances of
      /// the generic argument type, the first being the source object
      /// and the second being the clone of it. The source object is 
      /// open for read, and the clone is open for write.</param>
      /// <returns>An IdMapping instance representing the result of 
      /// the operation</returns>

      public static IdMapping DeepCloneObjects<T>(this Database db, 
         ObjectIdCollection ids, 
         ObjectId ownerId,
         bool primaryOnly, 
         Action<T, T> action) where T : DBObject
      {
         if(db == null)
            throw new ArgumentNullException(nameof(db));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         IdMapping map = new IdMapping();
         using(new Overrule<T>(ownerId, primaryOnly, action))
         {
            db.DeepCloneObjects(ids, ownerId, map, false);
         }
         return map;
      }

      /// <summary>
      /// An overloaded version of the above that deep clones 
      /// entities to the same owner space:
      /// </summary>

      public static IdMapping DeepCloneObjects<T>(this Database db,
         ObjectIdCollection ids,
         Action<T, T> action) where T : DBObject
      {
         if(db == null)
            throw new ArgumentNullException(nameof(db));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         IdMapping map = new IdMapping();
         if(ids.Count == 0)
            return map;
         ObjectId ownerId = GetOwnerId(ids[0]);
         if(ownerId.Database != db)
            throw new AcRx.Exception(AcRx.ErrorStatus.WrongDatabase);
         using(new Overrule<T>(ownerId, true, action))
         {
            db.DeepCloneObjects(ids, ownerId, map, false);
         }
         return map;
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

      public static IdMapping Copy(this Database db,
         ObjectIdCollection ids,
         Matrix3d xform) 
      {
         return DeepCloneObjects<Entity>(db, ids, (s, c) => c.TransformBy(xform));
      }


      /// <summary>
      /// A Copy() extension method that targets ObjectIdCollection.
      /// </summary>
      /// <typeparam name="T">The type of the DBObject that is
      /// passed to the action. Only instances of the this argument
      /// type are passed to the action. All other types of objects
      /// that are also cloned are not passed to the action.</typeparam>
      /// <param name="ids">The ObjectIdCollection containing the 
      /// objects to be cloned. All objects must have the same owner.</param>
      /// <param name="ownerId">The ObjectId of the destination owner
      /// object which the clones are to be added to or ObjectId.Null
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

      public static IdMapping Copy<T>(this ObjectIdCollection ids,
         ObjectId ownerId,
         bool primaryOnly,
         Action<T, T> action) where T : DBObject
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         IdMapping map = new IdMapping();
         if(ids.Count == 0)
            return map;
         if(ownerId.IsNull)
            ownerId = GetOwnerId(ids[0]);
         if(ownerId.Database != ids[0].Database)
            throw new AcRx.Exception(AcRx.ErrorStatus.WrongDatabase);
         Database db = ownerId.Database;
         if(db == null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NoDatabase);
         using(new Overrule<T>(ownerId, primaryOnly, action))
         {
            db.DeepCloneObjects(ids, ownerId, map, false);
         }
         return map;
      }

      /// <summary>
      /// An extension method targeting ObjectIdCollection, 
      /// that simplifies copying entities to the same owner 
      /// space, and transforming the copies.
      /// </summary>
      /// <param name="ids">An ObjectIdCollection containing the
      /// Ids of the entities to be copied. All elements must be 
      /// references to Entity or a derived type, and must have
      /// the same owner.</param>
      /// <param name="xform">The transformation matrix to be 
      /// applied to the clones of the input entities</param>
      /// <returns>An IdMapping representing the result of the
      /// operation</returns>

      public static IdMapping Copy(this ObjectIdCollection ids, Matrix3d xform)
      {
         return Copy<Entity>(ids, ObjectId.Null, true, 
            (source, clone) => clone.TransformBy(xform));
      }

      static ObjectId GetOwnerId(ObjectId id)
      {
         using(var tr = new OpenCloseTransaction())
            return tr.GetObject(id, OpenMode.ForRead).OwnerId;
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
      /// the clones can be done with a single line of code.
      /// </summary>

      [CommandMethod("MYCOPY")]
      public static void MyCopyCommand()
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
         var ppo = new PromptPointOptions("\nBase point: ");
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
         var ids = new ObjectIdCollection(psr.Value.GetObjectIds());
         Matrix3d xform = Matrix3d.Displacement(from.GetVectorTo(ppr.Value));
         db.DeepCloneObjects<Entity>(ids, 
            (source, clone) => clone.TransformBy(xform));
      }
   }
}
