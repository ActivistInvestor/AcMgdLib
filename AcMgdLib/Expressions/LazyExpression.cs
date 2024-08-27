/// LazyExpression.cs
/// 
/// ActivistInvestor / Tony Tanzillo
///
/// Distributed under terms of the MIT License

using System.Diagnostics.Extensions;
using System.Linq.Expressions.Predicates;

namespace System.Linq.Expressions.Extensions
{
   /// <summary>
   /// A class that encapsulates an Expression<Func<TArg, TResult>>
   /// and performs lazy compilation.
   /// 
   /// </summary>

   public struct LazyExpression<TArg, TResult>
   {
      Expression<Func<TArg, TResult>> expression;
      Func<TArg, TResult> function;

      public LazyExpression(Expression<Func<TArg, TResult>> expression = null)
      {
         Assert.IsNotNull(expression, nameof(expression));
         this.expression = expression;
         /// This field should never be accessed via any means
         /// other than the Function property that returns it.
         this.function = null;
      }

      public static LazyExpression<TArg, TResult> Create(Expression<Func<TArg, TResult>> expression)
      { 
         Assert.IsNotNull(expression, nameof(expression));
         return new LazyExpression<TArg, TResult>(expression);
      }

      public Func<TArg, TResult> Function
      {
         get
         {
            return function ?? (function = expression.Compile());
         }
      }

      public TResult Invoke(TArg arg)
      {
         return Function(arg);
      }

      /// <summary>
      /// After assignment, any value previously returned by
      /// the above Function property is no longer valid, and
      /// must be reacquired from that property.
      /// </summary>
      public Expression<Func<TArg, TResult>> Expression 
      { 
         get => expression;
         private set
         {
            Assert.IsNotNull(value, nameof(value));
            if(!this.expression.IsEqualTo(value))
            {
               expression = value;
               function = null;
            }
         }
      }

      public static implicit operator Expression<Func<TArg, TResult>>(LazyExpression<TArg, TResult> expr)
      {
         Assert.IsNotNull(expr, nameof(expr));
         return expr.expression;
      }

      public static implicit operator Func<TArg, TResult>(LazyExpression<TArg, TResult> expr)
      {
         Assert.IsNotNull(expr, nameof(expr));
         return expr.Function;
      }

      public static implicit operator LazyExpression<TArg, TResult>(Expression<Func<TArg, TResult>> expression)
      {
         Assert.IsNotNull(expression, nameof(expression));
         return new LazyExpression<TArg, TResult>(expression);
      }

      public override string ToString()
      {
         return expression?.ToString() ?? base.ToString();
      }

   }


}

