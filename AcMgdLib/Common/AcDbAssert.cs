/// AcDbAssert.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Diagnostic and validation helper methods.

using Autodesk.AutoCAD.Runtime;
using System.Runtime.CompilerServices;

namespace System.Diagnostics.Extensions
{
   /// <summary>
   /// Members of the Assert class that have a dependence on AutoCAD.
   /// </summary>
   
   public static partial class Assert
   {
      /// <summary>
      /// Use this rather than IsNotNull() on any DisposableWrapper
      /// </summary>
      /// <param name="arg"></param>
      /// <param name="msg"></param>

      public static void IsNotNullOrDisposed(DisposableWrapper arg, [CallerArgumentExpression("arg")] string msg = "null or disposed object")
      {
         if(arg is null)
            throw new ArgumentNullException(msg).Log(arg);
         if(arg.IsDisposed)
            throw new ObjectDisposedException(arg.GetType().FullName).Log(arg);
      }
   }


}



