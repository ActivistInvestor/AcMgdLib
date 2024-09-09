/// RibbonStateEventArgs.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license
/// 

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Windows;

namespace Autodesk.AutoCAD.Ribbon.Extensions
{
   public delegate void RibbonStateEventHandler(object sender, RibbonStateEventArgs e);

   public class RibbonStateEventArgs : EventArgs
   {
      public RibbonStateEventArgs(RibbonState state)
      {
         this.State = state;
      }

      /// <summary>
      /// Conditionally adds one or more tabs to the Ribbon
      /// if they are not already present on it.
      /// </summary>
      /// <param name="items">One or more RibbonTab instances</param>
      /// <returns>The number of tabs added to the ribbon</returns>

      public int AddTabs(params RibbonTab[] items)
      {
         return AddTabs((IEnumerable<RibbonTab>)items);
      }

      public int AddTabs(IEnumerable<RibbonTab> items)
      {
         if(RibbonControl == null)
            return 0;
         if(items == null)
            throw new ArgumentNullException(nameof(items));
         return RibbonControl.TryAddTabs(items.ToArray());
      }

      public RibbonState State { get; private set; }
      public RibbonPaletteSet RibbonPaletteSet =>
         RibbonServices.RibbonPaletteSet;
      public RibbonControl RibbonControl =>
         RibbonPaletteSet?.RibbonControl;
   }

}

