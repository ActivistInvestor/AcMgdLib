/// ProtectedTransaction.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

using System;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A wrapper for a Transaction that prevents it
   /// from being Comitted, Aborted or Disposed.
   /// 
   /// This wrapper is for use when a Transaction is
   /// exposed via a Property, or passed as an argument 
   /// in a method call, and the passed transaction must
   /// not be ended or disposed by the callee, or the
   /// property accessor.
   /// 
   /// If you pass a Transaction to a method, and want
   /// to ensure that the called method does not Commit, 
   /// Abort, or dispose the transaction, you can do this:
   /// 
   /// Given a method that takes a transaction:
   /// 
   ///   public void SomeMethod(Transaction trans)
   ///   {
   ///      // Code in this method may erroneously
   ///      // abort, commit, or dispose the argument,
   ///      // which the caller explicitly wants to 
   ///      // prohibit, and where any attempt to do
   ///      // those things should be treated as an
   ///      // exception.
   ///   }
   /// 
   /// The above method can be called and passed a
   /// transaction that it cannot abort, commit, or
   /// dispose without triggering an exception:
   /// 
   ///   Transaction trans = // start a transaction
   ///   
   ///   Call SomeMethod() and pass it the transaction:
   ///   
   ///   SomeMethod(trans.AsProtected());
   ///   
   /// The ProtectedTransaction can be used just like
   /// any Transaction, with the sole exception that 
   /// attempting to end or dispose it will trigger an 
   /// exception. 
   /// 
   /// The original Transaction on which AsProtected()
   /// was invoked on remains unaffected and continues 
   /// to work. That original transaction must not be 
   /// discarded, as it is that Transaction that must 
   /// be ended or disposed.
   /// 
   /// Note that a ProtectedTransaction should never
   /// be determinstically-disposed. It should be left
   /// to the GC to garbage-collect it.
   ///   
   /// </summary>

   class ProtectedTransaction : Transaction
   {
      public ProtectedTransaction(Transaction trans)
         : base(new nint(-1), false)
      {
         Assert.IsNotNullOrDisposed(trans);
         Interop.DetachUnmanagedObject(this);
         Interop.AttachUnmanagedObject(this, trans.UnmanagedObject, trans.AutoDelete);
         GC.SuppressFinalize(this);
      }

      public override void Abort()
      {
         if(this.UnmanagedObject > 0)
            Interop.DetachUnmanagedObject(this);
         throw new InvalidOperationException("Transaction is read-only");
      }

      public override void Commit()
      {
         if(this.UnmanagedObject > 0)
            Interop.DetachUnmanagedObject(this);
         throw new InvalidOperationException("Transaction is read-only");
      }

      protected override void DeleteUnmanagedObject()
      {
      }

      protected override void Dispose(bool disposing)
      {
         if(this.UnmanagedObject > 0)
            Interop.DetachUnmanagedObject(this);
         if(disposing)
            throw new InvalidOperationException("Transaction is read-only");
      }
   }

   public static partial class TransactionExtensions
   {
      public static Transaction AsProtected(this Transaction trans)
      {
         Assert.IsNotNullOrDisposed(trans);
         return trans as ProtectedTransaction 
            ?? new ProtectedTransaction(trans);
      }
   }
}
