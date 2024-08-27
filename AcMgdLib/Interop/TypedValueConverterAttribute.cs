/// TypedValueConverterAttribute.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Reflection.Extensions;

namespace Autodesk.AutoCAD.Runtime.LispInterop
{
   [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
   public class TypedValueConverterAttribute : System.Attribute
   {
      static TypeAttributeHandler<TypedValueConverterAttribute> handler;

      public TypedValueConverterAttribute(Type targetType)
      {
         this.TargetType = targetType;         
      }

      public Type TargetType { get; private set; }

      /// <summary>
      /// This method must be called to initialize this type,
      /// and cause it to search for and collect all attributes. 
      /// </summary>

      public static void Initialize()
      {
         handler = new TypeAttributeHandler<TypedValueConverterAttribute>(OnAttributeFound);
      }

      static void OnAttributeFound(TypedValueConverterAttribute att, Type converterType)
      {
         TypedValueConverter.Add(att.TargetType, converterType);
      }

   }


}




