/// InteropTestHelpers.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime.Extensions;

/// This allows the use of ListBuilder methods 
/// without the class prequalifier:


namespace AcMgdLib.Common.Examples
{
   internal static class InteropTestHelpers
   {
      /// <summary>
      /// Create a Dictionary<string, List<int>> and populate it with
      /// data for testing purposes:
      /// </summary>

      internal static Dictionary<string, IEnumerable<int>> CreateListDictionary()
      {
         var map = new Dictionary<string, IEnumerable<int>>();
         int i = 1;
         int v = 0;
         foreach(string key in new[] { "First", "Second", "Third", "Fourth" })
         {
            List<int> list = new List<int>();
            for(int j = 0; j < (3 * i); j++)
            {
               list.Add(v++);
            }
            map.Add(key, list);
            if(key == "Second")
               map.Add("Dotted", new List<int>() { 99 });
            else
               ++i;
         }
         return map;
      }

      /// <summary>
      /// Create and populate a simple dictionary of 
      /// key/value pairs for testing purposes:
      /// </summary>
      /// <returns></returns>
      internal static Dictionary<string, int> CreateDictionary()
      {
         Dictionary<string, int> result = new Dictionary<string, int>();
         int i = 1;
         foreach(var key in new[] { "One", "Two", "Three", "Four", "Five", "Six" })
            result[key] = i++;
         return result;
      }


      public static void Dump<TKey, TValue>(this Dictionary<TKey, IEnumerable<TValue>> map, string label = null)
      {
         WriteLine((label ?? "") + map.Format());
      }

      public static void Dump(this ResultBuffer rb, string label = null)
      {
         WriteLine((label ?? "") + rb.Cast<TypedValue>().Format());
      }

      public static void Dump(this IEnumerable<TypedValue> items, string label = null)
      {
         WriteLine((label ?? "") + items.Format());
      }

      public static string Format<TKey, TValue>(this Dictionary<TKey, IEnumerable<TValue>> map)
      {
         StringBuilder sb = new StringBuilder();
         string name = map.GetType().CSharpName();
         sb.Append(name + ":\n");
         foreach(var pair in map)
         {
            sb.AppendLine($"{pair.Key} = "
               + string.Join(", ", pair.Value.Select(v => v.ToString())));
         }
         return sb.ToString();
      }

      public static void WriteLine(this object value, params object[] args)
      {
         Write("\n" + value?.ToString() ?? "(null)", args);
      }

      public static void Write(this object value, params object[] args)
      {
         string val = value?.ToString() ?? "(null)";
         if(args.Length == 0)
            Application.DocumentManager.MdiActiveDocument?
               .Editor.WriteMessage(val);
         else
            Application.DocumentManager.MdiActiveDocument?
               .Editor.WriteMessage(val, args);

      }

      public static bool IsEqualTo<TKey, TValue>(this Dictionary<TKey, IEnumerable<TValue>> left, Dictionary<TKey, IEnumerable<TValue>> right)
      {
         if(left.Count != right.Count)
            return false;
         if(left.Comparer.GetType() != right.Comparer.GetType())
            return false;
         foreach(var pair in left)
         {
            if(!right.TryGetValue(pair.Key, out var value))
               return false;
            if(!pair.Value.SequenceEqual(value))
               return false;
         }
         return true;
      }



   }

}




