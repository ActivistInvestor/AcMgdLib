

using System;

/// IFilter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public interface IFilter<T> where T : DBObject
   {
      bool IsMatch(T source);
      Func<T, bool> MatchPredicate { get;}
   }
}