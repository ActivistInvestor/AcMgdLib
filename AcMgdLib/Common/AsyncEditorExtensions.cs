/// AsyncEditorExtensions.cs
/// 
/// AcMgdLib - https://github.com/ActivistInvestor/AcMgdLib
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.


using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.EditorInput
{
   public static partial class AsyncEditorExtensions
   {
      static System.Threading.SynchronizationContext context = AcRx.SynchronizationContext.Current;

      public static void WriteMessageAsync(this Editor editor, string msg, params object[] args)
      {
         context ??= AcRx.SynchronizationContext.Current;
         if(context != null)
            context.Post(o => editor.WriteMessage(msg, args), null);
      }

      /// <summary>
      /// This method should be called on the main thread
      /// (e.g., from IExtensionApplication.Initialize())
      /// to set the synchronization context. 
      /// </summary>
      
      public static void Initialize()
      {
         context ??= AcRx.SynchronizationContext.Current;
      }

   }


}
