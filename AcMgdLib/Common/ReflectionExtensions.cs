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

/// Documentation to come

namespace System.Reflection.Extensions
{
   public static partial class ReflectionExtensions
   {

      public static IEnumerable<Type> GetTypes(Assembly asm, Func<Type, bool> predicate)
      {
         bool all = predicate == null;
         foreach(Type type in asm.GetExportedTypes())
         {
            if(all || predicate(type))
               yield return type;
         }
      }

      public static IEnumerable<Type> GetTypes(Func<Type, bool> predicate = null)
      {
         foreach(Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
         {
            foreach(Type type in GetTypes(asm, predicate))
               yield return type;
         }
      }

      public static IEnumerable<(T attrib, Type type)> GetTypeAttributes<T>(Assembly asm = null) where T : Attribute
      {
         if(asm == null)
         {
            return GetTypes(type => type.IsDefined(typeof(T), false))
              .Select(type => (type.GetCustomAttribute<T>(), type));
         }
         else
         {
            return GetTypes(asm, type => type.IsDefined(typeof(T), false))
               .Select(type => (type.GetCustomAttribute<T>(), type));
         }
      }
   }

   public class TypeAttributeHandler<T> where T : System.Attribute
   {
      static Type attributeType = typeof(T);
      bool initialized = false;
      Action<T, Type> handler;

      public TypeAttributeHandler(Action<T, Type> handler = null)
      {
         Assert.IsNotNull(handler, nameof(handler));
         this.handler = handler;
         var atts = ReflectionExtensions.GetTypeAttributes<T>();
         foreach(var rec in atts)
            OnAttributeFound(rec.attrib, rec.type);
         AppDomain.CurrentDomain.AssemblyLoad += assemblyLoad;
      }

      private void assemblyLoad(object sender, AssemblyLoadEventArgs args)
      {
         var atts = ReflectionExtensions.GetTypeAttributes<T>(args.LoadedAssembly);
         foreach(var rec in atts)
            OnAttributeFound(rec.attrib, rec.type);
      }

      protected virtual void OnAttributeFound(T attribute, Type appliedType)
      {
         handler?.Invoke(attribute, appliedType);
      }
   }

}




