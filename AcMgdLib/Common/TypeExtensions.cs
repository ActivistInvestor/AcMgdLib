﻿/// TypeExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Extensions;
using System.Linq;
using System.Text;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static partial class TypeExtensions
   {
      /// <summary>
      /// Produces the unmangled name of a generic type 
      /// in the same form it has in C# List code:
      /// </summary>
      /// <param name="type"></param>
      /// <returns></returns>

      public static string CSharpName(this Type type)
      {
         return type.IsGenericType ? csharpNames[type] : type.Name;
      }

      static Cache<Type, string> csharpNames =
         new Cache<Type, string>(getCSharpName);

      static string getCSharpName(Type type)
      {
         var name = type.Name;
         if(!type.IsGenericType)
            return name;
         var sb = new StringBuilder();
         sb.Append(name.Substring(0, name.IndexOf('`')));
         sb.Append("<");
         sb.Append(string.Join(", ",
            type.GetGenericArguments()
              .Select(getCSharpName)));
         sb.Append(">");
         return sb.ToString();
      }

   }

}



