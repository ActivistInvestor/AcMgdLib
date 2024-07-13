/// ExpressionBuilder.cs
///
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under the terms of the MIT License

using System.Collections.Generic;
using System.Linq.Expressions.Extensions;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using System.Diagnostics.Extensions;

namespace System.Linq.Expressions.Predicates
{

   /// <summary>
   /// A class that composes predicate expressions of 
   /// varying complexity.
   /// 
   /// This code is very loosely based on Joe Albahari's
   /// PredicateBuilder:
   /// 
   ///   https://www.albahari.com/nutshell/predicatebuilder.aspx
   ///   
   /// But, takes a radically-different approach, mainly
   /// by making everything extension methods. As a result, 
   /// there is no need to reference ExpressionBuilder or
   /// any other custom type. 
   /// 
   /// Almost all functionality can be accessing using 
   /// extension methods targeting Expression<Func<T, bool>> 
   /// and Func<T, bool>.
   /// 
   /// </summary>

   public static class ExpressionBuilder
   {
      public static Expression<Func<T, bool>> Join<T>(
         this Expression<Func<T, bool>> left,
         Expression<Func<T, bool>> right,
         Func<Expression, Expression, BinaryExpression> Operator)
      {
         Assert.IsNotNull(left, nameof(left));
         Assert.IsNotNull(right, nameof(right));
         Assert.IsNotNull(Operator, nameof(Operator));
         if(left.IsDefault())
            return right;
         if(right.IsDefault())
            return left;
         var name = left.Parameters.First().Name;
         var parameter = Expression.Parameter(typeof(T), name);
         return Expression.Lambda<Func<T, bool>>(
            Visitor.Replace(parameter, Operator, left, right), parameter);
      }

      public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
      {
         Assert.IsNotNull(expression, nameof(expression));
         return Expression.Lambda<Func<T, bool>>(
            Expression.Not(expression.Body), expression.Parameters);
      }

      public static Expression<Func<T, bool>> And<T>(
         this Expression<Func<T, bool>> left,
         Expression<Func<T, bool>> right, 
         bool reverse = false)
      {
         Assert.IsNotNull(left, nameof(left));
         if(reverse)
            return right.Join(left, Expression.AndAlso);
         else
            return left.Join(right, Expression.AndAlso);
      }

      public static Expression<Func<T, bool>> Or<T>(
         this Expression<Func<T, bool>> left,
         Expression<Func<T, bool>> right,
         bool reverse = false)
      {
         Assert.IsNotNull(left, nameof(left));
         if(reverse)
            return right.Join(left, Expression.OrElse);
         else
            return left.Join(right, Expression.OrElse);
      }

      public static Expression<Func<T, bool>> Xor<T>(
         this Expression<Func<T, bool>> left,
         Expression<Func<T, bool>> right,
         bool reverse = false)
      {
         Assert.IsNotNull(left, nameof(left));
         if(reverse)
            return right.Join(left, Expression.ExclusiveOr);
         else
            return left.Join(right, Expression.ExclusiveOr);
      }

      public static Expression<Func<T, bool>> Any<T>(IEnumerable<Expression<Func<T, bool>>> args)
      {
         Assert.IsNotNull(args, nameof(args));
         return Any(args as Expression<Func<T, bool>>[] ?? args.ToArray());
      }

      public static Expression<Func<T, bool>> Any<T>(params Expression<Func<T, bool>>[] args)
      {
         if(args == null || args.Length == 0)
            throw new ArgumentNullException(nameof(args));
         if(args.Length == 1)
            return args.GetAt(0, nameof(args));
         return args.Aggregate((left, right) => left.Join(right, Expression.OrElse));
      }

      public static Expression<Func<T, bool>> All<T>(IEnumerable<Expression<Func<T, bool>>> args)
      {
         Assert.IsNotNull(args, nameof(args));
         return All(args as Expression<Func<T, bool>>[] ?? args.ToArray());
      }

      public static Expression<Func<T, bool>> All<T>(params Expression<Func<T, bool>>[] args)
      {
         if(args == null || args.Length == 0)
            throw new ArgumentNullException(nameof(args));
         if(args.Length == 1)
            return args.GetAt(0, nameof(args));
         return args.Aggregate((left, right) 
            => left.Join(right, Expression.AndAlso));
      }

      /// <expr>.AndAll(<expr1>, <expr2>, <expr3>[, ...])
      ///    => <expr> && <expr1> && <expr2> && <expr3> [&& ...]

      public static Expression<Func<T, bool>> AndAll<T>(
         this Expression<Func<T, bool>> target,
         params Expression<Func<T, bool>>[] args)
      {
         Assert.IsNotNull(target, nameof(target));
         Assert.IsNotNullOrEmpty(args, nameof(args));
         if(args.Length == 1)
            return And(target, args.GetAt(0, nameof(args)));
         return args.Cons(target).Aggregate((left, right) 
            => left.Join(right, Expression.AndAlso));
      }

      public static Expression<Func<T, bool>> AndAll<T>(
         this Expression<Func<T, bool>> target,
         IEnumerable<Expression<Func<T, bool>>> args)
      {
         Assert.IsNotNull(args, nameof(args));
         return AndAll(target, args.ToArray());
      }

      /// <expr>.OrAny(<expr1>, <expr2>, <expr3>, ....)
      ///    => <expr> || <expr1> || <expr2> || <expr3> || ....

      public static Expression<Func<T, bool>> OrAny<T>(
         this Expression<Func<T, bool>> target,
         params Expression<Func<T, bool>>[] args)
      {
         Assert.IsNotNull(target, nameof(target));
         Assert.IsNotNullOrEmpty(args, nameof(args));
         if(args.Length == 1)
            return Or(target, args.GetAt(0, nameof(args)));
         return args.Cons(target).Aggregate((l, r) => l.Join(r, Expression.OrElse));
      }

      public static Expression<Func<T, bool>> OrAny<T>(
         this Expression<Func<T, bool>> target,
         IEnumerable<Expression<Func<T, bool>>> args)
      {
         Assert.IsNotNull(target, nameof(target));
         return OrAny(target, args.ToArray());
      }

      /// <summary>
      /// An alternate means of performing one of the above
      /// operations, using the Logical enum type to specify 
      /// the type of logical operation and evaluation order.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="left"></param>
      /// <param name="operation"></param>
      /// <param name="right"></param>
      /// <returns></returns>
      /// <exception cref="NotSupportedException"></exception>
      
      public static Expression<Func<T, bool>> Add<T>(this Expression<Func<T, bool>> left, Logical operation, Expression<Func<T, bool>> right)
      {
         Assert.IsNotNull(left, nameof(left));
         Assert.IsNotNull(right, nameof(right));
         switch(operation)
         {
            case Logical.And:
               return left.And(right);
            case Logical.Or:
               return left.Or(right);
            case Logical.ReverseAnd:
               return right.And(left);
            case Logical.ReverseOr:
               return right.Or(left);
            case Logical.Xor:
               return left.Xor(right);
            case Logical.ReverseXor:
               return right.Xor(left);
            default:
               throw new NotSupportedException(operation.ToString());
         }
      }

      public static bool IsDefault<T>(this Expression<Func<T, bool>> expression)
      {
         return DefaultExpression<T>.IsDefault(expression);
      }

      public static Expression<Func<T, bool>> Default<T>(bool value = false)
      {
         return DefaultExpression<T>.GetValue(value);
      }

      public static IEnumerable<T> Cons<T>(this IEnumerable<T> rest, T head)
      {
         yield return head;
         foreach(T item in rest)
            yield return item;
      }

      internal static T GetAt<T>(this T[] array, int index, string name = "array")
      {
         Assert.IsNotNull(array, name);
         if(index > array.Length - 1)
            throw new ArgumentOutOfRangeException(name,
               $"{name} requires at least {index + 1} elements");
         if(array[index] == null)
            throw new ArgumentException($"{name}[{index}] is null");
         return array[index];
      }

      /// <summary>
      /// Compares two expressions for functional 
      /// equivalence, disregarding parameter names.
      /// 
      /// For example:
      /// 
      ///   Expression<int,bool> left = x => x > 10;   // left and right use differing
      ///   Expression<int,bool> right = y => y > 10;  // parameter names
      ///   Expression<int,bool> other = x => x > 20;
      ///   
      ///   left.IsEqualTo(right) -> true
      ///   left.IsEqualTo(other) -> false
      ///   right.IsEqualTo(other) -> false
      /// 
      /// </summary>
      /// <param name="thisExpr"></param>
      /// <param name="other"></param>
      /// <returns>a value indicating if the two expressions
      /// are functionally-equivalent</returns>

      public static bool IsEqualTo(this Expression thisExpr, Expression other)
      {
         return ExpressionEqualityComparer.Instance.Equals(thisExpr, other);
      }

      class Visitor : ExpressionVisitor
      {
         readonly ParameterExpression parameter;

         Visitor(ParameterExpression parameter)
         {
            this.parameter = parameter;
         }

         protected override Expression VisitParameter(ParameterExpression node)
             => base.VisitParameter(parameter);

         public static BinaryExpression Replace<T>(ParameterExpression parameter,
            Func<Expression, Expression, BinaryExpression> LogicalOperator,
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
         {
            return (BinaryExpression)new Visitor(parameter)
               .Visit(LogicalOperator(left.Body, right.Body));
         }
      }


      /// <summary>
      /// The concept of a 'default' expression allows them to act
      /// as invocation targets for extension methods that combine
      /// multiple expressions into compound expressions. 
      /// 
      /// PrdicateExpression<int>.Empty
      /// 
      /// When a default expression is logically combined with another 
      /// expression, the result is always the other expression.
      /// 
      /// There are two default expressions, one that returns true
      /// and one that returns false. Which one should be used is
      /// entirely dependent on the context, although it is usually
      /// the one that returns false.
      /// </summary>
      /// <typeparam name="T"></typeparam>

      static class DefaultExpression<T>
      {
         public static Expression<Func<T, bool>> GetValue(bool value) => value ? True : False;

         public static readonly Expression<Func<T, bool>> True = x => true;
         public static readonly Expression<Func<T, bool>> False = x => false;

         public static bool IsDefault(Expression<Func<T, bool>> expr)
         {
            Assert.IsNotNull(expr, nameof(expr));
            return expr.IsEqualTo(False) || expr.IsEqualTo(True);
         }
      }
   }

   //class TestCases
   //{
   //   public void Main()
   //   {
   //      var expr = PredicateExpression<int>.Empty;

   //      expr |= x => x > 10;
   //      expr |= x => x < 5;

   //      Expected result equivalent

   //      Func<int, bool> f = x => x > 10 || x < 5;

   //   }
   //}


}

