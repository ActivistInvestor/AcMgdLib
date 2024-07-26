using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcRx = Autodesk.AutoCAD.Runtime;

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

      /// <summary>
      /// An extension that overloads the DBObject.UpgradeOpen() method,
      /// that takes a Transaction argument. The purpose of this method
      /// is to simplify upgrading the OpenMode of an Entity on a locked 
      /// layer, which is not supported by DBObject.UpgradeOpen().
      /// </summary>

      public static void UpgradeOpen(this DBObject target, Transaction trans)
      {
         Assert.IsNotNullOrDisposed(target, nameof(target));
         Assert.IsNotNull(trans, nameof(trans));
         AcRx.ErrorStatus.NullObjectId.ThrowIf(target.ObjectId.IsNull);
         if(!target.IsWriteEnabled)
            trans.GetObject(target.ObjectId, OpenMode.ForWrite, false, true);
      }

      public static bool IsNullOrDisposed(this DBObject target) 
         => target == null || target.IsDisposed;

   }
}
