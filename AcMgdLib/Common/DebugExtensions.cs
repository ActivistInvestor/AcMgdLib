using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.Diagnostics.Extensions
{

   public static class DebugExtensions
   {
      static MethodInfo defaultMethod = 
         ((Func<object, string>)ToDebugString).Method;

      static Dictionary<Type, Delegate> delegates = new Dictionary<Type, Delegate>();

      /// <summary>
      /// Dynamically-resolves overloads of ToDebugString()
      /// and dispatches calls to them based on the runtime
      /// type of the first argument.
      /// 
      /// To implement a custom debug formatter for a given
      /// type of object, add a method to this class with the
      /// name 'ToDebugString', that takes the type that it
      /// is to provide formatting for as its only argument.
      /// See the existing ToDebugString() methods below for
      /// examples.
      /// 
      /// Note that within a given AppDomain, there can exist
      /// one and only one ToDebugString() method for a given
      /// type. 
      /// 
      /// </summary>

      public static string Format(this object obj, string format = "{0}")
      {
         try
         {
            if(obj == null)
               return nullstr;
            Delegate func;
            Type type = obj.GetType();
            func = GetDelegate(type);
            if(func == null)
               return $"(error: Runtime binding failed for type: {type.Name})";
            string result = (string)func.DynamicInvoke(obj);
            return string.Format(result, format ?? "{0}");
         }
         catch(System.Exception ex)
         {
            return $"Format() Error: {ex.ToString()}";
         }
      }

      static Delegate GetDelegate(this Type type)
      {
         Delegate func;
         if(!delegates.TryGetValue(type, out func))
         {
            MethodInfo method = TryGetMethod(type);
            if(method == null)
               return null;
            var p0 = Expression.Parameter(type, "arg");
            Expression arg = p0;
            if(type != method.GetParamType() && ! type.IsClass)
               arg = Expression.Convert(p0, typeof(object));
            func = Expression.Lambda(Expression.Call(method, arg), p0).Compile();
            delegates[type] = func;
         }
         return func;
      }

      static Type GetParamType(this MethodInfo m, int index = 0)
      {
         var array = m.GetParameters();
         return array.Length > index ? array[index].ParameterType : null ;
      }

      static MethodInfo TryGetMethod(Type argType)
      {
         if(argType == null)
            throw new ArgumentNullException(nameof(argType));
         MethodInfo method = null;
         while(argType != null)
         {
            method = typeof(DebugExtensions).GetMethod(
               nameof(ToDebugString),
               BindingFlags.Static | BindingFlags.Public,
               null, new Type[] { argType }, null);
            if(method != null)
               return method;
            if(argType == typeof(object) || !argType.IsClass)
               return defaultMethod;
            argType = argType.BaseType;
         }
         return method;
      }

      /// <summary>
      /// Default handler for types that do not have
      /// a more-specific matching overload.
      /// </summary>

      public static string ToDebugString(object obj)
      {
         return obj?.ToString() ?? nullstr;
      }

      public static string ToDebugString(this IdMapping map)
      {
         return $"\n{GetProperties(map)}";
      }

      public static string ToDebugString(this Database db)
      {
         if(db == null)
            return nullstr;
         return string.Format("Database (0x{0:x}) [{1}]",
            db.UnmanagedObject.ToInt64(),
            string.IsNullOrWhiteSpace(db.Filename) ? "(unnamed)" :
               Path.GetFileName(db.Filename));
      }

      public static string ToDebugString(this ObjectId id)
      {
         if(id.IsNull)
            return nullstr;
         else if(id.IsValid)
         {
            return string.Format("{0} (0x{1}){2}",
               id.ObjectClass.Name,
               id.Handle.ToString(),
               id.IsErased ? " (erased)" : "");
         }
         return "(invalid ObjectId)";
      }

      public static string ToDebugString(this DBObject dbObj)
      {
         if(dbObj == null)
            return nullstr;
         string name = TryGetName(dbObj);
         if(!string.IsNullOrEmpty(name))
            name = $"[{name}]";
         else
            name = string.Empty;
         string handle = string.Format("0x{0:X}", dbObj.Handle.Value);
         return $"{dbObj.GetType().Name} ({handle}) {name}";
      }

      static readonly string nullstr = "(null)";

      public static string ToDebugString(this IdPair pair)
      {
         return $"{pair.Key.ToDebugString()} => {pair.Value.ToDebugString()} " +
            $" IsCloned: {pair.IsCloned}  IsPrimary: {pair.IsPrimary}";
      }

      /// <summary>
      /// Trys to get the Name property of the given DBObject
      /// if it has one, or if the DBObject's owner is a 
      /// DBDictionary, it trys to get the object's dictionary
      /// key.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns></returns>
      
      private static string TryGetName(DBObject obj)
      {
         string result = string.Empty;
         if(obj == null)
            return result;
         if(obj.OwnerId.IsA<DBDictionary>())
         {
            try
            {
               using(DBDictionary owner = obj.OwnerId.Open<DBDictionary>())
               {
                  try
                  {
                     result = owner.GetKeyOf(obj.ObjectId);
                  }
                  catch(System.Exception ex)
                  {
                     result = $"({ex.Message})";
                  }
               }
            }
            catch(System.Exception ex)
            {
               result = $"({ex.Message})";
            }
         }
         else
         {
            try
            {
               result = ((dynamic)obj).Name;
            }
            catch
            {
            }
         }
         return result;
      }

      public static string GetKeyOf(this DBDictionary owner, ObjectId key, string defaultResult = "")
      {
         foreach(DictionaryEntry entry in owner)
         {
            if(key == (ObjectId) entry.Value)
               return (string) entry.Key;
         }
         return defaultResult;
      }

      public static string FormatHandle(this Handle handle, bool parens = false)
      {
         if(parens)
            return string.Format("(0x{0:X})", handle.Value);
         else
            return string.Format("0x{0:X}", handle.Value);
      }

      public static string GetProperties(this object target, string delimiter = "\n", int indent = 2)
      {
         StringBuilder sb = new StringBuilder();
         var props = TypeDescriptor.GetProperties(target);
         if(props != null && props.Count > 0)
         {
            string pad = new string(' ', indent);
            string targetstr = target?.ToString() ?? nullstr;
            sb.Append($"\n{pad}{targetstr} properties:\n");
            if(target != null)
            {
               pad = pad + "   ";
               foreach(PropertyDescriptor prop in TypeDescriptor.GetProperties(target))
               {
                  string value = GetValue(target, prop);
                  string str = $"{pad}{prop.Name} = " + value + delimiter;
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
            return obj != null ? obj.Format() : nullstr;
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

      public static string DumpFormat(this IdMapping idMap)
      {
         var sb = new StringBuilder();
         sb.AppendLine("\n\n---------------------------------------------------");
         sb.AppendLine(idMap.OriginalDatabase.Format("  Original Database: "));
         sb.AppendLine(idMap.DestinationDatabase.Format("  Destination Database: "));
         foreach(IdPair pair in idMap)
         {
            sb.AppendLine($"{pair.Key.Format()} => {pair.Value.Format()}");
         }
         sb.AppendLine("\n---------------------------------------------------\n\n");
         return sb.ToString();
      }

      public static string Dump(this IdMapping idMap)
      {
         var sb = new StringBuilder();
         AcConsole.Write("\n\n---------------------------------------------------");
         AcConsole.Write(idMap.OriginalDatabase.Format("  Original Database: "));
         AcConsole.Write(idMap.DestinationDatabase.Format("  Destination Database: "));
         foreach(IdPair pair in idMap)
         {
            AcConsole.Write(pair.ToDebugString());
         }
         AcConsole.Write("\n---------------------------------------------------\n\n");
         return sb.ToString();
      }

   }

}