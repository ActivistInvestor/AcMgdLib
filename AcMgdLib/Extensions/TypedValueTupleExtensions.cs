/// RuntimeExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System.Collections;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace System.Extensions
{

   /// <summary>
   /// Extension methods that facilitate conversion between
   /// TypedValue[], ResultBuffer and ValueTuple[]
   /// </summary>

   public static partial class TupleExtensions
   {

      public static TypedValue ToTypedValue(this (DxfCode code, object value) item)
      {
         return new TypedValue((short)item.code, item.value);
      }

      public static TypedValue ToTypedValue(this (LispDataType code, object value) item)
      {
         return new TypedValue((short)item.code, item.value);
      }

      public static TypedValue ToTypedValue(this (short code, object value) item)
      {
         return new TypedValue(item.code, item.value);
      }

      public static TypedValue[] ToTypedValues(this (DxfCode code, object value)[] args)
      {
         Assert.IsNotNull(args, (nameof(args)));
         TypedValue[] result = new TypedValue[args.Length];
         for(int i = 0; i < args.Length; i++)
            result[i] = new TypedValue((short)args[i].code, args[i].value);
         return result;
      }

      public static TypedValue[] ToTypedValues(this (LispDataType code, object value)[] args)
      {
         Assert.IsNotNull(args, (nameof(args)));
         TypedValue[] result = new TypedValue[args.Length];
         for(int i = 0; i < args.Length; i++)
            result[i] = new TypedValue((short)args[i].code, args[i].value);
         return result;
      }

      public static TypedValue[] ToTypedValues(this (short code, object value)[] args)
      {
         Assert.IsNotNull(args, (nameof(args)));
         TypedValue[] result = new TypedValue[args.Length];
         for(int i = 0; i < args.Length; i++)
            result[i] = new TypedValue(args[i].code, args[i].value);
         return result;
      }

      public static TypedValue[] ToTypedValues(this (int code, object value)[] args)
      {
         Assert.IsNotNull(args, (nameof(args)));
         TypedValue[] result = new TypedValue[args.Length];
         for(int i = 0; i < args.Length; i++)
            result[i] = new TypedValue((short)args[i].code, args[i].value);
         return result;
      }

      public static ResultBuffer ToResultBuffer(this (DxfCode code, object value)[] args)
      {
         return new ResultBuffer(ToTypedValues(args));
      }

      public static ResultBuffer ToResultBuffer(this (LispDataType code, object value)[] args)
      {
         return new ResultBuffer(ToTypedValues(args));
      }

      public static ResultBuffer ToResultBuffer(this (short code, object value)[] args)
      {
         return new ResultBuffer(ToTypedValues(args));
      }

      public static ResultBuffer ToResultBuffer(this (int code, object value)[] args)
      {
         return new ResultBuffer(ToTypedValues(args));
      }

   }
}

