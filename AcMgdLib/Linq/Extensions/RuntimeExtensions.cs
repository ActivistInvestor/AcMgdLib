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

namespace Autodesk.AutoCAD.Runtime
{
   /// Assorted helper and utility classes associated 
   /// with AutoCAD runtime classes.
   /// 
   /// <em>Note: This code requires C# 7 or later</em>
   ///
   /// For AutoCAD 2024 or earlier, you can easily modify
   /// a project's .csproj file to support C# 7:
   /// 
   ///   <PropertyGroup>
   ///     <TargetFramework>net4.8</TargetFramework>
	///     <LangVersion>7</LangVersion>
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
      /// The above throws and exception if the argument does not
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

      public static void Requires<T>(this AcRx.ErrorStatus es, ObjectId id, bool exact = false, string msg = "")
         where T:RXObject
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         if(!RXClass<T>.IsAssignableFrom(id, exact))
            throw new AcRx.Exception(es, !string.IsNullOrWhiteSpace(msg) ? msg 
               : $"(requires a {typeof(T).Name})")
                  .Log(es, id, exact, msg);
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

      public static bool IsEqualTo(this string str, string other)
      {
         return string.Equals(str, other, StringComparison.InvariantCultureIgnoreCase);
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

      public static bool IsA<T>(this ObjectId id, bool exactMatch = false) where T : RXObject
      {
         return id.IsNull ? false
            : !exactMatch ? id.ObjectClass.IsDerivedFrom(RXClass<T>.Value)
               : id.ObjectClass == RXClass<T>.Value;
      }

      /// <summary>
      /// Replaces one DisposableWrapper with another.
      /// 
      /// This API must be used with extreme care.
      /// 
      /// The replacement wrapper must be a class derived from 
      /// the original wrapper, or be a class derived from the 
      /// nearest concrete base type of the original wrapper.
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

      public static void ReplaceWith(this DisposableWrapper original, DisposableWrapper replacement)
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
   /// as a local variable, an instance member, or a parameter 
   /// of a containing method.
   /// </summary>

   public static partial class RXClass<T> where T : RXObject
   {
      static readonly bool isAbstract = typeof(T).IsAbstract;

      public static readonly RXClass Value = RXObject.GetClass(typeof(T));

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
      /// <returns>A predicate that when passed an ObjectId returns
      /// a value indicating if the ObjectId matches the criteria</returns>

      public static Func<ObjectId, bool> GetIdPredicate(bool exactMatch = false)
      {
         return exactMatch && !typeof(T).IsAbstract ? MatchIdExact : MatchId;
      }

      public static Func<RXClass, bool> GetClassPredicate(bool exactMatch = false)
      {
         return exactMatch && !typeof(T).IsAbstract ? MatchClassExact : MatchClass;
      }

      public static bool Matches(ObjectId id, bool exact = false)
      {
         return exact ? MatchIdExact(id) : MatchId(id);
      }

      /// <summary>
      /// Delegates returned by the above methods. Returning these
      /// instead of embedding them into the above methods avoids 
      /// allocating a new delegate on each call to those methods.
      /// 
      /// There are two versions of each delegate. One matches 
      /// exactly and the other matches any derived type. There
      /// are overloads that test the ObjectClass of an ObjectId,
      /// and overloads that test an RXClass.
      /// 
      /// Note: No check is performed on the ObjectId argument
      /// to ensure it is not null or has a null ObjectClass:
      /// </summary>

      public static bool IsMatch(ObjectId id)
      {
         return id.ObjectClass.IsDerivedFrom(Value);
      }

      public static bool IsMatchExact(ObjectId id)
      {
         return id.ObjectClass == Value;
      }

      public static bool IsMatch(RXClass rxclass)
      {
         return rxclass?.IsDerivedFrom(Value) ?? false;
      }

      public static bool IsMatchExact(RXClass rxclass)
      {
         return rxclass == Value;
      }

      /// <summary>
      /// Delegate versions of the above which are returned
      /// by GetIdPredicate(). 
      /// </summary>

      public static readonly Func<ObjectId, bool> MatchId =
         id => id.ObjectClass.IsDerivedFrom(Value);

      public static readonly Func<ObjectId, bool> MatchIdExact =
         id => id.ObjectClass == Value;

      public static readonly Func<RXClass, bool> MatchClass =
         rxclass => rxclass.IsDerivedFrom(Value);

      public static readonly Func<RXClass, bool> MatchClassExact =
         rxclass => rxclass == Value;

   }
}




