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
using System.Extensions;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Autodesk.AutoCAD.Runtime;

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

      internal static void TryCheckTransaction(this object source, Transaction trans)
      {
         Assert.IsNotNull(source, nameof(source));
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         if(trans is OpenCloseTransaction)
            return;
         if(trans.GetType() != typeof(Transaction))
            return; // can't perform check without pulling in AcMgd/AcCoreMgd
         if(source is DBObject obj && obj.Database is Database db
               && trans.TransactionManager != db.TransactionManager)
            throw new ArgumentException("Transaction not from this Database");
      }

      /// <summary>
      /// Should be self-explanatory
      /// </summary>

      public static bool IsUserBlock(this BlockTableRecord btr)
      {
         return !(btr.IsAnonymous
            || btr.IsLayout
            || btr.IsFromExternalReference
            || btr.IsFromOverlayReference
            || btr.IsDependent);
      }

      public static Func<BlockTableRecord, bool> IsUserBlock(this IEnumerable<BlockTableRecord> blocks)
      {
         return IsUserBlock;
      }

      /// <summary>
      /// Disposes all the elements in the source sequence,
      /// and the source if it is an IDisposable. Useful with
      /// DBObjectCollection to ensure that all of the items
      /// retreived from it are disposed.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="source"></param>

      internal static void Dispose<T>(this IEnumerable<T> source) where T : IDisposable
      {
         foreach(var obj in source ?? new T[0])
         {
            DisposableWrapper wrapper = obj as DisposableWrapper;
            if(wrapper?.IsDisposed == true)
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
      ///    Transaction tr;            // assigned to a Transaction
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
      /// <param name="collection"></param>
      /// <returns></returns>

      public static DBObjectCollection EnsureDispose(this DBObjectCollection collection)
      {
         return new ItemsDisposer<DBObjectCollection>(collection);
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

      public static string ToShortString(this Expression expr, string pad = "   ")
      {
         string res = expr?.ToString() ?? "(null)";
         return Reformat(StripNamespaces(res), pad);
      }

      public static string ToIdentity(this object obj)
      {
         if(obj == null)
            return "(null)";
         return StripNamespaces(obj.GetType().CSharpName()) +
            $" (0x{obj.GetHashCode().ToString("x")})";
      }

      public static string CSharpName(this Type type)
      {
         return csharpNames[type];
      }

      static Cache<Type, string> csharpNames = new Cache<Type, string>(getCSharpName);

      static string getCSharpName(Type type)
      {
         var sb = new StringBuilder();
         var name = type.Name;
         if(!type.IsGenericType)
            return name;
         sb.Append(name.Substring(0, name.IndexOf('`')));
         sb.Append("<");
         sb.Append(string.Join(", ",
            type.GetGenericArguments()
              .Select(CSharpName)));
         sb.Append(">");
         return sb.ToString();
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
         return CSharpName(type);
      }

   }

}



