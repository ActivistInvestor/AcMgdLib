/// AcDbLinqHelpers.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for the AcDbLinq library.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Linq.Expressions;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public static partial class AcDbLinqHelpers
   { 

      /// Helper methods

      /// <summary>
      /// A common error is using the wrong Transaction manager
      /// to obtain a transaction for a Database that's not open
      /// in the editor. This API attempts to check that.
      /// 
      /// If the Transaction is a DatabaseServices.Transaction
      /// and the Transaction's TransactionManager is not the 
      /// Database's TransactionManager, an exception is thrown.
      /// 
      /// The check cannot be fully-performed without a depenence
      /// on AcMgd/AcCoreMgd.dll, but usually isn't required when
      /// using a Document's TransactionManager.
      /// </summary>
      /// <param name="db">The Database to check</param>
      /// <param name="trans">The Transaction to check against the Database</param>
      /// <exception cref="ArgumentNullException"></exception>
      /// <exception cref="ArgumentException"></exception>

      internal static void CheckTransaction(this Database db, Transaction trans)
      {
         Assert.IsNotNullOrDisposed(db, nameof(db));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         if(trans is OpenCloseTransaction)
            return;
         if(trans.GetType() != typeof(Transaction))
            return;   // can't perform this check without pulling in AcMgd/AcCoreMgd
         if(trans.TransactionManager != db.TransactionManager)
            throw new ArgumentException("Transaction not from this Database");
      }

      internal static void CheckTransaction(this ObjectId id, Transaction trans)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         if(trans is OpenCloseTransaction || trans is DBOpenCloseTransaction)
            return;
         var tm = TryGetTransactionManager(trans);
         if(tm != null && tm != id.Database.TransactionManager)
            throw new ArgumentException("Transaction not from this Database");
      }

      static TransactionManager TryGetTransactionManager(Transaction trans)
      {
         try
         {
            return trans.TransactionManager;
         }
         catch
         {
            return null;
         }
      }

      internal static void TryCheckTransaction(this object source, Transaction trans)
      {
         Assert.IsNotNull(source, nameof(source));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         if(trans is OpenCloseTransaction || trans is DBOpenCloseTransaction)
            return;
         if(!(trans is Transaction))
            return; // can't perform check without pulling in AcMgd/AcCoreMgd
         TransactionManager tm = TryGetTransactionManager(trans);
         if(source is DBObject obj && obj.Database is Database db
             && tm != null && tm != db.TransactionManager)
            throw new ArgumentException("Transaction not from this Database");
      }

      internal static void CheckTransaction(this DBObject obj, Transaction trans)
      {
         Assert.IsNotNullOrDisposed(obj,nameof(obj));
         CheckTransaction(obj.Database, trans);
      }

      /// <summary>
      /// Should be self-explanatory
      /// </summary>

      /// <summary>
      /// Disposes all the elements in the List sequence,
      /// and the List if it is an IDisposable. Useful with
      /// DBObjectCollection to ensure that all of the list
      /// retreived from it are disposed.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="source"></param>

      internal static void Dispose<T>(this IEnumerable<T> source) where T : IDisposable
      {
         foreach(var obj in source ?? new T[0])
         {
            if(obj is DisposableWrapper wrapper && wrapper.IsDisposed)
               continue;
            obj?.Dispose();
         }
      }

      /// <summary>
      /// Like Enumerable.First() except it targets the
      /// non-generic IEnumerable.
      /// </summary>

      public static object TryGetFirst(this IEnumerable enumerable)
      {
         Assert.IsNotNull(enumerable, nameof(enumerable));
         var e = enumerable.GetEnumerator();
         try
         {
            if(e.MoveNext())
               return e.Current;
            else
               return null;
         }
         finally
         {
            (e as IDisposable)?.Dispose();
         }
      }

      /// <summary>
      /// Safe disposer for DBObjectCollections.
      /// 
      /// It is common to have code that disposes all elements in a
      /// DBObjectCollection after using it, in a loop. This method
      /// automates that by returning an object that calls Dispose() 
      /// on each element in a DBObjectCollection when the instance 
      /// is disposed, and also disposes the DBObjectCollection.
      /// 
      /// For example:
      /// <code>
      /// 
      ///    Curve curve;               // assigned to a Curve entity
      ///    Point3dCollection points;  // assigned to a Point3dCollection
      ///    
      ///    DBObjectCollection fragments = curve.GetSplitCurves(points);
      ///    
      ///    using(fragments.EnsureDispose())
      ///    {
      ///       // use fragments here, the
      ///       // DBObjectCollection and its
      ///       // elements are disposed upon
      ///       // exiting this using() block.
      ///    }
      /// 
      /// <code>
      /// 
      /// </code>
      /// </summary>
      /// <param name="collection">The DBObjectCollection that is to be managed</param>
      /// <param name="disposeOwner">A value indicating if the collection argument
      /// should be disposed along with its elements (default = true)</param>
      /// <returns>An IDisposable that when disposed, disposes the <paramref name="collection"/>
      /// argument its elements</returns>

      public static DBObjectCollection EnsureDispose(this DBObjectCollection collection, bool disposeOwner = true)
      {
         return new ItemsDisposer<DBObjectCollection>(collection, disposeOwner);
      }

      class ItemsDisposer<T> : IDisposable where T: IEnumerable
      {
         T items;
         bool disposed;
         bool disposeOwner = true;

         public ItemsDisposer(T items, bool disposeOwner = true)
         {
            this.items = items;
            this.disposeOwner = disposeOwner;
         }

         public void Dispose()
         {
            if(!disposed && items != null)
            {
               disposed = true;
               items.OfType<IDisposable>().Dispose();
               if(disposeOwner)
                  (items as IDisposable)?.Dispose();
            }
         }

         public static implicit operator T(ItemsDisposer<T> disposer)
         {
            return disposer.items;
         }
      }


   }

   public static class DiagnosticSupport
   {
      /// <summary>
      /// Supporting code for diagnostic tracing of DBObjectFilters
      /// </summary>

      public static string SafeToString(this object value)
      {
         return value == null ? "(null)" : value.ToString();
      }

      public static string ToShortString(this Expression expr, string pad = "   ")
      {
         string res = expr?.ToString() ?? "(null)";
         return Reformat(StripNamespaces(res), pad);
      }

      /// <summary>
      /// Displays the argument's type, handle, and
      /// name if it is a named object.
      /// </summary>
      /// <param name="obj"></param>

      public static string ToIdString(this DBObject obj)
      {
         if(obj == null)
            return "(null)";
         string name = TryGetName(obj);
         if(!string.IsNullOrEmpty(name))
            name = $" [{name}]";
         else
            name = string.Empty;
         return StripNamespaces(obj.GetType().CSharpName()) +
            $"({obj.Handle.Format()}){name}";
      }

      public static string ToIdString(this ObjectId id)
      {
         if(id.IsNull)
            return "ObjectId.Null";
         return $"{id.ObjectClass.Name} ({id.Handle.Format()})";
      }

      public static string Format(this Handle handle)
      {
         return string.Format("0x{0:X}", handle.Value);
      }

      private static string TryGetName(DBObject obj)
      {
         string result = null;
         try
         {
            result = ((dynamic)obj).Name;
         }
         catch
         {
         }
         return result;
      }

      // public static string Format()
      
      public static string ToIdString(this object obj)
      {
         if(obj == null)
            return "(null)";
         if(obj is ObjectId id)
            return id.ToIdString();
         if(obj is DBObject dbObj)
            return dbObj.ToIdString();
         return StripNamespaces(obj.GetType().CSharpName()) +
            $" (0x{obj.GetHashCode().ToString("x")})";
      }

      static string Reformat(string s, string pad = "   ")
      {
         return s.Replace("AndAlso", $"\n{pad}   &&")
            .Replace("OrElse", $"\n{pad}   ||");
      }

      static string StripNamespaces(string input)
      {
         return input.Replace("Autodesk.AutoCAD.", "")
            .Replace("DatabaseServices.", "")
            .Replace("ApplicationServices.", "")
            .Replace("EditorInput.", "")
            .Replace("AutoCAD.AcDbLinq.", "")
            .Replace("Extensions.", "")
            .Replace("Expressions.", "")
            .Replace("Examples.", "");
      }

      public static string ToShortString(this Type type)
      {
         return type.CSharpName();
      }

   }

}



