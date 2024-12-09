/// Assert.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.BoundaryRepresentation
{
   public static class Assert
   {
      public static void IsNotNull(object arg, [CallerArgumentExpression(nameof(arg))] string msg = "")
      {
         if(arg is null)
            throw new ArgumentNullException(msg);
      }

      public static void IsNotNullOrDisposed(object arg, [CallerArgumentExpression(nameof(arg))] string msg = "")
      {
         if(arg is null)
            throw new ArgumentNullException(msg);
         if(arg is DisposableWrapper wrapper && wrapper.IsDisposed)
            throw new ObjectDisposedException(msg);
      }

   }

}
