/// Filter.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.


using System;
using System.Diagnostics.Extensions;
using System.Linq.Expressions;

namespace System.Linq.Expressions.Predicates
{
   /// <summary>
   /// An abstract base type for building simple, 
   /// non-relational, composable filters that use 
   /// lazy compilation.
   /// 
   /// The motivation behind this class was to provide
   /// the composablity that allows multiple instances
   /// to be logically-combined to efficiently perform
   /// complex filtering operations without having to 
   /// incurr the function-call overhead that would be
   /// otherwise needed without using Expressions.
   /// 
   /// When two instances are logically combined, their
   /// expressions are combined into a single delegate
   /// that can perform the functional equivalent of two
   /// seperate delegates connected by a logical and/or 
   /// operation. As a result, complex filters consisting 
   /// of arbitrarily-complex criteria can be built that 
   /// can be evaluated by a single delegate invocation, 
   /// rather than many.
   /// </summary>
   /// <typeparam name="T">The type of Entity to be filtered</typeparam>

   public abstract class Filter<T> : IFilter<T>, ICompoundExpression<T> 
   {
      Expression<Func<T, bool>> expression = null;
      Func<T, bool> predicate = null;

      public Func<T, bool> MatchPredicate
      {
         get
         {
            return predicate ?? (predicate = Expression.Compile());
         }
      }
 
      protected abstract Expression<Func<T, bool>> GetExpression();

      public Expression<Func<T, bool>> Expression
      {
         get
         {
            if(expression == null)
            {
               var result = GetExpression();
               Assert.IsNotNull(result, nameof(result));
               expression = result;
               Invalidate();
            }
            return expression;
         }
         protected set
         {
            Assert.IsNotNull(value, nameof(value));
            if(expression != value)
            {
               expression = value;
               Invalidate();
            }
         }
      }

      public virtual void Invalidate()
      {
         predicate = null;
      }

      /// <summary>
      /// Modifies the instance to include the argument
      /// in a logical operation.
      /// </summary>
      /// <param name="operation"></param>
      /// <param name="predicate"></param>

      public void Add(Logical operation, Expression<Func<T, bool>> predicate)
      {
         Expression = Expression.Add(operation, predicate);
      }

      public void Add(Expression<Func<T, bool>> predicate)
      {
         this.Add(Logical.And, predicate);
      }

      public Filter<T> And(Expression<Func<T, bool>> predicate)
      {
         Add(Logical.And, predicate);
         return this;
      }

      public Filter<T> Or(Expression<Func<T, bool>> predicate)
      {
         Add(Logical.Or, predicate);
         return this;
      }

      /// <summary>
      /// Not efficient.
      /// 
      /// In preference to calling this, get the delegate
      /// from the MatchPredicate property, assign it a
      /// a variable, and use that.
      /// </summary>
      
      public bool IsMatch(T source)
      {
         return MatchPredicate(source);
      }

      public static implicit operator Func<T, bool>(Filter<T> operand)
      {
         return operand?.MatchPredicate ?? throw new ArgumentNullException(nameof(operand));
      }

      public static implicit operator Expression<Func<T, bool>>(Filter<T> operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Expression;
      }

   }


}



