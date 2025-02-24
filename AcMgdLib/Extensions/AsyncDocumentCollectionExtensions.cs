/// AsyncDocumentCollectionExtensions.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under the terms of the MIT License

using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Internal;
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
         if(!docs.IsApplicationContext)
            throw new AcRx.Exception(AcRx.ErrorStatus.InvalidContext);
         if(docs == null)
            throw new ArgumentNullException(nameof(docs));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         Document doc = docs.MdiActiveDocument;
         if(doc == null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NoDocument);
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
      /// This API mitigates a problem associated with the use of 
      /// async/await in AutoCAD. If you try the example code shown 
      /// below, you'll see that problem, which is that exceptions 
      /// that are thrown by delegates passed into an awaited call 
      /// to ExecuteInCommandContextAsync() cannot be caught by the 
      /// calling code, and will terminate AutoCAD. 
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
      /// try/catch block. Instead of the exception being caught and 
      /// handled, AutoCAD terminates.
      /// 
      /// In fact, the problem is not specific to any AutoCAD managed
      /// API, including ExecuteInCommandContextAsync(). It applies 
      /// to any use of async/await in AutoCAD, where a delegate is 
      /// passed to an asynchrnous awaited method.
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
      ///         Document doc = docs.MdiActiveDocument;
      ///         doc.Editor.WriteMessage("\nREGEN complete");
      ///            
      ///      }
      ///      catch(System.Exception ex)
      ///      {
      ///         // deal with the exception and do not re-throw it.
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

      public static async Task InvokeAsCommandAsync(this DocumentCollection docs, 
         Action<Document> action)
      {
         if(docs == null)
            throw new ArgumentNullException(nameof(docs));
         if(action == null)
            throw new ArgumentNullException(nameof(action));
         Document doc = docs.MdiActiveDocument;
         if(doc == null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NoDocument);
         if(!docs.IsApplicationContext)
            throw new AcRx.Exception(AcRx.ErrorStatus.InvalidContext);
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
            throw task.Exception;
         }
      }

      /// <summary>
      /// A version of the Editor's Command() method that
      /// can be safely called from the application context. 
      /// 
      /// This method targets the DocumentCollection and 
      /// always operates on the active document. 
      /// 
      /// Calls to the method can be awaited to execute 
      /// code that follows after the command sequence 
      /// has executed.
      /// 
      /// Important:
      ///
      /// Calls to this method should _always_ be wrapped
      /// in a try{} block, that's followed by a catch{}
      /// block that handles any exception that may be
      /// thrown by the Editor's Command() method (e.g.,
      /// ErrorStatus.InvalidInput).
      ///
      /// Failing to catch exceptions thrown by this method,
      /// or by any statements that follow an await'ed call 
      /// to it will most-likely crash AutoCAD.
      /// 
      /// A simple example:
      /// 
      /// This example was intended to be called from the
      /// click handler of a button on a modeless UI such
      /// as a PaletteSet or modeless window. Note the use
      /// of 'async' in the method's declaration, which is
      /// required.
      /// 
      ///   public static async void DrawCircle()
      ///   {
      ///      var docs = Application.DocumentManager;
      ///      Document doc = docs.MdiActiveDocument;
      ///      Point3d center = new Point3d(10, 10, 0);
      ///      Point3d radius = 5.0;
      ///      try
      ///      {
      ///         await docs.CommandAsync("._CIRCLE", center, radius);
      ///         doc.Editor.WriteMessage("\nCircle created");
      ///      }
      ///      catch(System.Exception ex)
      ///      {
      ///         doc.Editor.WriteMessage($"\nError: {ex.Message}");
      ///      }
      ///   }
      ///   
      /// The use of await in the call to CommandAsync() allows
      /// the code that follows that call to not run until the 
      /// command has fully-executed and the circle is created.
      /// 
      /// The use of try/catch in the above example is required,
      /// to prevent AutoCAD from crashing if the Command() method
      /// throws an exception.
      /// 
      /// </summary>
      /// <param name="args">The command arguments.</param>

      public static async Task CommandAsync(this DocumentCollection docs,
         params object[] args)
      {
         if(docs == null)
            throw new ArgumentNullException(nameof(docs));
         if(!docs.IsApplicationContext)
            throw new AcRx.Exception(AcRx.ErrorStatus.InvalidContext);
         Document doc = docs.MdiActiveDocument;
         if(doc == null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NoDocument);
         Task task = Task.CompletedTask;
         await docs.ExecuteInCommandContextAsync(
            delegate (object unused)
            {
               try
               {
                  doc.Editor.Command(args);
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
            throw task.Exception;
         }
      }
   }


}
