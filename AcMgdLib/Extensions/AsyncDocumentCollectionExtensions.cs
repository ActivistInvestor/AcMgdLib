/// AsyncDocumentCollectionExtensions.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under the terms of the MIT License

using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.ApplicationServices
{
   public static partial class AsyncDocumentCollectionExtensions
   {
      /// <summary>
      /// A wrapper for ExecuteInCommandContextAsync()
      /// that can be called from any execution context,
      /// that also provides a safety-net that prevents
      /// AutoCAD from terminating if an exception is
      /// thrown by the delegate argument.
      /// 
      /// Executes the given action in the document/command 
      /// context. If called from the application context, the
      /// command executes asynchronously and callers should
      /// not rely on side effects of the action which will not
      /// execute until after the calling code returns.
      /// 
      /// <remarks>
      /// ExecuteInCommandContextAsync() is a highly-volatile API
      /// that can cause AutoCAD to terminate, if is not used with
      /// extreme care. Any exception thrown by the delegate that
      /// is passed to that API will cause AutoCAD to terminate.
      /// 
      /// For this reason, this wrapper catches exceptions thrown 
      /// by the delegate and supresses them. When an exception is 
      /// caught, the standard .NET exception dialog is displayed. 
      /// 
      /// The caller of this method cannot trap an exception thrown 
      /// by the delegate at the call site or from any calling code,
      /// because the delegate executes assynchronously, after the 
      /// caller has exited and control is returned to AutoCAD.
      /// </remarks>
      /// </summary>
      /// <param name="docs">The DocumentCollection</param>
      /// <param name="action">A delegate that takes a Document as a
      /// parameter, represeting the active document at the point when
      /// the delgate executes.</param>
      /// <exception cref="ArgumentNullException"></exception>

      public static void InvokeAsCommand(this DocumentCollection docs,
         Action<Document> action,
         bool showExceptionDialog = true)
      {
         if(docs == null)
            throw new ArgumentNullException(nameof(docs));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         Document doc = ActiveDocumentChecked;
         if(docs.IsApplicationContext)
         {
            docs.ExecuteInCommandContextAsync(delegate (object o)
            {
               try
               {
                  action(doc);
                  return Task.CompletedTask;
               }
               catch(System.Exception ex)
               {
                  if(showExceptionDialog)
                     UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
                  return Task.FromException(ex);
               }
            }, null);
         }
         else
         {
            action(doc);
         }
      }

      /// <summary>
      /// An asynchronous / awaitable version of InvokeAsCommand()
      /// 
      /// This method can be awaited so that code that follows the
      /// awaited call does not execute until the delegate passed to
      /// this method has executed and returned. 
      /// 
      /// Use this method when there is code that is dependent on 
      /// side-effects of the delegate, and that code must execute 
      /// in the application context.
      /// 
      /// <remarks>
      /// Handling Exceptions:
      /// 
      /// This API mitigates a significant problem associated with
      /// the use of async/await in AutoCAD. If you try the example
      /// code shown below, you'll see that problem, which is that 
      /// exceptions that are thrown by delegates passed into an
      /// awaited call to ExecuteInCommandContextAsync() cannot be 
      /// caught by the calling code, and will terminate AutoCAD. 
      /// 
      /// <code>
      /// 
      ///   public static async void MyAsyncMethod()
      ///   {
      ///      Documents docs = Application.DocumentManager;
      ///      Document doc = docs.MdiActiveDocument;
      ///      Editor ed = doc.Editor;
      ///      try
      ///      {
      ///         await docs.ExecuteInCommandContextAsync((o) =>
      ///           throw new NotSupportedException(), null);
      ///           
      ///         ed.WriteMessage("\nOperation completed.");
      ///      }
      ///      catch(System.Exception ex)
      ///      {
      ///         ed.WriteMessage($"\nCaught {ex.Message}");
      ///      }
      ///   }
      /// 
      /// </code>
      /// The above code calls ExecuteinCommandContextAsync() and
      /// passes a delegate that simulates an unhandled exception.
      /// If you run this code, you will see that the exception that
      /// is thrown by the delegate is not caught by the enclosing
      /// try/catch block (it can't be because the delegate executes
      /// after the async method returns). Instead of the exception
      /// being caught and handled, AutoCAD terminates.
      /// 
      /// 
      /// In fact, the problem is not specific to any AutoCAD managed
      /// API, including ExecuteInCommandContextAsync(). It applies 
      /// to any use of await in AutoCAD, where a delegate is passed 
      /// to an asynchrnous awaited method.
      /// 
      /// The InvokeAsCommandAsync() wrapper solves that problem by
      /// propagating exceptions thrown by the delegate passed to it, 
      /// back to the caller, which can easily catch and handle them 
      /// using try/catch.
      /// 
      /// In addition to the problem of not being able to handle an
      /// exception thrown by a delegate, exceptions that are thrown
      /// by continuations that follow an awaited call to any async
      /// method will terminate AutoCAD if the exception isn't caught
      /// and handled by an enclosing try/catch.
      /// 
      /// For those reasons, code that calls InvokeAsCommandAsync()
      /// must always enclose calls to that method within a try block, 
      /// followed by a catch() block that handles all exceptions and 
      /// does _not_ re-throw them. Failure to do that will result in
      /// AutoCAD terminating if an exception is thrown in either the
      /// delegate, or the continuation that follows an awaited call
      /// to this method.
      /// 
      /// The required try block should contain the awaited call to 
      /// this method, along with any continuation statements that
      /// are to execute after the delegate has executed. Exceptions
      /// thrown by the delegate or by a continuation statement must
      /// be handled by the catch() block, and the catch() block must
      /// NOT re-throw any exceptions that are caught.
      /// 
      /// A minimal example:
      /// 
      /// <code>
      /// 
      ///   static DocumentCollection docs = Application.DocumentManager;
      ///   
      ///   public static async void MyAsyncMethod()
      ///   {
      ///      try     // This is NOT optional
      ///      {
      ///         await docs.InvokeAsCommandAsync(doc => 
      ///            doc.Editor.Command("._REGEN"));
      ///            
      ///         // do stuff here after the REGEN command 
      ///         // has completed.
      ///         
      ///         Document doc = Application.DocumentManager.MdiActiveDocument;
      ///         doc.Editor.WriteMessage("\nREGEN complete");
      ///            
      ///      }
      ///      catch(System.Exception ex)
      ///      {
      ///         // deal with the exception and do NOT re-throw it!!!
      ///         
      ///         // For example, you can do this:
      ///         UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
      ///      }
      ///   }
      ///   
      /// </code>
      /// 
      /// In the above example, if an exception is thrown by the delegate, or
      /// by the continuation (which is the code that follows the awaited call 
      /// to InvokeAsCommandAsync), it will be caught by the catch() block, 
      /// allowing the caller to deal with it accordingly. The catch() block 
      /// must not re-throw exceptions, which is essentially the same as not 
      /// having try and catch blocks at all.
      /// 
      /// </remarks>
      /// </summary>
      /// <param name="docs">The DocumentCollection</param>
      /// <param name="action">A delegate that takes a Document as a
      /// parameter, represeting the active document at the point when
      /// the delgate executes.</param>
      /// <returns>A task representing the the asynchronous operation</returns>
      /// <exception cref="Autodesk.AutoCAD.Runtime.Exception">There is no
      /// active document</exception>
      /// <exception cref="ArgumentNullException">A required parameter was null</exception>
      /// </summary>

      public static async Task InvokeAsCommandAsync(this DocumentCollection docs, 
         Action<Document> action)
      {
         if(docs == null)
            throw new ArgumentNullException(nameof(docs));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         Document doc = ActiveDocumentChecked;
         if(docs.IsApplicationContext)
         {
            Task task = Task.CompletedTask;
            await docs.ExecuteInCommandContextAsync(
               delegate (object unused)
               {
                  try
                  {
                     action(doc);
                     return task;
                  }
                  catch(System.Exception ex)
                  {
                     return task = Task.FromException(ex);
                  }
               },
               null
            );
            if(task.IsFaulted)
            {
               throw task.Exception ?? 
                  new AggregateException(
                     new InvalidOperationException("Unspecified error"));
            }
         }
         else
         {
            action(doc);
         }
      }

      /// <summary>
      /// An overload of InvokeAsCommandAsync that internally 
      /// starts and manages a DocumentTransaction, that is 
      /// passed to the delegate argument.
      /// 
      /// The delegate must accept a single argument of the type
      /// DocumentTransaction. It can obtain the active Document
      /// from the DocumentTransaction's Document property.
      /// 
      /// Just before the delegate is called, the transaction is
      /// started and is then passed in the call to the delegate. 
      /// When the delegate returns normally, the transaction is 
      /// commited, if it was not committed or aborted by the 
      /// delegate. 
      /// 
      /// The delegate can commit or abort the transaction if it 
      /// chooses to do so. If the delegate does not commit the
      /// transaction, it is commited after the delegate returns.
      /// If an exception is thrown from within the delegate, the 
      /// transaction is aborted. 
      /// 
      /// The delegate must not dispose the DocumentTransaction
      /// argument.
      /// 
      /// </summary>
      /// <param name="docs">The DocumentCollection</param>
      /// <param name="action">A delegate that takes a 
      /// DocumentTransaction as its only argument. The delegate
      /// must not dispose the DocumentTransaction</param>
      /// <returns>A task representing the the asynchronous operation</returns>
      /// <exception cref="Autodesk.AutoCAD.Runtime.Exception">There is no
      /// active document</exception>
      /// <exception cref="ArgumentNullException">A required parameter was null</exception>
      
      public static async Task DocTransInvokeAsCommandAsync(this DocumentCollection docs,
         Action<DocumentTransaction> action)
      {
         if(docs == null)
            throw new ArgumentNullException(nameof(docs));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         if(docs.IsApplicationContext)
         {
            Task task = Task.CompletedTask;
            await docs.ExecuteInCommandContextAsync(
               delegate (object unused)
               {
                  try
                  {
                     using(var tr = new DocumentTransaction())
                     {
                        action(tr);
                        tr.TryCommit();
                        return task;
                     }
                  }
                  catch(System.Exception ex)
                  {
                     return task = Task.FromException(ex);
                  }
               },
               null
            );
            if(task.IsFaulted)
            {
               throw task.Exception ?? 
                  new AggregateException(new InvalidOperationException("Unspecified"));
            }
         }
         else
         {
            using(var tr = new DocumentTransaction())
            {
               action(tr);
               tr.TryCommit();
            }
         }
      }

      public static Document ActiveDocumentChecked
      {
         get
         {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            AcRx.ErrorStatus.NoDocument.ThrowIf(doc == null);
            return doc;
         }
      }


   }


}
