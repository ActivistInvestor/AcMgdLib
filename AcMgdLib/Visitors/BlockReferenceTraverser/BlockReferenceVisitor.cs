
/// BlockReferenceVisitor.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// A 'lightweight' version of the BlockVisitor class
/// that can be used for counting block references.

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.DatabaseServices
{
   public class BlockReferenceVisitor
   {
      ObjectId rootBlockId;
      IEnumerable<ObjectId> blockRefIds;
      Transaction trans;
      static IntPtr blkRefClassPtr = 
         RXObject.GetClass(typeof(BlockReference)).UnmanagedObject;
      static bool IsBlockRefId(ObjectId id) => 
         id.ObjectClass.UnmanagedObject == blkRefClassPtr;
      Dictionary<ObjectId, IEnumerable<BlockReference>> map =
         new Dictionary<ObjectId, IEnumerable<BlockReference>>();
      bool visiting = false;
      Stack<BlockReference> containers;
      Database db;

      /// <summary>
      /// Traverses all block references that are nested in 
      /// the definition of the BlockTableRecord referenced 
      /// by the argument.
      /// 
      /// The argument can be the ObjectId of a layout block
      /// (e.g., model space or a paper space block).
      /// </summary>
      /// <param name="blockId">The ObjectId of a BlockTableRecord
      /// representing the block whose nested insertions are to be 
      /// counted.</param>
      /// <param name="resolveDynamic">True to resolve 
      /// references to anonymous dynamic blocks to the 
      /// dynamic block definition</param>
      /// <exception cref="ArgumentException"></exception>
      
      public BlockReferenceVisitor(ObjectId blockId)
      {
         if(blockId.IsNull || !blockId.IsValid)
            throw new ArgumentException("Null or Invalid ObjectId");
         if(blockId.ObjectClass != RXObject.GetClass(typeof(BlockTableRecord)))
            throw new ArgumentException("Wrong object type");
         rootBlockId = blockId;
         Initialize();
      }

      /// <summary>
      /// Traverses the block references whose Ids are passed
      /// in the argument, and all block references nested 
      /// within same.
      /// 
      /// The arguments must be the ObjectIds of one or more
      /// BlockReference objects, all having the same owner.
      /// </summary>
      /// <param name="blockRefIds">The ObjectIds of the block
      /// references to be counted.</param>
      /// <param name="resolveDynamic">True to resolve 
      /// references to anonymous dynamic blocks to the 
      /// dynamic block definition</param>
      /// <exception cref="ArgumentNullException"></exception>
     
      public BlockReferenceVisitor(IEnumerable<ObjectId> blockRefIds)
      {
         if(blockRefIds is null)
            throw new ArgumentNullException(nameof(blockRefIds));
         var ids = blockRefIds.Distinct().ToArray();
         Validate(ids);
         this.blockRefIds = ids;
         Initialize();
      }

      private void Initialize()
      {
         if(!rootBlockId.IsNull)
            this.db = rootBlockId.Database;
         else
         {
            if(blockRefIds == null || !blockRefIds.Any())
               throw new ArgumentException("No database");
            this.db = blockRefIds.First().Database;
         }
      }

      void Validate(IEnumerable<ObjectId> blockRefIds)
      {
         if(blockRefIds is null)
            throw new ArgumentNullException(nameof(blockRefIds));
         if(!blockRefIds.Any())
            throw new ArgumentException($"{nameof(blockRefIds)}: empty sequence.");
         var ids = blockRefIds.Distinct().ToArray();
         if(!ids.All(id => !id.IsNull && IsBlockRefId(id)))
            throw new ArgumentException($"{nameof(blockRefIds)}: Wrong object type.");
         System.Exception ex = null;
         using(var tr = new OpenCloseTransaction())
         {
            var blockrefs = GetObjects<BlockReference>(ids, tr);
            if(!blockrefs.All(IsTrueBlockReference))
               ex = new ArgumentException(
                  $"{nameof(blockRefIds)}: One or more invalid BlockReferences.");
            else if(!IsEqual(blockrefs, blockref => blockref.OwnerId))
               ex = new ArgumentException(
                  $"{nameof(blockRefIds)}: Elements must have a common owner.");
            tr.Commit();
         }
         if(ex != null)
            throw ex;
      }

      public Database Database => this.db;
      public ObjectId RootBlockId => rootBlockId;
      public IEnumerable<ObjectId> BlockReferenceIds => blockRefIds;
      public int Depth => containers?.Count ?? 0;
      public bool IsVisiting => visiting;

      public Stack<BlockReference> Containers
      {
         get
         {
            CheckVisiting();
            return containers;
         }
      }

      public virtual void Visit()
      {
         CheckVisiting(false);
         Clear();
         visiting = true;
         try
         {
            using(trans = new OpenCloseTransaction())
            {
               containers = new Stack<BlockReference>();
               foreach(var blockref in GetRootBlockReferences())
                  Visit(blockref, containers);
               trans.Commit();
            }
         }
         finally
         {
            visiting = false;
            map.Clear();
            containers = null;
            trans = null;
         }
      }

      IEnumerable<BlockReference> GetRootBlockReferences()
      {
         if(rootBlockId.IsNull)
         {
            if(blockRefIds is null || !blockRefIds.Any())
               throw new InvalidOperationException("No block or block references specified");
            return GetObjects<BlockReference>(blockRefIds).Where(IsTrueBlockReference);
         }
         else
         {
            return GetBlockReferences(GetObject<BlockTableRecord>(rootBlockId));
         }
      }

      public virtual void Clear()
      {
         map.Clear();
      }

      void Visit(BlockReference blockReference, Stack<BlockReference> containers)
      {
         if(VisitBlockReference(blockReference, containers))
         {
            containers.Push(blockReference); 
            var block = GetObject<BlockTableRecord>(blockReference.BlockTableRecord);
            foreach(BlockReference blkref in GetBlockReferences(block))
               Visit(blkref, containers);
            containers.Pop();
         }
      }

      protected virtual bool VisitBlock(BlockTableRecord btr)
      {
         return true;
      }

      protected virtual bool VisitBlockReference(BlockReference blockReference,
         Stack<BlockReference> containers)
      {
         return true;
      }

      protected Dictionary<ObjectId, IEnumerable<BlockReference>> Map => map;

      private IEnumerable<BlockReference> GetBlockReferences(BlockTableRecord btr)
      {
         IEnumerable<BlockReference> result;
         if(!map.TryGetValue(btr.ObjectId, out result))
         {
            if(VisitBlock(btr))
               result = GetObjects<BlockReference>(btr.Cast<ObjectId>())
                  .Where(IsTrueBlockReference).ToList();
            else
               result = Enumerable.Empty<BlockReference>();
            map[btr.ObjectId] = result;
         }
         return result;
      }

      static bool IsTrueBlockReference(BlockReference blkref)
      {
         return !(blkref is Table) && !DBObject.IsCustomObject(blkref.ObjectId);
      }

      protected T GetObject<T>(ObjectId id) where T: DBObject
      {
         if(trans is null)
            throw new InvalidOperationException("No transaction");
         return (T)trans.GetObject(id, OpenMode.ForRead);
      }

      protected IEnumerable<T> GetObjects<T>(IEnumerable<ObjectId> ids, Transaction trans = null)
         where T:DBObject
      {
         if(trans is null && this.trans is null)
            throw new ArgumentNullException(nameof(trans));
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         trans = trans ?? this.trans;
         RXClass rxclass = RXObject.GetClass(typeof(T));
         foreach(ObjectId id in ids)
            if(id.ObjectClass.IsDerivedFrom(rxclass))
               yield return (T)trans.GetObject(id, OpenMode.ForRead);
      }

      protected void CheckVisiting(bool mustBeVisiting = true)
      {
         if(mustBeVisiting ^ visiting)
            throw new InvalidOperationException("Invalid context");
      }

      /// <summary>
      /// (Excerpted from EnumerableExtensions)
      /// 
      /// Returns a value indicating if all values obtained
      /// from elements in the sequence are equal, according 
      /// to the supplied equality comparer or the default 
      /// comparer for the element type.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <typeparam name="TElement"></typeparam>
      /// <param name="source">The sequence whose elements
      /// are to be compared</param>
      /// <param name="selector">A function that takes an
      /// element and returns the value to be compared to
      /// values obtained from other elements.</param>
      /// <param name="comparer">The IEqualityComperer used
      /// to compare values.</param>
      /// <returns>A value indicating if the values obtained
      /// from all elements are all equal</returns>
      /// <exception cref="ArgumentNullException"></exception>

      static bool IsEqual<T, TElement>(IEnumerable<T> source,
         Func<T, TElement> selector,
         IEqualityComparer<TElement> comparer = null)
      {
         if(source == null)
            throw new ArgumentNullException(nameof(source));
         if(selector == null)
            throw new ArgumentNullException(nameof(selector));
         comparer = comparer ?? EqualityComparer<TElement>.Default;
         using(var e = source.GetEnumerator())
         {
            if(!e.MoveNext())
               return true;
            var current = selector(e.Current);
            while(e.MoveNext())
            {
               var next = selector(e.Current);
               if(!comparer.Equals(current, next))
                  return false;
               current = next;
            }
            return true;
         }
      }

   }

}
