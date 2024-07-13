/// AcConsole.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for the AcDbLinq library.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcRx = Autodesk.AutoCAD.Runtime;

/// A class that implements a proxy for diagnostic 
/// trace output, that can be used in-place of other
/// trace diagnostic functionlity that may not be
/// included with dependent code.
/// 
/// AcConsole simply routes trace output to the
/// AutoCAD console in lieu of other trace output 
/// functionality not included in this library.
/// 
/// Routing trace output to the AutoCAD console allows
/// it to be viewed 'in-context', along with standard
/// console output, to more-easily determine the order 
/// in which calls to these methods occur, relative to 
/// standard output messages and prompts.

namespace Autodesk.AutoCAD.Runtime
{
   public static class AcConsole
   {
      /// <summary>
      /// String.Format() and Editor.WriteMessage() can have problems with
      /// the value of the ContentRTF property of Mtext objects, requiring
      /// special handling.
      /// </summary>

      public static void Write(string fmt, params object[] args)
      {
         var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
         if(ed != null)
         {
            try
            {
               if(args.Length == 0)
                  ed.WriteMessage(fmt + "\n");
               else
                  ed.WriteMessage(fmt + "\n", args);
            }
            catch(System.Exception ex)
            {
               ed.WriteMessage($"\n*** Error: {ex.Message}");
            }
         }
      }

      public static void WriteLine(string fmt, params object[] args)
      {
         Write("\n" + fmt, args);
      }

      public static void TraceThread(string fmt = null, params object[] args)
      {
         if(!string.IsNullOrWhiteSpace(fmt))
            Write(fmt, args);
         Write($"Current Thread = {Thread.CurrentThread.ManagedThreadId}");
      }

      public static void TraceModule(Type type = null)
      {
         Write($"\nUsing {(type ?? typeof(AcConsole)).Assembly.Location}");
      }

      public static void TraceContext(string fmt = null, params object[] args)
      {
         var appctx = Application.DocumentManager.IsApplicationContext ?
            "Application" : "Document";
         if(!string.IsNullOrWhiteSpace(fmt))
            Write(fmt + $" Context: {appctx}", args);
         else
            Write($"\nContext: {appctx}");
      }

      public static void StackTrace(string fmt = null, params object[] args)
      {
         if(!string.IsNullOrWhiteSpace(fmt))
            WriteLine(fmt, args);
         Write(new StackTrace(1, true).ToString());
      }

      public static void TraceProperties(object target, string delimiter = "\n")
      {
         WriteLine(GetProperties(target, delimiter));
      }

      public static string GetProperties(object target, string delimiter = "\n")
      {
         StringBuilder sb = new StringBuilder();
         string targetstr = target?.ToString() ?? "(null)";
         sb.Append($"====[{targetstr}]====\n");
         if(target != null)
         {
            foreach(PropertyDescriptor prop in TypeDescriptor.GetProperties(target))
            {
               object value = GetValue(target, prop);
               string str = $"  {prop.Name} = " + value.ToString() + delimiter;
               sb.Append(str);
            }
         }
         return sb.ToString();
      }

      static string GetValue(object target, PropertyDescriptor pd)
      {
         object obj = null;
         try
         {
            obj = pd.GetValue(target);
            return obj != null ? obj.ToString() : "(null)";
         }
         catch(System.Exception ex)
         {
            if(ex.InnerException is AcRx.Exception inner)
            {
               return $"({inner.ErrorStatus})";
            }
            return "Error: " + ex.Message;
         }
         finally
         {
            if(obj != null && Marshal.IsComObject(obj))
               Marshal.ReleaseComObject(obj);
         }
      }

      public static void Dump(this DBObject obj, bool showTextScreen = true)
      {
         TraceProperties(obj);
         if(showTextScreen)
            Application.DisplayTextScreen = true;
      }
   }

}
