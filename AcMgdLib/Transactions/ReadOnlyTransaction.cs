/// ReadOnlyTransaction.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// An OpenCloseTransaction that always commits,
   /// and is intended exclusively for read-only use.
   /// </summary>

   class ReadOnlyTransaction : OpenCloseTransaction
   {
      protected override void Dispose(bool disposing)
      {
         if(!this.IsDisposed)
            this.Abort();
         base.Dispose(disposing);
      }

      public T GetObject<T>(ObjectId id) where T : DBObject
      {
         return (T)base.GetObject(id, OpenMode.ForRead, false, false);
      }

      public override void Abort()
      {
         base.Commit();
      }
   }
}



