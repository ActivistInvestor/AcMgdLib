﻿/// OwnerFilter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// High-level APIs that help simplify and streamline 
/// development of managed AutoCAD extensions.

using System.Collections.Generic;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// Filters entities by their owner BlockId, against a 
   /// set of one or more owner block ObjectIds.
   /// 
   /// To include only entities that are owned by a specific
   /// BlockTableRecord or set of BlockTableRecords, pass the 
   /// ObjectId(s) of the BlockTableRecord(s) to the constructor.
   /// 
   /// This class was built for use with the GetBlockReferences() 
   /// method, which returns BlockReferences owned by any block. 
   /// It can be used to constrain the result to only elements 
   /// owned by a specific block or set of blocks. Keep in mind
   /// that filtering block references to only those owned by a
   /// single owner block can easily be done with a simple use
   /// of 'Where(blkref => blkref.BlockId == someObjectId)', so
   /// its use in that scenario can be viewed as overkill. The
   /// usefulness comes into play when one wants to constrain a
   /// set of BlockReferences to those owned by one of several
   /// owner blocks. For example, to constrain a sequence of
   /// BlockReferences returned by GetBlockReferences() to only
   /// insertions in paper space layouts, one can use:
   /// 
   ///   new OwnerFilter(trans.GetLayoutBlockIds(false));
   /// 
   /// This class also optimizes the case of filtering aginst 
   /// a single owner verses multiple owners.
   /// 
   /// The non-generic version of this type targets the 
   /// Entity type.
   /// </summary>
   /// <typeparam name="T">The type of entity to filter</typeparam>

   public class OwnerFilter<T> : MemberFilter<T, ObjectId> where T : Entity
   {
      public OwnerFilter(params ObjectId[] ownerIds)
         : this(ownerIds as IEnumerable<ObjectId>)
      {
      }

      public OwnerFilter(IEnumerable<ObjectId> ids)
         : base(e => e.BlockId, ids)
      {
         Assert.IsNotNull(ids, nameof(ids));
         foreach(ObjectId id in ids)
            AcRx.ErrorStatus.WrongObjectType.Requires<BlockTableRecord>(id);
      }

      public bool IsEmpty => Count > 0;
   }

   public class OwnerFilter : OwnerFilter<Entity>
   {
      public OwnerFilter(params ObjectId[] ownerIds)
         : base((IEnumerable<ObjectId>)ownerIds)
      {
      }

      public OwnerFilter(IEnumerable<ObjectId> ids)
         : base(ids)
      {
      }
   }



}



