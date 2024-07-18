/// DllImports.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Autodesk.AutoCAD.Runtime.InteropServices
{
   /// <summary>
   /// The classes in this unit dynamically import 
   /// native methods from a loaded module, acting
   /// as an alternative to using the [DllImport()]
   /// attribute in cases where the module filename
   /// is version-dependent.
   /// 
   /// See AcDbNativeMethods.cs for an extended 
   /// description and example of using this class.
   /// </summary>

   public static class DllImport
   {
      [DllImport("kernel32.dll", SetLastError = true)]
      private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

      [DllImport("kernel32.dll", SetLastError = true)]
      public static extern IntPtr GetModuleHandle(string lpModuleName);

      /// <summary>
      /// Uses brute-force to find the name of acdbxx.dll:
      /// </summary>
      /// <returns></returns>
      /// <exception cref="InvalidOperationException"></exception>

      static string GetAcDbDllName()
      {
         string acad = Process.GetCurrentProcess().MainModule.FileName;
         string basePath = Path.GetDirectoryName(acad);
         for(int ver = 27; ver > 17; ver--)
         {
            string file = $"acdb{ver}.dll";
            string found = FindFile(Path.Combine(basePath, file));
            if(found != null)
               return file;
         }
         return null;
      }

      static string FindFile(string path)
      {
         try
         {
            return HostApplicationServices.Current.FindFile(path,
               null, FindFileHint.Default);
         }
         catch
         {
            return null;
         }
      }

      /// <summary>
      /// Gets a delegate for a function exported by acdbxx.dll
      /// 
      /// Suppports acdb17.dll through acdb27.dll
      /// </summary>
      /// <typeparam name="T">The type of the delegate representing
      /// the exported function signature</typeparam>
      /// <param name="entryPoint">The C++ mangled export name of 
      /// the API to import (should be the same string used as the 
      /// EntryPoint argument to the [DllImport] attribute).</param>
      /// <returns>A delegate representing the exported function.</returns>

      public static T AcDbImport<T>(string entryPoint) where T : Delegate
      {
         string filename = GetAcDbDllName();
         if(string.IsNullOrEmpty(filename))
            throw new InvalidOperationException("acdbxx.dll not found");
         T result = GetNativeDelegate<T>(filename, entryPoint);
         if(result == null)
            throw new InvalidOperationException(
               $"Failed to import {entryPoint} from {filename}");
         return result;
      }

      /// <summary>
      /// Gets a delegate representing a function exported by the 
      /// specified loaded module. This method can only be used to
      /// import methods from modules that are already loaded into 
      /// AutoCAD. For modules that are not loaded into AutoCAD, one 
      /// must use use LoadLibrary() to explicitly load the module
      /// before a method can be imported from it.
      /// </summary>
      /// <typeparam name="T">The type of the delegate representing
      /// the imported function signature</typeparam>
      /// <param name="module">The module's filename, including the
      /// extension</param>
      /// <param name="entryPoint">The C++ mangled export name of 
      /// the API to import (should be the same string used in the
      /// EntryPoint argument to the [DllImport] attribute).</param>
      /// <returns></returns>

      public static T GetNativeDelegate<T>(string module, string entryPoint) where T : Delegate
      {
         if(string.IsNullOrWhiteSpace(module))
            throw new ArgumentException(nameof(module));
         if(string.IsNullOrWhiteSpace(entryPoint))
            throw new ArgumentException(nameof(entryPoint));
         IntPtr hModule = GetModuleHandle(module);
         if(hModule == IntPtr.Zero)
            throw new InvalidOperationException($"module {module} not found.");
         IntPtr funcPtr = GetProcAddress(hModule, entryPoint);
         if(funcPtr == IntPtr.Zero)
            throw new InvalidOperationException(
               $"entry point {entryPoint} not found in module {module}");
         return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
      }
   }
}
