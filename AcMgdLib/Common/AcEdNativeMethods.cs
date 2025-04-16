/// AcEdNativeMethods.cs
/// 
/// Activist Investor / Tony T
///
/// Distributed under the terms of the MIT license.

using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;

namespace Autodesk.AutoCAD.Runtime.NativeInterop
{
   public static class AcEdNativeMethods
   {
      const string ACCORE_DLL = "accore.dll";

      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "?acedIsLispOrScriptActive@@YA_NXZ")]
      extern static bool acedIsLispOrScriptActive();

      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "acedGetActiveCommandFlags")]
      extern static CommandFlags acedGetActiveCommandFlags();

      public static bool IsLispOrScriptActive(this Document doc)
      {
         return acedIsLispOrScriptActive();
      }

      public static CommandFlags GetActiveCommandFlags(this Document doc)
      {
         return acedGetActiveCommandFlags();
      }

      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "?acedSetOLELock@@YA_NHH@Z")]
      internal extern static bool acedSetOleLock(int cookie, CommandFlags flags);

      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "?acedClearOLELock@@YA_NH@Z")]
      internal extern static bool acedClearOleLock(int cookie);

   }
}
