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
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
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

      public static T GetObjectChecked<T>(this Transaction trans,
            ObjectId id,
            OpenMode mode = OpenMode.ForRead,
            bool openErased = false,
            bool openOnLockedLayer = false) where T : DBObject
      {
         AcRx.ErrorStatus.WrongObjectType.Requires<T>(id);
         return Unsafe.As<T>(trans.GetObject(id, mode, openErased, openOnLockedLayer));
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
            obj = Unsafe.As<T>(trans.GetObject(id, mode, false, openOnLockedLayer));
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
         AcRx.ErrorStatus.WrongObjectType.Requires<T>(id);
         using(var obj = id.Open(OpenMode.ForRead, true, false))
         {
            return func(Unsafe.As<T>(obj));
         }
      }

      public static TValue GetValue<TValue>(this ObjectId id, Func<DBObject, TValue> func)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         using(var obj = id.Open(OpenMode.ForRead, true, false))
         {
            return func(obj);
         }
      }

      /// <summary>
      /// An overload of GetOwnerValue() that implicitly uses 
      /// DBObject as the type of the owner object.
      /// 
      /// If the DBObject with the given Id has a no owner, 
      /// and exception is thrown.
      /// <typeparam name="TValue">The type of the result</typeparam>
      /// <param name="id">The ObjectId of the object whose owner
      /// object is to be operated on</param>
      /// <param name="func">A function that takes the owner object
      /// as its argument and returns the requested value.</param>
      /// <returns>The result of applying the given function to
      /// the owner of the DBObject with the given ObjectId</returns>

      public static TValue GetOwnerValue<TValue>(this ObjectId id, Func<DBObject, TValue> func) 
      {
         var ownerId = id.GetValue<ObjectId>(obj => obj.OwnerId);
         AcRx.ErrorStatus.InvalidOwnerObject.ThrowIf(ownerId.IsNull);
         return ownerId.GetValue<TValue>(func);
      }

      /// <summary>
      /// Opens the DBObject having the specifed ObjectId, 
      /// and its owner object, and applys the given function
      /// to the owner object and returns the result.
      /// 
      /// The owner object must be an instance of the generic
      /// argument type T. See the overload below for a version
      /// that implicitly uses DBObject as the owner type.
      /// </summary>
      /// <typeparam name="T">The type of the owner object</typeparam>
      /// <typeparam name="TValue">The type of the result</typeparam>
      /// <param name="id">The ObjectId of the object whose owner
      /// is to be operated on</param>
      /// <param name="func">A function that takes the owner object
      /// as its argument and returns the requested value.</param>
      /// <returns>The result of applying the given function to
      /// the owner of the DBObject with the given ObjectId</returns>

      public static TValue GetOwnerValue<T, TValue>(this ObjectId id, Func<T, TValue> func) where T : DBObject
      {
         ObjectId oid = id.GetValue<ObjectId>(obj => obj.OwnerId);
         AcRx.ErrorStatus.InvalidOwnerObject.ThrowIf(oid.IsNull);
         AcRx.ErrorStatus.WrongObjectType.Requires<T>(oid);
         return oid.GetValue<T, TValue>(func);
      }

      /// <summary>
      /// Returns the BlockId of an Entity, or the OwnerId
      /// of a non-entity.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>

      public static ObjectId GetBlockOrOwnerId(this DBObject obj)
      {
         Assert.IsNotNull(obj, nameof(obj));
         return obj is Entity ent ? ent.BlockId : obj.OwnerId;
      }


      public static T Open<T>(this ObjectId id, OpenMode mode = OpenMode.ForRead) where T : DBObject
      {
         AcRx.ErrorStatus.WrongObjectType.Requires<T>(id);
         return Unsafe.As<T>(id.Open(mode, true, true));
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

      public static ObjectId TryGetOwnerId(this ObjectIdCollection ids)
      {
         Assert.IsNotNullOrDisposed(ids, nameof(ids));
         if(ids.Count == 0)
            return ObjectId.Null;
         return ids[0].GetOwnerId();
      }

      public static ObjectId TryGetOwnerId(this IEnumerable<ObjectId> ids)
      {
         Assert.IsNotNull(ids, nameof(ids));
         if(!ids.Any())
            return ObjectId.Null;
         return ids.First().GetOwnerId();
      }

      public static ObjectId GetOwnerId(this ObjectId id)
      {
         return id.GetValue<ObjectId>(obj => obj.OwnerId);
      }

      public static IEnumerable<ObjectId> GetPrimaryCloneIds(this IdMapping map)
      {
         Assert.IsNotNull(map, nameof(map));
         return map.Cast<IdPair>().Where(p => p.IsPrimary && p.IsCloned).Select(p => p.Value);
      }

      public static IEnumerable<ObjectId> GetPrimarySourceIds(this IdMapping map)
      {
         Assert.IsNotNull(map, nameof(map));
         return map.Cast<IdPair>().Where(p => p.IsPrimary).Select(p => p.Key);
      }

      public static IEnumerable<ObjectId> GetPrimarySourceIds<T>(this IdMapping map)
         where T: DBObject
      {
         Assert.IsNotNull(map, nameof(map));
         return map.Cast<IdPair>()
            .Where(p => p.IsPrimary)
            .Select(p => p.Key)
            .Where(RXClass<T>.GetIdPredicate(false));
      }

      public static IEnumerable<ObjectId> GetPrimaryCloneIds<T>(this IdMapping map)
         where T : DBObject
      {
         Assert.IsNotNull(map, nameof(map));
         return map.Cast<IdPair>()
            .Where(p => p.IsPrimary && p.IsCloned)
            .Select(p => p.Value)
            .Where(RXClass<T>.GetIdPredicate(false));
      }


      public static IEnumerable<ObjectId> Translate(this IEnumerable<ObjectId> ids, IdMapping map)
      {
         ObjectId[] array = ids as ObjectId[];
         if(array != null)
            return Array.ConvertAll(array, id => map[id].Value);
         else
            return ids.Select(id => map[id].Value);
      }

      public static IEnumerable<ObjectId> Translate(this IdMapping map, IEnumerable<ObjectId> ids)
      {
         return ids.Translate(map);
      }

      static readonly RXClass entityClass = RXObject.GetClass(typeof(Entity));

      public static bool IsAllEntities(this ObjectIdCollection ids)
      {
         Assert.IsNotNullOrDisposed(ids, nameof(ids));
         foreach(ObjectId id in ids)
         {
            if(!id.ObjectClass.IsDerivedFrom(entityClass))
               return false;
         }
         return true;
      }

      public static bool IsAllEntities(this IEnumerable<ObjectId> ids)
      {
         Assert.IsNotNull(ids, nameof(ids));
         return ids.All(id => id.ObjectClass.IsDerivedFrom(entityClass));
      }

      public static string ToHexString(this ObjectId id)
      {
         return string.Format("0x{0:X}", id.Handle.Value);
      }

      public static IEnumerable<ObjectId> OfType<T>(this IEnumerable<ObjectId> ids, bool exactMatch = false)
         where T: DBObject
      {
         Assert.IsNotNull(ids);
         return ids.Where(RXClass<T>.GetIdPredicate(exactMatch));
      }

      /// <summary>
      /// Tentative - pending integration of RXClass 
      /// extensions from CacheUtils.cs
      /// </summary>
      /// <param name="ids"></param>
      /// <returns></returns>

      public static RXClass GetRXClass(this IEnumerable<ObjectId> ids)
      {
         return ids.Select(id => id.ObjectClass)
            .Aggregate((left, right) => left.IntersectWith(right));
      }

      static readonly RXClass objectClass = RXObject.GetClass(typeof(RXObject));

      static int GetDepth(this RXClass rxclass)
      {
         int i = 0;
         while(rxclass != objectClass)
         {
            ++i;
            rxclass = rxclass.MyParent;
         }
         return i;
      }

      static RXClass IntersectWith<TLeft, TRight, TDefault>()
         where TDefault : RXObject where TLeft: TDefault where TRight : TDefault
      {
         return IntersectWith(RXClass<TLeft>.Value, 
            RXClass<TRight>.Value,
            RXClass<TDefault>.Value);
      }

      static void Swap<T>(ref T left, ref T right)
      {
         T temp = left;
         left = right;
         right = temp;
      }

      static RXClass IntersectWith(this RXClass left, RXClass right, RXClass defaultClass = null)
      {
         if(left.IsDerivedFrom(right))
            return right;
         if(right.IsDerivedFrom(left))
            return left;
         int lDepth = left.GetDepth();
         int rDepth = right.GetDepth();
         if(lDepth > rDepth)
            Swap(ref left, ref right);
         defaultClass = defaultClass ?? RXObject.GetClass(typeof(RXObject));
         for(RXClass next = left.MyParent; next != defaultClass; next = next.MyParent)
         {
            if(right.IsDerivedFrom(next))
               return next;
         }
         return defaultClass;
      }

   }

}



