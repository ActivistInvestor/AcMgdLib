/// EditorUIManager.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System.ComponentModel;
using System.Windows.Input;
using Autodesk.AutoCAD.EditorInput.Extensions;

namespace Autodesk.AutoCAD.Ribbon.Extensions
{
   /// <summary>
   /// Provides functionality that enables automatic
   /// updating of UI elements when the state of the
   /// AutoCAD drawing editor changes.
   /// </summary>
   
   public static class EditorUIManager
   {
      static bool queryCanExecute = false;
      static readonly EditorStateView stateView;

      static EditorUIManager()
      {
         stateView = EditorStateView.Instance;
         stateView.AddRef();
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
      /// occurs, one only needs to do this:
      /// 
      ///   EditorUIManager.QueryCanExecute = true;
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
                  stateView.PropertyChanged += OnQuiescentStateChanged;
               else
                  stateView.PropertyChanged -= OnQuiescentStateChanged;
               queryCanExecute = value;
            }
         }
      }

      static void OnQuiescentStateChanged(object sender, PropertyChangedEventArgs e)
      {
         CommandManager.InvalidateRequerySuggested();
      }

      /// <summary>
      /// This can be called at a high frequency by numerous
      /// ICommands, which can be very expensive. To minimize
      /// the overhead of referencing this property, the value
      /// it returns is cached and used until one of the events 
      /// signaling the state may have changed is raised.
      /// 
      /// Returns a value indicating if there is an active
      /// document, and it is in a quiescent state. If there
      /// are no documents open, this property returns false.
      /// </summary>

      public static bool IsQuiescentDocument =>
         stateView.IsQuiescentDocument;
   }

}

