/// EntityExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Entity class.

using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using System.Diagnostics.Extensions;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   /// <summary>
   /// This class is merely an example showing how to write 
   /// 'Linq-friendly' extension methods to perform common 
   /// operations on multiple entities that are enumerated 
   /// by IEnumerable<Entity>.
   /// </summary>

   public static partial class EntityExtensions
   {
      /// <summary>
      /// Erases all entities in the sequence. 
      /// They must be open for write. 
      /// 
      /// Use the UpgradeOpen() extension method
      /// to to ensure that entities are open for
      /// write, such as:
      /// <code>
      /// 
      ///    myEntities.UpgradeOpen().Erase();
      ///    
      /// </code>
      /// 
      /// </summary>

      public static int Erase(this IEnumerable<DBObject> objects, bool erase = true)
      {
         Assert.IsNotNull(objects, nameof(objects));
         int count = 0;
         foreach(DBObject obj in objects)
         {
            obj.Erase(erase);
            count++;
         }
         return count;
      }

      /// <summary>
      /// Explodes each block reference in the input sequence
      /// to its owner space, and optionally collects and returns 
      /// the ObjectIds of all objects created by the operation.
      /// 
      /// Notes: eNotApplicable or eCannotExplodeEntity is not
      /// handled by this code. Callers should ensure that all
      /// input block references can be exploded (e.g., they're
      /// uniformly-scaled, etc.).
      /// </summary>
      /// <param name="blockrefs">A sequence of BlockReferences</param>
      /// <param name="erase">A value indicating if the source
      /// objects are to be erased.</param>
      /// <param name="collect">A value indicating if newly-
      /// created objects resulting from exploding the source
      /// collection are to be collected and returned.</param>
      /// <returns>A DBObjectCollection containing the ObjectIds
      /// of all objects created by the operation</returns>

      public static ObjectIdCollection ExplodeToOwnerSpace(
         this IEnumerable<BlockReference> blockrefs, 
         out int count,
         bool erase = false,
         bool collect = false)
      {
         Assert.IsNotNull(blockrefs, nameof(blockrefs));
         Database db = null;
         ObjectIdCollection ids = new ObjectIdCollection();
         count = 0;
         if(blockrefs.Any())
         {
            if(collect)
            {
               db = blockrefs.TryGetDatabase(true);
               db.ObjectAppended += objectAppended;
            }
            try
            {
               int cnt = 0;
               foreach(BlockReference br in blockrefs)
               {
                  br.ExplodeToOwnerSpace();
                  ++cnt;
                  if(erase && br.IsWriteEnabled)
                     br.Erase(true);
               }
               count = cnt;
            }
            finally
            {
               if(collect)
               {
                  db.ObjectAppended -= objectAppended;
               }
            }
         }
         return ids;

         void objectAppended(object sender, ObjectEventArgs e)
         {
            if(e.DBObject is Entity ent)
               ids.Add(ent.ObjectId);
         }
      }

      /// <summary>
      /// An overload of ExplodeToOwnerSpace() that doesn't
      /// report the number of objects exploded.
      /// </summary>

      public static ObjectIdCollection ExplodeToOwnerSpace(
         this IEnumerable<BlockReference> entities,
         bool erase = false,
         bool collect = false)
      {
         int count = 0;
         return ExplodeToOwnerSpace(entities, out count, erase, collect);
      }


      /// <summary>
      /// Explode all entities in the sequence and collect
      /// the ObjectIds of the resulting objects.
      /// </summary>
      /// <param name="entities"></param>
      /// <param name="erase">A value indicating if the
      /// object to be exploded should be erased</param>
      /// <returns>A DBObjectCollection containing the
      /// entities produced by exploding the input</returns>

      public static DBObjectCollection Explode(this IEnumerable<Entity> entities, bool erase = false)
      {
         DBObjectCollection result = new DBObjectCollection();
         Explode(entities, result, erase);
         return result;
      }

      /// <summary>
      /// Explodes all entities in the sequence and adds
      /// the resulting objects to the DBObjectCollection
      /// argument.
      /// 
      /// This method uses deferred execution, and must
      /// have its result enumerated in order to perform
      /// the operation on the input sequence.
      /// 
      /// The resulting sequence of integers represents
      /// the indices of the first entity added to the
      /// DBObjectCollection argument for each entity
      /// that was exploded. The difference between each
      /// element and the one following it is the number
      /// of entities produced by exploding each entity
      /// in the input sequence.
      /// 
      /// This method uses deferred execution, and the
      /// results must be enumerated to perform the
      /// operation.
      /// </summary>
      /// <param name="entities">The input sequence of
      /// entities to be exploded.</param>
      /// <param name="output">A DBObjectCollection to
      /// which all objects created by exploding the input
      /// sequence are added.</param>
      /// <param name="erase">A value indicating if the
      /// entities to be exploded should be erased. The
      /// entities are erased only if they are currently 
      /// open for write.</param>
      /// <returns>A sequence of integers, where each 
      /// element represents the index of the first 
      /// entity in the DBObjectCollection argument
      /// that was produced by exploding an entity from
      /// the input sequence.</returns>

      public static IEnumerable<int> Explode(this IEnumerable<Entity> entities, 
         DBObjectCollection output, bool erase = false)
      {
         Assert.IsNotNull(entities, nameof(entities));
         Assert.IsNotNull(output, nameof(output));
         foreach(var entity in entities)
         {
            int index = output.Count;
            entity.Explode(output);
            if(erase && entity.IsWriteEnabled)
               entity.Erase(true);
            yield return index;
         }
      }

      /// <summary>
      /// Attempts to explode all entities in the sequence and 
      /// adds the resulting objects to the DBObjectCollection
      /// passed as the output argument.
      /// 
      /// </summary>
      /// <param name="entities">The input sequence of
      /// entities to be exploded.</param>
      /// <param name="output">A DBObjectCollection to
      /// which all objects created by exploding the input
      /// sequence are added.</param>
      /// <param name="erase">A value indicating if the
      /// entities to be exploded should be erased. The
      /// entities are erased only if they are currently 
      /// open for write and were successfully exploded.</param>
      /// <returns>A sequence of integers, where each 
      /// element represents the number of objects that
      /// were created by exploding each entity in the 
      /// source sequence</returns>

      /// TODO: To be revised.
      /// 
      /// The revised version will enumerate the DBObjectCollection 
      /// index of the first entity produced by exploding each input 
      /// entity:
      /// 
      /// The functional spec requires that the index of the first
      /// entity produced by exploding an input entity in the ouput
      /// DBObjectCollection be identified, allowing the caller to
      /// correlate each input entity with the set of entities that
      /// were prodoced by exploding it.
      
      public static List<int> TryExplode(this IEnumerable<Entity> entities,
         DBObjectCollection output, bool erase = true)
      {
         Assert.IsNotNull(entities, nameof(entities));
         Assert.IsNotNull(output, nameof(output));
         List<int> list = new List<int>();
         foreach(var entity in entities)
         {
            try
            {
               int start = output.Count;
               entity.Explode(output);
               if(output.Count > start)
               {
                  list.Add(output.Count - start);
                  if(erase && entity.IsWriteEnabled)
                     entity.Erase(true);
               }
            }
            catch(Autodesk.AutoCAD.Runtime.Exception ex)
            {
               if(ex.ErrorStatus != AcRx.ErrorStatus.NotApplicable)
                  throw ex;
               list.Add(0);
               continue;
            }
         }
         return list;
      }


      /// <summary>
      /// Get the geometric extents of a sequence 
      /// of entities:
      /// </summary>
      /// <param name="entities"></param>
      /// <returns></returns>

      public static Extents3d GeometricExtents(this IEnumerable<Entity> entities)
      {
         Assert.IsNotNull(entities, nameof(entities));
         if(entities.Any())
         {
            Extents3d extents = entities.First().GeometricExtents;
            foreach(var entity in entities.Skip(1))
               extents.AddExtents(entity.GeometricExtents);
            return extents;
         }
         return new Extents3d();
      }
   }
}
