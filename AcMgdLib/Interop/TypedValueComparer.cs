/// TypedValueComparer.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Autodesk.AutoCAD.Runtime
{
   public class TypedValueComparer : IEqualityComparer<TypedValue>
   {
      public bool Equals(TypedValue x, TypedValue y)
      {
         return x.TypeCode == y.TypeCode 
            && valueComparer.Equals(x.Value, y.Value);
      }

      public int GetHashCode(TypedValue obj)
      {
         return HashCode.Combine(obj.TypeCode.GetHashCode(), 
            valueComparer.GetHashCode(obj.Value));
      }

      static readonly IEqualityComparer valueComparer = 
         StructuralComparisons.StructuralEqualityComparer;

      static TypedValueComparer instance;

      public static TypedValueComparer Instance => 
         instance ?? (instance = new TypedValueComparer());
   }


}




