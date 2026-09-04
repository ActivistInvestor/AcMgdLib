

using System;

/// AcNativeLibraryResolverAttribute.cs  
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license
namespace AcMgdLib.Runtime
{
   /// <summary>
   /// Provides a declarative means of registering assemblies for dynamic 
   /// DllImport dll name resolution using an Assembly-level attribute. 
   /// 
   /// This attribute can be applied to an assembly to automatically register 
   /// it for DllImport module name resolution, thereby eliminating the need 
   /// to explicitly call AcNativeLibraryResolver.Register() for the Assembly.
   /// 
   /// To enable this attribute, the AcNativeLibraryResolverAttribute's
   /// Initialize() method must be called prior to any DllImport calls 
   /// that use wildcard dllnames. This is typically done from within the
   /// initialize() method of the IExtensionApplication implementation of 
   /// the AutoCAD .NET assembly.
   /// 
   /// </summary>

   [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
   public class AcNativeLibraryResolverAttribute : Attribute
   {
      static bool initialized = false;
      public static void Initialize()
      {
         if(initialized)
            return;
         AssemblyAttribute<AcNativeLibraryResolverAttribute>.Initialize((asm, _) 
            => AcNativeLibraryResolver.Register(asm));
         initialized = true;
      }
   }
}



