﻿/// MinimalRibbonEventManagerExample.cs
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
using Autodesk.Windows;

// TODO: This must be uncommented, and if needed
// changed to the actual class that implements
// IExtensionApplication:

// [assembly: ExtensionApplication(typeof(Namespace1.MyApplication))]

namespace Namespace1
{
   public class MyApplication : IExtensionApplication
   {
      static RibbonTab ribbonTab = null;

      public void Initialize()
      {
         RibbonEventManager.InitializeRibbon += LoadMyRibbonContent;
      }

      private void LoadMyRibbonContent(object sender, RibbonStateEventArgs e)
      {
         /// This example creates a RibbonTab the first time 
         /// this method is called, and stores it in a static 
         /// variable for use on this and all subsequent calls 
         /// to this method. 
         /// 
         /// This method may be called any number of times 
         /// during an AutoCAD session whenever ribbon content 
         /// must be added to the ribbon.
         
         if(ribbonTab == null)
         {
            ribbonTab = new RibbonTab();
            ribbonTab.Name = "MyTab";
            ribbonTab.Id = "ID_MyTab";
            ribbonTab.Title = "MyTab";
         }
        
         /// Add the tab to the ribbon on every call to
         /// this method:
        
         e.RibbonControl.Tabs.Add(ribbonTab);
      }

      public void Terminate()
      {
      }
   }
}
