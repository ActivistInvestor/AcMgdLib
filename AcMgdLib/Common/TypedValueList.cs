using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Extensions;
using System.Runtime.InteropServices;

namespace Autodesk.AutoCAD.Runtime
{
   /// <summary>
   /// An update of the original TypedValueList that was originally
   /// published here:
   /// 
   ///    http://www.theswamp.org/index.php?topic=14495.msg186823#msg186823
   /// 
   /// This class remains only for the purpose of allowing implicit
   /// casts to/from ResultBuffer, TypedValue[] and SelectionFilter;
   /// 
   /// Most functionality that was previously provided by the original
   /// TypedValueList is now implmented as extension methods that can 
   /// target List<TypedValue>, IList<TypedValue>, and TypedValueList.
   /// 
   /// Note that this class targets .NET 5 or later, and will not work
   /// with earlier versions of the .NET framework.
   /// 
   /// See TypedValueListExtensions.cs
   /// </summary>

   public class TypedValueList : List<TypedValue>
   {
      public TypedValueList()
      {
      }

      public TypedValueList(params TypedValue[] args)
      {
         if(args == null)
            throw new ArgumentNullException(nameof(args));
         if(args.Length > 0)
         {
            CollectionsMarshal.SetCount(this, args.Length);
            var span = new ReadOnlySpan<TypedValue>(args);
            span.CopyTo(CollectionsMarshal.AsSpan(this));
         }
      }

      /// <summary>
      /// New: constructors that take ValueTuples instead 
      /// of TypedValues. The first element of the tuple can 
      /// be a DxfCode, a short, an int, or a LispDataType:
      /// 
      ///   new TypedValueList(
      ///      (DxfCode.Text, "Moe"),
      ///      (DxfCode.Text, "Larry"),
      ///      (DxfCode.Text, "Curly"));
      ///      
      /// or: 
      /// 
      ///   new TypedValueList((1, "Moe"), (1, "Larry"), (1, "Curly"));
      ///   
      /// or: 
      ///  
      ///   new TypedValueList(
      ///      (LispDataType.ListBegin, null),
      ///      (LispDataType.ObjectId, someObjectId),
      ///      (LispDataType.Point3d, new Point3d(0, 0, 0)),
      ///      (LispDataType.ListEnd, null)
      ///   );
      /// 
      /// </summary>

      public TypedValueList(params (short code, object value)[] args)
         : this(args.ToTypedValues())
      {
      }

      public TypedValueList(params (int code, object value)[] args)
         : this(args.ToTypedValues())
      {
      }

      public TypedValueList(params (DxfCode code, object value)[] args)
         : this(args.ToTypedValues())
      {
      }

      public TypedValueList(params (LispDataType code, object value)[] args)
         : this(args.ToTypedValues())
      {
      }

      public TypedValueList(IEnumerable<TypedValue> args)
      {
         if(args == null)
            throw new ArgumentNullException(nameof(args));
         AddRange(args);
      }

      /// The implicit conversion operators
      /// from the original TypedValueList
      /// with the addition of operators to
      /// support ValueTuples.
      
      
      // Implicit conversion to SelectionFilter
      public static implicit operator SelectionFilter(TypedValueList src)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         return new SelectionFilter(src);
      }

      // Implicit conversion to ResultBuffer
      public static implicit operator ResultBuffer(TypedValueList src)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         return new ResultBuffer(src);
      }

      // Implicit conversion to TypedValue[] 
      public static implicit operator TypedValue[](TypedValueList src)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         return src.ToArray();
      }

      // Implicit conversion from TypedValue[] 
      public static implicit operator TypedValueList(TypedValue[] src)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         return new TypedValueList(src);
      }

      // Implicit conversion from SelectionFilter
      public static implicit operator TypedValueList(SelectionFilter src)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         return new TypedValueList(src.GetFilter());
      }

      // Implicit conversion from ResultBuffer
      public static implicit operator TypedValueList(ResultBuffer src)
      {
         if(src == null)
            throw new ArgumentNullException(nameof(src));
         return new TypedValueList(src.AsArray());
      }

      // NEW: Implicit conversion from ValueTuple(short, object)[]
      public static implicit operator TypedValueList((short, object value)[] args)
      {
         return new TypedValueList(args);
      }

      // NEW: Implicit conversion from ValueTuple(int, object)[]
      public static implicit operator TypedValueList((int, object)[] args)
      {
         return new TypedValueList(args);
      }

      // NEW: Implicit conversion from ValueTuple(DxfCode, object)[]
      public static implicit operator TypedValueList((DxfCode, object value)[] args)
      {
         return new TypedValueList(args);
      }

      // NEW: Implicit conversion from ValueTuple(LispDataType, object)[]
      public static implicit operator TypedValueList((LispDataType, object value)[] args)
      {
         return new TypedValueList(args);
      }

   }



}


