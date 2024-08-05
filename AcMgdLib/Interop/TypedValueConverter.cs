/// TypedValueConverter.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;

namespace Autodesk.AutoCAD.Runtime.LispInterop
{

   /// <summary>
   /// Provides a non-invasive way for consumers of the 
   /// ListBuilder class to perform conversion of any type
   /// of data to a LISP-consumable form.
   /// </summary>

   public abstract class TypedValueConverter
   {
      // Key is the Target type that is converted.
      // value is the TypedValueConverter-based type

      static Dictionary<Type, Type> converterTypes = new Dictionary<Type, Type>();
      static Dictionary<Type, TypedValueConverter> converters = new Dictionary<Type, TypedValueConverter>();

      protected TypedValueConverter()
      {
      }

      public static TypedValueConverter GetConverter(Type targetType)
      {
         TypedValueConverter result;
         if(!converters.TryGetValue(targetType, out result))
         {
            if(converterTypes.TryGetValue(targetType, out Type type))
            {
               result = (TypedValueConverter)System.Activator.CreateInstance(type);
               if(result != null)
                  converters.Add(targetType, result);
            }
         }
         return result;
      }

      public static void Add<TTarget, TConverter>() where TConverter : TypedValueConverter
      {
         Add(typeof(TTarget), typeof(TConverter));
      }

      public static void Add(Type targetType, Type converterType)
      {
         if(!typeof(TypedValueConverter).IsAssignableFrom(converterType))
            throw new ArgumentException($"Type not derived from {nameof(TypedValueConverter)}");
         converterTypes.TryAdd(targetType, converterType);
      }

      public abstract bool CanConvert(bool toTypedValues);
      public abstract object ToTypedValues(object value, Context context = Context.Lisp);
      public abstract object FromTypedValues(object value, Context context = Context.Lisp);

      public enum Context
      {
         Lisp = 0,
         Dxf = 1,
         Other = 2
      }

   }


}




