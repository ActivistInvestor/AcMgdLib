/// AcNativeLibraryResolver.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Internal;

namespace AcMgdLib.Runtime
{
   /// <summary>
   /// AcNativeLibraryResolver is a utility class that provides 
   /// a mechanism for dynamically resolving native library P/Invoke
   /// imports (DllImport) in .NET assemblies with thread-safe caching.
   /// </summary>
   public static class AcNativeLibraryResolver
   {
      private static readonly HashSet<Assembly> _registeredAssemblies = new HashSet<Assembly>();
      private static readonly object _lock = new object();

      // Caches pattern -> resolved module file path (or empty string for negative/ambiguous matches)
      private static readonly ConcurrentDictionary<string, string> _modulePathCache =
          new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      /// <summary>
      /// Registers the dynamic DllImport resolver for the calling assembly.
      /// </summary>
      public static void Register(Assembly targetAssembly = null)
      {
         targetAssembly ??= Assembly.GetCallingAssembly();
         lock(_lock)
         {
            if(_registeredAssemblies.Add(targetAssembly))
               NativeLibrary.SetDllImportResolver(targetAssembly, ResolveNativeLibrary);
         }
      }

      /// <summary>
      /// Initializes declarative, attribute-driven registration of assemblies
      /// for DllImport dll name resolution.
      /// </summary>
      
      public static void Initialize()
      {
         AcNativeLibraryResolverAttribute.Initialize();
      }

      private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
      {
         if(string.IsNullOrWhiteSpace(libraryName))
            return IntPtr.Zero;

         // Fetch from cache or execute FindLoadedModuleFilePath only on cache miss
         string moduleFilePath = _modulePathCache.GetOrAdd(libraryName, pattern =>
         {
            ProcessModule module = FindLoadedModule(pattern);
            return module?.FileName ?? string.Empty;
         });

         if(!string.IsNullOrEmpty(moduleFilePath) &&
             NativeLibrary.TryLoad(moduleFilePath, assembly, searchPath, out IntPtr handle))
         {
            Debug.WriteLine($"[DllImport(\"{libraryName}\"...)] Resolved to {moduleFilePath}");
            return handle;
         }

         Debug.WriteLine($"[DllImport \"{libraryName}\"] Failed to resolve to a loaded module.");

         // Return IntPtr.Zero to let standard .NET runtime resolution handle non-matching names
         return IntPtr.Zero;
      }

      /// <summary>
      /// Finds a loaded process module matching the specified wildcard pattern.
      /// Returns null if zero or multiple (ambiguous) matches exist.
      /// </summary>
      internal static ProcessModule FindLoadedModule(string pattern)
      {
         var matches = Process.GetCurrentProcess().Modules
             .Cast<ProcessModule>()
             .Where(m => Utils.WcMatchEx(m.ModuleName, pattern, true));

         if(matches.Skip(1).Any())
            return null;

         return matches.FirstOrDefault();
      }
   }
}