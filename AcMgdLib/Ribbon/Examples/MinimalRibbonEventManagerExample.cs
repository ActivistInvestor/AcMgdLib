/// RibbonEventManagerExample.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license
/// 
/// An minimal example showing the use of the 
/// RibbonEventManager class.
/// 
/// This class demonstrates the absolute bare
/// minimum code needed to ensure that content
/// is always present on the ribbon.
/// 
/// The only requirement is to add code to the
/// LoadMyRibbonContent() method that creates
/// and adds content to the ribbon.
/// 
/// The RibbonStateEventArgs passed to that method 
/// can be used to access the RibbonControl.


using Autodesk.AutoCAD.Ribbon.Extensions;
using Autodesk.AutoCAD.Runtime;

// TODO: This must be uncommented, and if needed
// changed to specify the actual class that
// implements IExtensionApplication:

// [assembly: ExtensionApplication(typeof(Namespace1.MyApplication))]

namespace Namespace1
{
   public class MyApplication : IExtensionApplication
   {
      public void Initialize()
      {
         RibbonEventManager.InitializeRibbon += LoadMyRibbonContext;
      }

      private void LoadMyRibbonContext(object sender, RibbonStateEventArgs e)
      {
         /// TODO: Add content to ribbon here
      }

      public void Terminate()
      {
      }
   }
}
