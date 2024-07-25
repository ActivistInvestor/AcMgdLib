using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// This file is current just a placeholder for extension
/// methods that are scheduled to be added to this library

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static partial class DBObjectExtensions
   {
      public static T TryUpgradeOpen<T>(this T target, Transaction trans = null) where T : DBObject
      {
         Assert.IsNotNullOrDisposed(target, nameof(target));
         if(!target.IsWriteEnabled)
         {
            if(trans == null)
               target.UpgradeOpen();
            else
               trans.GetObject(target.ObjectId, OpenMode.ForWrite, false, true);
         }
         return target;
      }

   }
}
