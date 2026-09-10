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
   /// AcNativeLibraryResolver Class
   /// 
   /// AcNativeLibraryResolver is a utility class that provides 
   /// a mechanism for dynamically resolving native library P/Invoke
   /// imports (DllImport) in .NET assemblies, by enabling the use
   /// of AutoCAD wcmatch-style wildcard patterns in the library name.
   /// 
   /// Using this class, you can specify a wildcard pattern for the 
   /// library name in your DllImport attribute, and the resolver will 
   /// attempt to locate the corresponding loaded module in the current 
   /// process.
   /// 
   /// The primiary use case is for calling native APIs that reside in
   /// DLLs that have release-dependent filenames.
   /// 
   /// The following is a list of common AutoCAD DLLs that have release-
   /// dependent names, and the wildcard patterns that can be used to
   /// resolve them to the correct file for the AutoCAD release which the
   /// code is running on, from AutoCAD 2025 and later. The '#' character 
   /// in the pattern is a wildcard that matches a single digit, which is 
   /// the release number of the AutoCAD DLL. Note that the files listed 
   /// may not be included with all supported releases (AutoCAD 2025 or
   /// later).
   /// 
   /// AutoCAD 2025 filename      Recommended Wildcard pattern
   /// =======================================================
   /// adui25.dll                 adui2#.dll
   /// acdb25.dll                 acdb2#.dll 
   /// AcDimX25.dll               AcDimX2#.dll
   /// acge25.dll                 acge2#.dll
   /// acgex25.dll                acgex2#.dll
   /// AcGradient25.dll           AcGradient2#.dll
   /// AcPersSubentNaming25.dll   AcPersSubentNaming2#.dll
   /// acui25.dll                 acui2#.dll
   /// AcWebDAV25.dll             AcWebDAV2#.dll
   /// atlst25.dll                atlst2#.dll
   /// hcreg25.dll                hcreg2#.dll
   /// heidi25.dll                heidi2#.dll
   /// modlr25.dll                modlr2#.dll
   /// oletohdi25.dll             oletohdi2#.dll
   /// plotcfg25.dll              plotcfg2#.dll
   /// pm25.dll                   pm2#.dll
   /// pmutil25.dll               pmutil2#.dll
   /// regacad25.dll              regacad2#.dll
   /// 
   /// </summary>

   public static class AcNativeLibraryResolver
   {
      public const string ACDB_DLL = "acdb2#.dll";
      public const string ACGE_DLL = "acge2#.dll";
      public const string ACGEX_DLL = "acgex2#.dll";

      static ConcurrentDictionary<Assembly, bool> knownAssemblies = new();
      private static bool initialized;
      private static readonly HashSet<Assembly> _registeredAssemblies = new HashSet<Assembly>();
      private static readonly object _lock = new object();

      ///  Caches pattern -> resolved module file path (or empty string for negative/ambiguous matches)
      private static readonly ConcurrentDictionary<string, string> _modulePathCache =
          new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      /// <summary>
      /// Registers the dynamic DllImport resolver for the calling assembly.
      /// </summary>

      public static void Register(Assembly assembly = null)
      {
         assembly ??= Assembly.GetCallingAssembly();
         if(!IsExempt(assembly))
         {
            lock(_lock)
            {
               if(_registeredAssemblies.Add(assembly))
               {
                  NativeLibrary.SetDllImportResolver(assembly, ResolveNativeLibrary);
                  DebugWrite($"Registered assembly {assembly.GetName().Name} for DllImport resolution");
               }
            }
         }
      }

      /// <summary>
      /// Registers all currently- and subsequently-loaded custom 
      /// assemblies for DllImport resolution. .NET Framework and 
      /// AutoCAD assemblies are not registered.
      /// </summary>

      public static void Initialize()
      {
         if(initialized)
            return;
         initialized = true;
         foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
         {
            Register(assembly);
         }
         AppDomain.CurrentDomain.AssemblyLoad += assemblyLoad;
      }

      private static void assemblyLoad(object sender, AssemblyLoadEventArgs args)
      {
         if(!IsExempt(args.LoadedAssembly))
         {
            Register(args.LoadedAssembly);
         }
      }

      private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
      {
         if(string.IsNullOrWhiteSpace(libraryName))
            return IntPtr.Zero;

         string msg = $"[DllImport(\"{libraryName}\")]";
         string name = assembly.GetName().Name;

         DebugWrite($"Resolving {msg} for assembly {name}");

         ///  Fetch from cache or execute FindLoadedModuleFilePath only on cache miss
         string modulePath = _modulePathCache.GetOrAdd(libraryName, pattern =>
         {
            ProcessModule module = FindLoadedModule(pattern);
            return module?.FileName ?? string.Empty;
         });

         if(!string.IsNullOrWhiteSpace(modulePath))
         {
            lock(_lock)
            {
               if(NativeLibrary.TryLoad(modulePath, assembly, searchPath, out IntPtr handle))
               {
                  DebugWrite($"{msg} resolved to {modulePath}");
                  return handle;
               }
            }
         }

         DebugWrite($"{msg} Failed to resolve to a loaded module.");

         ///  Return IntPtr.Zero to let standard .NET runtime resolution handle non-matching names
         return IntPtr.Zero;
      }

      /// <summary>
      /// Finds a loaded process module matching the specified wildcard pattern.
      /// Returns null if zero or multiple (ambiguous) matches exist.
      /// 
      /// The pattern argument must match one and only one loaded module name 
      /// (case-insensitive) for a successful match. If multiple matches are found, 
      /// null is returned to indicate ambiguity.
      /// </summary>
      
      internal static ProcessModule FindLoadedModule(string pattern, bool nested = false)
      {
         var matches = Process.GetCurrentProcess().Modules
             .Cast<ProcessModule>()
             .Where(m => Utils.WcMatchEx(m.ModuleName, pattern, true));

         if(matches.Skip(1).Any())     ///  Multiple ambiguous matches found.
            return null;

         if(!matches.Any() && !nested)
         {
            /// Special handling for commonly-used DLLs with
            /// version-dependent filenames (e.g., acdb2#.dll, 
            /// acge2#.dll).
            ///
            /// This code eliminates the need to use wildcards, by
            /// simply replacing a mis-matched version number in the 
            /// DLL filename with the version number for the release 
            /// the code is running on.
            /// 
            /// Hence, if your existing DllImport attribute specifies 
            /// "acdb24.dll", and your code is running on AutoCAD 2026,
            /// the code will replace "acdb24.dll" with "acdb26.dll".

            if(Utils.WcMatchEx(pattern, ACDB_DLL, true))
            {
               return FindLoadedModule(ACDB_DLL, true);
            }  
            else if(Utils.WcMatchEx(pattern, ACGE_DLL, true))
            {
               return FindLoadedModule(ACGE_DLL, true);
            }
            else if(Utils.WcMatchEx(pattern, ACGEX_DLL, true))
            {
               return FindLoadedModule(ACGEX_DLL, true);
            }
            return null;
         }

         return matches.FirstOrDefault();
      }

      /// <summary>
      /// Loaded assemblies are checked to determine if they are 
      /// framework or AutoCAD assemblies, or dynamically-generated. 
      /// If so, they are not registered for DllImport resolution.
      /// </summary>

      static bool IsExempt(Assembly asm)
      {
         if(asm is null)
            return true;
         if(asm.IsDynamic)
            return true;
         bool result = false;
         if(knownAssemblies.TryGetValue(asm, out result))
            return result;
         var att = asm.GetCustomAttribute<AssemblyCompanyAttribute>();
         if(att != null)
         {
            string company = att.Company;
            result = company.StartsWith("Autodesk, Inc")
               || company.StartsWith("Microsoft Corporation");
         }
         knownAssemblies.TryAdd(asm, result);
         return result;
      }

      static void DebugWrite(string msg)
      {
         Debug.WriteLine($"{nameof(AcNativeLibraryResolver)}: {msg}");
      }

   }
}