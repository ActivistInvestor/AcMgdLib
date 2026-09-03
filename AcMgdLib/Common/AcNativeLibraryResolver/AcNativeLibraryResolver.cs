/// AcNativeLibraryResolver.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Internal;

namespace AcMgdLib.Runtime
{
   /// <summary>
   /// AcNativeLibraryResolver is a utility class that provides 
   /// a mechanism for dynamically resolving native library P/Invoke
   /// imports (DllImport) in .NET assemblies.
   /// 
   /// This class is specifically designed for AutoCAD extensions. It 
   /// allows developers to use version-independent wildcards in the 
   /// dllName argument of [DllImport] attributes, enabling the same 
   /// code to target different releases of AutoCAD that use different 
   /// release-dependent DLL names, with no code modifications.
   /// 
   /// This overcomes the problem of using the [DllImport] attribute to 
   /// P/Invoke APIs from AutoCAD dlls with version-dependent file names
   /// (for example, acdb24.dll, acdb25.dll, etc.), which would otherwise 
   /// require separate builds for each release of AutoCAD.
   /// 
   /// Background:
   /// 
   /// When using [DllImport], you must explicitly specify the name of 
   /// the dll. If the dll's name is different in each product release, 
   /// then you can't use the same assembly across different product 
   /// releases in which the name of the DLL differs (most-notoriously, 
   /// acdbXX.dll).
   /// 
   /// So for example, a build that targets releases of AutoCAD that 
   /// use acdb23.dll, cannot be used with releases of AutoCAD that use 
   /// acdb24.dll, acdb25.dll and so forth, and in some cases, it may be 
   /// due to nothing other than the use of the [DllImport] attribute to 
   /// import functions from acdbxx.dll.
   /// 
   /// For example:
   /// 
   ///   [DllImport("acdb23.dll", CallingConvention = 
   ///      CallingConvention.Cdecl, 
   ///      EntryPoint = "someFunction")]
   ///
   /// Nothing more than the above use of [DllImport] makes the assembly 
   /// that contains it dependent on acdb23.dll (AutoCAD 2019/2020), which 
   /// means that the assembly cannot be used with other releases of AutoCAD 
   /// that do not use acdb23.dll.
   /// 
   /// AcNativeLibraryResolver solves that problem by allowing the use of
   /// AutoCAD wcmatch-style wildcards in the name of the dll passed to
   /// the [DllImport] attribute. 
   /// 
   /// For example, the following use of [DllImport] will work on any
   /// release of AutoCAD starting from 2025 or later. The actual dll
   /// name will be resolved at runtime to the version of acdbXX.dll that
   /// is in use (e.g. acdb25.dll, acdb26.dll, etc.).
   /// 
   ///   [DllImport("acdb##.dll",   // note the use of the "##" wildcard 
   ///   
   ///      CallingConvention = CallingConvention.Cdecl, 
   ///      EntryPoint = "someFunction")]
   ///
   /// This code replaces the existing legacy solutions that can be found
   /// in the following locations:
   /// 
   ///   https://github.com/ActivistInvestor/AcMgdLib/blob/main/AcMgdLib/Common/DllImport.cs
   ///   https://github.com/ActivistInvestor/AcMgdLib/blob/main/AcMgdLib/Common/AcDbNativeMethods.cs
   /// 
   /// Because AcNativeLibraryResolver uses .NET apis that are not supported
   /// in legacy versions of .NET, it is only supported in AutoCAD releases
   /// that use .NET 8.0 or later (e.g. AutoCAD 2025 and later). If you need to 
   /// support earlier releases of AutoCAD, you will need to use the the legacy 
   /// AcDbNativeMethods.cs and DllImport.cs code at the above links instead.
   /// 
   /// </summary>

   public static class AcNativeLibraryResolver
   {
      static HashSet<Assembly> _registeredAssemblies = new HashSet<Assembly>();
      private static readonly object _lock = new object();

      /// <summary>
      /// Registers the dynamic DllImport resolver for the calling assembly.
      /// Intercepts any [DllImport] specifying wildcards (e.g. "acdb##.dll", 
      /// "acge##.dll", etc.) and resolves them to the active AutoCAD version 
      /// (e.g. "acdb25.dll", "acge25.dll").
      /// 
      /// This method must be called prior to any [DllImport] calls that use version 
      /// placeholders, otherwise the .NET runtime will fail to resolve the library.
      /// Typically, this method would be called from the Initialize() method of the
      /// IExtensionApplication implementation of the AutoCAD .NET assembly. The
      /// argument is the assembly that contains the external methods having the 
      /// DLLImport attributes applied to them which need to be dynamically resolved. 
      /// If no argument is provided, the calling assembly is used. Any number of 
      /// assemblies can be registered, but each should only be registered once.
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

      public static void Initialize()
      {
         AcNativeLibraryResolverAttribute.Initialize();
      }

      /// <summary>
      /// The DllImportSearchPath should contain the location of acad.exe, which the 
      /// code is running in, and is also the location of the AutoCAD native libraries. 
      /// </summary>
      /// <param name="libraryName"></param>
      /// <param name="assembly"></param>
      /// <param name="searchPath"></param>
      /// <returns></returns>

      private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
      {
         if(string.IsNullOrWhiteSpace(libraryName))
            return IntPtr.Zero;
         ProcessModule module = FindLoadedModule(libraryName);
         if(module != null && NativeLibrary.TryLoad(module.FileName, assembly, searchPath, out IntPtr handle))
         {
            Debug.WriteLine($"[DllImport(\"{libraryName}\"...)] Resolved to {module.FileName}"); 
            return handle;
         }
         else
         {
            Debug.WriteLine($"[DllImport \"{libraryName}\"] Failed to resolve to a loaded module.");
         }
         // Return IntPtr.Zero to let standard .NET runtime resolution
         // handle non-matching library names and ambiguous matches.
         return IntPtr.Zero;
      }

      /// <summary>
      /// If multiple matching modules are found, this will fallback
      /// to the default .NET runtime module resolution, which will
      /// usually result in an exception.
      /// </summary>
      /// <param name="pattern">A dll name or a wcmatch-style wildcard 
      /// pattern that matches the name of exactly one DLL.  Wildcard 
      /// patterns must be used with great care, and should be as specific 
      /// as possible. If more than one dll filename matches a wildcard 
      /// pattern, it is treated as an ambiguous match and an error. For 
      /// example, "acdb##.dll" will only matche the string "acdb" followed 
      /// by exactly 2 numeric digits. But, "acdb*.dll" will match many 
      /// files and will result in an ambiguous match error. 
      /// </param>
      /// <returns>A ProcessModule instance if a unique match is found; 
      /// otherwise, null.</returns>

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



