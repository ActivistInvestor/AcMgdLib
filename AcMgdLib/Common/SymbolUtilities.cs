/// SymbolUtilities.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static class SymbolUtilities
   {
      /// <summary>
      /// SymbolUtilityServices extensions
      /// </summary>

      public static bool TryValidateSymbolName(string name, bool allowVerticalBar = false)
      {
         Assert.IsNotNullOrWhiteSpace(name, nameof(name));
         try
         {
            SymbolUtilityServices.ValidateSymbolName(name, allowVerticalBar);
            return true;
         }
         catch(AcRx.Exception)
         {
            return false;
         }
      }


   }
}
