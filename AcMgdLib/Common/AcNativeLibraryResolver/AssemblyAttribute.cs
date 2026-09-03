using System;
using System.Reflection;

namespace AcMgdLib.Runtime
{

   /// <summary>
   /// Generic boilerplate class that allows you to register a callback 
   /// to be invoked for each currently-loaded assembly, and as well as
   /// all subseqently-loaded assemblies having a specific assembly-level 
   /// attribute applied to them.
   /// </summary>
   /// <typeparam name="T">The type of the assembly-level attribute to 
   /// be acted upon. The callback is only called if this attribute is 
   /// applied to an assembly.</typeparam>

   public static class AssemblyAttribute<T> where T : Attribute
   {
      static Action<Assembly, T> action = null;
      static bool initialized = false;

      public static void Initialize(Action<Assembly, T> action)
      {
         if(initialized)
            return;
         if(action is null)
            throw new ArgumentNullException(nameof(action));
         AssemblyAttribute<T>.action = action;
         foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
         {
            TryInvoke(assembly);
         }
         AppDomain.CurrentDomain.AssemblyLoad += assemblyLoad;
         initialized = true;
      }

      static void TryInvoke(Assembly assembly)
      {
         T attr = assembly?.GetCustomAttribute<T>();
         if(attr != null)
            action(assembly, attr);
      }
      
      private static void assemblyLoad(object sender, AssemblyLoadEventArgs args)
      {
         TryInvoke(args.LoadedAssembly);
      }
   }
}
