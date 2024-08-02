/// LispInteropTests.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System.Extensions;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using Autodesk.AutoCAD.Runtime.LispInterop;

/// This allows the use of ListBuilder methods 
/// without the class prequalifier:

using static Autodesk.AutoCAD.Runtime.LispInterop.ListBuilder;

namespace AcMgdLib.Interop.Examples
{
   public static class LispInteropTests
   { 

      /// <summary>
      /// Uses the ListBuilder class to build and a return 
      /// a list of arbitrary-complexity back to LISP. The
      /// ListBuilder's List() method accepts an object[]
      /// array, which it then transforms into an array of
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
      /// Note that all members of ListBuilder can be used
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

         /// Create a list that can be returned to LISP:
         
         var list =
            List("Hello", "List", 
               12.0,
               List("Item1", 2, "Item3", 44.0),
               99,

               /// Sublists can be expressed using nested 
               /// calls to List(), nested to any depth:

               List("A", List(10, 20, 30, 40), "C", "D"),

               /// Point3d elements are supported and are
               /// converted to their LISP representation:
               
               new Point3d(20.0, 40.0, 60.0),

               /// Sub-lists can also be expressed as a sequence
               /// of individual elements that start/end with the 
               /// ListBegin and ListEnd elements:
               
               ListBegin,                      // Begin a sublist
               "Single List Element",          // sublist element
               ListEnd,                        // End sublist

               // Error checking is performed on list nesting.
               // Uncommenting either of the following two
               // lines triggers a malformed list exception:

               // ListEnd,
               // ListBegin,

               // An IEnumerable is converted to a nested list:

               new object[] { "Object1", 2.0, "Object3", 44 },

               // List() provides optimized support for both
               // ObjectIdCollections and Point3dCollections,
               // both of which are converted to nested lists
               // of entity names and point lists:

               objectIds,              // Adds a sublist containing the
                                       // elements in the ObjectIdCollection

               new object[0],           // Converts to nil

               points,                  // Adds a sublist containing the
                                        // elements in the Point3dCollection
               
               doc.Database.LayerZero,  // Add a single ObjectId
               
               null,                    // Converts to nil

               Insert(objectIds),       // Inserts the collection elements into
                                        // the list without nesting them within 
                                        // a sublist. See the (mgd-insert-test)
                                        // LispFunction included below.

               // In case it isn't obvious, this
               // adds a nested association list:
               
               List(                       
                  Cons("key1", "Value1"),  // Association lists can
                  Cons("key2", "Value2"),  // be created using the
                  Cons("Key3", "Value3")   // Cons() method.
               ),
               200,

               // Because the result of a call to List() can be
               // passed as an argument in another call to that
               // method, calls can be nested to any depth, just
               // like the LISP counterpart:

               List(300, List("1", 2, List("31", "32", 33), 44.0), 400),

               500
            );

         return list.ToResultBuffer();
      }

      /// <summary>
      /// Calls the above (mgd-list) function and
      /// dumps the alist to the console:
      /// </summary>

      [LispFunction("mgd-list-dump")]
      public static ResultBuffer DumpGetMgdList(ResultBuffer args)
      {
         var result = GetMgdList(new ResultBuffer());
         result.Dump();
         return result;
      }

      /// <summary>
      /// Builds and returns an association list back
      /// to LISP, using the ListBuilder's Cons() and
      /// List() methods:
      /// </summary>

      [LispFunction("mgd-alist")]
      public static ResultBuffer GetMgdAList(ResultBuffer args)
      {
         var alist =
            List(
               Cons("key1", "Value1"),
               Cons("key2", "Value2"),
               Cons("Key3", "Value3"),
               Cons("key4", "Value4"),
               Cons("key5", "Value5")
            );

         return alist.ToResultBuffer();
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

      /// <summary>
      /// Tests the Cons() method with different
      /// types of arguments:
      /// </summary>

      [LispFunction("mgd-cons-test")]
      public static ResultBuffer MgdConsTest(ResultBuffer args)
      {
         var items = new object[] { "One", 2, 33.0 };

         // Add a new first element:
         var result = Cons("car value", items);
         // Expecting '("car value" "One" 2 33.0)
         result.Dump("Cons(<atom>, <list>):\n");

         // Create a dotted pair:
         result = Cons("car value", "cdr value");
         // Expecting '("car value" . "cdr value")
         result.Dump("Cons(<atom>, <atom>):\n");

         // Add a list as the new first element:
         object[] list2 = new object[]{ "car 1", 2, "car 3", "car 4" };
         var result2 = Cons(list2, items);
         // Expecting '(("car 1" 2 "car 3" "car 4") "One" 2 33.0)
         result2.Dump("Cons(<list>, <list>):\n");
         return result2.ToResultBuffer();
      }

      /// <summary>
      /// Compares the List() method's behavior when instances
      /// of IEnumerable<TypedValue> are passed as arguments to
      /// the behavior of the Insert() method.
      /// 
      /// Unlike the way they are treated by the List() method,
      /// instances of IEnumerable<TypedValue> are appended to
      /// the list, rather than nested in it.
      /// 
      /// The only difference between the following two
      /// LispFunctions is that one uses List() and the
      /// other uses Insert() to add the second element.
      /// 
      /// The results of the two methods are:
      /// 
      ///    Command: (mgd-insert-test)
      ///    
      ///    (("One" "Two" "Three") 100 200 300 (1.0 2.0 3.0))
      ///    
      ///    Command: (mgd-list-test)
      ///    
      ///    (("One" "Two" "Three") (100 200 300) (1.0 2.0 3.0))
      ///
      /// When the Insert() method is used in lieu of List(), 
      /// the elements are not nested in a sub-list, but are
      /// instead spliced into the list containing the call to
      /// Insert().
      /// </summary>

      [LispFunction("mgd-insert-test")]
      public static ResultBuffer MgdInsertTest(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");
         var list2 = List(100, 200, 300);
         var list3 = List(1.0, 2.0, 3.0);

         var result = List(list1, Insert(list2), list3);

         return result.ToResultBuffer();
      }

      [LispFunction("mgd-list-test")]
      public static ResultBuffer MgdListTest(ResultBuffer args)
      {
         var list1 = List("One", "Two", "Three");
         var list2 = List(100, 200, 300);
         var list3 = List(1.0, 2.0, 3.0);
         var result = List(list1, list2, list3);
         
         return result.ToResultBuffer();
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
         var result = Append(list1, list2, list3);
         return result.ToResultBuffer();
      }

      [LispFunction("mgd-dump")]
      public static void MgdDumpFunc(ResultBuffer args)
      {
         args.Dump();
      }

      /// <summary>
      /// A LISP-callable function that returns the test
      /// dictionary used in this code as an association
      /// list including an element that is a dotted pair.
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

      static string[] lispFuncs = null;

      static Cached<string[]> lispFunctions = new(() => 
         typeof(LispInteropTests).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(mi => mi.IsDefined(typeof(LispFunctionAttribute), false))
            .Select(mi => mi.GetCustomAttribute<LispFunctionAttribute>().GlobalName)
            .ToArray());

      /// <summary>
      /// List the LispFunctions exposed by this type:
      /// </summary>

      [CommandMethod("MgdLispInteropTests")]
      public static void Mgd2LispFunctionsList()
      {
         foreach(var name in (string[]) lispFunctions)
         {
            name.WriteLine();
         }
      }
   }

}




