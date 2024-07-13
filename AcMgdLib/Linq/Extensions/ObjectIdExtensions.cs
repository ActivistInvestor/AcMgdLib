/// ObjectIdExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for CollectionExtensions and
/// DatabaseExtensions classes.
/// 
/// Note: Recent refactorings may require C# 7.0.

using System;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static partial class ObjectIdExtensions
   { 

      /// <summary>
      /// An extension of ObjectId that opens the ObjectId and
      /// casts it to the specified argument type (no checking
      /// is done to verify that the ObjectId is compatible).
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="trans"></param>
      /// <param name="id"></param>
      /// <param name="mode"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static T GetObject<T>(this ObjectId id,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead,
            bool openErased = false,
            bool openOnLockedLayer = false) where T : DBObject
      {
         return (T)trans.GetObject(id, mode, openErased, openOnLockedLayer);
      }

      /// <summary>
      /// A version of the above that targets Transactions:
      /// </summary>

      public static T GetObject<T>(this Transaction trans,
            ObjectId id,
            OpenMode mode = OpenMode.ForRead,
            bool openErased = false,
            bool openOnLockedLayer = false) where T : DBObject
      {
         return (T)trans.GetObject(id, mode, openErased, openOnLockedLayer);
      }

      public static bool TryGetObject<T>(this ObjectId id,
         Transaction trans,
         out T obj,
         OpenMode mode = OpenMode.ForRead,
         bool exact = false,
         bool openOnLockedLayer = false) where T : DBObject
      {
         if(trans == null)
            throw new ArgumentNullException(nameof(trans));
         if(id.IsNull)
            throw new ArgumentNullException(nameof(id));
         bool result = RXClass<T>.IsAssignableFrom(id.ObjectClass, exact);
         if(result)
            obj = (T)trans.GetObject(id, mode, false, openOnLockedLayer);
         else
            obj = null;
         return result;
      }

      /// <summary>
      /// Opens a DBObject of the specified generic argument
      /// type, and returns the result of invoking the given
      /// function on the open DBObject.
      /// </summary>
      /// <typeparam name="T">The type of the DBObject and the
      /// type of the function argument.</typeparam>
      /// <typeparam name="TValue">The type of the result of
      /// this method, and the given function.</typeparam>
      /// <param name="id">The ObjectId to be opened.</param>
      /// <param name="func">A function that takes an instance
      /// of the generic argument, and returns a value of the
      /// type TValue</param>
      /// <returns>The result of invoking the function on the
      /// opened DBObject.</returns>
      /// <exception cref="ArgumentException"></exception>

      public static TValue GetValue<T, TValue>(this ObjectId id, Func<T, TValue> func)
         where T : DBObject
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         using(var obj = id.Open(OpenMode.ForRead, false, false))
         {
            return func((T)obj);
         }
      }

      /// <summary>
      /// Synonym for GetValue(), included 
      /// for backward-compatiblity reasons.
      /// </summary>

      public static TValue Invoke<T, TValue>(this ObjectId id, Func<T, TValue> func)
         where T : DBObject
      {
         return GetValue<T, TValue>(id, func);
      }
   }

}



