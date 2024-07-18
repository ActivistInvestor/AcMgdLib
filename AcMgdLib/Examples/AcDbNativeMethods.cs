using System;
using System.Runtime.InteropServices;
using AcRx = Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;

namespace Autodesk.AutoCAD.Runtime.InteropServices
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
   /// other than use of [DllImport] to import methods 
   /// from acdbxx.dll.
   /// 
   /// For example:
   /// 
   ///   [DllImport("acdb19.dll", CallingConvention = 
   ///      CallingConvention.Cdecl, EntryPoint = "someFunction")]
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
   /// Note that 32 bit product support is completely off
   /// the table.
   /// 
   /// This class demonstrates the basic pattern used to
   /// dynamically load exported native methods without 
   /// the use of the [DllImport] attribute. 
   /// 
   /// The process of dynamically loading methods from
   /// acdbXX.dll invovles the following:
   /// 
   /// 1. Define a delegate having the managed signature
   ///    of the exported native method. In the following
   ///    example, this is 'acdbGetAdsNameFunc'. It must
   ///    have the same signature as the equivalent method 
   ///    having the [DllImport] attribute applied to it:
   ///    
   ///      delegate ErrorStatus acdbGetAdsNameFunc(out AdsName ename, ObjectId id);
   ///    
   /// 2. Declare a static variable of the defined delegate 
   ///    type. In the example below, this is 'acdbGetAdsName':
   ///    
   ///      static acdbGetAdsNameFunc acdbGetAdsName;
   /// 
   /// 3. Assign the entry point/export name of the native
   ///    API, which is the same value that is passed to 
   ///    the [DllImport] attribute's EntryPoint argument.
   ///    In the example below, the string is assigned to
   ///    'acdbGetAdsName64':
   ///    
   ///       const string acdbGetAdsName64 = 
   ///          "?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z";
   /// 
   /// 3. Call DllImport.AcDbImport(acdbGetAdsName64)
   ///    using the delegate type as the generic argument,
   ///    and the entry point name as the sole argument,
   ///    and assign the result to the delegate variable
   ///    defined above:
   ///    
   ///      acdbGetAdsName = DllImport.AcDbImport<acdbGetAdsNameFunc>(acdbGetAdsName64);
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
   /// </summary>

   public static class AcDbNativeMethods
   {
      const string acdbGetAdsName64 =
         "?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z";
      delegate AcRx.ErrorStatus acdbGetAdsNameFunc(out AdsName ename, ObjectId id);
      static acdbGetAdsNameFunc acdbGetAdsName = acdbGetAdsNameLoader;

      /// <summary>
      /// This proxy/stub loader function is initially assigned to 
      /// acdbGetAdsName, so that the first time that method is called, 
      /// the real acdbGetAdsName method is loaded and assigned to the
      /// same identifier, so that it is subsequently called directly.
      /// </summary>

      static AcRx.ErrorStatus acdbGetAdsNameLoader(out AdsName ename, ObjectId id)
      {
         acdbGetAdsName = DllImport.AcDbImport<acdbGetAdsNameFunc>(acdbGetAdsName64);
         AcConsole.WriteLine("acdbGetAdsName() loaded");
         return acdbGetAdsName(out ename, id);
      }

      /// acdbEntGet doesn't need to be dynamically-loaded:
      [System.Security.SuppressUnmanagedCodeSecurity]
      [DllImport("accore.dll", CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "acdbEntGet")]
      static extern IntPtr acdbEntGet(AdsName ename);

      /// <summary>
      /// The public managed wrapper for acdbGetAdsName():
      /// </summary>

      public static AdsName GetAdsName(ObjectId id)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         AdsName result;
         var es = acdbGetAdsName(out result, id);
         if(es != AcRx.ErrorStatus.OK)
            throw new AcRx.Exception(es);
         AcRx.ErrorStatus.InvalidAdsName.ThrowIf(result.IsNull());
         return result;
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
         return dbObj.ObjectId.EntGet();
      }
   }
}
