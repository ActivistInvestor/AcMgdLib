
/// DeepExplodeVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example showing the use of the EntityVisitor class.

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// DeepExplodeVisitor<T>
   /// 
   /// An example specialization of the EntityVisitor class.
   /// 
   /// This example recusively explodes all entities of the 
   /// generic argument type, that are nested within a given
   /// BlockReference. The entities that are exploded include
   /// entities nested in other block references inserted into
   /// the block reference that is operated on, to any depth.
   /// 
   /// This class uses DeepCloneOverrule and various other
   /// classes and extension methods from this library to 
   /// simplify the operation.
   /// 
   /// The base type used by this specialization has two 
   /// generic argument types. The first generic argument 
   /// specifies the type of Entity that is to be visited. 
   /// Only instances of the generic argument type or of
   /// derived types are visited.
   /// 
   /// The second generic argument specifies the type of
   /// data that is associated with each visited block
   /// reference which in this type, is ObjecteIdCollection. 
   /// 
   /// The GetContext() method must be overridden to create 
   /// an instance for each Block Reference that is visited. 
   /// This class stores the ObjectIds of the block entities 
   /// that it clones in the ObjectIdCollection.
   /// 
   /// Because the objects are exploded using deep-cloning,
   /// only objects from a single owner can be deep-cloned
   /// within a single deep-clone operation. Additionally,
   /// because the transformation that must be applied to 
   /// each object depends on what block reference(s) it is 
   /// nested in, a deep-clone and transformation operations
   /// must be performed for each visited block reference, 
   /// involving only the entities contained in each block 
   /// reference's definition.
   /// 
   /// Special Notes:
   /// 
   /// This derived type cannot have its Visit(ObjectId)
   /// method called with the nested argument set to
   /// false if the Id is the Id of a BlockTableRecord, 
   /// because it requires a BlockReference to be on 
   /// the stack in every case. 
   /// </summary>

   public class DeepExplodeVisitor<T> : EntityVisitor<T, ObjectIdCollection>
      where T : Entity 
   {
      /// The collection that holds the ObjectIds of
      /// all resulting cloned entities.
      
      ObjectIdCollection result = new ObjectIdCollection();
      
      int nonUniformlyScaledCount = 0;

      /// <summary>
      /// The count of entities that were not exploded
      /// or deep-cloned because the current compound
      /// block transformation was non-uniformly scaled.
      /// </summary>
      
      public int NonUniformlyScaledCount => nonUniformlyScaledCount;
      
      /// <summary>
      /// The Visit(T) method is called and passed
      /// every entity that's visited, which is all
      /// entities of the specified generic argument
      /// nested within the top-level block insertion,
      /// regardless of how deeply-nested they are.
      /// 
      /// When this method is called, the Containers
      /// property of this class holds the collection
      /// of BlockReferences that contain the visited
      /// Entity.
      /// 
      /// The contents of the Containers collection is 
      /// functionally-similar to the value returned by 
      /// the PromptNestedEntityResult's GetContainers()
      /// method, except that the Containers property of 
      /// this class holds open BlockReferences, rather 
      /// than ObjectIds.
      /// 
      /// The Transform property of the EntityVisitor
      /// is functionally-identical to the same-named
      /// property of the PromptNestedEntityResult. 
      /// 
      /// The IsUniformlyScaled property indicates if
      /// the matrix returned by the Transform property
      /// is unifomly-scaled and therefore can be used
      /// to transform entities contained in the block 
      /// reference currently being visited.
      /// 
      /// The DeepExplodeVisitor will not transform or
      /// clone entities through a non-uniformly-scaled
      /// transformation.
      /// 
      /// The Current property represents the instance
      /// of the second generic argument type (which is
      /// ObjectIdCollection in this class) associated 
      /// with the block reference whose entities are 
      /// currently being visited. 
      /// 
      /// There's a distinct ObjectIdCollection for each 
      /// visited block reference, which are internally 
      /// stored in a Stack<T>, where the instance at the 
      /// top of the stack is the instance associated with 
      /// the block reference whose entities are currently 
      /// being visited. The EntityVisitor class manages 
      /// instances of the generic argument for derived
      /// types, which only must override the GetContext()
      /// method to create instances when they're needed,
      /// and access them via the Current property.
      /// </summary>

      public virtual void Visit(T entity)
      {
         TryAdd(entity);
      }

      /// <summary>
      /// Adds the entity to the ObjectIdCollection if the
      /// current transformation is uniformly-scaled:
      /// </summary>

      bool TryAdd(T entity)
      {
         if(IsUniformlyScaled)
         {
            Current.Add(entity.ObjectId);
            return true;
         }
         else
         {
            ++nonUniformlyScaledCount;
            return false;
         }
      }

      /// <summary>
      /// This method is overridden to create instances of 
      /// the second generic argument, which in the case of
      /// this class, is ObjectIdCollection. There will be
      /// one instance of an ObjectIdCollection for each
      /// BlockReference that is visited, allowing this class
      /// to associate client data with those block references.
      /// </summary>
      /// <param name="blockref"></param>
      /// <returns></returns>
      
      protected override ObjectIdCollection GetContext(BlockReference blockref)
      {
         return new ObjectIdCollection();
      }

      /// <summary>
      /// Overridden to ensure that the nested argument is true,
      /// because it cannot be false if the id argument is the
      /// ObjectId of a BlockTableRecord.
      /// </summary>

      protected override bool NestedOnly => true;

      /// <summary>
      /// The OnVisited() virtual method is called after all
      /// entities in a block reference have been visited.
      /// In this override, after all entities have been 
      /// visited and collected, it clones them to the owner 
      /// space of the outer-most container block reference
      /// and transforms them accordingly.
      /// 
      /// Note: this method uses other APIs from this library,
      /// namely the CopyTo() extension method, which in-turn
      /// uses the DeepCloneOverrule class that allows the code
      /// in this method to avoid having to open the resulting 
      /// clones to transform them to the destination space.
      /// </summary>

      protected override void OnVisited(BlockReference blkref)
      {
         if(HasContext && Current.Count > 0 && !BlockId.IsNull)
         {
            var xform = Transform;
            Current.CopyTo(BlockId,
               delegate(T source, T clone) 
               {
                  clone.TransformBy(xform);
                  result.Add(clone.ObjectId);
               }
            );
         }
         base.OnVisited(blkref);
      }

      /// <summary>
      /// The ObjectId collection that holds the ids of the
      /// clones produced by deep-cloning the entitys to the 
      /// new owner block. 
      /// 
      /// This property is not fully-populated until the 
      /// entire operation has completed.
      /// </summary>

      public ObjectIdCollection Result => result;
   }

}
