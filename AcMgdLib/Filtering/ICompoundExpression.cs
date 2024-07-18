/// FilteredEnumerable.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Linq.Expressions;

namespace System.Linq.Expressions.Predicates
{
   public interface ICompoundExpression<T>
   {
      void Add(Logical operation, Expression<Func<T, bool>> predicate);
      void Add(Expression<Func<T, bool>> predicate);
   }


}



