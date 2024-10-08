﻿/// AcConsole.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for the AcDbLinq library.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Diagnostics.Extensions;
using DeepCloneMappingExample;
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
      static DocumentCollection docs = Application.DocumentManager;
      
      /// <summary>
      /// String.Format() and Editor.WriteMessage() can have problems with
      /// the value of the ContentRTF property of Mtext objects, requiring
      /// special handling.
      /// </summary>

      public static void Write(string fmt, params object[] args)
      {
         var ed = docs.MdiActiveDocument?.Editor;
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
         Write(new StackTrace(1, false).ToString());
      }

      public static void TraceCaller(int skip = 0)
      {
         var st = new StackTrace(1 + skip, false);
         Write(st.GetFrame(0).ToString());
      }

      [Conditional("DEBUG")]
      public static void Trace(string fmt, params object[] args)
      {
         Write($"{Caller()}: {string.Format(fmt, args)}");
      }

      [Conditional("DEBUG")]
      public static void Trace(this object target, string fmt, params object[] args)
      {
         Write($"{CallerOf(target)}: {string.Format(fmt, args)}");
      }

      static string Caller(bool withType = true, int skip = 0)
      {
         var m = new StackTrace(2 + skip, false).GetFrame(0).GetMethod();
         if(withType)
            return $"{m.ReflectedType.CSharpName()}.{m.Name}()"; 
         else
            return $"{m.Name}()";
      }

      static string CallerOf(object instance, int skip = 0)
      {
         var m = new StackTrace(2 + skip, false).GetFrame(0).GetMethod();
         if(instance != null)
            return $"{instance.GetType().CSharpName()}.{m.Name}()";
         else
            return $"{m.Name}()";
      }

      public static string GetCallingFrame()
      {
         return new StackTrace(1, false).GetFrame(1).ToString();
      }

      public static MethodBase GetCallingMethod(int skip = 0)
      {
         return new StackTrace(skip + 1, false).GetFrame(0).GetMethod();
      }

      public static string GetCaller(bool withType = true, int skip = 0)
      {
         var method = GetCallingMethod(skip);
         if(withType)
            return $"{method.ReflectedType.CSharpName()}.{method.Name}";
         else
            return method.Name;
      }

      [Conditional("DEBUG")]
      public static void Report([CallerMemberName] string caller = "(unknown)")
      {
         Write($"*** {caller} ***\n");
      }

      [Conditional("DEBUG")]
      public static void ReportMsg(string msg, [CallerMemberName] string caller = "(unknown)")
      {
         if(!string.IsNullOrEmpty(msg))
            msg = $" [{msg}]";
         else
            msg = string.Empty;
         Write($"*** {caller} *** {msg}");
      }

      public static void TraceProps(object target, string delimiter = "\n")
      {
         WriteLine(GetCallingFrame() + GetProperties(target, delimiter));
      }

      public static string GetProperties(object target, string delimiter = "\n")
      {
         StringBuilder sb = new StringBuilder();
         var props = TypeDescriptor.GetProperties(target);
         if(props != null && props.Count > 0)
         {
            string targetstr = target?.ToString() ?? "(null)";
            sb.Append($"\n====[{targetstr}]====\n");
            if(target != null)
            {
               foreach(PropertyDescriptor prop in TypeDescriptor.GetProperties(target))
               {
                  object value = GetValue(target, prop);
                  string str = $"  {prop.Name} = " + value.Format() + delimiter;
                  sb.Append(str);
               }
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
         TraceProps(obj);
         if(showTextScreen)
            Application.DisplayTextScreen = true;
      }
   }

}
