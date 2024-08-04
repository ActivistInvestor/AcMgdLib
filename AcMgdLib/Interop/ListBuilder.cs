/// ListBuilder.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.DirectoryServices;
using System.Linq;
using System.Text;
using System.Utility;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;

namespace Autodesk.AutoCAD.Runtime.LispInterop
{
   /// <summary>
   /// Using this class in an intuitive and simplified
   /// way can be achieved by adding this to the top
   /// of a code unit:
   /// 
   ///   using static Autodesk.AutoCAD.Runtime.LispInterop.ListBuilder;
   ///   
   /// See the included LispInteropTests.cs for more detail.
   /// </summary>

   public static class ListBuilder
   {
      /// <summary>
      /// Converts a sequence of managed objects to an array 
      /// of TypedValues that is the LISP representation of 
      /// the input.
      /// 
      /// This method is functionally similar to the LISP
      /// (list) function, except that it accepts managed
      /// types and transforms them to LISP types.
      /// 
      /// Calls to this method can be nested, to create a
      /// complex list containing nested 'sublists'. There
      /// is no limit to the depth of nested calls to this
      /// method, or to the complexity of the resulting list.
      /// 
      /// The value returned by a call to this method can be
      /// cast to a ResultBuffer that can be returned as the 
      /// result of a method having the LispFunction attribute 
      /// applied to it, to return the result to LISP.
      /// 
      /// The result of List() is a type that can act as an
      /// IEnumerable<TypedValue> and also implicitly convert 
      /// itself to a ResultBuffer. Hence, the result can be 
      /// returned directly by a LispFunction or any other 
      /// method that returns a ResultBuffer, or be assigned 
      /// to a ResultBuffer variable. 
      /// 
      /// The result of List() can also be used as an argument 
      /// in another call to List(), and various other methods 
      /// of this class, such as Append(), Insert(), and Cons().
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static TypedValueIterator List(params object[] args)
      {
         return new Iterator(ToListWorker(args)).ToResbuf();
      }

      /// <summary>
      /// The analog of the LISP (cons) function.
      /// 
      /// This method will create dotted pairs, or add 
      /// its first argument to the front of a list of 
      /// list given as the second argument.
      /// 
      /// Note that this is not a functionally-complete 
      /// emulation of LISP's (cons) and may have some 
      /// limitations. Not all possible uses of the LISP 
      /// analog have been tested.
      /// </summary>
      /// <param name="car">The element to add to the
      /// head/car of the result </param>
      /// <param name="cdr">A list or atom that is to
      /// be consed with the car.</param>
      /// <returns>A dotted pair if the second argument
      /// is not a list, or the second argument with the
      /// first argument as the new first element/car.
      /// </returns>
      
      public static TypedValueIterator Cons(object car, object cdr)
      {
         if(cdr is IEnumerable items && !(items is string))
         {
            return List(car, Insert(items));
         }
         else
         {
            return ToList(ListBegin, ToList(car), cdr, DotEnd).ToResbuf();
         }
      }

      /// <summary>
      /// This method can be used like the List() method,
      /// with the difference being that it does not return
      /// the result nested in a list.
      /// </summary>
      
      public static TypedValueIterator ToList(params object[] args)
      {
         return ToListWorker(args, false).ToResbuf();
      }

      /// <summary>
      /// This method uses the List() method to convert the 
      /// arguments to a LISP list and returns the list in
      /// a ResultBuffer.
      /// </summary>

      public static ResultBuffer ToResultBuffer(params object[] args)
      {
         return new ResultBuffer(ToList(args).ToArray());
      }

      /// <summary>
      /// Inserts the collection argument into the current list
      /// without nesting its elements in a sublist. Within a call
      /// to the List() method, if a collection is included as an
      /// argument, the collection becomes a nested list within the
      /// containing list. If the same collection is instead passed 
      /// to this method, its elements will become elements of the 
      /// containing list.
      ///
      /// This method is only meaningful when its result is
      /// passed as an argument to the List() method. In any
      /// other context, the result is undefined.
      /// 
      /// There is no LISP analog to this method.
      /// </summary>
      /// <param name="arg"></param>
      /// <returns></returns>
      /// <exception cref="ArgumentException"></exception>

      public static TypedValueIterator Insert(IEnumerable arg)
      {
         Assert.IsNotNull(arg, nameof(arg));
         if(!IsEnumerable(arg))
            throw new ArgumentException("Invalid IEnumerable (no strings)");
         return new Iterator(ToListWorker(arg), IteratorType.Explode).ToResbuf();
      }

      /// <summary>
      /// Appends the elements of multiple collections into
      /// a single list representation, and is analagous to
      /// the LISP (append) function.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static TypedValueIterator Append(params IEnumerable[] args)
      {
         return args.OfType<IEnumerable>().SelectMany(Insert).ToResbuf();
      }

      /// <summary>
      /// Performs the work for the List() method. This method is not
      /// public, and implements the core functionality for converting
      /// managed objects to their LISP representations.
      /// </summary>
      /// <param name="args">An IEnumerable to be transformed</param>
      /// <param name="convertIds">A value indicating if collections
      /// of ObjectIds that reference entities should be converted to 
      /// SelectionSets.</param>
      /// <returns>A sequence of TypedValues that can be returned
      /// to LISP in a ResultBuffer.</returns>
      /// <exception cref="ArgumentException"></exception>

      static IEnumerable<TypedValue> ToListWorker(
         this IEnumerable args,
         bool convertIds = false,
         int depth = 0)
      {
         int level = 0;
         int startDepth = depth;
         Assert.IsNotNull(args, nameof(args));
         if(args is string)
            throw new ArgumentException("Can't convert a string to a List");
         IEnumerator e = args.GetEnumerator();
         while(e.MoveNext())
         {
            object arg = e.Current;
            if(arg == null)
            {
               yield return Nil;
               continue;
            }
            if(arg is TypedValue tv)
            {
               if(IsListBegin(tv))
                  ++level;
               else if(IsListEnd(tv))
                  --level;
               yield return tv;
               continue;
            }
            if(arg is string s)
            {
               yield return ToText(s);
               continue;
            }
            if(arg is StringBuilder sb)
            {
               yield return ToText(sb.ToString());
               continue;
            }
            if(arg is double || arg is float)
            {
               yield return ToDouble((double)arg);
               continue;
            }
            if(arg is Int32 i)
            {
               yield return ToInt32(i);
               continue;
            }
            if(arg is Int16 || arg is byte || arg is char)
            {
               yield return ToInt16((short)arg);
               continue;
            }
            if(arg is Point3d p3d)
            {
               yield return ToPoint3d(p3d);
               continue;
            }
            if(arg is Point2d p2d)
            {
               yield return ToPoint2d(p2d);
               continue;
            }
            if(arg is ObjectId id)
            {
               yield return ToObjectId(id);
               continue;
            }
            if(arg is SelectionSet ss)
            {
               yield return ToSelectionSet(ss);
               continue;
            }
            if(arg is bool bVal)  // bool converts to T or NIL.
            {
               yield return bVal ? True : Nil;
               continue;
            }
            if(convertIds) // Convert sequences of ObjectId to a SelectionSet
            {
               if(arg is ObjectIdCollection ids2 && ids2.IsAllEntities())
               {
                  yield return ToSelectionSet(ids2);
                  continue;
               }
               else if(arg is IEnumerable<ObjectId> ents && ents.IsAllEntities())
               {
                  yield return ToSelectionSet(ents);
                  continue;
               }
            }
            if(arg is ResultBuffer rb)
               arg = rb.Cast<TypedValue>();

            /// The result of nested calls to List() are
            /// processed here. If special behavior is needed,
            /// the runtime type of the IEnumerable<TypedValue>
            /// can be tested and acted on accordingly.

            if(arg is IEnumerable<TypedValue> values)
            {
               foreach(var item in values)
                  yield return item;
               continue;
            }

            /// Optimized paths for ObjectIdCollection/IEnumerable<ToObjectId>
            /// and Point3dCollection/IEnumerable<ToPoint3d>:

            if(arg is Point3dCollection pc)
               arg = pc.Cast<Point3d>();
            if(arg is IEnumerable<Point3d> points)
            {
               foreach(var item in points.ToList(LispDataType.Point3d))
                  yield return item;
               continue;
            }

            if(arg is ObjectIdCollection collection)
               arg = collection.Cast<ObjectId>();

            if(arg is IEnumerable<ObjectId> ids)
            {
               foreach(var item in ids.ToList(LispDataType.ObjectId))
                  yield return item;
               continue;
            }

            /// Fallback for all IEnumerables not handled above.
            /// IEnumerables handled here will be elaborated as 
            /// nested lists:

            if(arg is IEnumerable enumerable)
            {
               if(IsEmpty(enumerable))
               {
                  yield return Nil;
                  continue;
               }
               yield return ListBegin;
               foreach(var item in ToListWorker(enumerable, convertIds, depth + 1))
                  yield return item;
               yield return ListEnd;
               continue;
            }

            /// If we get here, the element is not supported:
            throw new ArgumentException(
               $"Unsupported type ({arg.GetType().CSharpName()}): {arg.ToString()}");

         }
         if(level != 0)
            throw new ArgumentException($"Malformed list: (+{level}");
      }

      static bool IsListBegin(TypedValue tv)
      {
         return tv.IsEqualTo(ListBegin);
      }

      static bool IsListEnd(TypedValue tv)
      {
         return tv.IsEqualTo(ListEnd) || tv.IsEqualTo(DotEnd);
      }

      static bool IsEnumerable(object obj)
      {
         return obj is IEnumerable && !(obj is string);
      }

      /// <summary>
      /// Need to avoid superfluous calls to MoveNext():
      /// </summary>
      /// <param name="enumerable"></param>
      /// <returns></returns>

      static bool IsEmpty(IEnumerable enumerable)
      {
         if(enumerable is ICollection collection)
            return collection.Count == 0;
         if(enumerable is Array array)
            return array.Length == 0;
         var enumerator = enumerable.GetEnumerator();
         try
         {
            return !enumerator.MoveNext();
         }
         finally
         {
            (enumerator as IDisposable)?.Dispose();
         }
      }

      static TypedValue ToInt32(int value)
      {
         return new TypedValue((short)LispDataType.Int32, value);
      }

      static TypedValue ToInt16(short value)
      {
         return new TypedValue((short)LispDataType.Int16, value);
      }

      static TypedValue ToDouble(double value)
      {
         return new TypedValue((short)LispDataType.Double, value);
      }

      static TypedValue ToAngle(double value)
      {
         return new TypedValue((short)LispDataType.Angle, value);
      }

      static TypedValue ToOrientation(object value)
      {
         return new TypedValue((short)LispDataType.Orientation, value);
      }

      public static TypedValue ToObjectId(ObjectId value)
      {
         return new TypedValue((short)LispDataType.ObjectId, value);
      }

      public static TypedValue ToSelectionSet(IEnumerable<ObjectId> ids)
      {
         return ToSelectionSet(SelectionSet.FromObjectIds(ids.AsArray()));
      }

      public static TypedValue ToSelectionSet(ObjectIdCollection ids)
      {
         ObjectId[] idarray = new ObjectId[ids.Count];
         ids.CopyTo(idarray, 0);
         return ToSelectionSet(SelectionSet.FromObjectIds(idarray));
      }

      public static TypedValue ToSelectionSet(SelectionSet value)
      {
         return new TypedValue((short)LispDataType.SelectionSet, value);
      }

      public static TypedValue ToPoint3d(Point3d value)
      {
         return new TypedValue((short)LispDataType.Point3d, value);
      }

      public static TypedValue ToPoint3d(double x, double y, double z)
      {
         return new TypedValue((short)LispDataType.Point3d, new Point3d(x, y, z));
      }

      public static TypedValue ToPoint2d(Point2d value)
      {
         return new TypedValue((short)LispDataType.Point2d, value);
      }

      public static TypedValue ToPoint2d(double x, double y)
      {
         return new TypedValue((short)LispDataType.Point2d, new Point2d(x, y));
      }

      static TypedValue ToText(string value)
      {
         return new TypedValue((short)LispDataType.Text, value);
      }

      public static readonly TypedValue ListBegin =
         new TypedValue((short)LispDataType.ListBegin);

      public static readonly TypedValue ListEnd =
         new TypedValue((short)LispDataType.ListEnd);

      public static readonly TypedValue DotEnd =
         new TypedValue((short)LispDataType.DottedPair);

      public static readonly TypedValue Nil =
         new TypedValue((short)LispDataType.Nil);

      public static readonly TypedValue True =
         new TypedValue((short)LispDataType.T_atom);

      public static readonly TypedValue Void =
         new TypedValue((short)LispDataType.Void);

      public static readonly TypedValue None =
         new TypedValue((short)LispDataType.None);

      /// <summary>
      /// Converts a homogenous sequence of T to a sequence of
      /// TypedValues, optionally nested in ListBegin/End delimters.
      /// 
      /// Returns LispDataType.Nil if the given sequence is empty.
      /// </summary>
      /// <typeparam name="T">The type of the source objects</typeparam>
      /// <param name="source">The source objects</param>
      /// <param name="type">The TypeCode to set each resulting TypedValue to</param>
      /// <param name="list">A value indicating if the elements should be
      /// enclosed in a matching pair of ListBegin/ListEnd elements</param>
      /// <returns>The sequence of TypedValues derived from the source</returns>

      public static IEnumerable<TypedValue> ToList<T>(this IEnumerable<T> source, LispDataType type, bool list = true)
      {
         /// Refactored to avoid calling Enumerable.Any():
         Assert.IsNotNull(source, nameof(source));
         using(var e = source.GetEnumerator())
         {
            if(!e.MoveNext())
            {
               yield return Nil;
               yield break;
            }
            var code = (short)type;
            if(list)
               yield return ListBegin;
            yield return new TypedValue(code, e.Current);
            while(e.MoveNext())
               yield return new TypedValue(code, e.Current);
            if(list)
               yield return ListEnd;
         }
      }

      static TypedValueIterator ToResbuf(this IEnumerable<TypedValue> arg)
      {
         return arg as TypedValueIterator ?? new TypedValueIterator(arg);
      }

      /// <summary>
      /// An extension method that can be used with Linq
      /// operations to convert a sequence of objects to 
      /// a sequence of TypedValues representing a Lisp list.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static IEnumerable<TypedValue> ToLispList(this IEnumerable arg)
      {
         return ToListWorker(arg, false);
      }

      /// <summary>
      /// Converts a sequence of objects to a ResultBuffer
      /// representing a LISP list:
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static ResultBuffer ToResultBuffer(this IEnumerable args)
      {
         return ToLispList(args).ToResbuf();
      }

      [Flags]
      enum IteratorType
      {
         Default = 0,         // Enumerate all elements as-is.
         List = 1,            // Enclose all elements in a ListBegin/End sequence.
         Explode = 2,         // Explode top-most nested lists
         DeepExplode = 4      // Explode nested lists at any depth.
      }

      class Iterator : IEnumerable<TypedValue>
      {
         IEnumerable<TypedValue> source;
         IteratorType type = IteratorType.Default;

         public Iterator(IEnumerable<TypedValue> source, IteratorType type = IteratorType.List)
         {
            this.source = source;
            this.type = type;
         }

         public IEnumerator<TypedValue> GetEnumerator()
         {
            return GetValues().GetEnumerator();
         }

         IEnumerator IEnumerable.GetEnumerator()
         {
            return this.GetEnumerator();
         }

         IEnumerable<TypedValue> GetValues()
         {
            if(type.HasFlag(IteratorType.List))
               yield return ListBegin;

            bool deepExplode = type.HasFlag(IteratorType.DeepExplode);
            bool explode = type.HasFlag(IteratorType.Explode);
            int depth = 0;
            if(!(deepExplode || explode))
            {
               foreach(var tv in source)
                  yield return tv;
            }
            else
            {
               foreach(TypedValue tv in source)
               {
                  if(tv.IsListBegin())
                  {
                     ++depth;
                     if(depth > 1 && deepExplode)
                        continue;
                     if(depth == 1 && explode)
                        continue;
                  }
                  else if(tv.IsListEnd())
                  {
                     --depth;
                     if(depth < 0)
                        throw new InvalidOperationException("Malformed List");
                     if(depth > 0 && deepExplode)
                        continue;
                     if(depth == 0 && explode)
                        continue;
                  }
                  yield return tv;
               }
            }
            if(type.HasFlag(IteratorType.List))
               yield return ListEnd;
         }

      }

   }

}




