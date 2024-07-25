/// RibbonEventManager.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license
/// 

using System;
using System.ComponentModel;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using Autodesk.Windows;

namespace Autodesk.AutoCAD.Ribbon.Extensions
{
   /// Simplified ribbon content management:
   /// 
   /// A class that provides a simplified means of 
   /// initializing and managing application-provided
   /// content for AutoCAD's ribbon.


   /// <summary>
   /// RibbonEventManager exposes a single event that can be
   /// handled to be notified whenever it is necessary to add 
   /// or refresh ribbon content.
   /// 
   /// The InitializeRibbon event:
   /// 
   /// The typical usage pattern for using this event, is to
   /// simply add a handler to it when the application/extension
   /// is loaded (e.g., from an IExtensionApplication.Initialize
   /// method). If that is done, it isn't necessary to check to
   /// see if the ribbon exists, add handlers to other events, etc.. 
   /// One need only add a handler to the RibbonEventManager's 
   /// InitializeRibbon event, and in the handler, add content to 
   /// the ribbon.
   /// 
   /// Using this class and its single event relieves the developer
   /// from the complicated burden of having to check conditions and
   /// handle multiple events to ensure that their content is always 
   /// present on the ribbon.
   /// 
   /// A minimal example IExtensionApplication that uses this class
   /// to manage ribbon content:
   /// 
   /// <code>
   ///  
   ///   public class MyApplication : IExtensionApplication
   ///   {
   ///      public void Initialize()
   ///      {
   ///         RibbonEventManager.InitializeRibbon += LoadMyRibbonContent;
   ///      }
   ///      
   ///      private void LoadMyRibbonContent(object sender, RibbonStateEventArgs e)
   ///      {
   ///         // Here, one can safely assume the ribbon exists,
   ///         // and that content should be added to it.
   ///      }
   ///
   ///      public void Terminate()
   ///      {
   ///      }
   ///   }
   /// 
   /// </code>
   /// 
   /// The handler for the InitializeRibbon event will be 
   /// called whenever it is necessary to add content to 
   /// the ribbon, which includes:
   ///   
   ///   1. When the handler is first added to the 
   ///      InitializeRibbon event and the ribbon 
   ///      currently exists.
   ///   
   ///   2. When the ribbon is first created and shown 
   ///      when it did not exist when the handler was 
   ///      added to the InitializeRibbon event.
   ///      
   ///   3. When a workspace is loaded, after having 
   ///      added content to the ribbon.
   ///   
   /// The State property of the event argument indicates
   /// which of the these three conditions triggered the
   /// event.
   /// 
   /// 6/4/24 Revisons:
   /// 
   /// 1. The IdleAction class has been replaced with the
   ///    IdleAwaiter class, to defer execution of code 
   ///    until the next Application.Idle event is raised.
   /// 
   /// 2. A new AddRibbonTabs() method was added to the event
   ///    argument type (RibbonStateEventArgs), that will add
   ///    one or more ribbon tabs to the ribbon if they are not
   ///    already present on the ribbon.
   ///    
   /// 7/7/24
   /// 
   /// 1. Revision 1 above has been rolled-back due to issues
   ///    related to unhandled exceptions thrown from await'ed 
   ///    continuations, that cause AutoCAD to terminate.
   ///    
   /// 7/9/24 
   /// 
   /// 1. Merging CanExecuteManager into RibbonEventManager.
   /// 
   /// Test scenarios covered:
   /// 
   /// 1. Client extension application loaded at startup:
   /// 
   ///    - With ribbon existing at startup.
   ///    
   ///    - With ribbon not existing at startup,
   ///      and subsequently created by issuing 
   ///      the RIBBON command.
   ///       
   /// 2. Client extension application loaded at any point
   ///    during session via NETLOAD or demand-loading when 
   ///    a registered command is first issued:
   ///    
   ///    - With ribbon existing at load-time.
   ///    
   ///    - With ribbon not existing at load-time, 
   ///      and subsequently created by issuing the 
   ///      RIBBON command.
   /// 
   /// 3. With client extension loaded and ribbon content
   ///    already added to an existing ribbon, that is
   ///    subsequently removed by one of these actions:
   ///    
   ///    - CUI command
   ///    - MENULOAD command.
   ///    - CUILOAD/CUIUNLOAD commands.
   ///    
   /// In all of the above cases, the InitializeRibbon 
   /// event is raised to signal that content should be
   /// added to the ribbon.
   /// 
   /// To summarize, if your app adds content to the ribbon
   /// and you want to ensure that it is always added when
   /// needed, just handle the InitializeRibbon event, and 
   /// add the content to the ribbon in the event's handler.
   ///    
   /// Feel free to post comments in the repo discussion
   /// regarding other scenarious not covered, or about
   /// any other issues or bugs you may have come across.
   /// 
   /// </summary>

   public static class RibbonEventManager
   {
      static DocumentCollection documents = Application.DocumentManager;
      static event RibbonStateEventHandler initializeRibbon = null;
      static bool queryCanExecute = false;
      static bool initialized = false;
      static readonly EditorStateView stateView;

      static RibbonEventManager()
      {
         stateView = EditorStateView.Instance;
         stateView.AddRef();
         if(RibbonCreated)
            Initialize(RibbonState.Active);
         else
            RibbonServices.RibbonPaletteSetCreated += ribbonPaletteSetCreated;
      }

      static void Initialize(RibbonState state)
      {
         Idle.Invoke(delegate ()
         {
            if(initializeRibbon != null)
            {
               try
               {
                  initializeRibbon?.Invoke(RibbonPaletteSet, new RibbonStateEventArgs(state));
               }
               catch(System.Exception ex)
               {
                  UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
               }
            }
            RibbonPaletteSet.WorkspaceLoaded += workspaceLoaded;
            initialized = true;
         });
      }
      
      static void RaiseInitializeRibbon(RibbonState state)
      {
         if(initializeRibbon != null)
         {
            Idle.Invoke(delegate ()
            {
               try
               {
                  initializeRibbon?.Invoke(RibbonPaletteSet, new RibbonStateEventArgs(state));
               }
               catch(System.Exception ex)
               {
                  UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
               }
            });
         }
      }

      static void ribbonPaletteSetCreated(object sender, EventArgs e)
      {
         RibbonServices.RibbonPaletteSetCreated -= ribbonPaletteSetCreated;
         Initialize(RibbonState.Initalizing);
      }

      static void workspaceLoaded(object sender, EventArgs e)
      {
         if(RibbonControl != null)
            RaiseInitializeRibbon(RibbonState.WorkspaceLoaded);
      }

      /// <summary>
      /// If a handler is added to this event and the ribbon 
      /// exists, the handler will be invoked immediately, or
      /// on the next Idle event, depending on the execution
      /// context the handler is added from.
      /// 
      /// Note: Adding the same event handler to this event
      /// multiple times will result in undefined behavior.
      /// </summary>

      public static event RibbonStateEventHandler InitializeRibbon
      {
         add
         {
            if(value == null)
               throw new ArgumentNullException(nameof(value));
            if(initialized)
               InvokeHandler(value);
            else
               initializeRibbon += value;
         }
         remove
         {
            initializeRibbon -= value;
         }
      }

      static void InvokeHandler(RibbonStateEventHandler handler)
      {
         Idle.Invoke(delegate ()
         {
            try
            {
               handler(RibbonPaletteSet, new RibbonStateEventArgs(RibbonState.Active));
            }
            catch(System.Exception ex)
            {
               UnhandledExceptionFilter.CerOrShowExceptionDialog(ex);
               return;
            }
            initializeRibbon += handler;
         });
      }

      /// <summary>
      /// Forces the WPF framework to requery the CanExecute()
      /// method of all registered ICommands, to update their 
      /// associated UI's enabled/disabled state when:
      /// 
      ///   1. AutoCAD or LISP commands start and end.
      ///   2. Dragging starts/ends in the drawing editor.
      ///   3. The active document changes.
      ///   4. The lock state of the active document changes.
      /// 
      /// To enable updating when one of the above events
      /// occurs, one need do this:
      /// 
      ///   RibbonEventManager.QueryCanExecute = true;
      ///    
      /// The default implementation of CanExecute() for the
      /// RibbonCommandButton always returns true, and most
      /// other ribbon elements respond similarly, and do not
      /// become disabled when commands are running. This is
      /// of course, the intended behavior, because standard
      /// ribbon command buttons act the same way that AutoCAD 
      /// menu macros have always worked, which is to cancel 
      /// any currently-running commands when clicked.
      /// 
      /// A specialization of RibbonCommandButton included in
      /// this library (ModalRibbonCommandButton) provides the
      /// functionality needed to automatically enable/disable 
      /// itself depending on if there is a quiescent active 
      /// document, using the functionality provided by this
      /// class.
      /// 
      /// Example ModalRibbonCommandButtons can be found in 
      /// the RibbonEventManagerExample.cs file.
      /// 
      /// </summary>

      public static bool QueryCanExecute
      {
         get
         {
            return queryCanExecute;
         }
         set
         {
            if(queryCanExecute ^ value)
            {
               if(value)
                  stateView.PropertyChanged += IsQuiescentDocumentChanged;
               else
                  stateView.PropertyChanged -= IsQuiescentDocumentChanged;
               queryCanExecute = value;
            }
         }
      }

      static void IsQuiescentDocumentChanged(object sender, PropertyChangedEventArgs e)
      {
         CommandManager.InvalidateRequerySuggested();
      }

      /// <summary>
      /// This can be called at a high frequency by numerous
      /// ICommands, which can be very expensive. To minimize
      /// the overhead of referencing this property, the value
      /// it returns is cached and reused until one of the source
      /// events signaling the state may have changed is raised.
      /// 
      /// Returns a value indicating if there is an active
      /// document, and it is in a quiescent state. If there
      /// are no documents open, this property returns false.
      /// </summary>

      public static bool IsQuiescentDocument => 
         stateView.IsQuiescentDocument;

      public static bool RibbonCreated => RibbonControl != null;

      public static RibbonPaletteSet RibbonPaletteSet =>
         RibbonServices.RibbonPaletteSet;

      public static RibbonControl? RibbonControl =>
         RibbonPaletteSet?.RibbonControl;
   }

}

