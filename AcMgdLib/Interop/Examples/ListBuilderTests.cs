/// LispInteropTests.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System.Collections.Generic.Extensions;
using System.Extensions;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.EditorInput.Extensions;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using Autodesk.AutoCAD.Runtime.LispInterop;

/// This allows the use of ListBuilder methods 
/// without the class prequalifier:

using static Autodesk.AutoCAD.Runtime.LispInterop.ListBuilder;

namespace AcMgdLib.Interop.Examples
{
   public static class ListBuilderTests
   {

      /// <summary>
      /// Uses the ListBuilder class to build and a return 
      /// a list of arbitrary-complexity back to LISP. The
      /// ListBuilder's List() method accepts an object[]
      /// array, which it will transform into an array of
      /// TypedValues that can be returned back to LISP as
      /// a list.
      /// 
      /// Collection support: 
      /// 
      /// The List() method supports collections, and will
      /// convert them to lists containing their elements, 
      /// as can be seen in this example, which includes both
      /// a Point3dCollection and an ObjectIdCollection.
      ///   
      /// Simplified Syntax:
      /// 
      /// Note that static members of ListBuilder can be used
      /// directly without prequalifying them with the name
      /// of that class, which is made possible through the
      /// use of the 'using static' declaration:
      /// 
      ///   using static Autodesk.AutoCAD.Runtime.LispInterop.ListBuilder;
      ///   
      /// </summary>

      [LispFunction("mgd-list")]
      public static ResultBuffer GetMgdList(ResultBuffer args)
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Database db = doc.Database;

         Point3d[] ptsArray = new Point3d[]
         {
            new(2, 2, 0),
            new(12, 5, 0),
            new(22, 7, 9),
            new(14, 6, 0)
         };

         /// Create a Point3dCollection
         Point3dCollection points = new Point3dCollection(ptsArray);

         /// Create an ObjectIdCollection
         ObjectIdCollection objectIds = new ObjectIdCollection();
         objectIds.Add(db.LayerZero);
         objectIds.Add(db.LayerTableId);
         objectIds.Add(db.BlockTableId);
         objectIds.Add(db.GroupDictionaryId);

         /// Build a list and return it back to LISP:

         return List("Hello List()",
            12.0,
            List("Item1", 2, "Item3", 44.0),
            99,

            /// Sublists can be expressed using nested 
            /// calls to List(), nested to any depth:

            List("A", List(10, 20, 30, 40), "C", "D"),

            (byte) 255,  // byte
            "Hello"[0],  // char
            5230.5f,     // float
            true,        // A bool that produces the symbol T
            false,       // A bool that produces nil

            /// Point3d produces a list of 3 doubles:

            new Point3d(20.0, 40.0, 60.0),

            /// Sub-lists can also be expressed as a sequence
            /// of individual elements that start/end with the 
            /// ListBegin and ListEnd elements, although doing
            /// this is far more confusing than simply using the 
            /// List() method:

            ListBegin,                            /// Begin a sublist
               "First Element",                   /// sublist element
               "Second Element",                  /// sublist element
               "Third Element",                   /// sublist element
            ListEnd,                              /// End sublist

            /// What the above 5 lines of code do is 
            /// precisely equivalent to doing this:

            List("First Element", "Second Element", "Third Element"),

            /// Error checking is performed on list nesting.
            /// Uncommenting either of the following two
            /// lines triggers a malformed list exception:

            /// ListEnd,
            /// ListBegin,

            /// An IEnumerable is converted to a nested list.
            /// Note that an IEnumerable can contain the result
            /// of one or more calls to List():

            new object[] { "Object1", 2.0, List(100, 200, 300), "Object2", 44 },

            objectIds,                     /// Produces a sublist containing the
                                           /// elements in the ObjectIdCollection

            new object[0],                 /// Produces nil

            points,                        /// Produces a sublist containing the
                                           /// elements in the Point3dCollection

            doc.Database.LayerZero,        /// Adds a single ObjectId

            null,                          /// Null elements are allowed and produce nil

            Insert(objectIds),             /// Inserts the collection elements into
                                           /// the list without nesting them within 
                                           /// a sublist. See the (mgd-insert-test)
                                           /// LispFunction included below.

            /// In case it isn't obvious, this
            /// produces a nested association list:

            List(
               Cons("key1", "Value1"),      /// Association lists can
               Cons("key2", "Value2"),      /// be created using the
               Cons("Key3", "Value3")       /// Cons() method.
            ),

            /// This will produce an association list from the
            /// same ObjectIdCollection used above, where each
            /// key/car has the positional index of the element, 
            /// and each value/cdr is one of the ObjectIds from 
            /// the collection:
            
           Cons(objectIds.Cast<ObjectId>(), 
               (id, i) => i, 
               (id, i) => id),

            200,

            /// Because the result of a call to List() can be
            /// passed as an argument in another call to that
            /// method, calls can be nested to any depth, just
            /// like the LISP analog:

            List(300, List("1", 2, List("31", "32", 33), 44.0), 400),

            500
         );
      }

      /// The above call to List() should return a list 
      /// that looks like this:

      /*
          (  "Hello List()" 
             12.0
             ("Item1" 2 "Item3" 44.0)
             99
             ("A" (10 20 30 40) "C" "D")
             255
             72
             5230.5
             T
             nil
             (20.0 40.0 60.0)
             ("First Element" "Second Element" "Third Element")
             ("First Element" "Second Element" "Third Element")
             ("Object1" 2.0 (1 2 3) "Object2" 44)
             (<Entity name: 23295d92100> <Entity name: 23295d92020> 
                <Entity name: 23295d92010> <Entity name: 23295d920d0>
             )
             nil
             ((2.0 2.0 0.0) (12.0 5.0 0.0) (22.0 7.0 9.0) (14.0 6.0 0.0))
             <Entity name: 23295d92100>
             nil
             <Entity name: 23295d92100>
             <Entity name: 23295d92020>
             <Entity name: 23295d92010>
             <Entity name: 23295d920d0>
             ( ("key1" . "Value1") 
               ("key2" . "Value2") 
               ("Key3" . "Value3")
             )
             ( (0 . <Entity name: 24e56cdd100>) 
               (1 . <Entity name: 24e56cdd020>)
               (2 . <Entity name: 24e56cdd010>)
               (3 . <Entity name: 24e56cdd0d0>)
             )
             200
             (300 ("1" 2 ("31" "32" 33) 44.0) 400)
             500
          )
      */

      /// <summary>
      /// Calls the above (mgd-list) function and dumps
      /// the returned ResultBuffer to the console. 
      /// 
      /// The various Dump() extension methods included
      /// herein are primarily for diagnostic purposes.
      /// </summary>

      [LispFunction("mgd-list-dump")]
      public static ResultBuffer DumpGetMgdList(ResultBuffer args)
      {
         var result = GetMgdList(args);
         result.Dump();
         return result;
      }

      /// <summary>
      /// Builds and returns an association list back
      /// to LISP, using the ListBuilder's Cons() and
      /// List() methods. Note that one of the elements
      /// is cons'd with a list rather than an atom. 
      /// 
      /// As is the case with the LISP analog, the second
      /// argument to Cons() can be an atom or a list:
      /// </summary>

      [LispFunction("mgd-alist")]
      public static ResultBuffer GetMgdAList(ResultBuffer args)
      {
         return List(
            Cons("key1", "Value1"),
            Cons("key2", "Value2"),
            Cons("Key3", List("Value31", "Value32", "Value33")),
            Cons("key4", "Value4"),
            Cons("key5", "Value5")
         );
      }

      /// <summary>
      /// While the included ToDictionary() extension method 
      /// can be used to convert a Dictionary of keys and values
      /// to a LISP association list, that can also be done with 
      /// a bit of Linq and the Cons() method:
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      [LispFunction("mgd-alist-from-dict")]
      public static ResultBuffer MgdDictToAList(ResultBuffer args)
      {
         var dict = InteropTestHelpers.CreateDictionary();

         return dict.Select(p => Cons(p.Key, p.Value)).ToResultBuffer();
      }

      [LispFunction("mgd-alist-from-dict2")]
      public static ResultBuffer MgdAlistFromDict2(ResultBuffer args)
      {
         var dictionary = InteropTestHelpers.CreateDictionary();

         return Cons(dictionary, p => p.Key, p => p.Value);
      }

      /// <summary>
      /// Producees the same association list as the (mgd-alist-from-dict) 
      /// lisp function above, using the ListBuilder's ConsAll() method to 
      /// simplify the operation and perform it more effieciently, which
      /// also allows the result to be used as an argument to another call
      /// to a ListBuilder method. 
      /// 
      /// This variation nests the resulting association 
      /// list within another list.
      /// </summary>

      [LispFunction("mgd-nested-alist-from-dict")]
      public static ResultBuffer MgdNestedAlistFromDict(ResultBuffer args)
      {
         var dictionary = InteropTestHelpers.CreateDictionary();

         return List(
            "First", 
            Cons(dictionary, p => p.Key, p => p.Value), 
            "Third"
         );
      }

      /// <summary>
      /// Tests the Cons() method with different
      /// types of arguments:
      /// </summary>

      [LispFunction("mgd-cons-test")]
      public static ResultBuffer MgdConsTest(ResultBuffer args)
      {
         var items = new object[] { "One", 2, 33.0 };

         /// Add a new first element to a list:
         var result = Cons("car value", items);
         /// Expecting: '("car value" "One" 2 33.0)
         result.Dump("Cons(<atom>, <list>):\n");

         /// Create a dotted pair:
         result = Cons("car value", "cdr value");
         /// Expecting: '("car value" . "cdr value")
         result.Dump("Cons(<atom>, <atom>):\n");

         /// Add a list as new first element of a list:
         object[] list2 = new object[] { "car 1", 2, "car 3", 4 };
         var result2 = Cons(list2, items);
         /// Expecting: '(("car 1" 2 "car 3" 4) "One" 2 33.0)
         result2.Dump("Cons(<list>, <list>):\n");
         return result2;
      }

      /// <summary>
      /// Compares the behavior of the List(), Insert(), 
      /// and Append() methods when combining lists.
      /// 
      /// The only difference between the following four
      /// LispFunctions is that one uses List(), the second
      /// uses Insert() to add the second element, the third 
      /// uses Append() to concatenate the three lists, and
      /// the last uses Insert() multiple times.
      /// 
      /// The Insert() method has no LISP analog. It can be 
      /// thought of as a selective form of Append(), where
      /// only the argument to Insert() is appended to the
      /// result list. The use of Insert() is only meaningful
      /// when its result is passed as an argument to List().
      /// 
      /// For example, the following two expressions produce 
      /// identical results:
      /// 
      ///    Append(list1, list2, list3)
      ///    
      ///    List(Insert(list1), Insert(list2), Insert(list3))
      ///    
      /// All four methods use this input but transform
      /// it in different ways:
      /// 
      ///   var list1 = List("One", "Two", "Three");
      ///   var list2 = List(100, 200, 300);
      ///   var list3 = List(1.0, 2.0, 3.0);
      /// 
      /// The results of the four methods with the same input:
      /// 
      ///    (mgd-list-test):
      ///    
      ///      return List(list1, list2, list3);
      ///      
      ///        => (("One" "Two" "Three") (100 200 300) (1.0 2.0 3.0))
      ///    
      ///    (mgd-insert-test):
      ///    
      ///      return List(list1, Insert(list2), list3);
      ///      
      ///        => (("One" "Two" "Three") 100 200 300 (1.0 2.0 3.0))
      ///    
      ///    (mgd-append-test):
      ///    
      ///      return Append(list1, list2, list3);
      ///      
      ///        => ("One" "Two" "Three" 100 200 300 1.0 2.0 3.0)
      ///         
      ///    (mgd-insert-test2):
      ///    
      ///      return List(Insert(list1), list2, Insert(list3));
      ///      
      ///        => ("One" "Two" "Three" (100 200 300) 1.0 2.0 3.0)
      ///
      /// </summary>

      [LispFunction("mgd-list-test")]
      public static ResultBuffer MgdListTest(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");
         var list2 = List(100, 200, 300);
         var list3 = List(1.0, 2.0, 3.0);

         return List(list1, list2, list3);
      }

      [LispFunction("mgd-insert-test")]
      public static ResultBuffer MgdInsertTest(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");
         var list2 = List(100, 200, 300);
         var list3 = List(1.0, 2.0, 3.0);

         return List(list1, Insert(list2), list3);
      }

      /// <summary>
      /// The Append() method works just like it's LISP analog:
      /// </summary>

      [LispFunction("mgd-append-test")]
      public static ResultBuffer MgdAppendTest(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");
         var list2 = List(100, 200, 300);
         var list3 = List(1.0, 2.0, 3.0);

         return Append(list1, list2, list3);
      }

      [LispFunction("mgd-insert-test2")]
      public static ResultBuffer MgdInsertTest2(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");
         var list2 = List(100, 200, 300);
         var list3 = List(1.0, 2.0, 3.0);

         return List(Insert(list1), list2, Insert(list3));
      }

      /// <summary>
      /// Dumps the ResultBuffer argument passed from LISP
      /// </summary>

      [LispFunction("ui-mgd-dump")]
      public static void MgdDumpFunc(ResultBuffer args)
      {
         args.Dump();
      }

      /// <summary>
      /// Tests the IEnumerable caching that is 
      /// done by the ListBuilder class. In this
      /// code, the list assigned to list1 will
      /// be enumerated only once, and the result
      /// will be cached and reused each time the
      /// variable subsequently appears in a call
      /// to List().
      /// 
      /// </summary>

      [LispFunction("mgd-cache-test")]
      public static ResultBuffer MgdCacheTest(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");

         var list2 = List(list1, list1, list1);

         // The result is comprised of 5 occurrences of list1,
         // but the arguments will be enumerated and converted
         // to TypedValues only once:

         return List(list1, list2, list1);
      }

      /// <summary>
      /// Tests/exercises extended type support via the
      /// TypedValueConverter class. This code will use
      /// the custom StringBuilderConverter example class
      /// to allow instances of StringBuilder to be passed
      /// into calls to List() and other ListBuilder APIs.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>
      
      [LispFunction("mgd-convert-test")]
      public static ResultBuffer MgdConvertTest(ResultBuffer args)
      {
         StringBuilder sb = new StringBuilder();
         sb.Append("One, ");
         sb.Append("Two, ");
         sb.Append("Three");

         return List(1, 2, sb, 3, 4);
      }

      /// <summary>
      /// Issue this command to enable diagnostic messages
      /// displayed by the CachedEnumerable class:
      /// </summary>
      /// <param name="args"></param>

      [LispFunction("C:CACHEDENUMTRACE")]
      public static void TraceCache(ResultBuffer args)
      {
         var enabled = CachedEnumerable.TraceEnabled ^= true;
         var what = enabled ? "enabled" : "disabled";
         AcConsole.Write($"CachedEnumerable diagnostics messages {what}.");
      }


      /// <summary>
      /// A LISP-callable function that returns a dictionary 
      /// as an association list including an element that is 
      /// a dotted pair.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      [LispFunction("mgd-dictionary-to-list")]
      public static ResultBuffer GetListDictionary(ResultBuffer args)
      {
         var dict = InteropTestHelpers.CreateListDictionary();
         return dict.ToResultBuffer<string, int>(
            LispDataType.Text, LispDataType.Int32);
      }

      /// <summary>
      /// Tests the ToLispSelectionSet() method that converts
      /// a collection of ObjectIds to a SelectionSet and
      /// returns it back to LISP:
      /// </summary>

      [LispFunction("ui-mgd-ids-to-ss")]
      public static ResultBuffer TestIdsToSelectionSet(ResultBuffer args)
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var psr = ed.GetSelection();
         if(psr.IsFailed())
            return null;
         var ids = psr.Value.GetObjectIds();
         return List("One", ids.ToLispSelectionSet(), "Three");
      }

      /// <summary>
      /// Tests support for using a PromptEntityResult as an argument 
      /// to the List() method. The result returned back to LISP is an
      /// entsel-style list containing the entity name and the point 
      /// used to select the entity.
      /// 
      /// The support for passing a PromptEntityResult to the List() 
      /// function is provided by the PromptEntityResultConverter 
      /// example class, that demonstrates how to add support for any 
      /// managed type to ListBuilder methods.
      /// 
      /// See the PromptEntityResultConverter and TypedValueConverter
      /// base type for details.
      /// </summary>

      [LispFunction("ui-mgd-to-entsel-list")]
      public static ResultBuffer TestEntSelList(ResultBuffer args)
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var result = ed.GetEntity("\nPick an entity: ");
         if(result.IsFailed())
            return null;

         // The PromptEntityResult can be passed
         // as an argument to the List() method:

         return List("One", result, "Two"); 
      }

      [LispFunction("ui-mgd-functions")]
      public static ResultBuffer NonUiMgdFuncs(ResultBuffer args)
      {
         return lispFuncs.Value.ToList(LispDataType.Text, false).ToResult();
      }

      static Cached<string[]> lispFuncs = new(() =>
         typeof(ListBuilderTests).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(mi => mi.IsDefined(typeof(LispFunctionAttribute), false))
            .Select(mi => mi.GetCustomAttribute<LispFunctionAttribute>().GlobalName)
            .Where(name => name.StartsWith("mgd-", System.StringComparison.OrdinalIgnoreCase))
            .ToArray());

      /// <summary>
      /// List the LispFunctions exposed by this type:
      /// </summary>

      [CommandMethod("MgdLispInteropTests")]
      public static void Mgd2LispFunctionsList()
      {
         ((string[])lispFuncs).WriteLines();
      }
   }

   public static class OffLineTests
   {
      [LispFunction("mgd-rnd")]
      public static ResultBuffer MgdRnd(ResultBuffer args)
      {
         return new ResultBuffer(
            new TypedValue[] {
            new TypedValue((int)LispDataType.Int32, 0),
            new TypedValue((int)LispDataType.Int32, 1),
            new TypedValue((int) LispDataType.Point3d, new Point3d(2.0, 4.0, 6.0)),
            new TypedValue((int)LispDataType.Int32, 2),

         });
      }
   }

}




