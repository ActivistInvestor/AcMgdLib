
/// ModalRibbonCommandButtonHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using Autodesk.Windows;
using Autodesk.Windows.Extensions;

#pragma warning disable CS0612 // Type or member is obsolete

namespace Autodesk.AutoCAD.Ribbon.Extensions
{
   /// <summary>
   /// A concrete ICommand implementation that's designed
   /// to work with the RibbonCommandButton class. This
   /// class can be passed a RibbonCommandButton in its
   /// constructor, and it will use the RibbonEventManager
   /// to manage the RibbonCommandButton's enabled state
   /// so that the button is only enabled when there is
   /// a quiescent document in the drawing editor. 
   /// 
   /// Because a RibbonCommandButton usually provides the
   /// implementation of Execute(), an instance of this
   /// class will delegate to the Execute() method of the
   /// owning RibbonCommandButton. If an instance of this
   /// class is associated with many RibbonCommandButtons
   /// as described below, the Execute() method requires
   /// the parameter argument to be the RibbonCommandButton
   /// that is to be executed.
   /// 
   /// A single instance of this class can be used with
   /// many RibbonCommandButtons, by passing null to the
   /// constructor and assigning each RibbonCommandButtons's
   /// CommandParameter property to itself, and assigning 
   /// the handler to each button's CommandHandler property 
   /// as shown below:
   /// 
   ///   ModalRibbonCommandButtonHandler handler = 
   ///      new ModalRibbonCommandButtonHandler();
   ///
   ///   RibbonCommandButton button1 = new RibbonCommandButton("REGEN", "ID_REGEN");
   ///   button1.CommandParameter = button1;
   ///   button1.CommandHandler = handler;
   ///   
   ///   RibbonCommandButton button2 = new RibbonCommandButton("REGENALL", "ID_REGENALL");
   ///   button2.CommandParameter = button2;
   ///   button2.CommandHandler = handler;
   ///   
   /// The above can also be accomplished more-easily using 
   /// the SetAsHandler() method:
   /// 
   ///   handler.SetAsHandler(button1, button2);
   ///   
   /// When the instance is used by multiple RibbonCommandButtons,
   /// the Execute() method requires the parameter argument to be
   /// the instance of the RibbonCommandButton that is to execute,
   /// which is done by assigning each button's CommandParameter
   /// property to each button as shown above. The SetAsHandler() 
   /// method performs the required assignments for the programmer.
   ///   
   /// If a ModalRibbonCommandButtonHandler is passed an instance 
   /// of RibbonCommandButton to its constructor, or has an instance 
   /// of a RibbonCommandButton assigned to its Button property, the 
   /// instance cannot be shared by multiple RibbonCommandButtons.
   /// 
   /// DeepExplode of the above can be further automated by simply using
   /// the included ModalRibbonCommandButton, rather than its
   /// base class, as shown in RibbonEventManagerExample.cs.
   /// 
   /// Custom ICommands:
   /// 
   /// Any RibbonCommandItem that uses a custom ICommand handler 
   /// can also provide an implementation of CanExecute() that 
   /// behaves like the CanExecute() method of these classes. 
   /// 
   /// That can be done by just returning the value returned by 
   /// the RibbonEventManager's IsQuiescentDocument property:
   /// 
   ///   public bool CanExecute(object parameter)
   ///   {
   ///      return RibbonEventManager.IsQuiescentDocument;
   ///   }
   /// 
   /// </summary>

   public class ModalRibbonCommandButtonHandler : ModalCommandHandler
   {
      RibbonCommandButton button;
      bool shared = false;

      public ModalRibbonCommandButtonHandler(RibbonCommandButton button = null)
      {
         this.Button = button;
         shared = button == null;
         this.IsModal = true;
      }

      public RibbonCommandButton Button
      {
         get { return button; }
         set
         {
            if(shared && value != null)
               throw new InvalidOperationException("Instance already associated with multiple RibbonCommandButtons");
            if(button != value) 
            {
               if(button != null)
                  button.CommandHandler = null;
               button = value;
               if(button != null)
                  button.CommandHandler = this;
            }
         }
      }

      /// <summary>
      /// Can be used to simplify sharing of a single
      /// instance of this class with many instances of
      /// RibbonCommandButton, by simply calling this
      /// method and passing one or more instances of 
      /// RibbonCommandButton as arguments.
      /// 
      /// Note: Using a single instance of this class as a 
      /// handler for multiple RibbonCommandButtons requires 
      /// each RibbonCommandButton's CommandParameter be
      /// assigned to the RibbonCommandButton instance.
      /// </summary>

      public void SetAsHandler(params RibbonCommandButton[] buttons)
      {
         SetAsHandler((IEnumerable<RibbonCommandButton>) buttons);
      }

      public void SetAsHandler(IEnumerable<RibbonCommandButton> buttons)
      {
         if(this.button != null)
            throw new InvalidOperationException(
               "Instance already associated with a single RibbonCommandButton");
         if(buttons != null && buttons.Any())
         {
            foreach(RibbonCommandButton button in buttons)
            {
               if(button != null)
               {
                  button.CommandHandler = this;
                  button.CommandParameter = button;
                  shared = true;
               }
            }
         }
      }

      public override void Execute(object parameter)
      {
         (button ?? parameter as RibbonCommandButton)?.Execute(null);
      }

      public void Execute(object parameter, string macro)
      {
         RibbonCommandButton btn = (button ?? parameter as RibbonCommandButton);
         if(btn != null && !string.IsNullOrWhiteSpace(macro))
         {
            string oldMacro = btn.Macro;
            try
            {
               btn.Macro = macro;
               btn.Execute(null);
            }
            finally
            {
               btn.Macro = oldMacro;
            }
         }
      }
   }

   public static class ModalRibbonCommandButtonExtensions
   {
      public static void SetDefaultCommandButtonHandler(this RibbonItemCollection items, ModalRibbonCommandButtonHandler handler)
      {
         Assert.IsNotNull(items, nameof(items));
         Assert.IsNotNull(handler, nameof(handler));
         string empty = "(null)";
         foreach(RibbonCommandButton item in items.OfType<RibbonCommandButton>())
         {
            if(item.CommandHandler == null)
            {
               item.CommandHandler = handler;
               item.CommandParameter = item;
            }
         }
      }
   }

}