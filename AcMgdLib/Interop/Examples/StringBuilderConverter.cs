/// StringBuilderConverter.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.LispInterop;

namespace AcMgdLib.Interop.Examples
{
   /// <summary>
   /// A second example demonstrating how to provide
   /// extended type and implicit conversion support 
   /// for the ListBuilder class. 
   /// 
   /// This class allows a StringBuilder to be passed 
   /// as an argument to various ListBuilder methods 
   /// that convert objects to Lisp data:
   /// </summary>

   [TypedValueConverter(typeof(StringBuilder))]
   public class StringBuilderConverter : TypedValueConverter
   {

      public override bool CanConvert(bool toTypedValues)
      {
         return toTypedValues;
      }

      public override object ToTypedValues(object value, Context context = Context.Lisp)
      {
         if(value is StringBuilder sb)
         {
            return new TypedValue((short)LispDataType.Text, sb.ToString());
         }
         return null;
      }

      public override object FromTypedValues(object value, Context context = Context.Lisp)
      {
         throw new NotSupportedException();
      }
   }
}




