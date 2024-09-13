/// RuntimeExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Internal;            // Utils.WcMatchEx()
using System.Diagnostics.Extensions;
using AcRx = Autodesk.AutoCAD.Runtime;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Autodesk.AutoCAD.Runtime
{
   /// Assorted helper and utility classes associated 
   /// with AutoCAD runtime classes.
   /// 
   /// <em>Note: This code requires C# 7 or later</em>
   ///
   /// For AutoCAD 2024 or earlier, you can easily modify
   /// a project's .csproj file to support C# 12:
   /// 
   ///   <PropertyGroup>
   ///     <TargetFramework>net4.8</TargetFramework>
	///     <LangVersion>12</LangVersion>
   ///   </PropertyGroup>
   /// 
   ///
   ///

   /// <summary>
   /// Pulled from original RuntimeExtensions.cs - only includes
   /// functionatlity required by the associated consuming code 
   /// in this distribution.
   /// </summary>

   public static partial class RuntimeExtensions
   {

      /// <summary>
      /// Invokable on an ErrorStatus, for example:
      /// 
      ///   ObjectId someId = ....
      ///   
      ///   ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
      ///   
      /// </summary>
      /// <param name="es"></param>
      /// <param name="condition"></param>
      /// <param name="msg"></param>
      /// <exception cref="AcRx.Exception"></exception>

      public static void ThrowIf(this AcRx.ErrorStatus es, bool condition, string msg = "")
      {
         if(condition)
            throw new AcRx.Exception(es, msg).Log(es, condition, msg);
      }

      public static void ThrowIfNot(this AcRx.ErrorStatus es, bool condition, string msg = "")
      {
         if(!condition)
            throw new AcRx.Exception(es, msg).Log(es, condition, msg);
      }

      public static void Throw(this AcRx.ErrorStatus es, string msg = "")
      {
         throw new AcRx.Exception(es, msg);
      }

      /// <summary>
      /// Throws an exception if the given ObjectId does not
      /// represent an instance of the generic argument or a
      /// type derived from it.
      /// 
      /// E.g.:
      /// 
      ///    ErrorStatus.WrongObjectType.Requires<Curve>(id);
      ///    
      /// throws an exception if the ObjectId does not represent
      /// an object that is derived from Curve. This method can
      /// also be used with concrete types:
      ///    
      ///    ErrorStatus.WrongObjectType.Requires<Polyline>(id);
      ///    
      /// The above throws an exception if the argument does not
      /// represent a Polyline.
      ///    
      /// If the given ObjectId is null, the ErrorStatus will be
      /// ErrorStatus.NullObjectId, rather than the ErrorStatus
      /// which the method is invoked through.
      /// 
      /// This method can be used to both validate an ObjectId as 
      /// not being null, and as representing an instance of the
      /// specified managed wrapper type.
      /// </summary>
      /// <typeparam name="T">The managed wrapper type which the
      /// ObjectId must represent an instance of</typeparam>
      /// <param name="es">The ErrorStatus to pass to the Exception</param>
      /// <param name="id">The ObjectId to validate</param>
      /// <param name="exact">A value indicating if the ObjectId can
      /// represent an instance of a derived type or not.</param>
      /// <param name="msg">An optional message to display</param>

      public static ObjectId Requires<T>(this AcRx.ErrorStatus es, ObjectId id, bool exact = false, string msg = "")
         where T:RXObject
      {
         if(id.IsNull)
            throw new AcRx.Exception(ErrorStatus.NullObjectId).Log(es, id);
         if(!RXClass<T>.IsAssignableFrom(id, exact))
            throw new AcRx.Exception(es, !string.IsNullOrWhiteSpace(msg) ? msg 
               : $"({typeof(T).Name} required)")
                  .Log(es, id, exact, msg);
         return id;
      }

      public static void ThrowIfNot<T>(this AcRx.ErrorStatus es, ObjectId id, bool exact = false, string msg = "") 
         where T:RXObject
      {
         if(!RXClass<T>.IsAssignableFrom(id, exact))
            throw new AcRx.Exception(id.IsNull ? AcRx.ErrorStatus.NullObjectId : es, msg).Log(es, id, exact, msg);
      }

      /// <summary>
      /// Provided mainly for compatibility with older code
      /// </summary>

      public static void Check(this AcRx.ErrorStatus es, bool condition, string msg = "")
      {
         if(!condition)
            throw new AcRx.Exception(es, msg).Log(es, condition, msg);
      }

      /// <summary>
      /// Extension method of the string class that uses
      /// Autodesk.AutoCAD.Internal.Utils.WcmathEx().
      /// </summary>
      /// <param name="str"></param>
      /// <param name="pattern"></param>
      /// <param name="ignoreCase"></param>
      /// <returns></returns>

      /// Overloads are used instead of optional arguments, because
      /// optional arguments cannot be omitted in Linq expressions:
      
      public static bool Matches(this string str, string pattern)
         => Utils.WcMatchEx(str, pattern, true);

      public static bool Matches(this string str, string pattern, bool ignoreCase)
         => Utils.WcMatchEx(str, pattern, ignoreCase);

      /// <summary>
      /// System.String Extension to simplify 
      /// case-insenstive string comparisons.
      /// </summary>

      public static bool IsEqualTo(this string str, string other, bool ignoreCase = true)
      {
         return string.Equals(str, other, 
            ignoreCase ? StringComparison.InvariantCultureIgnoreCase 
               : StringComparison.InvariantCulture);
      }

      /// <summary>
      /// Fuzzy comparison for System.Double that uses a caller
      /// specified tolerance (default = 1.0e-6)
      /// </summary>

      public static bool IsEqualTo(this double a, double b, double tolerance = 1.0e-6)
      {
         return Math.Abs(a - b) < tolerance;
      }

      /// <summary>
      /// Tests if the argument/invocation target is a runtime class
      /// that is equal to or derived from the runtime class associated 
      /// with the generic argument type, depending on the exactMatch
      /// argument.
      /// </summary>
      /// <typeparam name="T">The Managed wrapper type</typeparam>
      /// <param name="rxclass">The runtime class to test</param>
      /// <param name="exactMatch">A value indicating if the 
      /// comparision is for equality or ancestry.</param>
      /// <returns>A value indicating if the runtime class which
      /// the method is invoked on is an instance of or equal to 
      /// the runtime class associated with the generic argument</returns>

      public static bool IsTypeOf<T>(this RXClass rxclass, bool exactMatch = false) where T : RXObject
      {
         return RXClass<T>.IsAssignableFrom(rxclass, exactMatch);
      }

      /// <summary>
      /// Tests if the ObjectClass property of the argument/invocation 
      /// target is a runtime class equal to or derived from the runtime 
      /// class associated with the generic argument type, depending on
      /// the exactMatch argument. 
      /// 
      /// The test performed by this method does not entail a call to 
      /// RXObject.GetClass(), allowing it to avoid the overhead of 
      /// repeatedly getting an instance of an attribute from the type.
      /// 
      /// Example:
      /// 
      ///    ObjectId id = .....
      ///    
      ///    if(id.IsA<Circle>())
      ///       Console.WriteLine("This id references a Circle entity");
      ///    else
      ///       Console.WriteLine("This id does not reference a Circle entity");
      ///       
      /// </summary>
      /// <typeparam name="T">The Managed wrapper type</typeparam>
      /// <param name="rxclass">The runtime class to test</param>
      /// <param name="exactMatch">A value indicating if the 
      /// comparision is for equality or ancestry.</param>
      /// <returns>A value indicating if the runtime class which
      /// the method is invoked on is an instance of or equal to 
      /// the runtime class associated with the generic argument</returns>

      /// Provided for backward-compatibility, use IsAssignableTo()
      /// in newer code.

      public static bool IsA<T>(this ObjectId id, bool exactMatch) where T : RXObject
      {
         return id.IsNull ? false
            : !exactMatch ? id.ObjectClass.IsDerivedFrom(RXClass<T>.Value)
               : id.ObjectClass == RXClass<T>.Value;
      }

      public static bool IsA<T>(this ObjectId id) where T : RXObject
      {
         return !id.IsNull && id.ObjectClass.IsDerivedFrom(RXClass<T>.Value);
      }

      public static bool IsAssignableTo<T>(this ObjectId id, bool exactMatch = false) where T : RXObject
      {
         return id.IsNull ? false
            : !exactMatch ? id.ObjectClass.IsDerivedFrom(RXClass<T>.Value)
               : id.ObjectClass == RXClass<T>.Value;
      }

      public static bool IsAssignableTo<T>(this ObjectId id) where T : RXObject
      {
         return !id.IsNull && id.ObjectClass.IsDerivedFrom(RXClass<T>.Value);
      }

      public static RXClass GetRXClass(this Type type)
      {
         Assert.IsNotNull(type);
         if(!type.IsDerivedFrom<RXObject>())
            throw new ArgumentException("Type must be RXObject or a derived type");
         return RXClass.GetClass(type);
      }

      public static bool IsDerivedFrom<T>(this Type type)
      {
         Assert.IsNotNull(type);
         return type.IsAssignableFrom(typeof(T));
      }

      /// <summary>
      /// Replaces one DisposableWrapper with another.
      /// 
      /// This API must be used with extreme care.
      /// 
      /// The replacement wrapper must be the same type
      /// as the original wrapper, or a derived type.
      /// 
      /// After replacement, the <paramref name="replacement"/> argument
      /// becomes the managed wrapper for the <paramref name="original"/>'s
      /// UnmanagedObject, and all interaction with the native object must
      /// be through the replacment. The <paramref name="original"/> argument
      /// is no-longer usable or valid after this method returns.
      /// </summary>
      /// <param name="original">The DisposableWrapper that is to be replaced</param>
      /// <param name="replacement">The DisposableWrapper that is to replace the 
      /// <paramref name="original"/> argument</param>
      /// <exception cref="InvalidOperationException"></exception>

      public static void ReplaceWith<T>(this T original, T replacement) where T: DisposableWrapper
      {
         Assert.IsNotNullOrDisposed(original, nameof(original));
         Assert.IsNotNullOrDisposed(replacement, nameof(replacement));
         if(replacement.UnmanagedObject.ToInt64() > 0)
            throw new InvalidOperationException("Invalid replacmement");
         bool autoDelete = original.AutoDelete;
         IntPtr ptr = original.UnmanagedObject;
         if(ptr.ToInt64() < 1)
            throw new InvalidOperationException("Invalid original wrapper");
         Interop.DetachUnmanagedObject(original);
         Interop.SetAutoDelete(original, false);
         GC.SuppressFinalize(original);
         Interop.DetachUnmanagedObject(replacement);
         Interop.AttachUnmanagedObject(replacement, ptr, autoDelete);
      }

      public static void TryDispose(this DisposableWrapper wrapper)
      {
         if(wrapper != null && !wrapper.IsDisposed)
            wrapper.Dispose();
      }

      public static IEnumerable<RXClass> GetDescendents(this RXClass parent)
      {
         foreach(DictionaryEntry entry in SystemObjects.ClassDictionary)
         {
            RXClass rxclass = entry.Value as RXClass;
            if(rxclass != null && rxclass.IsDerivedFrom(parent))
               yield return rxclass;
         }
      }

      public static string GetSelectionFilterString<T>() where T:Entity
      {
         return GetSelectionFilterString(typeof(T));
      }

      public static string GetSelectionFilterString(this Type type)
      {
         if(!typeof(Entity).IsAssignableFrom(type))
            throw new ArgumentException("Invalid type");
         RXClass rxclass = RXObject.GetClass(type);
         List<string> list = new List<string>();
         if(!type.IsAbstract && ! string.IsNullOrEmpty(rxclass.DxfName))
            list.Add(rxclass.DxfName);
         foreach(RXClass child in rxclass.GetDescendents())
         {
            string dxfname = child.DxfName;
            if(!string.IsNullOrEmpty(dxfname))
               list.Add(dxfname);
         }
         return string.Join(",", list);
      }

   }

   /// <summary>
   /// Allows the delegates returned by methods that use an 
   /// RXClass to avoid having to capture a local variable,
   /// and eliminates the overhead of repeated calls to the
   /// RXObject.GetClass() method with the same argument.
   /// 
   /// When a runtime class is needed for a type that is
   /// expressed as a generic parameter, instead of using
   /// <code>RXObject.GetClass(typeof(T))</code> to get the 
   /// associated runtime class, one can instead use 
   /// <code>RXClass<T>.Value</code> which doesn't require 
   /// a call to GetClass() and more importantly, when used 
   /// within lambda functions, allows the lambda function to 
   /// avoid a variable capture of the equivalent value stored 
   /// as a local variable of a containing method, an instance 
   /// member, or a parameter of a containing method.
   /// </summary>

   public static partial class RXClass<T> where T : RXObject
   {
      static readonly bool isAbstract = typeof(T).IsAbstract;

      public static readonly RXClass Value = RXObject.GetClass(typeof(T));

      /// <summary>
      /// Returns the Name property if the DxfName property 
      /// is a null/empty string:
      /// </summary>

      public static string DxfNameOrName
      {
         get
         {
            string dxfName = Value.DxfName;
            return !string.IsNullOrEmpty(dxfName) ? dxfName : Value.Name;
         }
      }
      
      public static string DxfName => Value.DxfName;
      public static string Name => Value.Name;
      public static RXClass Parent => Value.MyParent;

      /// <summary>
      /// Returns a value indicating if an instance of an
      /// RXObject associated with the given RXClass argument
      /// can be assigned to a variable of the generic argument 
      /// type.
      /// </summary>
      /// <param name="rxclass">The runtime class to test
      /// against the generic argument of this type</param>
      /// <param name="exactMatch">A value indicating if the
      /// type of the DBObject represented by an ObjectId must 
      /// be equal to the type of the generic argument (true), 
      /// or can be any type that is derived from same (false).
      /// 
      /// If the generic argument type is an abstract type, this 
      /// argument is ignored and is effectively-false</param>      
      /// <returns>a value indicating if the RXClass matches the criteria</returns>

      public static bool IsAssignableFrom(RXClass rxclass, bool exactMatch = false)
      {
         return rxclass == null ? false 
            : exactMatch && !isAbstract ? 
                rxclass == Value : rxclass.IsDerivedFrom(Value);
      }

      /// <summary>
      /// Returns a value indicating if the ObjectClass property of
      /// the argument is derived from the runtime class associated 
      /// with the generic argument type.
      /// </summary>
      /// <param name="id">The ObjectId whose runtime class is to be 
      /// compared to the runtime class of the generic argument</param>
      /// <param name="exactMatch">A value indicating if the
      /// type of the DBObject represented by an ObjectId must 
      /// be equal to the type of the generic argument (true), 
      /// or be any type that is derived from same (false).
      /// 
      /// If the generic argument type is an abstract type, this 
      /// argument is ignored and is effectively-false</param>      
      /// <returns></returns>

      public static bool IsAssignableFrom(ObjectId id, bool exactMatch = false)
      {
         return id.IsNull ? false
            : exactMatch && ! isAbstract ? id.ObjectClass == Value
               : id.ObjectClass.IsDerivedFrom(Value);
      }

      /// <summary>
      /// Returns a predicate function that takes an ObjectId
      /// as an argument and returns a value indicating if the
      /// ObjectId argument represents a DBObject whose managed
      /// type equals the generic argument type or, is a type 
      /// derived from the generic argument type, depending on 
      /// the <paramref name="exactMatch"/> argument.
      /// 
      /// The returned predicate uses a generic type that stores
      /// the runtime class associated with the generic argument
      /// type in a static field, allowing the delegate to avoid 
      /// a relatively-expensive local variable capture.
      /// 
      /// In the delegates returned by this method, the 
      /// expression:
      /// 
      ///   <code>  RXClass<T>.Value  </code>
      ///   
      /// is merely a reference to a <em>static field of a static 
      /// type</em>, which has significantly-less overhead compared
      /// to that of a captured local variable.
      /// </summary>
      /// <typeparam name="T">The type of DBObject to match</typeparam>
      /// <param name="exactMatch">A value indicating if the
      /// type of the DBObject represented by an ObjectId must 
      /// be equal to the type of the generic argument (true), 
      /// or be any type that is derived from same (false). The
      /// default is false (e.g., matches any derived type).
      /// 
      /// If the generic argument type is abstract, this argument 
      /// is ignored and is effectively-false</param>
      /// <param name="includingErased">A value indicating if the
      /// returned predicate should match the Ids of erased objects</param>
      /// <returns>A predicate that when passed an ObjectId returns
      /// a value indicating if the ObjectId matches the criteria</returns>

      public static Func<ObjectId, bool> GetIdPredicate(
         bool exactMatch = false, bool includingErased = true)
      {
         exactMatch &= !typeof(T).IsAbstract;
         if(includingErased)
            return exactMatch ? (Func<ObjectId, bool>) IsMatchExact : IsMatch;
         else
            return exactMatch ? (Func<ObjectId, bool>) NonErasedIsMatchExact : NonErasedIsMatch;
      }

      public static Func<RXClass, bool> GetClassPredicate(bool exactMatch = false)
      {
         return exactMatch && !typeof(T).IsAbstract ? (Func<RXClass, bool>) IsMatchExact : IsMatch;
      }

      /// <remarks>
      /// Delegates returned by the above methods. Returning these
      /// instead of embedding them into the above methods avoids 
      /// allocating a new delegate on each call to those methods.
      /// 
      /// Note: Delegates have been depreciated, and have been 
      /// replaced with static methods of this type.
      /// 
      /// There are two versions of each method. One matches 
      /// exactly and the other matches any derived type. There
      /// are overloads that test the ObjectClass of an ObjectId,
      /// and overloads that test an RXClass. There are versions
      /// that check the IsErased property of ObjectIds.
      /// 
      /// Note: No check is performed on the ObjectId argument
      /// to ensure it is not null or has a null ObjectClass:
      /// </remarks>

      /// <summary>
      /// The Id's ObjectClass is the RXClass associated
      /// with the generic argument type, or is derived
      /// from same.
      /// </summary>
      
      public static bool IsMatch(ObjectId id)
      {
         return id.ObjectClass.IsDerivedFrom(Value);
      }

      /// <summary>
      /// The Id's ObjectClass is the RXClass associated
      /// with the generic argument type. Derived types
      /// do not match.
      /// </summary>

      public static bool IsMatchExact(ObjectId id)
      {
         return id.ObjectClass == Value;
      }

      /// <summary>
      /// The ObjectId is not the id of an erased object,
      /// and its ObjectClass is the RXClass associated
      /// with the generic argument type, or is derived
      /// from same.
      /// </summary>

      public static bool NonErasedIsMatch(ObjectId id)
      {
         return !id.IsErased && id.ObjectClass.IsDerivedFrom(Value);
      }

      /// <summary>
      /// The ObjectId is not the id of an erased object 
      /// and its ObjectClass is the RXClass associated
      /// with the generic argument type. Derived types
      /// do not match.
      /// </summary>

      public static bool NonErasedIsMatchExact(ObjectId id)
      {
         return !id.IsErased && id.ObjectClass == Value;
      }

      /// <summary>
      /// The RXClass argument is the RXClass associated
      /// with the generic argument type, or is derived
      /// from same.
      /// </summary>

      public static bool IsMatch(RXClass rxclass)
      {
         return rxclass?.IsDerivedFrom(Value) ?? false;
      }

      /// <summary>
      /// The RXClass argument is the RXClass associated
      /// with the generic argument type. Derived types
      /// do not match.
      /// </summary>
      
      public static bool IsMatchExact(RXClass rxclass)
      {
         return rxclass == Value;
      }

      public static Expression<Func<ObjectId, bool>> GetMatchExpression(bool exactMatch = false)
      {
         return exactMatch ? IsMatchExactExpression : IsMatchExpression;
      }

      public static Expression<Func<RXClass, bool>> GetClassMatchExpression(bool exactMatch = false)
      {
         return exactMatch ? IsMatchClassExactExpression : IsMatchClassExpression;
      }

      public static readonly Expression<Func<ObjectId, bool>> IsMatchExpression =
         id => id.ObjectClass.IsDerivedFrom(Value);

      public static readonly Expression<Func<ObjectId, bool>> IsMatchExactExpression =
         id => id.ObjectClass == Value;

      public static readonly Expression<Func<RXClass, bool>> IsMatchClassExpression =
         arg => arg.IsDerivedFrom(Value);

      public static readonly Expression<Func<RXClass, bool>> IsMatchClassExactExpression =
         arg => arg == Value;

   }
}




