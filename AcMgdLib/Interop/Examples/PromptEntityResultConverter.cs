/// PromptEntityResultConverter.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.LispInterop;
using static Autodesk.AutoCAD.Runtime.LispInterop.ListBuilder;

namespace AcMgdLib.Interop.Examples
{
   /// <summary>
   /// An example showing how to add support for 
   /// a managed type to the ListBuilder class.
   /// 
   /// Adding support for a managed type to ListBuilder
   /// and its methods allows instances of the supported
   /// managed type to be passed as arguments to List(),
   /// and various other ListBuilder methods. When that
   /// happens, instances of the supported managed types 
   /// are converted to LISP-consumable data taking the 
   /// form of one or more TypedValue instances.
   /// 
   /// The TypedValueConverter class facilitates this
   /// functionality in ways that are similar to the
   /// way that the System.ComponentModel.TypeConverter 
   /// class is used to convert values to/from strings
   /// and other types.
   /// 
   /// This example specializes a TypedValueConverter
   /// that targets the PromptEntityResult type, and
   /// allows instances of that type to be passed as
   /// arguments to the ListBuilder's List() method,
   /// as well as other methods that convert managed
   /// objects to Lisp-consumable data.
   /// 
   /// The steps needed to implement ListBuilder support 
   /// for a managed type are:
   /// 
   /// 1. Derive a class from the TypedValueConverter
   ///    class, and override/implement its abstract
   ///    methods. In the ToTypedValues() method, add
   ///    code that takes the argument (which will be
   ///    an instance of the type that is specified in
   ///    the TypedValueConverterAttribute) and returns
   ///    one or more TypedValues that represent the 
   ///    argument in a ResultBuffer that is passed to
   ///    LISP. 
   ///    
   ///    The ToTypedValues() method can return either a 
   ///    single TypedValue, or an array of same, which 
   ///    is the case in this example.
   ///    
   ///    In this example, the argument to ToTypedValues()
   ///    is a PromptEntityResult, and the return value 
   ///    that is passed back to LISP is an array of four
   ///    TypedValues representing a list containing the 
   ///    selected object's entity name, and the point that 
   ///    was used to select the entity. This list is of 
   ///    the same format as the list returned by the LISP 
   ///    (entsel) function.
   ///    
   /// 2. Apply the [TypedValueConverter] attribute to the
   ///    class derived from TypedValueConverter, and in
   ///    the attribute, specify the managed type that is 
   ///    to be converted to/from its LISP representation,
   ///    which is PromptEntityResult in this example.
   ///    
   ///    The included (mgd-to-entsel-list) example
   ///    LispFunction exercises/demonstrates this example.
   ///      
   /// </summary>

   [TypedValueConverter(typeof(PromptEntityResult))]
   public class PromptEntityResultConverter : TypedValueConverter
   {
      /// <summary>
      /// Converts an instance of the managed type 
      /// (PromptEntityResult) to its LISP representation.
      /// 
      /// This method returns an array containing four
      /// TypedValues (ListBegin, ObjectId, Point3d,
      /// and ListEnd), which is what gets passed back 
      /// to LISP.
      /// 
      /// Note that this method uses the List() method
      /// to produce the result.
      /// 
      /// Without the use of the List() method, using
      /// only built-in APIs, the result would require
      /// this:
      /// 
      ///   return new TypedValue[]
      ///   {
      ///      new TypedValue((int) LispDataType.ListBegin),
      ///      new TypedValue((int) LispDataType.ObjectId, per.ObjectId),
      ///      new TypedValue((int) LispDataType.Point3d, per.PickedPoint),
      ///      new TypedValue((int) LispDataType.ListEnd),
      ///   };
      ///
      /// 
      /// </summary>

      public override object ToTypedValues(object value, Context context = Context.Lisp)
      {
         if(value is PromptEntityResult per)
         { 
            return List(per.ObjectId, per.PickedPoint);
         }
         return null;
      }

      public override object FromTypedValues(object value, Context context = Context.Lisp)
      {
         return null; // Conversion from LISP is not implmented in this example.
      }
   }
}




