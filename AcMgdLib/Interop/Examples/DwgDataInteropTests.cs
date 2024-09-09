/// DwgDataInteropTests.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using Autodesk.AutoCAD.Runtime.LispInterop;

/// This allows the use of ListBuilder methods 
/// without the class prequalifier:


namespace AcMgdLib.Interop.Examples
{
   public static class DwgDataInteropTests
   {
      /// <summary>
      /// Exercises/tests the ToResultBuffer() extension method by
      /// writing a Dictionary<string, List<int>> to an Xrecord.
      /// </summary>

      [CommandMethod("WRITEXRECORD")]
      public static void WriteXrecord()
      {
         var map = InteropTestHelpers.CreateListDictionary();
         var peo = new PromptEntityOptions("\nPick an entity: ");
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetEntity(peo);
         if(psr.Status != PromptStatus.OK)
            return;
         var resbuf = map.ToResultBuffer(DxfCode.Text, DxfCode.Int32);
         using(var trans = new DocumentTransaction())
         {
            var ent = trans.GetObject<Entity>(psr.ObjectId, OpenMode.ForRead);
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
               var ent = tr.GetObject<Entity>(psr.ObjectId, OpenMode.ForRead); //  OpenMode.ForWrite);
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
      /// Utility command to list raw Xrecord data:
      /// </summary>

      [CommandMethod("DUMPXREC")]
      public static void DumpXrecord()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         PromptStringOptions pso = new PromptStringOptions("\nKey: ");
         pso.AllowSpaces = false;
         pso.DefaultValue = "test";
         pso.UseDefaultValue = true;
         var sr = ed.GetString(pso);
         if(sr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(sr.StringResult))
            return;
         string key = sr.StringResult.Trim();
         var peo = new PromptEntityOptions("\nPick an entity: ");
         while(true)
         {
            var psr = ed.GetEntity(peo);
            if(psr.Status != PromptStatus.OK)
               return;
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

         var map = InteropTestHelpers.CreateListDictionary();

         /// Save the initial dictionary for comparision
         /// with the instance that is reconstituted from 
         /// the ResultBuffer;

         var originalMap = map;

         /// Dump contents of dictionary:
         InteropTestHelpers.Write("\nInitial Dictionary<string, List<int>>:\n");
         map.Dump();

         /// Convert dictionary to resultbuffer:

         var resbuf = map.ToResultBuffer(DxfCode.Text, DxfCode.Int32);

         /// Dump contents of resultbuffer:

         InteropTestHelpers.Write("\nDictionary<string, List<int>> converted to ResultBuffer:\n");
         foreach(TypedValue tv in resbuf)
            InteropTestHelpers.Write(tv.Format());
         // tv.Format().WriteLine();

         /// Convert the ResultBuffer back to a dictionary.
         /// The first generic argument is the type of the key,
         /// and the second is the type of the list elements.

         map = resbuf.ToListDictionary<string, int>();

         /// Dump contents of reconstituted dictionary:

         InteropTestHelpers.Write("\nResultBuffer converted back to Dictionary<string, List<int>>:\n");
         map.Dump();

         /// Compare the original and reconstituted dictionaries:
         InteropTestHelpers.Write($"\nmap.IsEqualTo(originalMap) = {map.IsEqualTo(originalMap)}");

         Application.DisplayTextScreen = true;

         void Write(string fmt, params object[] args)
         {
            fmt.WriteLine(args);
         }
      }

      /// <summary>
      /// Round-trip tests the ToResultBuffer() overload that 
      /// takes LispDataType arguments and its ToLispDictionary() 
      /// complementing method. 
      /// </summary>

      [CommandMethod("DICT2ALIST")]
      public static void Dict2AList()
      {
         var dict = InteropTestHelpers.CreateListDictionary();
         dict.Dump("\nOriginal dictionary:\n");
         var resbuf = dict.ToResultBuffer<string, int>(
            LispDataType.Text, LispDataType.Int32);
         resbuf.Format().Write(); //  Line();
         var result = resbuf.ToLispDictionary<string, int>();
         result.Dump("\nConverted dictionary:\n");
         $"result.IsEqualTo(dict) = {result.IsEqualTo(dict)}".WriteLine();
      }

   }

}




