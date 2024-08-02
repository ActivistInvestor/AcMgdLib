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
      /// used to create a ResultBuffer that can be returned
      /// as the result of a lisp-callable method having the
      /// LispFunction attribute applied to it.
      /// 
      /// The result of List() is a type that can act as an
      /// IEnumerable<TypedValue> and also implicitly convert 
      /// itself to a ResultBuffer. Hence, the result of List()
      /// can be returned directly by a LispFunction or any other 
      /// method that returns a ResultBuffer, and can be assigned 
      /// to a ResultBuffer variable.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static TypedValueIterator List(params object[] args)
      {
         return new Iterator(ToListWorker(args)).ToIterator();
      }

      /// <summary>
      /// The analog of the LISP (cons) function.
      /// 
      /// This method will create dotted pairs, or add 
      /// its first argument to the front of a list of 
      /// items given as the second argument.
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
         if(IsEnumerable(cdr))
         {
            return List(car, Insert((IEnumerable)cdr));
         }
         else
         {
            return ToList(ListBegin, ToList(car), cdr, DotEnd).ToIterator();
         }
      }

      /// <summary>
      /// This method can be used like the List() method,
      /// with the difference being that it does not return
      /// the result nested in a list.
      /// </summary>
      
      public static TypedValueIterator ToList(params object[] args)
      {
         return ToListWorker(args, false).ToIterator();
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
         return new Iterator(ToListWorker(arg), IteratorType.ShallowExplode).ToIterator();
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
         return args.OfType<IEnumerable>().SelectMany(Insert).ToIterator();
      }

      /// <summary>
      /// Performs the work for the List() method. 
      /// </summary>
      /// <param name="args">An IEnumerable to be transformed</param>
      /// <param name="convertIds">A value indicating if collections
      /// of ObjectIds that reference entities should be converted to 
      /// SelectionSets.</param>
      /// <returns>A sequence of TypedValues that can be returned
      /// to LISP in a ResultBuffer.</returns>
      /// <exception cref="ArgumentException"></exception>

      internal static IEnumerable<TypedValue> ToListWorker(
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
            {
               arg = rb.Cast<TypedValue>();
            }
            if(arg is IEnumerable<TypedValue> values)
            {
               if(!values.Any()) // Empty sequence translates to empty list/nil.
               {
                  yield return Nil;
                  continue;
               }

               foreach(TypedValue item in values)
                  yield return item;

               continue;
            }

            /// Optimized paths for ObjectIdCollection/IEnumerable<ToObjectId>
            /// and Point3dCollection/IEnumerable<ToPoint3d>:

            if(arg is Point3dCollection pc)
               arg = pc.Cast<Point3d>();
            if(arg is IEnumerable<Point3d> points)
            {
               foreach(var item in points.ToTypedValues(LispDataType.Point3d))
                  yield return item;
               continue;
            }

            if(arg is ObjectIdCollection collection)
               arg = collection.Cast<ObjectId>();

            if(arg is IEnumerable<ObjectId> ids)
            {
               foreach(var item in ids.ToTypedValues(LispDataType.ObjectId))
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

      /// <summary>
      /// Returns LispDataType.Nil if the given sequence is empty.
      /// </summary>

      public static IEnumerable<TypedValue> ToTypedValues<T>(this IEnumerable<T> source, LispDataType type)
      {
         Assert.IsNotNull(source, nameof(source));
         if(!source.Any())
         {
            yield return Nil;
         }
         else
         {
            yield return ListBegin;
            foreach(var item in source)
               yield return new TypedValue((int)type, item);
            yield return ListEnd;
         }
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

      static bool IsEmpty(IEnumerable enumerable)
      {
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

      static IEnumerable<T> ConsT<T>(T car, IEnumerable<T> list)
      {
         yield return car;
         foreach(T item in list)
            yield return item;
      }

      public static TypedValue ToInt32(int value)
      {
         return new TypedValue((short)LispDataType.Int32, value);
      }

      public static TypedValue ToInt16(short value)
      {
         return new TypedValue((short)LispDataType.Int16, value);
      }

      public static TypedValue ToDouble(double value)
      {
         return new TypedValue((short)LispDataType.Double, value);
      }

      public static TypedValue ToAngle(double value)
      {
         return new TypedValue((short)LispDataType.Angle, value);
      }

      public static TypedValue ToOrientation(object value)
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

      public static TypedValue ToText(string value)
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

      [Flags]
      enum IteratorType
      {
         Default = 0,         // Enumerate all elements as-is.
         List = 1,            // Enclose all elements in a ListBegin/End sequence.
         ShallowExplode = 2,  // Explode only top-level nested lists
         DeepExplode = 4      // Explode nested lists at any depth.
      }

      class Iterator : IEnumerable<TypedValue>
      {
         IEnumerable<TypedValue> source;
         IteratorType type = IteratorType.Default;
         IEnumerable<TypedValue> values = null;
         ResultBuffer buffer = null;

         public Iterator(IEnumerable<TypedValue> source, IteratorType type = IteratorType.List)
         {
            this.source = source;
            this.type = type;
         }

         public ResultBuffer ToResultBuffer()
         {
            if(buffer == null)
            {
               buffer = new ResultBuffer(this.ToArray());
            }
            return buffer;
         }

         public IEnumerator<TypedValue> GetEnumerator()
         {
            if(values == null)
               values = GetValues().ToArray();
            return values.GetEnumerator();
         }

         IEnumerator IEnumerable.GetEnumerator()
         {
            return this.GetEnumerator();
         }

         IEnumerable<TypedValue> GetValues()
         {
            if(type.HasFlag(IteratorType.List))
               yield return ListBegin;

            bool explodeAll = type.HasFlag(IteratorType.DeepExplode);
            bool explodeTop = type.HasFlag(IteratorType.ShallowExplode);
            int depth = 0;
            if(!(explodeAll || explodeTop))
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
                     if(depth > 1 && explodeAll)
                        continue;
                     if(depth == 1 && explodeTop)
                        continue;
                  }
                  else if(tv.IsListEnd())
                  {
                     --depth;
                     if(depth < 0)
                        throw new InvalidOperationException("Malformed List");
                     if(depth > 0 && explodeAll)
                        continue;
                     if(depth == 0 && explodeTop)
                        continue;
                  }
                  yield return tv;
               }
            }

            if(type.HasFlag(IteratorType.List))
               yield return ListEnd;
         }

         public static implicit operator ResultBuffer(Iterator operand)
         {
            Assert.IsNotNull(operand, nameof(operand));
            return operand.ToResultBuffer();
         }
      }
   }

   public static class ListBuilderExtensions
   {
      /// <summary>
      /// An extension method that can be used with Linq
      /// operations to convert a sequence of values to
      /// a sequence of TypedValues representing a Lisp list.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static IEnumerable<TypedValue> ToLispList(this IEnumerable arg)
      {
         return ListBuilder.ToListWorker(arg, false);
      }

      /// <summary>
      /// Converts a sequence of objects to a ResultBuffer
      /// representing a LISP list:
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>
      
      public static ResultBuffer ToResultBuffer(this IEnumerable args)
      {
         return new ResultBuffer(ToLispList(args).ToArray());
      }

      public static TypedValueIterator ToIterator(this IEnumerable<TypedValue> arg)
      {
         return arg as TypedValueIterator ?? new TypedValueIterator(arg);
      }

   }
}




