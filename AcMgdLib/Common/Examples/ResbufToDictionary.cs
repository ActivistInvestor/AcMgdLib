/// TestResbufToDictionary.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

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
      /// Exercises/tests the ToListDictionary() and
      /// ToResultBuffer() extension methods, by writing
      /// a Dictionary<string, int> to an Xrecord.
      /// </summary>
      [CommandMethod("SETXREC")]
      public static void SetXRec()
      {
         var map = CreateDictionary();
         map.Dump();
         var peo = new PromptEntityOptions("\nPick an entity: ");
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetEntity(peo);
         if(psr.Status != PromptStatus.OK)
            return;
         var rb = map.ToResultBuffer(DxfCode.Text, DxfCode.Int32);
         try
         {
            using(var tr = new DocumentTransaction())
            {
               var ent = tr.GetObject<Entity>(psr.ObjectId, OpenMode.ForWrite);
               ent.SetXRecordData("test", tr, rb);
               tr.Commit();
            }
         }
         catch(System.Exception ex)
         {
            ed.WriteMessage(ex.ToString());
         }
      }

      /// <summary>
      /// Exercises/tests the ToListDictionary() and
      /// ToResultBuffer() extension methods, by reading
      /// a Dictionary<string, int> from an Xrecord that
      /// was written by the above command.
      /// </summary>
      [CommandMethod("GETXREC")]
      public static void GetXRec()
      {
         var map = CreateDictionary();
         map.Dump();
         var peo = new PromptEntityOptions("\nPick an entity: ");
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetEntity(peo);
         if(psr.Status != PromptStatus.OK)
            return;
         try
         {
            using(var tr = new DocumentTransaction())
            {
               var ent = tr.GetObject<Entity>(psr.ObjectId, OpenMode.ForWrite);
               var rb = ent.GetXRecordData("test");
               var dict = rb.ToListDictionary<string, int>();
               dict.Dump();
               tr.Commit();
            }
         }
         catch(System.Exception ex)
         {
            ed.WriteMessage(ex.ToString());
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

         var map = CreateDictionary();

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

      static Dictionary<string, IEnumerable<int>> CreateDictionary()
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
            ++i;
         }
         return map;
      }
   }

   public static class Helpers
   {

      public static void Dump<TKey, TValue>(this Dictionary<TKey, IEnumerable<TValue>> map)
      {
         WriteLn(map.Format());
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

      public static string Format(this TypedValue typedValue, string format = null)
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




