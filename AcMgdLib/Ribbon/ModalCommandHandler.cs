
/// ModalCommandHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license


using System;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Ribbon.Extensions;

#pragma warning disable CS0612 // Type or member is obsolete

namespace Autodesk.Windows.Extensions
{
   /// <summary>
   /// An abstract base for ICommand-based types that enables
   /// support for extended querying of CanExecute() by the
   /// WPF framework, in conjunction with included APIs.
   /// 
   /// Use this class as a base type for ICommand-based types
   /// when you want the command to only be available when the
   /// AutoCAD drawing editor is in a quiescent state.
   /// 
   /// </summary>
   
   public abstract class ModalCommandHandler : ICommand
   {
      public event EventHandler CanExecuteChanged;

      static ModalCommandHandler()
      {
         RibbonEventManager.QueryCanExecute = true;
      }

      public ModalCommandHandler()
      {
         IsModal = true;
      }

      /// <summary>
      /// Indicates if the RibbonCommandItem associated
      /// with the instance should be disabled when there
      /// is an active command.
      /// </summary>
      public virtual bool IsModal { get; set; }   

      /// <summary>
      /// If IsModal is true, this enables the command only 
      /// when there is an active document that is quiescent:
      /// </summary>
      
      public virtual bool CanExecute(object parameter)
      {
         return IsModal ? RibbonEventManager.IsQuiescentDocument : true;
      }

      public abstract void Execute(object parameter);

      protected static Editor Editor =>
         Application.DocumentManager.MdiActiveDocument?.Editor;
   }

}