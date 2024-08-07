﻿

using System;

/// IFilter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

namespace System.Linq.Expressions.Predicates
{
   public interface IFilter<T> 
   {
      bool IsMatch(T source);
      Func<T, bool> Predicate { get;}
   }
}