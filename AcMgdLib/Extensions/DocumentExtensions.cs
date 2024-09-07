using Autodesk.AutoCAD.ApplicationServices;
using System.Diagnostics.Extensions;

namespace Autodesk.AutoCAD.ApplicationServices.Extensions
{
   public static partial class DocumentExtensions
   {
      public static bool IsLocked(this Document doc)
      {
         Assert.IsNotNullOrDisposed(doc, nameof(doc));
         return (doc.LockMode() & DocumentLockMode.NotLocked) == DocumentLockMode.NotLocked;
      }
   }
}
