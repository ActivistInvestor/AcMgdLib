/// ModalRibbonCommandButton.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license


using System.Windows.Input;

#pragma warning disable CS0612 // Type or member is obsolete


namespace Autodesk.AutoCAD.Ribbon.Extensions
{
   /// <summary>
   /// A specialization of RibbonCommandButton that is enabled
   /// only when there is an active, quiescent document
   /// </summary>

   public class ModalRibbonCommandButton : RibbonCommandButton
   {
      ModalRibbonCommandButtonHandler commandHandler;
      public ModalRibbonCommandButton()
      {
         this.CommandHandler = new ModalRibbonCommandButtonHandler(this);
      }

      public ModalRibbonCommandButton(string sMenuMacro, string sMenuMacroId = null, ModalRibbonCommandButtonHandler handler = null)
         : base(sMenuMacro, sMenuMacroId ?? $"ID_{sMenuMacro}") 
      {
         if(handler != null)
            handler.SetAsHandler(this);
         else
            this.CommandHandler = new ModalRibbonCommandButtonHandler(this);
      }

      public ModalRibbonCommandButton(object value, ModalRibbonCommandButtonHandler handler = null)
         : base(value)
      {
         if(handler != null)
            handler.SetAsHandler(this);
         else
            this.CommandHandler = new ModalRibbonCommandButtonHandler(this);
      }
   }

}