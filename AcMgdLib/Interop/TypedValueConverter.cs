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

      /// <summary>
      /// If the argument is true, the method should return 
      /// a value indicating if a conversion to one or more
      /// TypedValues is supported. 
      /// 
      /// If the argument is false, the method should return
      /// a value indicating if a conversion from one or more
      /// TypedValues is supported.
      /// 
      /// ToTypedValues() is only called if this method
      /// returns true when the argument is true.
      /// </summary>
      /// <param name="toTypedValues"></param>
      /// <returns></returns>

      public abstract bool CanConvert(bool toTypedValues);

      /// <summary>
      /// Converts an instance of the target type to one or
      /// more TypedValues. Multiple TypedValues should be
      /// returned as an IEnumerable<TypedValue>, which can
      /// be an array of TypedValue[].
      /// </summary>
      /// <param name="value">The value to be converted</param>
      /// <param name="context">A value indicating the context
      /// in which the conversion is being performed.</param>
      /// <returns>A TypedValue or an IEnumerable<TypedValue>
      /// representing the converted value</returns>

      public abstract object ToTypedValues(object value, Context context = Context.Lisp);

      /// <summary>
      /// Converts one or more TypedValues to an instance of
      /// the target type.
      /// </summary>
      /// <param name="value">An instance of the target type
      /// to be converted</param>
      /// <param name="context">A value indicating the context
      /// in which the conversion is being performed.</param>
      /// <returns>An instance of the target type converted
      /// from the arguments.</returns>
      
      public abstract object FromTypedValues(object value, Context context = Context.Lisp);

      /// <summary>
      /// Indicates the context in which a conversion is performed.
      /// 
      /// This value can be used for instance, to alter the delimiter
      /// used to represent nested lists, which would differ if the
      /// result is to be stored in an XRecord, verses being returned
      /// back to LISP. 
      /// 
      /// For example, when storing complex data in an Xrecord, nested 
      /// lists must be represented using DxfCode.ControlString with a 
      /// value of "{" or "}". When passing data back to LISP, nested 
      /// lists must be delimited using the LispDataType.ListBegin and 
      /// LispDataType.ListEnd list delimiters.
      /// 
      /// </summary>
      public enum Context
      {
         Lisp = 0,
         Dxf = 1,
         Other = 2
      }

   }


}




