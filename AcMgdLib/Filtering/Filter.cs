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
   /// <typeparam name="T">The type of object to be filtered</typeparam>

   public abstract class Filter<T> : IFilter<T>, ICompoundExpression<T> 
   {
      Expression<Func<T, bool>> expression = null;
      Func<T, bool> predicate = null;

      public Func<T, bool> Predicate
      {
         get
         {
            return predicate ?? (predicate = Expression.Compile());
         }
      }

      protected abstract Expression<Func<T, bool>> GetBaseExpression();

      public Expression<Func<T, bool>> Expression
      {
         get
         {
            if(expression == null)
            {
               var result = GetBaseExpression();
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

      /// <summary>
      /// Clears any compound expression created by any
      /// logical operations performed on the instance
      /// and restores the original base expression that
      /// is provided by derived types.
      /// </summary>
      
      public void Clear()
      {
         this.expression = null;
         this.predicate = null;
      }

      /// <summary>
      /// Invalidates the current compiled predicate 
      /// causing it to be recompiled from the current
      /// expression.
      /// 
      /// This API must be called whenever the current 
      /// expression has changed.
      /// 
      /// After this API is called, any delegate that
      /// was previously-obtained through the Predicate 
      /// property is no longer valid.
      /// </summary>
      
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

      /// <summary>
      /// Modifies the instance to include the argument
      /// in a logical 'and' operation.
      /// </summary>
      /// <param name="operation"></param>
      /// <param name="predicate"></param>

      public void Add(Expression<Func<T, bool>> predicate)
      {
         this.Add(Logical.And, predicate);
      }

      /// <summary>
      /// Modifies the instance to include the argument
      /// in a logical 'and' operation, and returns the
      /// instance.
      /// </summary>
      /// <param name="predicate"></param>
      /// <returns></returns>

      public Filter<T> And(Expression<Func<T, bool>> predicate)
      {
         Add(Logical.And, predicate);
         return this;
      }

      /// <summary>
      /// Modifies the instance to include the argument
      /// in a logical 'or' operation, and returns the
      /// instance.
      /// </summary>
      /// <param name="predicate"></param>
      /// <returns></returns>

      public Filter<T> Or(Expression<Func<T, bool>> predicate)
      {
         Add(Logical.Or, predicate);
         return this;
      }

      /// <summary>
      /// Not efficient.
      /// 
      /// Calling this method entails a null check at best, or
      /// an additional level of indirection at worst. 
      /// 
      /// In preference to calling this numerous times, get the 
      /// value of the Predicate property, assign it to a 
      /// variable, and use that:
      /// 
      ///   Filter<string> filter = ....
      ///   
      ///   Func<string, bool> predicate = filter.Predicate;
      ///   
      ///   foreach(string item in items)
      ///   {
      ///      if(predicate(item))
      ///         Console.WriteLine("Item meets filter criteria");
      ///   }
      /// 
      /// Important:
      /// 
      /// The delegate obtained from the Predicate property is valid 
      /// up to the point when the Invalidate() method is subsequently-
      /// called, or a value is assigned to the Expression property. 
      /// 
      /// After either of those events has occured, any delegate that
      /// was previously obtained from the Predicate property is no 
      /// longer valid and should be discarded or be reassigned to the 
      /// property's current value.
      /// </summary>
      
      public bool IsMatch(T source)
      {
         return predicate?.Invoke(source) ?? Predicate(source);
      }

      public static implicit operator Func<T, bool>(Filter<T> operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Predicate;
      }

      public static implicit operator Expression<Func<T, bool>>(Filter<T> operand)
      {
         Assert.IsNotNull(operand, nameof(operand));
         return operand.Expression;
      }

   }


}



