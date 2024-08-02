/// Logical.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Classes that aid in the efficient filtering of 
/// DBObjects in Linq queries and other sceanrios.


namespace System.Linq.Expressions.Predicates
{
   public enum Logical
   {
      /// <summary>
      /// Both input expressions must return true
      /// </summary>

      And, 

      /// <summary>
      /// Either input expression must return true
      /// </summary>

      Or, 

      /// <summary>
      /// Like And, except that the order in which 
      /// two expressions are evaluated is reversed.
      /// </summary>

      ReverseAnd,

      /// <summary>
      /// Like Or, except that the order in which 
      /// two expressions are evaluated is reversed.
      /// </summary>

      ReverseOr,

      /// <summary>
      /// The two expressions must produce the logical 
      /// complement of each other.
      /// 
      /// Note that combining an expression with a
      /// default expression using this operation will 
      /// not produce the desired result. 
      /// 
      /// For example:
      /// 
      ///    a xor true
      ///    
      /// returns true if a evaluates to false, and
      /// is functionally equivalent to !a, but because
      /// default expressions are ignored, the result
      /// will be the left-side 'a'.
      /// 
      /// Supported by ExpressionBuilder, but not
      /// currently not supported by DBObjectFilter,
      /// PredicateExpression or LazyExpression.
      /// </summary>

      Xor, 
      ReverseXor,

      /// <summary>
      /// The logical complement of a single expression,
      /// not supported by operations that combine two 
      /// expressions.
      /// </summary>

      Not
   }


}



