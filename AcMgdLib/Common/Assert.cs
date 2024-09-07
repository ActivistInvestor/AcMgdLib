/// Assert.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Diagnostic and validation helper methods.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace System.Diagnostics.Extensions
{
   /// <summary>
   /// internal-use-only proxy for methods taken
   /// from the Diagnostics.Assert class, with
   /// the addition of AutoCAD-dependent methods.
   /// </summary>

   public static partial class Assert
   {
      public static void MustBeTrue(bool condition, [CallerArgumentExpression("condition")] string msg = "condition must be false")
      {
         if(!condition)
            throw new AssertionFailedException(msg + " (Must be true)");
      }

      public static void MustBeFalse(bool condition, [CallerArgumentExpression("condition")] string msg = "condition must be true")
      {
         if(condition)
            throw new AssertionFailedException(msg + " (Must be false)");
      }

      public static void IsNotNull(object arg, [CallerArgumentExpression("arg")] string msg = "null argument")
      {
         if(arg is null)
            throw new ArgumentNullException(msg).Log(arg);
      }

      public static void IsNotNullOrEmpty(string str, [CallerArgumentExpression("str")] string name = "null or empty string")
      {
         if(string.IsNullOrEmpty(str))
            throw new ArgumentNullException(nameof(str)).Log(str, name);
      }

      public static void IsNotNullOrEmpty<T>(IEnumerable<T> src, string name)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src)).Log(src, name);
         if(!src.Any())
            throw new ArgumentException("Empty sequence", nameof(src)).Log(src, name);
      }

      public static void IsNotNullOrWhiteSpace(string str, [CallerArgumentExpression("str")] string message = "string is null or whitespace")
      {
         if(string.IsNullOrWhiteSpace(str))
            throw new ArgumentNullException(message).Log(str, message);
      }

      public static void IsAssignableTo<T>(Type type, string argumentName = null)
      {
         Assert.IsNotNull(type, nameof(argumentName));
         if(!typeof(T).IsAssignableFrom(type))
            throw new InvalidCastException(
               $"Argument {argumentName}: instance of {type.Name} cannot be assigned to varaible of type {typeof(T).Name}");
      }

      /// Proxy for actual logging functionality (not included)
      
      public static T Log<T>(this T exception, params object[] args) where T : System.Exception
      {
         return exception;
      }
   }

   [Serializable]
   internal class AssertionFailedException : InvalidOperationException
   {
      public AssertionFailedException()
      {
      }

      public AssertionFailedException(string message) 
         : base("Assertion failed: " + message ?? "Unknown")
      {
      }

      public AssertionFailedException(string message, Exception innerException) 
         : base("Assertion failed: " + message ?? "Unknown", innerException)
      {
      }
   }
}



