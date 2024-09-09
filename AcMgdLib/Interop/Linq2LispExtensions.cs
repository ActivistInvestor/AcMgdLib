/// Linq2LispExtensions.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.LispInterop;

namespace Linq2Lisp
{
   /// <summary>
   /// Excerpts from the Linq2Lisp library, circa 2012.
   /// </summary>
   
   public static class Linq2LispExtensions
   {
      /// <summary>
      /// This method is similar to the Linq ToDictionary() method,
      /// except that it returns a ResultBuffer that when returned
      /// to LISP, produces an association list of keys/values.
      /// 
      /// Unlike the ListBuilder's Cons<T>() method, data is not
      /// automatically marshaled to LISP types, and the caller 
      /// must explicitly specify the LispDataType to be used for
      /// the keys and values, and provide two delegates that will
      /// extract the key and the value from each input element.
      /// 
      /// The input sequence can be empty. In that case, the result
      /// returned to LISP is nil.
      /// 
      /// This method has no dependence on AcMgdLib and can be
      /// copied and pasted into any project for use.
      /// </summary>
      /// <typeparam name="T">The type of the input sequence</typeparam>
      /// <param name="src">The input sequence</param>
      /// <param name="keyType">The LispDataType to use for the keys</param>
      /// <param name="valueType">The LispDataType to use for the values</param>
      /// <param name="keySelector">A function that takes an input element 
      /// and returns the Key (e.g., the 'car').</param>
      /// <param name="valueSelector">A function that takes an input element 
      /// and returns the Value (e.g., the 'cdr').</param>
      /// <returns>A ResultBuffer that when returned back to LISP
      /// produces an association list of keys/values.</returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static ResultBuffer ToAssocList<T>(
         this IEnumerable<T> src,
         LispDataType keyType,
         LispDataType valueType,
         Func<T, object> keySelector,
         Func<T, object> valueSelector)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         if(keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));
         if(valueSelector == null)
            throw new ArgumentNullException(nameof(valueSelector));
         short nKey = (short)keyType;
         short nVal = (short)valueType;
         var rb = new ResultBuffer();
         rb.Add(tvListBegin);
         foreach(T item in src)
         {
            rb.Add(tvListBegin);
            rb.Add(keySelector(item).ToTypedValue(keyType));
            rb.Add(valueSelector(item).ToTypedValue(valueType));
            rb.Add(tvDotEnd);
         }
         rb.Add(tvListEnd);
         return rb;
      }

      static readonly TypedValue tvListBegin = 
         new TypedValue((short)LispDataType.ListBegin);
      static readonly TypedValue tvDotEnd = 
         new TypedValue((short)LispDataType.DottedPair);
      static readonly TypedValue tvListEnd = 
         new TypedValue((short)LispDataType.ListEnd);

      public static TypedValue ToTypedValue(this object value, LispDataType type = LispDataType.None)
      {
         if(type == LispDataType.None)
         {
            if(value == null)
               type = LispDataType.Nil;
            else
               type = value.GetType().ToLispDataType(true);
         }
         return new TypedValue((short) type, value);
      }

      public static TypedValue ToTypedValue(this object value)
      {
         if(value == null)
            return new TypedValue((short)LispDataType.Nil);
         LispDataType type = value.GetType().ToLispDataType(true);
         return new TypedValue((short)type, value);
      }

      public static LispDataType ToLispDataType(this Type type, bool throwIfNotFound = false)
      {
         if(typeof(SelectionSet).IsAssignableFrom(type))
            return LispDataType.SelectionSet;
         if(typeToLispDataTypeMap.TryGetValue(type, out LispDataType result))
            return result;
         if(throwIfNotFound)
            throw new ArgumentException($"Unsupported type {type.CSharpName()}");
         return LispDataType.None;
      }

      static readonly Dictionary<Type, LispDataType> typeToLispDataTypeMap = new Dictionary<Type, LispDataType>()
      {
         { typeof(double), LispDataType.Double },
         { typeof(float), LispDataType.Double },
         { typeof(Point2d), LispDataType.Point2d },
         { typeof(short), LispDataType.Int16 },
         { typeof(sbyte), LispDataType.Int16 },
         { typeof(byte), LispDataType.Int16 },
         { typeof(char), LispDataType.Int16 },
         { typeof(string), LispDataType.Text },
         { typeof(ObjectId), LispDataType.ObjectId },
         { typeof(SelectionSet), LispDataType.SelectionSet }, // requires is test (abstract)
         { typeof(Point3d), LispDataType.Point3d },
         { typeof(int), LispDataType.Int32 },
      };

   }

   public static class ToAssocListExample
   {
      [LispFunction("TestToAssocList")]
      public static ResultBuffer TestToAssocList(ResultBuffer args)
      {
         /// Create a dictionary having integers as keys and
         /// strings as values, and add some items to it:

         Dictionary<short, string> map = new Dictionary<short, string>()
         {  {0, "Zero" },
            {1, "One" },
            {2, "Two" }
         };

         /// Return the dictionary to LISP as an
         /// association list:

         return map.ToAssocList(
            LispDataType.Int16,
            LispDataType.Text,
            p => p.Key,
            p => p.Value);
      }

   }

}




