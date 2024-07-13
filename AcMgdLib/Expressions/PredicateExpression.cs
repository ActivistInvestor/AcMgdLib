/// PredicateExpression.cs
/// 
/// ActivistInvestor / Tony Tanzillo
///
/// Distributed under terms of the MIT License

using System.Diagnostics.Extensions;

namespace System.Linq.Expressions.Predicates
{
   /// <summary>
   /// A class that encapsulates an Expression<Func<T, bool>>
   /// that supports performing various logical operations on 
   /// them using binary operator syntax and lazy/just-in-time
   /// compilation/execution.
   /// /// </summary>

   public struct PredicateExpression<T>
   {
      Expression<Func<T, bool>> expression;
      Func<T, bool> predicate;

      public static readonly PredicateExpression<T> True =
         new PredicateExpression<T>(a => true);
      public static readonly PredicateExpression<T> False =
         new PredicateExpression<T>(a => false);
      public static readonly PredicateExpression<T> Default = False;

      /// <summary>
      /// The "Empty" expression is treated specially by any
      /// operation that combines expressions with a logical
      /// operator. If the empty expression appears in that
      /// context, it is ignored and the result is the other 
      /// expression argument unmodified, such that given:
      /// 
      ///    PredicateExpression<int> expr = 
      ///      new PredicateExpression<int>(x => x > 10);
      ///      
      ///    PredicateExpression<int> empty = 
      ///      PredicateExpression<int>.Empty; 
      ///    
      /// These will always be true:
      /// 
      ///    empty.And(expr) == expr;
      ///    
      ///    expr.And(empty) == expr;
      ///    
      ///    expr == expr.And(empty, empty, empty);
      ///    
      /// The main purpose behind the use of the Empty 
      /// expression is to allow it to serve as an 
      /// invocation target for extension methods that
      /// target Expression<Func<T, bool>> and return
      /// results that can be implicitly converted to 
      /// PredicateExpression<T>. One can construct a
      /// compound PredicateExpression<T> by starting
      /// with the Empty expression, and then use And()
      /// and Or() to create compound expressions that
      /// do not include the initial Empty expression
      /// which they started with.
      /// 
      /// <code>
      /// 
      ///   var expr = PredicateExpression<int>.Empty;
      ///   
      ///   expr |= x => x > 10;             
      ///   expr |= x < 5;                   
      ///   
      ///   produces:  x => x > 10 || x < 5;    
      /// 
      /// </code>
      /// 
      ///    
      /// </summary>

      public static readonly PredicateExpression<T> Empty = False;
         
      /// <summary>
      /// If no argument is provided, the expression is set 
      /// to an expression that unconditionaly returns false;
      /// </summary>
      /// <param name="expression"></param>

      public PredicateExpression(Expression<Func<T, bool>> expression = null)
      {
         this.expression = expression ?? Empty.expression;
         this.predicate = null;
      }


      public static PredicateExpression<T> Create(Expression<Func<T, bool>> expression)
      { 
         Assert.IsNotNull(expression, nameof(expression));
         return new PredicateExpression<T>(expression);
      }

      public static PredicateExpression<T> GetDefault(bool value = false)
      {
         return value ? True : False;
      }

      public Func<T, bool> Predicate
      {
         get
         {
            return predicate ?? (predicate = expression.Compile());
         }
      }

      public Expression<Func<T, bool>> Expression 
      { 
         get => expression;
         private set
         {
            Assert.IsNotNull(value, nameof(value));
            if(!object.ReferenceEquals(expression, value))
            {
               expression = value;
               predicate = null;
            }
         }
      }

      public bool IsDefault()
      {
         return expression.IsDefault();
      }

      /// params Expression<Func<T, bool>>[]
      public PredicateExpression<T> And(params Expression<Func<T, bool>>[] elements)
      {
         Assert.IsNotNullOrEmpty(elements, nameof(elements));
         if(elements.Length == 1)
            return ExpressionBuilder.And(this, elements.GetAt(0, nameof(elements)));
         return this.expression.AndAll(elements);
      }

      /// params PredicateExpression<T>[]
      public PredicateExpression<T> And(params PredicateExpression<T>[] elements)
      {
         Assert.IsNotNull(elements, nameof(elements));
         return And(elements.Select(e => e.expression).ToArray());
      }

      /// params Expression<Func<T, bool>>[]
      public PredicateExpression<T> Or(params Expression<Func<T, bool>>[] elements)
      {
         Assert.IsNotNullOrEmpty(elements, nameof(elements));
         if(elements.Length == 1)
            return ExpressionBuilder.Or(this, elements.GetAt(0, nameof(elements)));
         else
            return this.expression.OrAny(elements);
      }

      /// params PredicateExpression<T>[]
      public PredicateExpression<T> Or(params PredicateExpression<T>[] elements)
      {
         Assert.IsNotNull(elements, nameof(elements));
         return Or(elements.Select(e => e.expression).ToArray());
      }

      public static PredicateExpression<T> All(params Expression<Func<T, bool>>[] elements)
      {
         Assert.IsNotNull(elements, nameof(elements));
         if(elements.Length < 2)
            throw new ArgumentException("Requires at least 2 arguments.");
         return Create(ExpressionBuilder.All(elements));
      }

      public static PredicateExpression<T> Any(params Expression<Func<T, bool>>[] elements)
      {
         Assert.IsNotNull(elements, nameof(elements));
         if(elements.Length < 2)
            throw new ArgumentException("Requires at least 2 arguments.");
         return Create(ExpressionBuilder.Any(elements));
      }

      public static PredicateExpression<T> Not(Expression<Func<T, bool>> expression)
      {
         Assert.IsNotNull(Not(expression), nameof(expression));
         return expression.Not();
      }

      /// <summary>
      /// Operators
      ///   
      ///    (x & y) is equivalent to x.And(y)
      ///    (x | y) is equivalent to x.Or(y)
      ///    
      ///    x &= y1 & y2 & y3 
      ///    
      ///    is equivalent to either of these:
      ///    
      ///        x = x.And(y1, y2, y3);
      ///        
      ///        x = x.And(y1).And(y2).And(y3);
      ///        
      /// & and | operators can accept a combination of
      /// PredicateExpression<T> and Expression<Func<T, bool>>
      /// on either side, but one operand must be the former.
      /// 
      /// With C# 13, things are going to become a bit more
      /// interesting.
      ///     
      /// </summary>

      public static PredicateExpression<T> operator &(
         PredicateExpression<T> left,
         Expression<Func<T, bool>> right)
      {
         return left.And(right);
      }

      public static PredicateExpression<T> operator &(
         Expression<Func<T, bool>> left,
         PredicateExpression<T> right)

      {
         return left.And(right);
      }

      public static PredicateExpression<T> operator |(
         PredicateExpression<T> left,
         Expression<Func<T, bool>> right)
      {
         return left.Or(right);
      }

      public static PredicateExpression<T> operator |(
         Expression<Func<T, bool>> left,
         PredicateExpression<T> right)

      {
         return left.Or(right);
      }

      /// <summary>
      /// Conversion operators
      /// 
      /// Bi-directional conversion from/to
      /// PredicateExpression<T> and 
      /// Expression<Func<T, bool>>,
      /// 
      /// Unidirectional conversion to Func<T, bool>
      /// </summary>

      public static implicit operator Expression<Func<T, bool>>(PredicateExpression<T> expr)
      {
         Assert.IsNotNull(expr, nameof(expr));
         return expr.Expression;
      }

      public static implicit operator Func<T, bool>(PredicateExpression<T> expr)
      {
         Assert.IsNotNull(expr, nameof(expr));
         return expr.Predicate;
      }

      public static implicit operator PredicateExpression<T>(Expression<Func<T, bool>> expression)
      {
         return Create(expression ?? throw new ArgumentNullException(nameof(expression))); 
      }

      public override string ToString()
      {
         return expression?.ToString() ?? base.ToString();
      }

   }


}

