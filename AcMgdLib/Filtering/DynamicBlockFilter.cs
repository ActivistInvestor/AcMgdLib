/// DynamicBlockFilter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// A simple specialization of DBObjectFilter that
/// filters block references, including references
/// to anonymous dynamic blocks by a user-specified 
/// criteria.

using System;
using System.Linq.Expressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{ 
   public class DynamicBlockFilter : DBObjectFilter<BlockReference, BlockTableRecord>
   {
      public DynamicBlockFilter(Expression<Func<BlockTableRecord, bool>> predicate)
        : base(blockref => blockref.DynamicBlockTableRecord, predicate)
      {
      }
   }
}



