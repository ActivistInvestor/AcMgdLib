/// RibbonControlExtensions.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license
/// 

using System;
using System.Collections.Generic;
using Autodesk.Windows;

namespace Autodesk.AutoCAD.Ribbon
{
   public static partial class RibbonControlExtensions
   {
      /// <summary>
      /// An extension method targeting the RibbonControl, that 
      /// conditionally adds one or more tabs to the ribbon if 
      /// not already present.
      /// </summary>
      /// <param name="items">One or more RibbonTab instances</param>
      /// <returns>The number of tabs added to the ribbon</returns>

      public static int TryAddTabs(this RibbonControl ribbon, params RibbonTab[] items)
      {
         if(items == null)
            throw new ArgumentNullException(nameof(items));
         return TryAddTabs(ribbon, (IEnumerable<RibbonTab>)items);
      }

      public static int TryAddTabs(this RibbonControl ribbon, IEnumerable<RibbonTab> items)
      {
         if(ribbon == null)
            throw new ArgumentNullException(nameof(ribbon));
         if(items == null)
            throw new ArgumentNullException(nameof(items));
         int result = 0;
         var tabs = ribbon.Tabs;
         foreach(RibbonTab tab in items)
         {
            if(tab == null)
               throw new ArgumentException("null element");
            if(!tabs.Contains(tab))
            {
               ++result;
               tabs.Add(tab);
            }
         }
         return result;
      }

   }

}