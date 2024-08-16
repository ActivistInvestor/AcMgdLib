/// ******* EXPERIMENTAL *******
/// DBOpenClosTransaction.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// Note: This file is intentionally kept free of any
/// dependence on AcMgd/AcCoreMgd.

using Autodesk.AutoCAD.Runtime;
using System;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A specialization of DatabaseTransaction that 
   /// wraps an OpenCloseTransaction.
   /// 
   /// This code is largely-untested and not suitable 
   /// for production use.
   /// 
   /// This class looks and acts like the DatabaseTransaction, 
   /// but internally, it uses an OpenCloseTransaction, which
   /// addresses a need to use the latter in certain situations.
   /// </summary>

   public class DBOpenCloseTransaction : DatabaseTransaction
   {
      OpenCloseTransaction trans;

      public DBOpenCloseTransaction(Database database, bool asWorkingDatabase = true) 
         : base(database, asWorkingDatabase)
      {
      }

      public override Transaction Transaction => trans;

      protected override void StartTransaction(TransactionManager manager)
      {
         trans = new OpenCloseTransaction();
      }

      public override void Commit()
      {
         trans.Commit();
         Interop.SetAutoDelete(this, false);
      }

      public override void Abort()
      {
         trans.Abort();
         Interop.SetAutoDelete(this, false);
      }

      public override void AddNewlyCreatedDBObject(DBObject obj, bool add)
      {
         trans.AddNewlyCreatedDBObject(obj, add);
      }

      public override DBObject GetObject(ObjectId id, OpenMode mode, bool openErased, bool forceOpenOnLockedLayer)
      {
         ErrorStatus.WrongDatabase.ThrowIf(id.Database != this.Database);
         return trans.GetObject(id, mode, openErased, forceOpenOnLockedLayer);
      }

      public override DBObject GetObject(ObjectId id, OpenMode mode)
      {
         ErrorStatus.WrongDatabase.ThrowIf(id.Database != this.Database);
         return trans.GetObject(id, mode);
      }

      public override DBObject GetObject(ObjectId id, OpenMode mode, bool openErased)
      {
         ErrorStatus.WrongDatabase.ThrowIf(id.Database != this.Database);
         return trans.GetObject(id, mode, openErased);
      }

      public override int NumberOfOpenedObjects => trans.NumberOfOpenedObjects;

      public override TransactionManager TransactionManager =>
         throw new NotImplementedException();

      public override bool IsOpenCloseTransaction => true;
   }

}




