/// TestResbufToDictionary.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.Common.Examples
{
   public static class TestResbufToDictionary
   {
      /// <summary>
      /// Exercises/tests the ToResultBuffer() extension method by
      /// writing a Dictionary<string, List<int>> to an Xrecord.
      /// </summary>

      [CommandMethod("WRITEXRECORD")]
      public static void WriteXrecord()
      {
         var map = CreateListDictionary();
         var peo = new PromptEntityOptions("\nPick an entity: ");
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetEntity(peo);
         if(psr.Status != PromptStatus.OK)
            return;
         var resbuf = map.ToResultBuffer(DxfCode.Text, DxfCode.Int32);
         using(var trans = new DocumentTransaction())
         {
            var ent = trans.GetObject<Entity>(psr.ObjectId, OpenMode.ForWrite);
            ent.SetXRecordData("test", trans, resbuf);
            trans.Commit();
            ed.WriteMessage("\nDictionary<string, List<int>> written to Xrecord: ");
            map.Dump();
         }
      }

      /// <summary>
      /// Exercises/tests the ToListDictionary() extension method, 
      /// by reading a Dictionary<string, List<int>> from an Xrecord 
      /// that was written by the WRITEXRECORD command.
      /// </summary>
      
      [CommandMethod("READXRECORD")]
      public static void ReadXrecord()
      {
         var peo = new PromptEntityOptions("\nPick an entity: ");
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetEntity(peo);
         if(psr.Status != PromptStatus.OK)
            return;
         using(var tr = new DocumentTransaction())
         {
            try
            {
               var ent = tr.GetObject<Entity>(psr.ObjectId, OpenMode.ForWrite);
               var resbuf = ent.GetXRecordData("test");
               if(resbuf == null)
               {
                  ed.WriteMessage("\nNo Xrecord with the key 'test' was found.");
               }
               else
               {
                  var map = resbuf.ToListDictionary<string, int>();
                  ed.WriteMessage("\nDictionary<string, List<int>> read from Xrecord: ");
                  map.Dump();
               }
               tr.Commit();
            }
            catch(System.Exception ex)
            {
               ed.WriteMessage(ex.ToString());
            }
         }
      }

      /// <summary>
      /// Utility command to view 'raw' Xrecord data:
      /// </summary>
      
      [CommandMethod("DUMPXREC")]
      public static void DumpXrecord()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var peo = new PromptEntityOptions("\nPick an entity: ");
         var psr = ed.GetEntity(peo);
         if(psr.Status != PromptStatus.OK)
            return;
         PromptStringOptions pso = new PromptStringOptions("\nKey: ");
         pso.AllowSpaces = false;
         pso.DefaultValue = "test";
         pso.UseDefaultValue = true;
         var sr = ed.GetString(pso);
         if(sr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(sr.StringResult))
            return;
         string key = sr.StringResult.Trim();
         using(var tr = new DocumentTransaction())
         {
            var ent = tr.GetObject<Entity>(psr.ObjectId, OpenMode.ForWrite);
            var resbuf = ent.GetXRecordData(key);
            if(resbuf != null)
               ed.WriteMessage(resbuf.Format());
            else
               ed.WriteMessage($"\nAn Xrecord having the key {key} was not found.");
            tr.Commit();
         }
      }

      /// <summary>
      /// Tests/excerises the ToResultBuffer() and 
      /// ToListDictionary() extension methods that 
      /// convert between Dictionaries and ResultBuffers.
      /// </summary>

      [CommandMethod("TESTRESBUFTODICT")]
      public static void ResbufToDict()
      {
         /// Create and populate dictionary:

         var map = CreateListDictionary();

         /// Save the initial dictionary for comparision
         /// with the instance that is reconstituted from 
         /// the ResultBuffer;

         var originalMap = map; 

         /// Dump contents of dictionary:
         Write("\nInitial Dictionary<string, List<int>>:\n");
         map.Dump();

         /// Convert dictionary to resultbuffer:

         var resbuf = map.ToResultBuffer(DxfCode.Text, DxfCode.Int32);

         /// Dump contents of resultbuffer:

         Write("\nDictionary<string, List<int>> converted to ResultBuffer:\n");
         foreach(TypedValue tv in resbuf)
            tv.Format().WriteLn();

         /// Convert the ResultBuffer back to a dictionary.
         /// The first generic argument is the type of the key,
         /// and the second is the type of the list elements.

         map = resbuf.ToListDictionary<string, int>();

         /// Dump contents of reconstituted dictionary:

         Write("\nResultBuffer converted back to Dictionary<string, List<int>>:\n");
         map.Dump();

         /// Compare the original and reconstituted dictionaries:
         Write($"\nmap.IsEqualTo(oldmap) = {map.IsEqualTo(originalMap)}");

         Application.DisplayTextScreen = true;

         void Write(string fmt, params object[] args)
         {
            fmt.WriteLn(args);
         }
      }

      [LispFunction("get-list-dict")]
      public static ResultBuffer GetListDictionary(ResultBuffer args)
      {
         var dict = CreateListDictionary();
         ResultBuffer rb = dict.ToResultBuffer<string, int>(
            LispDataType.Text, LispDataType.Int32);
         rb = new ResultBuffer(rb.Cast<TypedValue>().ToLispList().ToArray());
         return rb;
      }

      /// <summary>
      /// Round-trip tests the ToResultBuffer() overload that 
      /// takes LispDataType arguments and its ToLispDictionary() 
      /// complementing method. 
      /// </summary>
      
      [CommandMethod("DICT2ALIST")]
      public static void Dict2AList()
      {
         var dict = CreateListDictionary();
         dict.Dump("\nOriginal dictionary:\n");
         var resbuf = dict.ToResultBuffer<string, int>(
            LispDataType.Text, LispDataType.Int32);
         resbuf.Format().WriteLn();
         var result = resbuf.ToLispDictionary<string, int>();
         result.Dump("\nConverted dictionary:\n");
         $"result.IsEqualTo(dict) = {result.IsEqualTo(dict)}".WriteLn();
      }

      //public static IEnumerable<TypedValue> List(params object[] args)
      //{
      //   var p = Autodesk.AutoCAD.Runtime.Marshaler.ObjectsToResbuf(args);
      //   ResultBuffer rb = ResultBuffer.Create(p, true);
      //   yield return new TypedValue((short)LispDataType.ListBegin);
      //   foreach(TypedValue tv in rb)
      //      yield return tv;
      //   var p2 = Autodesk.AutoCAD.Runtime.Marshaler.ObjectToResbuf("foo");
      //   yield return new TypedValue((short)LispDataType.ListEnd);
      //}

      //static void Test()
      //{
      //   var list = List("One", 2, 300.0, "Four", 555);
      //}



      /// <summary>
      /// Create a Dictionary<string, List<int>> and populate it with
      /// data for testing purposes:
      /// </summary>

      static Dictionary<string, IEnumerable<int>> CreateListDictionary()
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

      static Dictionary<string, int> CreateDictionary()
      {
         Dictionary<string, int> result = new Dictionary<string, int>();
         int i = 1;
         foreach(var key in new[] { "One", "Two", "Three", "Four", "Five", "Six"})
            result[key] = i++;
         return result;
      }
   }

   public static class Helpers
   {

      public static void Dump<TKey, TValue>(this Dictionary<TKey, IEnumerable<TValue>> map, string label = null)
      {
         WriteLn((label ?? "") + map.Format());
      }

      public static string Format<TKey, TValue>(this Dictionary<TKey, IEnumerable<TValue>> map)
      {
         StringBuilder sb = new StringBuilder();
         foreach(var pair in map)
         {
            sb.AppendLine($"{pair.Key} = "
               + string.Join(", ",
                     pair.Value.Select(v => v.ToString())));
         }
         return sb.ToString();
      }

      public static void WriteLn(this object value, params object[] args)
      {
         string val = value?.ToString() ?? "(null)";
         Application.DocumentManager.MdiActiveDocument?
            .Editor.WriteMessage("\n" + val, args);
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

      const short LispDataTypeMin = (short)LispDataType.None - 1;

      public static string Format(this ResultBuffer rb, string format = "\n{0} = {1}")
      {
         StringBuilder sb = new StringBuilder();
         foreach(TypedValue tv in rb)
            sb.Append(tv.Format(format));
         return sb.ToString();
      }

      public static string Format(this TypedValue typedValue, string format = "{0} = {1}")
      {
         if(format == null)
            format = "{0} = {1}";
         short code = typedValue.TypeCode;
         object val = typedValue.Value ?? "(null)";
         if(code > LispDataTypeMin)
            return string.Format(format, (LispDataType) code, val);
         else
            return string.Format(format, (DxfCode)code, val);
      }

   }



}




