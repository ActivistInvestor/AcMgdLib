/// BlockReferenceExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Entity class.

using Autodesk.AutoCAD.Runtime;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Runtime.InteropServices;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static partial class BlockTableRecordExtensions
   {
      public static ObjectId Copy(this BlockTableRecord btr, string newName, bool cloneDrawOrder = true, Action<BlockTableRecord, BlockTableRecord, IdMapping> OnCloned = null)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         if(string.IsNullOrEmpty(newName))
            throw new ArgumentException(nameof(newName));
         AcRx.ErrorStatus.NoDatabase.ThrowIf(btr.Database == null);
         if(btr.IsAnonymous || btr.IsFromExternalReference || btr.IsLayout || btr.IsDependent)
            throw new ArgumentException("Invalid block");
         if(newName.Equals(btr.Name, StringComparison.CurrentCultureIgnoreCase))
            throw new ArgumentException("new and existing names are the same");
         Database db = btr.Database;
         string name = btr.Name;
         if(newName.Contains("{0}"))
         {
            if(newName.Trim().Equals("{0}"))
               throw new ArgumentException($"Invalid block name: {newName}");
            newName = string.Format(newName, name);
         }
         SymbolUtilityServices.ValidateSymbolName(newName, false);
         using(var trans = new OpenCloseTransaction())
         {
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            if(bt.Contains(newName))
               throw new ArgumentException($"A block with the name \"{newName}\" already exists");
            trans.Commit();
         }
         using(Database dbTemp = new Database(true, true))
         {
            ObjectIdCollection ids = new ObjectIdCollection();
            ids.Add(btr.ObjectId);
            var map = new IdMapping();
            db.WblockCloneObjects(ids, dbTemp.BlockTableId, map, DuplicateRecordCloning.Replace, false);
            if(!map[btr.ObjectId].IsCloned)
               throw new InvalidOperationException("failed to clone source BlockTableRecord");
            ObjectId cloneId = map[btr.ObjectId].Value;
            using(var trans = new OpenCloseTransaction())
            {
               var clone = (BlockTableRecord)trans.GetObject(cloneId, OpenMode.ForWrite);
               clone.Name = newName;
               if(OnCloned != null)
               {
                  using(dbTemp.AsWorkingDatabase())
                  { 
                     OnCloned(btr, clone, map);
                  }
               }
               trans.Commit();
            }
            ids.Clear();
            ids.Add(cloneId);
            IdMapping map2 = new IdMapping();
            dbTemp.WblockCloneObjects(ids, db.BlockTableId, map2, DuplicateRecordCloning.Replace, false);
            if(!(map2.Contains(cloneId) && map2[cloneId].IsCloned))
               throw new InvalidOperationException($"Failed to clone BlockTableRecord {btr.Name}.");
            ObjectId copyId = map2[cloneId].Value;
            if(cloneDrawOrder)
               CloneDrawOrder(btr, copyId, map, map2);
            return copyId;
         }
      }

      /// <summary>
      /// Enumerates all references to the given BlockTableRecord,
      /// including anonymous dynamic block references.
      /// 
      /// Note: This method enumerates all block references, including
      /// those that are inserted into non-layout blocks. See the included 
      /// ExceptNested() extension method for a means of enumerating only 
      /// block references directly inserted into layout blocks using this 
      /// method.
      /// </summary>
      /// <param name="blockTableRecord">The BlockTableRecord whose references are to be enumerated</param>
      /// <param name="trans">The Transaction to use to open the results</param>
      /// <param name="mode">The OpenMode to open the results in</param>
      /// <param name="openLocked">A value indicating if references on locked
      /// layers should be opened for write</param>
      /// <param name="directOnly">A value indicating if indirect references
      /// to other blocks that contain a reference to the given BlockTableRecord
      /// should be included.</param>
      /// <returns>A sequence of BlockReferences</returns>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="ArgumentException"></exception>

      public static IEnumerable<BlockReference> GetBlockReferences(
         this BlockTableRecord blockTableRecord,
         Transaction trans,
         OpenMode mode = OpenMode.ForRead,
         bool openLocked = false,
         bool directOnly = true)
      {
         Assert.IsNotNullOrDisposed(blockTableRecord, nameof(blockTableRecord));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         if(blockTableRecord.IsLayout)
            throw new ArgumentException("Invalid BlockTableRecord");
         ObjectIdCollection ids = blockTableRecord.GetBlockReferenceIds(directOnly, true);
         int cnt = ids.Count;
         for(int i = 0; i < cnt; i++)
         {
            yield return (BlockReference)trans.GetObject(ids[i], mode, false, openLocked);
         }
         if(!blockTableRecord.IsAnonymous && blockTableRecord.IsDynamicBlock)
         {
            ObjectIdCollection blockIds = blockTableRecord.GetAnonymousBlockIds();
            cnt = blockIds.Count;
            for(int i = 0; i < cnt; i++)
            {
               BlockTableRecord btr2 = blockIds[i].GetObject<BlockTableRecord>(trans);
               ids = btr2.GetBlockReferenceIds(directOnly, true);
               int cnt2 = ids.Count;
               for(int j = 0; j < cnt2; j++)
               {
                  yield return (BlockReference)trans.GetObject(ids[j], mode, false, openLocked);
               }
            }
         }
      }

      /// <summary>
      /// Gets the Ids of all references to the dynamic block,
      /// and all references to all anonymous dynamic blocks.
      /// </summary>
      /// <param name="btr"></param>
      /// <param name="directOnly"></param>
      /// <returns></returns>

      public static ObjectIdCollection GetBlockReferenceIds(
         this BlockTableRecord btr, 
         bool includingDynamic = true,
         bool directOnly = true)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         ObjectIdCollection ids = btr.GetBlockReferenceIds(directOnly, true);
         if(includingDynamic && btr.IsDynamicBlock && ! btr.IsAnonymous)
         {
            using(var trans = new ReadOnlyTransaction())
            {
               ObjectIdCollection anonBlockIds = btr.GetAnonymousBlockIds();
               int cnt = anonBlockIds.Count;
               for(int i = 0; i < cnt; i++)
               {
                  BlockTableRecord btr2 = anonBlockIds[i].GetObject<BlockTableRecord>(trans);
                  ids = btr2.GetBlockReferenceIds(directOnly, true);
                  int cnt2 = ids.Count;
                  for(int j = 0; j < cnt2; j++)
                     ids.Add(ids[j]);
               }
            }
         }
         return ids;
      }

      /// <summary>
      /// Returns a sequence containing the ObjectIds of all
      /// BlockTableRecords whose definitions are dependent on
      /// the BlockTableRecord which this method is invoked on.
      /// </summary>
      /// <param name="btr">The BlockTableRecord whose dependent
      /// BlockTableRecords are to be searched for.</param>
      /// <param name="includingDynamic">A value indicating if
      /// anonymous dynamic block references should resolve to
      /// the dynamic block definition or the anonymous block
      /// definition.</param>
      /// <returns></returns>

      public static IEnumerable<ObjectId> GetDependentBlockIds(
         this BlockTableRecord btr,
         bool includingDynamic = true)
      {
         HashSet<ObjectId> ids = new HashSet<ObjectId>();
         using(var trans = new ReadOnlyTransaction())
         {
            var blkrefs = btr.GetBlockReferences(trans, OpenMode.ForRead, false, false);
            foreach(BlockReference blkref in blkrefs)
            {
               ids.Add(blkref.BlockTableRecord);
               if(blkref.IsDynamicBlock && includingDynamic && 
                     blkref.DynamicBlockTableRecord != blkref.BlockTableRecord)
                  ids.Add(blkref.DynamicBlockTableRecord);
            }
         }
         return ids;
      }

      public static IEnumerable<BlockReference> GetDependentBlockReferences(
         this BlockTableRecord btr, Transaction trans, OpenMode mode = OpenMode.ForRead, bool openLocked = false)
      {
         return btr.GetBlockReferences(trans, mode, openLocked, false);
      }

      /// <summary>
      /// Like GetBlockReferenceIds() except that it 
      /// returns the resulting ObjectIds in a HashSet.
      /// </summary>
      /// <param name="btr"></param>
      /// <param name="directOnly"></param>
      /// <returns></returns>

      public static HashSet<ObjectId> GetBlockReferenceIdSet(
         this BlockTableRecord btr,
         bool includingDynamic = true,
         bool directOnly = true)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         var ids = btr.GetBlockReferenceIds(directOnly, true);
         List<ObjectId> list = new List<ObjectId>(ids.Count);
         list.AddRange(ids.Cast<ObjectId>());
         Span<ObjectId> span = CollectionsMarshal.AsSpan(list);
         if(includingDynamic && btr.IsDynamicBlock && !btr.IsAnonymous)
         {
            using(var trans = new ReadOnlyTransaction())
            {
               ObjectIdCollection anonBlockIds = btr.GetAnonymousBlockIds();
               int cnt = anonBlockIds.Count;
               for(int i = 0; i < cnt; i++)
               {
                  var btr2 = trans.GetObject<BlockTableRecord>(anonBlockIds[i]);
                  ids = btr2.GetBlockReferenceIds(directOnly, true);
                  int cnt2 = ids.Count;
                  int k = list.Count;
                  CollectionsMarshal.SetCount(list, list.Count + cnt2);
                  for(int j = 0; j < cnt2; j++)
                     span[k++] = ids[j];
               }
            }
         }
         return new HashSet<ObjectId>(list);
      }

      /// <summary>
      /// Returns a value indicating if one or more elements in
      /// the argument has a direct or indirect dependence on the 
      /// BlockTableRecord which this method is invoked on.
      /// 
      /// This method's principal purpose is to detect and avoid 
      /// conditions that would cause an existing block to become 
      /// self-referencing when adding one or more objects to it.
      /// 
      /// If the includingDynamic argument is true, this method
      /// considers any direct or indirect reference to anonymous 
      /// dynamic blocks as references to the defining dynamic 
      /// block.
      /// </summary>
      /// <param name="btr">The BlockTableRecord to check for
      /// references to</param>
      /// <param name="entityIds">A sequence of ObjectIds of one
      /// or more entities, to check for references to the given
      /// BlockTableRecord</param>
      /// <param name="includingDynamic">A value indicating if 
      /// references to anonymous dynamic blocks should be regarded
      /// as references to their respective defining dynamic block 
      /// definition</param>
      /// <returns>A value indicating if one or more elements in 
      /// the entityIds argument directly or indirectly references 
      /// the block which the method is invoked on.</returns>

      public static bool IsReferencedByAny(this BlockTableRecord btr, 
         IEnumerable<ObjectId> entityIds,
         bool includingDynamic = true)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         Assert.IsNotNull(entityIds, nameof(entityIds));
         var idSet = btr.GetBlockReferenceIdSet(includingDynamic, false);
         if(entityIds is ObjectId[] array)
         {
            for(int i = 0; i < array.Length; i++)
            {
               if(idSet.Contains(array[i]))
                  return true;
            }
            return false;
         }
         foreach(var id in entityIds)
         {
            if(idSet.Contains(id))
               return true;
         }
         return false;
      }

      /// This is an inversion of the above, that can be 
      /// invoked on a sequence of ObjectIds:

      public static bool HasDependenceOn(
         this IEnumerable<ObjectId> entityIds,
         BlockTableRecord blockTableRecord,
         bool includingDynamic = true)
      {
         return blockTableRecord.IsReferencedByAny(entityIds, includingDynamic);
      }

      /// <summary>
      /// Overload that accepts an ObjectIdCollection in lieu
      /// of an IEnumerable<ToObjectId> and uses an optimized path.
      /// </summary>

      public static bool IsReferencedByAny(this BlockTableRecord btr, 
         ObjectIdCollection entityIds,
         bool includingDynamic = true)
      {
         Assert.IsNotNullOrDisposed(btr, nameof(btr));
         Assert.IsNotNull(entityIds, nameof(entityIds));
         var idSet = btr.GetBlockReferenceIdSet(includingDynamic, false);
         for(int i = 0; i < entityIds.Count; i++)
         {
            if(idSet.Contains(entityIds[i]))
               return true;
         }
         return false;
      }

      public static bool IsUserBlock(this BlockTableRecord btr) =>
         !(btr.IsAnonymous
            || btr.IsLayout
            || btr.IsFromExternalReference
            || btr.IsFromOverlayReference
            || btr.IsDependent);

      public static Func<BlockTableRecord, bool> IsUserBlockFunc(this IEnumerable<BlockTableRecord> blocks)
      {
         return IsUserBlock;
      }

      static void CloneDrawOrder(BlockTableRecord src, ObjectId destBtrId, IdMapping map1, IdMapping map2 = null)
      {
         Assert.IsNotNull(src, nameof(src));
         Assert.IsNotNull(map1, nameof(map1));
         ErrorStatus.NullObjectId.Check(!destBtrId.IsNull);
         using(var tr = new OpenCloseTransaction())
         {
            DrawOrderTable dotSrc = (DrawOrderTable)tr.GetObject(src.DrawOrderTableId, OpenMode.ForRead);
            var ids = dotSrc.GetFullDrawOrder(0);
            if(ids.Count > 0)
            {
               ObjectIdCollection cloneIds = new ObjectIdCollection();
               Func<ObjectId, ObjectId> translate;
               if(map2 != null)
                  translate = id => map2[map1[id].Value].Value;
               else
                  translate = id => map1[id].Value;
               foreach(ObjectId id in ids)
               {
                  if(dotSrc.GetSortHandle(id) != id.Handle)
                     cloneIds.Add(translate(id));
               }
               if(cloneIds.Count > 0)
               {
                  BlockTableRecord dest = (BlockTableRecord)tr.GetObject(destBtrId, OpenMode.ForRead);
                  DrawOrderTable dotDest = (DrawOrderTable)tr.GetObject(dest.DrawOrderTableId, OpenMode.ForWrite);
                  dotDest.SetRelativeDrawOrder(cloneIds);
               }
            }
            tr.Commit();
         }
      }

   }
}
