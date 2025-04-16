/// AcDbNativeMethods.cs
/// 
/// Activist Investor / Tony T
///
/// Distributed under the terms of the MIT license.

using System;
using System.Runtime.InteropServices;
using AcMgdLib.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime.Extensions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.Runtime.NativeInterop
{
   /// <summary>
   /// Dynamic DLL import example, that uses The DllImport 
   /// class from this library to dynamically load the 
   /// native ObjectARX apis acdbGetAdsName(); acdbGetDbmod();
   /// and acdbSetDbmod(), on any release of AutoCAD from 2013 
   /// to 2026 or later, from a single code base.
   /// 
   /// This overcomes the problem of using the [DllImport] 
   /// attribute to P/Invoke APIs from AutoCAD dlls with 
   /// version-dependent file names. 
   /// 
   /// Since you must explicitly specify the name of the 
   /// dll, you can't use the same assembly across different 
   /// product releases in which the name of the DLL differs 
   /// (most-notoriously, acdbXX.dll).
   /// 
   /// So for example, a build that targets releases of
   /// AutoCAD that use acdb23.dll, cannot be used with
   /// releases of AutoCAD that use acdb24.dll, and so
   /// forth, and in some cases, it may be due to nothing 
   /// other than the use of the [DllImport] attribute to 
   /// import functions from acdbxx.dll.
   /// 
   /// For example:
   /// 
   ///   [DllImport("acdb23.dll", CallingConvention = 
   ///      CallingConvention.Cdecl, 
   ///      EntryPoint = "someFunction")]
   ///
   /// Nothing more than the above use of [DllImport] 
   /// makes the assembly that contains it dependent on
   /// acdb23.dll (AutoCAD 2019/2020), which means that 
   /// the assembly cannot be used with other releases 
   /// of AutoCAD that do not use acdb23.dll.
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
   ///      delegate ErrorStatus acdbGetAdsNameFunc(out AdsName ename, ObjectId id);
   ///    
   /// 2. Declare a static variable of the defined delegate 
   ///    type. In the example below, this is 'acdbGetAdsName'.
   ///    This is the variable which the imported function
   ///    will ultimately be assigned to, and through which
   ///    it can be called:
   ///    
   ///      static acdbGetAdsNameFunc acdbGetAdsName;
   /// 
   /// 3. Assign the entry point/export name of the native
   ///    API (which is the same value that is passed to 
   ///    the [DllImport] attribute's EntryPoint argument)
   ///    to a static const variable. In the example below, 
   ///    the string is assigned to 'acdbGetAdsNameSym':
   ///    
   ///      const string acdbGetAdsNameSym = 
   ///         "?acdbGetAdsName@@YA?AW4ErrorStatus@Acad@@AEAY01_JVAcDbObjectId@@@Z";
   /// 
   /// 3. Call AcDbImport<acdbGetAdsNameFunc>(acdbGetAdsNameSym)
   ///    passing the delegate type defined above as the generic 
   ///    argument, and the entry point name as the sole argument,
   ///    and assign the result to the delegate variable defined 
   ///    in step 2 above:
   ///    
   ///       acdbGetAdsName = DllImport.AcDbImport<acdbGetAdsNameFunc>(acdbGetAdsNameSym);
   /// 
   ///    Alternatively, you can use the Load() extension 
   ///    method that targets the delegate type, to simplify 
   ///    the above method call:
   ///    
   ///       acdbGetAdsName = acdbGetAdsName.Load(acdbGetAdsNameSym);
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
   /// 
   /// While this file is presented as example code, the 
   /// native APIs it imports are used in various parts 
   /// of this library, which is dependent on them.
   /// 
   /// Another advantage offered by this scheme, is that
   /// native methods are imported using a just-in-time 
   /// pattern, such that if an imported native method is
   /// never called, it will never be imported. 
   /// 
   /// The just-in-time functionality is accomplished by
   /// using a loader proxy method that is assigned to the
   /// delegate that represents the imported native API.
   /// The first time that delegate is called, the proxy
   /// method loads the native method, invokes it, and then
   /// reassigns the native method to the delegate so that
   /// it will be called directly on all subsequent calls.
   /// </summary>

   /// Development notes:
   /// 
   /// Additional PInvokes yet to be implemented:
   /// 
   /// Acad::ErrorStatus acdbForceTextAdjust(const AcDbObjectIdArray& objIds)
   /// acdbForceAnnoAllVisible, bool __cdecl acdbForceAnnoAllVisible(class AcDbObject const * __ptr64,wchar_t const * __ptr64), ?acdbForceAnnoAllVisible@@YA_NPEBVAcDbObject@@PEB_W@Z, 0xb4c9bc, 3694, 3692, , 
   /// acdbForceErase, enum Acad::ErrorStatus __cdecl acdbForceErase(class AcDbObject * __ptr64), ? acdbForceErase@@YA? AW4ErrorStatus@Acad @@PEAVAcDbObject@@@Z, 0x40fd40, 3695, 3693, , 
   /// acdbForceOpenObjectOnLockedLayer, enum Acad::ErrorStatus __cdecl acdbForceOpenObjectOnLockedLayer(class AcDbObject * __ptr64 & __ptr64,class AcDbObjectId,enum AcDb::OpenMode,bool), ? acdbForceOpenObjectOnLockedLayer@@YA? AW4ErrorStatus@Acad @@AEAPEAVAcDbObject@@VAcDbObjectId @@W4OpenMode@AcDb @@_N@Z, 0xac6a8, 3696, 3694, , 
   /// acdbForcePreserveSourceDbForInsert, void __cdecl acdbForcePreserveSourceDbForInsert(class AcDbDatabase * __ptr64), ? acdbForcePreserveSourceDbForInsert@@YAXPEAVAcDbDatabase @@@Z, 0x913f2c, 3697, 3695, , 
   /// acdbForceTextAdjust, enum Acad::ErrorStatus __cdecl acdbForceTextAdjust(class AcArray<class AcDbObjectId,class AcArrayMemCopyReallocator<class AcDbObjectId> > const & __ptr64), ? acdbForceTextAdjust@@YA? AW4ErrorStatus@Acad @@AEBV?$AcArray @VAcDbObjectId@@V?$AcArrayMemCopyReallocator @VAcDbObjectId@@@@@@@Z, 0x799598, 3698, 3696, , 
   /// acdbForceUpgradeOpenOnLockedLayer, enum Acad::ErrorStatus __cdecl acdbForceUpgradeOpenOnLockedLayer(class AcDbEntity * __ptr64), ? acdbForceUpgradeOpenOnLockedLayer@@YA? AW4ErrorStatus@Acad @@PEAVAcDbEntity@@@Z, 0xa948fc, 3699, 3697, , 
   /// acdbSetForceAnnoAllVisible, enum Acad::ErrorStatus __cdecl acdbSetForceAnnoAllVisible(class AcDbObject * __ptr64,bool,wchar_t const * __ptr64), ? acdbSetForceAnnoAllVisible@@YA? AW4ErrorStatus@Acad @@PEAVAcDbObject@@_NPEB_W @Z, 0xb4ca04, 4112, 4108, , 
   /// acdbiForceUndo, enum Acad::ErrorStatus __cdecl acdbiForceUndo(class AcDbDatabase * __ptr64), ? acdbiForceUndo@@YA? AW4ErrorStatus@Acad @@PEAVAcDbDatabase@@@Z, 0xaec0d4, 4261, 4257, , 
   /// acdbiForceUndo, enum Acad::ErrorStatus __cdecl acdbiForceUndo(class AcDbDatabase * __ptr64,bool), ? acdbiForceUndo@@YA? AW4ErrorStatus@Acad @@PEAVAcDbDatabase@@_N @Z, 0xaec0dc, 4262, 4258, , 
   /// 


   public static class AcDbNativeMethods
   {
      const string ACCORE_DLL = "accore.dll";
      const string ACAD_EXE = "acad.exe";

      // Docs not updated yet - The entry point can be
      // specified by applying the [EntryPointAttribute]
      // (included in this library) to the delegate type.

      ///////////////////////////////////////////////////////////////
      /// <summary>
      /// acdbGetDbmod():
      /// 
      /// Managed wrapper: Database.GetDBMod()
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
      /// Indicates if a long transaction (REFEDIT) is active
      /// 
      /// Managed wrapper: 
      /// </summary>
      /// <returns></returns>

      [EntryPoint("?acdbIsLongTransactionActive@@YA_NXZ")]
      internal delegate bool acdbIsLongTransactionActiveFunc();
      internal static acdbIsLongTransactionActiveFunc acdbIsLongTransactionActive = () =>
      {
         acdbIsLongTransactionActive = acdbIsLongTransactionActive.Load();
         return acdbIsLongTransactionActive();
      };

      /// <summary>
      /// Indicates if the database object referenced by the
      /// given ObjectId is a member of the current working set,
      /// or false if there is no current working set.
      /// 
      /// Managed wrapper: ObjectId.IsInWorkingSet()
      /// </summary>
      /// <param name="id"></param>
      /// <returns></returns>
      
      [EntryPoint("?acdbIsInLongTransaction@@YA_NVAcDbObjectId@@@Z")]
      internal delegate bool acdbIsInLongTransactionFunc(ObjectId id);
      internal static acdbIsInLongTransactionFunc acdbIsInLongTransaction = (id) =>
      {
         acdbIsInLongTransaction = acdbIsInLongTransaction.Load();
         return acdbIsInLongTransaction(id);
      };

      ///////////////////////////////////////////////////////////////
      /// <summary>
      /// acdbSetDbmod():
      /// 
      /// Managed wrapper: Database.SetDBMod()
      /// 
      /// This import doesn't use the EntryPoint attribute, and
      /// instead passes the entry point to the Load() extension 
      /// method, for demonstration purposes:
      /// </summary>

      delegate int acdbSetDbmodFunc(IntPtr database, int newval);
      static acdbSetDbmodFunc acdbSetDbmod = acdbSetDbmodLoader;
      static int acdbSetDbmodLoader(IntPtr database, int newVal)
      {
         acdbSetDbmod = acdbSetDbmod.Load("?acdbSetDbmod@@YAHPEAVAcDbDatabase@@H@Z");
         return acdbSetDbmod(database, newVal);
      }

      ///////////////////////////////////////////////////////////////
      /// <summary>
      /// acdbGetAdsName()
      /// 
      /// Managed wrapper: ObjectId.GetAdsName()
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
      /// Not reliable - returns true when called from
      /// object.Finalize() on the GC's background thread.
      /// Use AcRx.SynchronizationContext.Current != null instead.
      /// </summary>
      /// <returns></returns>

      [EntryPoint("?acdbInMainThread@@YA_NXZ")]
      internal delegate bool acdbInMainThreadFunc();
      internal static acdbInMainThreadFunc acdbInMainThread = acdbInMainThreadLoader;
      static bool acdbInMainThreadLoader()
      {
         acdbInMainThread = acdbInMainThread.Load();
         return acdbInMainThread();
      }

      /// <summary>
      /// Gets the Document associated with a given Database.
      /// If a Database is created via the new() constructor and
      /// the NoDocument argument is false, that database will
      /// be returned by this method, even if it is not the 
      /// value of the Document's Database property.
      /// 
      /// </summary>
      /// <param name="db"></param>
      /// <returns></returns>
      [EntryPoint("?acdbiGetDocument@@YAPEAVAcApDocument@@PEAVAcDbDatabase@@@Z")]
      internal delegate IntPtr acdbiGetDocumentFunc(IntPtr db);
      internal static acdbiGetDocumentFunc acdbiGetDocument = acdbiGetDocumentLoader;
      static IntPtr acdbiGetDocumentLoader(IntPtr db)
      {
         return (acdbiGetDocument = acdbiGetDocument.Load())(db);
      }

      /// <summary>
      /// acdbEntGet():
      /// 
      /// Managed wrapper: ObjectId.EntGet()
      /// 
      /// acdbEntGet doesn't need to be dynamically-loaded, because 
      /// accore.dll doesn't have a version-dependent filename.
      /// </summary>

      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "acdbEntGet")]
      static extern IntPtr acdbEntGet(AdsName ename);


      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         CharSet = CharSet.Unicode,
         EntryPoint = "?acedPostCommand@@YA_NPEB_W@Z")]
      public static extern bool acedPostCommand(string cmd);

      public static void PostCancel()
      {
         acedPostCommand("CANCELCMD");
      }

      [System.Security.SuppressUnmanagedCodeSecurity]
      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "acdbEntMod")]
      static extern void acdbEntMod(IntPtr rb);

      /// <summary>
      /// The public managed wrappers for acdbGetAdsName(),
      /// acdbGetDbmod() acdbSetDbmod(), and acdbEntGet()
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
      /// Avoids operating on a disposed or destroyed database.
      /// If the database is no longer valid, this returns false
      /// and does NOT throw an exception.
      /// </summary>

      public static bool TrySetDBMod(this Database db, int value)
      {
         bool result = db.IsValid(true);
         if(result)
            acdbSetDbmod(db.UnmanagedObject, value);
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
         return EntGet(dbObj.ObjectId);
      }

      public static void EntMod(this ResultBuffer rb)
      {
         Assert.IsNotNullOrDisposed(rb);
         acdbEntMod(rb.UnmanagedObject);
      }

      public static void EntMod(this TypedValueList values)
      {
         Assert.IsNotNull(values);
         EntMod(values);
      }

      public static Document TryGetDocumentFast(this Database db)
      {
         Assert.IsNotNullOrDisposed(db);
         IntPtr result = acdbiGetDocument(db.UnmanagedObject);
         if(result == IntPtr.Zero)
            return null;
         return Document.Create(result);
      }

   }

   public static partial class DocumentCollectionExtensions
   {
      const string ACCORE_DLL = "accore.dll";
      [DllImport(ACCORE_DLL, CallingConvention = CallingConvention.Cdecl,
         EntryPoint = "?AcadIsQuitting@@YA_NXZ")]
      [return: MarshalAs(UnmanagedType.U1)]
      extern static bool AcadIsQuitting();

      /// <summary>
      /// This extension method can be used from the Dispose()
      /// method of a class managed by DocData<T>, to detect if
      /// the instance is being disposed because AutoCAD is in
      /// the process of shutting down.
      /// </summary>
      /// <param name="docs"></param>
      /// <returns></returns>

      public static bool IsQuitting(this DocumentCollection docs)
      {
         return AcadIsQuitting();
      }

      public static bool IsQuitting() => AcadIsQuitting();

      //public static bool InMainThread(this DocumentCollection docs)
      //   => AcDbNativeMethods.acdbInMainThread();
      public static bool InMainThread(this DocumentCollection docs)
         => AcRx.SynchronizationContext.Current != null;

   }

   public static partial class ObjectIdExtensions
   {
      /// <summary>
      /// A much faster way to determine if a given Object
      /// is in the current working set (any document).
      /// </summary>
      /// <param name="id"></param>
      /// <returns></returns>
      
      public static bool IsInWorkingSet(this ObjectId id)
      {
         if(id.IsNull)
            return false;
         return AcDbNativeMethods.acdbIsInLongTransaction(id);
      }


   }
}
