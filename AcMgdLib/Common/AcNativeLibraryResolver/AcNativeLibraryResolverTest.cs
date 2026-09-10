/// TestNativeLibraryResolver.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license


using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AcMgdLib.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

/// <summary>
/// In order for this code to work, the AcNativeLibraryResolver's
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
         /// Note the use of the "acdb2#.dll" wildcard in the DllImport 
         /// attribute, which will be resolved to the correct version 
         /// of acdbXX.dll at runtime. This allows this code to target
         /// any AutoCAD release from 2025 onward.

         [DllImport("acdb2#.dll", 
            EntryPoint = "?acdbSetDbmod@@YAHPEAVAcDbDatabase@@H@Z",
            CallingConvention = CallingConvention.Cdecl)]
         public static extern int acdbSetDbmod(IntPtr database, int newval);
      }

      /// <summary>
      /// Extension method wrapper for the Database class. 
      /// </summary>
      
      public static int SetDbmod(this Database database, int newval)
      {
         return NativeMethods.acdbSetDbmod(Validate(database).UnmanagedObject, newval);
      }

      static Database Validate(Database database, [CallerArgumentExpression(nameof(database))] string name = "database")
      {
         if(database is null)
            throw new ArgumentNullException(name);
         if(database.IsDisposed)
            throw new ObjectDisposedException(name);
         if(Database.IdFromDb(database) == 0)
            throw new ArgumentException($"{name}: Underlying AcDbDatabase was destroyed");
         return database;
      }


   }
}