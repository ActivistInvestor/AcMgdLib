
/// ModalRibbonItemCommandHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license

#pragma warning disable CS0612 // Type or member is obsolete

using System;

namespace Autodesk.Windows.Extensions
{
   /// <summary>
   /// This class surfaces only the CanExecute() functionality 
   /// provided by the abstract base type, which can be used
   /// for various types of RibbonItems as required.
   /// 
   /// To implement specific command functionality, this class
   /// can be specialized with an overridden Execute() method,
   /// or a delegate can be provided to the constructor that
   /// executes the command.
   /// </summary>
   
   public class ModalRibbonItemCommandHandler : ModalCommandHandler
   {
      Action<object> commandHandler = null;
      Func<object, bool> canExecute = null;
      public ModalRibbonItemCommandHandler(
         Action<object> commandHandler = null,
         Func<object, bool> canExecute = null)
      {
         this.commandHandler = commandHandler;
         this.canExecute = canExecute;
      }

      public override bool CanExecute(object parameter)
      {
         if(canExecute != null)
            return canExecute(parameter);
         else
            return base.CanExecute(parameter);
      }

      public override void Execute(object parameter)
      {
         commandHandler?.Invoke(parameter);
      }
   }

}