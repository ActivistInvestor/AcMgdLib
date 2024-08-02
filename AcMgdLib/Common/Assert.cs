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

namespace System.Diagnostics.Extensions
{
   /// <summary>
   /// internal-use-only proxy for methods taken
   /// from the Diagnostics.Assert class, with
   /// the addition of AutoCAD-dependent methods.
   /// </summary>

   public static partial class Assert
   {
      public static void IsNotNull(object arg, string msg = "null argument")
      {
         if(arg is null)
            throw new ArgumentNullException(msg).Log(arg);
      }

      public static void IsNotNullOrEmpty(string str, string name)
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

      public static void IsNotNullOrWhiteSpace(string s, string message = null)
      {
         if(string.IsNullOrWhiteSpace(s))
            throw new ArgumentNullException(message).Log(s, message);
      }

      /// Proxy for actual logging functionality (not included)
      
      public static T Log<T>(this T exception, params object[] args) where T : System.Exception
      {
         return exception;
      }
   }


}



