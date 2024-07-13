
/// RibbonEventManagerExample.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license
/// 
/// An example showing the use of the RibbonEventManager 
/// class, the ModalRibbonCommandButton class, and its
/// supporting types.
/// 
/// In addition to demonstrating how to simply manage
/// ribbon content, this example also demonstrates the
/// use of components that allow ribbon UI elements to
/// synchronize their enabled state with the state of 
/// the drawing editor. 
/// 
/// The buttons that are added to the RibbonTab in the
/// example below, will automatically enable and disable
/// themselves depending on whether there is an active,
/// quiescent document. 
/// 
/// If you try using the example, with the RibbonTab
/// added by it active and visible, try using various
/// AutoCAD commands, editing grips, and various other
/// operations, and you should see the example buttons
/// enable/disable automatically depending on what you
/// are doing in the editor. The example buttons will
/// only be enabled when there is an active document
/// with no active command.
/// 
/// The core functionality for this is provided by the 
/// RibbonEventManager and EditorStateObserver classes.
/// 
/// To implment synchronization of the enabled state of
/// a RibbonItem that uses a CommandHandler's CanExecute()
/// method, one only needs to do two things:
/// 
/// First, enable editor/UI synchronization globally 
/// like this:
/// 
///   RibbonEventManager.QueryCanExecuteEnabled = true;
///
/// Setting the above property to true will cause WPF to
/// query the CanExecute() method of registered ICommands
/// and enable/disable connected UIs accordingly when any
/// of the following happens:
/// 
///   1. Commands start/end (including LISP macros).
/// 
///   2. Grip editing or some other type of dragging 
///      is in progress.
///    
///   3. The lock state of the active document changes.
/// 
///   4. The active document changes.
/// 
/// To have a UI element synchronize with the editor's
/// Quiescent state, one only needs to implement an 
/// ICommand having a CanExecute() method that queries 
/// the current UI state:
/// 
///   public class MyCommandHandler : ICommand
///   {
///      public bool CanExecute(object parameter)
///      {
///         return RibbonEventManager.IsQuiescentDocument;
///      }
///      
///      // (balance of class omitted)
///   }
///   
/// The above implementation of CanExecute() will cause any
/// connected UI element to become disabled when there is a
/// command running.
/// 
/// A number of reusable base types that implement ICommand
/// are included in this library that will perform the above 
/// operation in their CanExecute() implementations, allowing 
/// derived types to inherit that functionality automatically.
/// 
/// Note that these included classes all target the Autodesk
/// RibbonCommandButton rather than the RibbonButton, mainly
/// because RibbonCommandButton has all of the functionality
/// required to execute both AutoCAD commands and complex menu 
/// macros, along with allowing the use of DIESEL for graying
/// and checking buttons. The RibbonCommandButton is the type 
/// used to implement ribbon buttons defined in CUI files.


using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Ribbon;
using Autodesk.AutoCAD.Ribbon.Extensions;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using Autodesk.Windows.Extensions;

#pragma warning disable CS0612 // Type or member is obsolete

/// TODO: If you use this example as a starting
/// point, then you should modify the argument 
/// to the ExtensionApplication attribute to be 
/// the name of the actual IExtensionApplication-
/// based class:

[assembly: ExtensionApplication(typeof(Namespace1.MyApplication))]

namespace Namespace1
{
   public class MyApplication : IExtensionApplication
   {
      /// Ribbon content should be assigned 
      /// to a static member variable:
      
      static RibbonTab myRibbonTab;

      /// <summary>
      /// IExtensionApplication.Initialize
      /// 
      /// Note: When using the RibbonEventManager,
      /// there is no need to defer execution of
      /// code until the Application.Idle event is
      /// raised, as the RibbonEventManager already
      /// does that for the programmer.
      /// 
      /// The handler for the InitializeRibbon event
      /// will not be called until the next Idle event 
      /// is raised, if the ribbon exists.
      /// 
      /// </summary>

      public void Initialize()
      {
         /// Add a handler to the InitializeRibbon event.

         RibbonEventManager.InitializeRibbon += LoadMyRibbonContent;
      }

      /// <summary>
      /// Handler for the InitializeRibbon event.
      /// 
      /// This handler can be called multiple times,
      /// such as when a workspace is loaded. See the
      /// docs for RibbonEventManager for details on
      /// when/why this event handler will be called.
      /// </summary>

      private void LoadMyRibbonContent(object sender, RibbonStateEventArgs e)
      {
         /// Create the ribbon content only if it has
         /// not already been created:

         CreateRibbonContent();

         /// Add the content to the ribbon:

         e.RibbonControl.Tabs.Add(myRibbonTab);
      }

      /// <summary>
      /// This creates the ribbon content on
      /// the first call to the above method.
      /// </summary>

      static void CreateRibbonContent()
      {
         if(myRibbonTab != null)
            return;
      
         var src = new RibbonPanelSource();

         /// Add a ModalRibbonCommandButton.
         /// This button has its own CommandHandler:

         RibbonCommandButton button;
         button = new ModalRibbonCommandButton("REGEN");
         src.Items.Add(button);
         button.Text = "REGEN";
         button.Size = RibbonItemSize.Large;
         button.Orientation = Orientation.Vertical;
         button.ShowText = true;
         button.MinWidth = 80;

         /// Add another ModalRibbonCommandButton:
         button = new ModalRibbonCommandButton("REGENALL");
         src.Items.Add(button);
         button.Text = "REGENALL";
         button.Size = RibbonItemSize.Large;
         button.Orientation = Orientation.Vertical;
         button.ShowText = true;
         button.MinWidth = 80;

         var commands = new string[]
         {
            "LINE",
            "CIRCLE",
            "SPLINE",
            "PLINE"
         };

         /// Create a single command handler that'll be 
         /// used by all RibbonCommandButtons added below:

         var handler = new ModalRibbonCommandButtonHandler();

         /// Add some standard RibbonCommandButtons that
         /// execute the commands in the commands array:

         foreach(string command in commands)
         {
            button = new RibbonCommandButton(command, $"ID_{command}");
            button.Orientation = Orientation.Vertical;
            button.Size = RibbonItemSize.Large;
            button.ShowText = true;
            button.Text = command;
            button.MinWidth = 80;
            button.CommandHandler = handler;
            button.CommandParameter = button;
            src.Items.Add(button);
         }

         /// Note that each RibbonCommandButton's CommandParameter
         /// property is assigned to the RibbonCommandButton instance.
         /// This is necessary in order for the command handler to act
         /// as the handler for multiple RibbonCommandButtons, allowing
         /// it to run each of their commands by receiving each Button
         /// in the Parameter argument and calling its Execute() method.
         /// 
         /// With the CommandHandler of each button assigned to the 
         /// ModalRibbonCommandButtonHandler, all buttons will become 
         /// disabled when there's a command or other modal operation 
         /// in progress.

         /// We'll add a RibbonTextBox that allows the user to enter 
         /// a macro that executes when they click the associated button 
         /// added below. The macro can be anything the user can type on 
         /// the command line, and also any valid menu macro string that 
         /// automates a sequence of one or more commands;

         RibbonTextBox textBox = new RibbonTextBox
         {
            Size = RibbonItemSize.Large,
            MinWidth = 300.0,
            AcceptTextOnLostFocus = false,
            IsEmptyTextValid = false,
            InvokesCommand = true,
            Prompt = "Enter a command or macro"
         };

         src.Items.Add(textBox);

         /// Add another RibbonCommandButton that when clicked,
         /// executes the macro in the RibbonTextBox. This button
         /// uses a custom ICommand-based type included below to
         /// establish the linkage with the RibbonTextBox.
         
         button = new RibbonCommandButton("PLACEHOLDER", "ID_MACROBUTTON");
         src.Items.Add(button);
         button.Orientation = Orientation.Vertical;
         button.Size = RibbonItemSize.Large;
         button.ShowText = true;
         button.Text = "Run\nTextBox\nMacro";
         button.MinWidth = 80;
         button.CommandParameter = button;
         button.CommandHandler = new MacroCommandHandler(textBox, button);

         /// Create a RibbonTab to host the above items
         /// and add them:

         RibbonPanel panel = new RibbonPanel();

         /// Assign the new RibbonTab to the static member
         /// so it can be added to the ribbon when needed:

         myRibbonTab = new RibbonTab();
         myRibbonTab.Id = "IDMyTab001";
         myRibbonTab.Name = "MyRibbonTab";
         myRibbonTab.Title = "My Ribbon Tab";
         panel.Source = src;
         myRibbonTab.Panels.Add(panel);
      }

      public void Terminate()
      {
      }
   }

   /// <summary>
   /// This class acts as a command handler for the button
   /// that executes the macro entered into the RibbonTextBox
   /// as well as the RibbonTextBox.
   /// 
   /// It derives from one of the reusable types included in
   /// this library that provides the logic for synchronizing
   /// the enabled state of controls to the quiescent state of
   /// the drawing editor.
   /// </summary>

   internal class MacroCommandHandler : ModalRibbonCommandButtonHandler
   {
      RibbonTextBox macroTextBox;

      static MacroCommandHandler()
      {
         EventManager.RegisterClassHandler(typeof(TextBox),
            TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(textChanged));
      }

      public MacroCommandHandler(RibbonTextBox textBox, RibbonCommandButton button)
         : base(button)
      {
         this.macroTextBox = textBox;
         macroTextBox.CommandHandler = this;
      }

      public override bool CanExecute(object parameter)
      {
         bool defaultValue = base.CanExecute(parameter);
         /// This is a kludge:
         macroTextBox.IsEnabled = defaultValue;
         return defaultValue && !string.IsNullOrWhiteSpace(macroTextBox.TextValue);
      }

      public override void Execute(object parameter)
      {
         string macro = macroTextBox.TextValue;
         if(!string.IsNullOrWhiteSpace(macro))
         {
            Button.Macro = macro;
            base.Execute(parameter);
         }
      }

      /// <summary>
      /// This event handler is needed to mitigate an issue
      /// with RibbonTextBox when it is not used with bindings, 
      /// which is that it does not update its TextValue or 
      /// Value property as the user types in the control or 
      /// performs clipboard operations.
      /// 
      /// Because we want to disable the button that executes
      /// the macro if this control contains no text, we need 
      /// to know when the text changes, so that the button can
      /// be enabled/disabled accordingly.
      /// </summary>

      static void textChanged(object sender, TextChangedEventArgs e)
      {
         if(e.Source is TextBox textBox && textBox.DataContext is RibbonTextBox macroTextBox)
         {
            if(macroTextBox.TextValue != textBox.Text)
            {
               macroTextBox.TextValue = textBox.Text;
            }
         }
      }

   }

   /// <summary>
   /// A specialization of RibbonTextBox that is used to
   /// specify a command macro to execute when a button 
   /// is clicked or the enter key is pressed.
   /// 
   /// This specialization is required to mitigate the
   /// issues with RibbonTextBox, as described below.
   /// </summary>

   public class MacroTextBox : RibbonTextBox
   {
      public MacroTextBox()
      {
      }

      //public RibbonCommandButton Button
      //{
      //   get => button;
      //   internal set
      //   { 
      //      button = value;
      //   }
      //}
   }


}