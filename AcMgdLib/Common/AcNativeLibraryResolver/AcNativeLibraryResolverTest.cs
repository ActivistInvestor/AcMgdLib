/// TestNativeLibraryResolver.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license


using System.Runtime.InteropServices;
using AcMgdLib.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

/// <summary>
/// This assembly-level attribute registers the containing assembly 
/// to enable the use of wcmatch-style wildcards in the [DllImport] 
/// attribute's dllname argument. 
/// 
/// This attribute can applied to an assembly in lieu of calling the 
/// AcNativeLibraryResolver.Register() method. 
/// 
/// In order for this attribute to work, the AcNativeLibraryResolver's
/// Initialize() method must be called, which is usually done from an 
/// IExtensionApplication's Initialize() method, as shown in this
/// example:
/// 
/// <code>
/// 
///   public class MyApplication : IExtensionApplication
///   {
///      public void Initialize()
///      {
///          AcNativeLibraryResolver.Initialize();
///      }
///      
///      public void Terminate()
///      {
///      }
///   }
/// 
/// </code>

/// Enable dynamic DllImport module name resolution for the
/// containing assembly:

[assembly: AcNativeLibraryResolver]

namespace AcNativeLibraryResolverTest
{
   /// <summary>
   /// Tests the AcNativeLibraryResolver's support for use of
   /// wcmatch-style wildcards in the [DLLImport] attribute's 
   /// dllname argument, by importing and calling the native 
   /// acdbSetDbmod() function, which is located in acdbXX.dll
   /// (where XX is the AutoCAD release number such as acdb24.dll,
   /// acdb25.dll, etc.). 
   /// 
   /// The RESETDBMOD command resets the current database's DBMOD 
   /// flags to 0.
   /// 
   /// </summary>

   public static class AcNativeLibraryResolverTest
   {
      [CommandMethod("RESETDBMOD")]
      public static void ResetDbmodCommand()
      {
         HostApplicationServices.WorkingDatabase?.SetDbmod(0);
      }
   }

   /// <summary>
   /// Adds the SetDbmod() wrapper extension method to the Database 
   /// class that sets the DBMOD flags using the native acdbSetDbmod() 
   /// function.
   /// </summary>
   
   public static partial class DatabaseExtensions
   {
      class NativeMethods
      {
         /// Note the use of the "acdb##.dll" wildcard in the DllImport 
         /// attribute, which will be resolved to the correct version 
         /// of acdbXX.dll at runtime.

         [DllImport("acdb##.dll", 
            EntryPoint = "?acdbSetDbmod@@YAHPEAVAcDbDatabase@@H@Z",
            CallingConvention = CallingConvention.Cdecl)]
         public static extern int acdbSetDbmod(IntPtr database, int newval);
      }

      public static int SetDbmod(this Database db, int newval)
      {
         IntPtr dbPtr = db.UnmanagedObject;
         return NativeMethods.acdbSetDbmod(dbPtr, newval);
      }


   }
}