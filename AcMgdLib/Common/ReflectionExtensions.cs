/// ReflectionExtensions.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Reflection;

/// Documentation incomplete.

namespace System.Reflection.Extensions
{
   public static partial class ReflectionExtensions
   {

      public static IEnumerable<Type> GetTypes(this Assembly asm, Func<Type, bool> predicate)
      {
         bool all = predicate == null;
         foreach(Type type in asm.GetExportedTypes())
         {
            if(all || predicate(type))
               yield return type;
         }
      }

      public static IEnumerable<Type> GetTypes(this AppDomain domain, Func<Type, bool> predicate = null)
      {
         foreach(Assembly asm in domain.GetAssemblies())
         {
            foreach(Type type in GetTypes(asm, predicate))
               yield return type;
         }
      }

      public static IEnumerable<TypeAttributeInfo<T>> GetTypeAttributes<T>(this AppDomain domain) where T : Attribute
      {
         return domain.GetTypes(type => type.IsDefined(typeof(T)))
            .Select(type => new TypeAttributeInfo<T>(type));
         
      }

      public static IEnumerable<TypeAttributeInfo<T>> GetTypeAttributes<T>(this Assembly asm) where T : Attribute
      {
         return asm.GetTypes(type => type.IsDefined(typeof(T), false))
            .Select(type => new TypeAttributeInfo<T>(type));
      }
   }

   /// <summary>
   /// A type that enumerates every Type attribute of the 
   /// specified generic argument type in every loaded assembly, 
   /// and in all subsequently-loaded assemblies.
   /// 
   /// This class only targets Attributes that are applied to
   /// Types.
   /// 
   /// The OnAttributeFound() virtual method is called for each
   /// attribute found.
   /// 
   /// Instead of deriving a type from this type and overriding
   /// OnAttributeFound(), an instance of this type can be used
   /// and passed a delegate that accepts the same arguments as 
   /// the OnAttributeFound() method, and that delegate will be
   /// called for each attribute found.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   
   public class TypeAttributeHandler<T> where T : System.Attribute
   {
      static Type attributeType = typeof(T);
      bool initialized = false;
      Action<T, Type> handler;

      public TypeAttributeHandler(Action<T, Type> handler = null)
      {
         this.handler = handler;
         foreach(var info in AppDomain.CurrentDomain.GetTypeAttributes<T>())
            OnAttributeFound(info.Attribute, info.Type);
         AppDomain.CurrentDomain.AssemblyLoad += assemblyLoad;
      }

      private void assemblyLoad(object sender, AssemblyLoadEventArgs args)
      {
         foreach(var info in args.LoadedAssembly.GetTypeAttributes<T>())
            OnAttributeFound(info.Attribute, info.Type);
      }

      /// <summary>
      /// Called for every instance of the 
      /// targeted Attribute that is found.
      /// </summary>
      /// <param name="attribute">The targeted attribute instance</param>
      /// <param name="appliedType">The type which the targeted
      /// attribute instance is applied to.</param>

      protected virtual void OnAttributeFound(T attribute, Type appliedType)
      {
         handler?.Invoke(attribute, appliedType);
      }
   }

   public struct TypeAttributeInfo<T> where T: System.Attribute
   {
      public TypeAttributeInfo(Type type)
      {
         this.Attribute = type.GetCustomAttribute<T>(false);
         this.Type = type;
      }

      public T Attribute { get; private set; }
      public Type Type { get; private set; }
   }
}




