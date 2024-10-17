/// ObservableDeepCloneExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods supporting deep clone operations 
/// that can be observed by an ObjectOverrule.

using System;
using System.Diagnostics.Extensions;
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
      /// the DeepCloneObjects<T>() method does all that's needed, 
      /// effectively reducing the task of transforming the clones
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
         ObjectId ownerId = GetOwnerId(ids[0]);
         IdMapping map = new IdMapping();
         using(new Overrule<T>(ownerId, true, action))
         {
            db.DeepCloneObjects(ids, ownerId, map, false);
         }
         return map;
      }

      static ObjectId GetOwnerId(ObjectId id)
      {
         using(var tr = new OpenCloseTransaction())
            return tr.GetObject(id, OpenMode.ForRead).OwnerId;
      }

      class Overrule<T> : ObjectOverrule where T : DBObject
      {
         static readonly RXClass targetClass = RXObject.GetClass(typeof(T));
         Action<T, T> OnCloned;
         ObjectId ownerId;
         bool primaryOnly = true;
         static Func<DBObject, ObjectId> GetOwnerId;

         static Overrule()
         {
            if(typeof(Entity).IsAssignableFrom(typeof(T)))
               GetOwnerId = obj => Unsafe.As<Entity>(obj).BlockId;
            else
               GetOwnerId = obj => obj.OwnerId;
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
               && GetOwnerId(obj) == ownerId
               && obj is T source && result is T clone)
            {
               OnCloned(source, clone);
            }
            return result;
         }

         protected override void Dispose(bool disposing)
         {
            RemoveOverrule(targetClass, this);
            base.Dispose(disposing);
         }

      }

   }

   public static class ObservableDeepCloneExtensionsExample
   {
      /// <summary>
      /// Example:
      /// 
      /// Basic emulation of the COPY command, sans dragging support.
      /// </summary>

      [CommandMethod("MYCOPY")]
      public static void MyCopyCommand()
      {
         var pso = new PromptSelectionOptions();
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetSelection();
         if(psr.Status != PromptStatus.OK)
            return;
         var ppo = new PromptPointOptions("\nBase point: ");
         var ppr = ed.GetPoint(ppo);
         if(ppr.Status != PromptStatus.OK)
            return;
         ppo.Message = "\nDisplacment point: ";
         Point3d from = ppr.Value;
         ppo.BasePoint = from;
         ppo.UseBasePoint = true;
         ppr = ed.GetPoint(ppo);
         if(ppr.Status != PromptStatus.OK)
            return;
         Matrix3d xform = Matrix3d.Displacement(from.GetVectorTo(ppr.Value));
         var ids = new ObjectIdCollection(psr.Value.GetObjectIds());
         Database db = doc.Database;
         db.DeepCloneObjects<Entity>(ids, 
            (source, clone) => clone.TransformBy(xform));
      }
   }
}
