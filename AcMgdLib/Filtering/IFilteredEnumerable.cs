/// FilteredEnumerable.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public interface IFilteredEnumerable<T, TCriteria> : IEnumerable<T>, IFilter<T>
      where T : DBObject
      where TCriteria : DBObject
   {
      ICompoundExpression<T> Predicate { get; }
      ICompoundExpression<TCriteria> Criteria { get; }

      void Add<TNewCriteria>(Logical operation,
         Expression<Func<T, ObjectId>> keySelector,
         Expression<Func<TNewCriteria, bool>> predicate) where TNewCriteria : DBObject;

      void Add<TNewCriteria>(
         Expression<Func<T, ObjectId>> keySelector,
         Expression<Func<TNewCriteria, bool>> predicate) where TNewCriteria : DBObject;
   }


}



