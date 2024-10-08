﻿/// AcDbNativeMethods.cs
/// 
/// Activist Investor / Tony T
///
/// Distributed under the terms of the MIT license.

using System;
using System.Diagnostics.Extensions;
using System.Runtime.InteropServices;
using AcRx = Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using System.Runtime.CompilerServices;

namespace Autodesk.AutoCAD.Runtime.NativeInterop
{
   /// <summary>
   /// Dynamic DLL import example, uses The DllImport class
   /// to dynamically load acdbGetAdsName(), on any release 
   /// of AutoCAD from 2013 to 2025 from a single code base.
   /// 
   /// This overcomes the problem of using [DllImport] to 
   /// import APIs from dlls with version-dependent file 
   /// names. Since you must explicitly specify the name
   /// of the dll, you can't use the same assembly across
   /// different product releases in which the name of the
   /// DLL differs (most-notoriously, acdbXX.dll).
   /// 
   /// So for example, a build that targets releases of
   /// AutoCAD that use acdb19.dll, cannot be used with
   /// releases of AutoCAD that use acdb20.dll, and so
   /// forth, and in some cases, it may be due to nothing 
   /// other than use of [DllImport] to import functions 
   /// from acdbxx.dll.
   /// 
   /// For example:
   /// 
   ///   [DllImport("acdb19.dll", CallingConvention = 
   ///      CallingConvention.Cdecl, 
   ///      EntryPoint = "someFunction")]
   ///
   /// Nothing more than the above use of [DllImport] 
   /// makes the assembly that contains it dependent on
   /// acdb19.dll, which means that it cannot be used 
   /// with releases of AutoCAD that use acdb20.dll, or 
   /// any other version of acdbXX.dll. 
   /// 
   /// Dynamically-loading exported native methods from
   /// a dll with a version-dependent name solves that
   /// problem, and the DllImport class simplifies doing 
   /// that.
   /// 
   /// Dynamic loading of functions is only useful if the
   /// dll which they are being imported from has a version-
   /// dependent filename (as is the case with acdbXX.dll).
   /// For example, for functions exported by accore.dll,
   /// there is no benefit to dynamic loading.
   /// 
   /// Note that in this example, 32 bit product support 
   /// is completely off the table.
   /// 
   /// This class demonstrates the basic pattern used to
   /// dynamically load exported native methods without 
   /// the use of the [DllImport] attribute. It uses the
   /// acdbGetAdsName() function as an example.
   /// 
   /// The process of dynamically loading methods from
   /// acdbXX.dll invovles the following:
   /// 
   /// 1. Define a delegate type with the managed signature
   ///    of the exported native method. In the following
   ///    example, this is 'acdbGetAdsNameFunc'. It must
   ///    have the same signature as the equivalent method 
   ///    having the [DllImport] attribute applied to it:
   ///    
   ///       delegate ErrorStatus acdbGetAdsNameFunc(out AdsName ename, ObjectId id);
   ///    
   /// 2. Declare a static variable of the defined delegate 
   ///    type. In the example below, this is 'acdbGetAdsName'.
   ///    This is the variable which the imported function
   ///    will ultimately be assigned to, and through which
   ///    it can be called:
   ///    
   ///       static acdbGetAdsNameFunc acdbGetAdsName;
   /// 
   /// 3. Assign the entry point/export name of the native
   ///    API (which is the same value that is passed to 
   ///    the [DllImport] attribute's EntryPoint argument)
   ///    to a static const variable. In the example below, 
   ///    the string is assigned to 'acdbGetAdsName64':
   ///    
   ///       const string acdbGetAdsName64 = 
   ///          "?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z";
   /// 
   /// 3. Call DllImport.AcDbImport(acdbGetAdsName64)
   ///    using the delegate type as the generic argument,
   ///    and the entry point name as the sole argument,
   ///    and assign the result to the delegate variable
   ///    defined in step 2 above:
   ///    
   ///       acdbGetAdsName = DllImport.AcDbImport<acdbGetAdsNameFunc>(acdbGetAdsName64);
   /// 
   ///    Alternatively, you can use the Load() extension 
   ///    method that targets the delegate type, to simplify 
   ///    the above method call:
   ///    
   ///       acdbGetAdsName = acdbGetAdsName.Load(acdbGetAdsName64);
   ///    
   /// Once complete, the delegate variable acdbGetAdsName
   /// represents the dynamically-loaded native method, and 
   /// can be used to invoke it:
   /// 
   ///    
   ///      ObjectId id = ...
   ///      AdsName ename;
   ///      ErrorStatus es;
   ///      es = acdbGetAdsName(out ename, id);
   /// 
   /// In the example below, the acdbGetAdsName() native API
   /// is dynamically imported from acdbXX.dll. It is used
   /// by the managed wrapper for the native acdbEntGet() 
   /// method (which is not dynamically loaded, because it 
   /// lives in a dll without a version-dependent name).
   /// 
   /// By using this strategy for importing functions from
   /// acdbxx.dll, one can avoid a dependence on specific
   /// versions of that dll, and allows the same code base
   /// to be used with any supported release, regardless of
   /// what specific filename acdbxx.dll has.
   /// </summary>

   public static class AcDbNativeMethods
   {
      const string ACCORE_DLL = "accore.dll";

      //const string acdbGetAdsName64 =
      //   "?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z";

      // Docs not updated yet - The entry point can be
      // specified by applying the EntryPointAttribute
      // to the delegate type:

      /// <summary>
      /// acdbGetDbmod():
      /// </summary>

      [EntryPoint("?acdbGetDbmod@@YAHPEAVAcDbDatabase@@@Z")]     // entry point
      delegate int acdbGetDbmodFunc(IntPtr database);            // delegate type
      static acdbGetDbmodFunc acdbGetDbmod = acdbGetDbmodLoader; // initial assignment
      static int acdbGetDbmodLoader(IntPtr database)             // loader proxy
      {
         acdbGetDbmod = acdbGetDbmod.Load();
         return acdbGetDbmod(database);
      }

      /// <summary>
      /// acdbSetDbmod():
      /// This import doesn't use the EntryPoint attribute and
      /// instead supplies it to the Load() extension method:
      /// </summary>

      delegate int acdbSetDbmodFunc(IntPtr database, int newval);
      static acdbSetDbmodFunc acdbSetDbmod = acdbSetDbmodLoader;
      static int acdbSetDbmodLoader(IntPtr database, int newVal)
      {
         acdbSetDbmod = acdbSetDbmod.Load("?acdbSetDbmod@@YAHPEAVAcDbDatabase@@H@Z");
         return acdbSetDbmod(database, newVal);
      }

      /// <summary>
      /// acdbGetAdsName()
      /// </summary>

      [EntryPoint("?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z")]
      delegate AcRx.ErrorStatus acdbGetAdsNameFunc(out AdsName ename, ObjectId id);
      static acdbGetAdsNameFunc acdbGetAdsName = acdbGetAdsNameLoader;
      static AcRx.ErrorStatus acdbGetAdsNameLoader(out AdsName ename, ObjectId id)
      {
         acdbGetAdsName = acdbGetAdsName.Load();
         return acdbGetAdsName(out ename, id);
      }

      /// <summary>
      /// The above proxy/stub loader function is initially assigned to 
      /// acdbGetAdsName, so that the first time that delegate is called, 
      /// the native acdbGetAdsName method is loaded and assigned to the
      /// same identifier the loader is assigned to, allowing the native
      /// function to subsequently be called directly. The loader's job 
      /// is to load the exported API and then execute it. This design 
      /// provides for 'just-in-time' loading of exported functions the 
      /// first time they are called. If an exported API is never called,
      /// it is never loaded.
      /// </summary>

      /// <summary>
      /// acdbEntGet doesn't need to be dynamically-
      /// loaded, because accore.dll is not a version-
      /// dependent filename.
      /// </summary>

      [System.Security.SuppressUnmanagedCodeSecurity]
      [DllImport(ACCORE_DLL, 
         CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "acdbEntGet")]
      static extern IntPtr acdbEntGet(AdsName ename);

      /// <summary>
      /// The public managed wrappers for acdbGetAdsName(),
      /// acdbGetDbmod() and acdbSetDbmod():
      /// </summary>

      public static AdsName GetAdsName(this ObjectId id)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         AdsName result;
         var es = acdbGetAdsName(out result, id);
         if(es != AcRx.ErrorStatus.OK)
            throw new AcRx.Exception(es);
         AcRx.ErrorStatus.InvalidAdsName.ThrowIf(result.IsNull());
         return result;
      }

      public static int GetDBMod(this Database db)
      {
         Assert.IsValid(db);
         return acdbGetDbmod(db.UnmanagedObject);
      }

      public static int SetDBMod(this Database db, int value)
      {
         Assert.IsValid(db);
         return acdbSetDbmod(db.UnmanagedObject, value);
      }

      /// <summary>
      /// Helper extension method to check if an AdsName is null
      /// </summary>

      public static bool IsNull(this AdsName ename)
      {
         return ename.name1 == 0L && ename.name2 == 0L;
      }

      /// <summary>
      /// Public managed wrappers for acdbEntGet():
      /// </summary>
      /// <param name="id"></param>
      /// <returns></returns>

      public static ResultBuffer EntGet(this ObjectId id)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         var result = acdbEntGet(GetAdsName(id));
         if(result != IntPtr.Zero)
            return ResultBuffer.Create(result, true);
         return null;
      }

      public static ResultBuffer EntGet(this DBObject dbObj)
      {
         Assert.IsNotNullOrDisposed(dbObj, nameof(dbObj));
         return EntGet(dbObj.ObjectId);
      }
   }



}
