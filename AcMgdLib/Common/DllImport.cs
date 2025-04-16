/// DllImports.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AcMgdLib.Diagnostics;

namespace Autodesk.AutoCAD.Runtime.NativeInterop
{
   /// <summary>
   /// The classes in this unit dynamically import 
   /// native methods from a loaded module, acting
   /// as an alternative to using the [DllImport]
   /// attribute in cases where the module filename
   /// is version-dependent.
   /// 
   /// See AcDbNativeMethods.cs for an exhaustive 
   /// discussion and example of using this class.
   /// </summary>
   /// 
   /// <remarks>
   /// Finding the name of acdbxx.dll.
   /// 
   /// Background:
   /// 
   /// acdbxx.dll has a different file name in each 
   /// major release ('Big R') of AutoCAD:
   /// 
   ///   2013-2014:   acdb19.dll
   ///   2015-2016:   acdb20.dll
   ///   2017:        acdb21.dll
   ///   2018:        acdb22.dll
   ///   2019-2020:   acdb23.dll
   ///   2021-2024:   acdb24.dll
   ///   2025-2026+:  acdb25.dll
   /// 
   /// The purpose of the following code is to allow the
   /// developer to avoid a dependence on a specific version
   /// of acdbxx.dll, which would happen if the [DllImport]
   /// attribute were used to import methods from acdbxx.dll.
   /// 
   /// Instead, this code will at runtime, determine what
   /// version of acdbxx.dll is in use, and will import the
   /// needed API from it dynamically, thereby allowing the 
   /// same code to be used with any of the above versions 
   /// of acdbxx.dll, or the subset of same that support the
   /// imported APIs.
   /// 
   /// The API's imported using this code must have the same
   /// signature in all releases it is used with, and there's
   /// no built-in support for entry point symbol differences 
   /// between 32 and 64-bit platforms. The user of this API 
   /// can use different entry point symbols to support both 
   /// platforms, and determine at runtime which platform the 
   /// code is running on and provide the appropriate entry 
   /// point symbol to the methods below that require them.
   /// 
   /// Revisions: 2-25:
   /// 
   /// Dependence on the native GetModuleHandle() API has been 
   /// depreciated and replaced with managed APIs that provide 
   /// access to loaded module information. The handle of a 
   /// loaded module is exposed as the BaseAddress property of 
   /// the ProcessModule class.
   /// 
   /// </remarks>

   public static class DllImport
   {
      [DllImport("kernel32.dll", SetLastError = true)]
      private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

      static IntPtr acdbHModule = IntPtr.Zero;

      /// <summary>
      /// Returns the actual name of the acdbXX.dll 
      /// that's loaded into the current process.
      /// </summary>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>

      static ProcessModule acdbModule = null;
      
      internal static ProcessModule AcDbModule
      {
         get
         {
            if(acdbModule is null)
            {
               Regex regex = new Regex(@"^acdb\d\d\.dll$",
                  RegexOptions.IgnoreCase | RegexOptions.Compiled);
               var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>();
               var module = modules.FirstOrDefault(m => regex.IsMatch(m.ModuleName));
               if(module == null)
                  throw new InvalidOperationException($"acdbxx.dll module not found.");
               acdbModule = module;
            }
            return acdbModule;
         }
      }

      public static FileVersionInfo AcDbVersion
      {
         get
         {
            return AcDbModule.FileVersionInfo;
         }
      }


      /// <summary>
      /// Gets a delegate for a function exported by acdbxx.dll
      /// 
      /// Suppports any version of acdbXX.dll.
      /// </summary>
      /// <typeparam name="T">The type of the delegate representing
      /// the exported function signature</typeparam>
      /// <param name="entryPoint">The C++ mangled export name of 
      /// the API to import (should be the same string used as the 
      /// EntryPoint= argument to the [DllImport] attribute).</param>
      /// <returns>A delegate representing the exported function.</returns>

      public static T AcDbImport<T>(string entryPoint = null) where T : Delegate
      {
         T result = GetNativeDelegate<T>(AcDbModule, entryPoint);
         if(result == null)
            throw new InvalidOperationException(
               $"Failed to import {entryPoint} from {AcDbModule.ModuleName}");
         return result;
      }

      public static T Load<T>(this T del, string entryPoint = null) where T:Delegate
      {
         return AcDbImport<T>(entryPoint);
      }

      /// <summary>
      /// Gets a delegate representing a function exported by the 
      /// specified loaded module. This method can only be used to
      /// import methods from modules that are already loaded into 
      /// AutoCAD. For modules that are not currently loaded into 
      /// AutoCAD, one must use LoadLibrary() to load the module 
      /// before a method can be imported from it.
      /// </summary>
      /// <param name="module">A ProcessModule representing the
      /// dynamic link library that exports the function that is 
      /// to be loaded.</param>
      /// <param name="entryPoint">The C++ mangled export name of 
      /// the API to import (should be the same string used as the
      /// EntryPoint when using the [DllImport] attribute).</param>
      /// <returns></returns>

      public static T GetNativeDelegate<T>(ProcessModule module, string entryPoint = null) where T : Delegate
      {
         Assert.IsNotNull(module);
         Type type = typeof(T);
         if(string.IsNullOrWhiteSpace(entryPoint))
         {
            var epa = type.GetCustomAttribute<EntryPointAttribute>();
            if(epa == null || string.IsNullOrWhiteSpace(epa.Name))
               throw new ArgumentException($"EntryPoint not specified and no EntryPoint Attribute " +
                  $"exists on delegate type {typeof(T).Name}").Log();
            entryPoint = epa.Name;
         }
         IntPtr funcPtr = GetProcAddress(module.BaseAddress, entryPoint);
         if(funcPtr == IntPtr.Zero)
            throw new InvalidOperationException(
               $"entry point {entryPoint} not found in module {module.FileName}").Log();
         return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
      }

   }


   [AttributeUsage(AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
   public class EntryPointAttribute : System.Attribute
   {
      public EntryPointAttribute(string name)
      {
         this.Name = name;
      }

      public string Name { get; set; }
   }

}
