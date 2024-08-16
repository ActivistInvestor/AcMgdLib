/// EntityFilter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Linq.Expressions;
using System.Linq.Expressions.Predicates;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// This class exists primiarly to constrain 
   /// TFiltered to Entity, to allow specialized
   /// methods targeting sequences of entities to 
   /// be implemented.
   /// 
   /// A few methods have been added showing examples 
   /// of more-specialized extension methods that use
   /// entity-specific criteria with DBObjectFilter,
   /// that targets entities or derived types.
   /// </summary>
   /// <typeparam name="TFiltered"></typeparam>
   /// <typeparam name="TCriteria"></typeparam>

   public class EntityFilter<TFiltered, TCriteria> : DBObjectFilter<TFiltered, TCriteria>
      where TFiltered : Entity
      where TCriteria : DBObject
   {
      public EntityFilter(Expression<Func<TFiltered, ObjectId>> keySelector, Expression<Func<TCriteria, bool>> valueSelector)
         : base(keySelector, valueSelector)
      {
      }
   }

   public static class DBObjectFilterExtensions
   { 

      /// <summary>
      /// Example extension methods targeting DBObjectFilter, 
      /// that simplify adding specific types of criteria to 
      /// a DBObjectFilter.
      /// 
      /// The extension methods abstract away the key selector 
      /// expression and require only a predicate expression.
      /// 
      /// Implementing this functionaliy as extension methods
      /// allows them to be constrained to target only those
      /// DBObjectFilters that operate on Entities or a derived 
      /// type.
      /// 
      /// </summary>
      /// <param name="predicate"></param>
      /// <returns></returns>

      /// Add Layer filtering critiera:
      
      public static DBObjectFilter<TFiltered, LayerTableRecord>
      AddLayer<TFiltered, TCriteria>(this DBObjectFilter<TFiltered, TCriteria> filter,
         Expression<Func<LayerTableRecord,bool>> predicate) 
         where TFiltered : Entity
         where TCriteria: DBObject
      {
         return filter.Add<LayerTableRecord>(Logical.And, e => e.LayerId, predicate);
      }

      /// <summary>
      /// Add linetype filtering criteria:
      /// </summary>
      /// <param name="predicate"></param>

      public static DBObjectFilter<TFiltered, LinetypeTableRecord>
      AddLinetype<TFiltered, TCriteria>(this DBObjectFilter<TFiltered, TCriteria> filter,
         Expression<Func<LinetypeTableRecord, bool>> predicate)
         where TFiltered : Entity
         where TCriteria : DBObject
      {
         return filter.Add<LinetypeTableRecord>(Logical.And, e => e.LinetypeId, predicate);
      }
   }

} // namespace



