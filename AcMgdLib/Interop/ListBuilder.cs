/// ListBuilder.cs
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Utility;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.Runtime.LispInterop
{
   /// <summary>
   /// Using this class in an intuitive and simplified
   /// way can be achieved by adding this to the top
   /// of a code unit:
   /// 
   ///   using static Autodesk.AutoCAD.Runtime.LispInterop.ListBuilder;
   ///   
   /// See the included LispBuilderTests.cs for an 
   /// example that shows how the above statement 
   /// helps simplify the use of this class.
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
      /// 
      /// Revisions (AcMgdLib 0.13):
      /// 
      /// Removed automatic conversion of collections of ObjectId 
      /// to selection sets. If a caller desires that conversion, 
      /// they only need to call the ToLispSelectionSet() method,
      /// pass it the collection of ObjectIds, and pass the result
      /// to List() or another method that accepts managed types.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static ListResult List(params object[] args)
      {
         if(args == null || args.Length == 0)
            throw new ArgumentException("Requires at least one argument");
         return GetIterator(ToListWorker(args)).ToResult();
      }

      /// <summary>
      /// The analog of the LISP (cons) function.
      /// 
      /// This method will create dotted pairs, or add 
      /// its first argument to the front of the list 
      /// given as the second argument.
      /// 
      /// Note that this is not a functionally-complete 
      /// emulation of LISP's (cons) and may have some 
      /// limitations. Not all possible uses of the LISP 
      /// analog have been tested.
      /// </summary>
      /// <param name="car">The element to add to the
      /// head/car of the result </param>
      /// <param name="cdr">A list or atom that the car
      /// is to be consed with.</param>
      /// <returns>A dotted pair if the second argument
      /// is not a list, or the second argument with the
      /// first argument as the new first element/car.
      /// </returns>
      
      public static ListResult Cons(object car, object cdr)
      {
         if(IsEnumerable(cdr, out IEnumerable items))
         {
            return List(car, Insert(items));
         }
         else
         {
            return ToList(ListBegin, ToList(car), cdr, DotEnd).ToResult();
         }
      }

      /// <summary>
      /// This method can be used like the List() method,
      /// with the difference being that it does not return
      /// the result nested in a list.
      /// </summary>
      
      public static ListResult ToList(params object[] args)
      {
         return ToListWorker(args).ToResult();
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
      /// Returns an association list constructed from a 
      /// sequence of T, using selector functions to extract 
      /// the key (car) and value (cdr) of each element.
      /// 
      /// This method is similar to the Linq ToDictionary()
      /// method, except that it produces a LISP association
      /// list rather than a managed Dictionary.
      /// 
      /// To convert a Dictionary<TKey, TValue> to an association
      /// list, use:
      /// 
      ///   Dictionary<TKey, TValue> dictionary = //.... assign to a value
      ///   
      ///   var result = Cons(dictionary, p => p.Key, p => p.Value);
      ///   
      ///   The result can be returned by a LispFunction
      ///   
      /// </summary>
      /// <typeparam name="T">The element type</typeparam>
      /// <param name="source">The input sequence of elements</param>
      /// <param name="car">A function that takes an
      /// element and returns its key or 'car'</param>
      /// <param name="cdr">A function that takes 
      /// an element and returns its value or 'cdr'</param>
      /// <returns>A sequence that when returned to LISP
      /// produces an association list of keys and values</returns>

      public static ListResult Cons<T>(IEnumerable<T> source,
         Func<T, object> car,
         Func<T, object> cdr)
      {
         return ToListResult(source.Select(item => Cons(car(item), cdr(item))));
      }

      /// <summary>
      /// An overload of Cons() in which both of the selector 
      /// functions are passed the zero-based positional index 
      /// of each element, in addtion to the element.
      /// </summary>
      /// <param name="car">A function that takes an element
      /// and its positional index within the sequence, and 
      /// returns the key or 'car'</param>
      /// <param name="cdr">A function that takes an 
      /// element and its positional index within the 
      /// sequence, and returns the value or 'cdr'</param>
      
      public static ListResult Cons<T>(IEnumerable<T> source,
         Func<T, int, object> car,
         Func<T, int, object> cdr)
      {
         return ToListResult(source.Select((item, i) => Cons(car(item, i), cdr(item, i))));
      }

      /// <summary>
      /// Converts multiple sequences of TypedValues to a ListResult
      /// </summary>

      public static ListResult ToListResult(
         IEnumerable<IEnumerable<TypedValue>> source,
         bool explode = false)
      {
         IteratorType type = explode ? IteratorType.Explode : IteratorType.List;
         return GetIterator(ToListWorker(source), type).ToResult();
      }
      
      /// <summary>
      /// Inserts the collection argument into the current list
      /// without nesting its elements in a sublist. Within a call
      /// to the List() method, if a collection is included as an
      /// argument, the collection becomes a nested list within the
      /// containing list. If the same collection is instead passed 
      /// to this method, its elements are promoted to elements of 
      /// the containing list.
      ///
      /// This method is only meaningful when its result is
      /// passed as an argument to the List() method. In any
      /// other context, the result is undefined.
      /// 
      /// There is no built-in LISP analog to this method.
      /// </summary>
      /// <param name="arg">The collection whose elements
      /// are to be inserted into a containng list.</param>
      /// <returns>The collection in a form that will cause
      /// them to be inserted into another containing list</returns>

      public static ListResult Insert(IEnumerable arg)
      {
         Assert.IsNotNull(arg, nameof(arg));
         if(!IsEnumerable(arg))
            throw new ArgumentException("Invalid IEnumerable (no strings)");
         return GetIterator(ToListWorker(arg), IteratorType.Explode).ToResult();
      }

      /// <summary>
      /// Appends the elements of multiple collections into
      /// a single list representation, and is analagous to
      /// the LISP (append) function.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static ListResult Append(params IEnumerable[] args)
      {
         return args.OfType<IEnumerable>().SelectMany(Explode).ToResult();
      }

      static IEnumerable<TypedValue> Explode(IEnumerable arg)
      {
         return GetIterator(ToListWorker(arg), IteratorType.Explode);
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

      static IEnumerable<TypedValue> ToListWorker(IEnumerable args, int depth = 0)
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
            LispDataType result = arg.GetType().ToLispDataType();
            if(result != LispDataType.None)
            {
               yield return new TypedValue((short) result, arg);
               continue;
            }

            /// This is required because SelectionSet
            /// is abstract and the concrete types derived 
            /// from it have no map entry (adding them to 
            /// the map would create a version-dependence 
            /// as some of them were recently-added).
            
            if(arg is SelectionSet ss)
            {
               yield return ToLisp(ss);
               continue;
            }
            //if(arg is StringBuilder sb)
            //{
            //   yield return ToLisp(sb.ToString());
            //   continue;
            //}
            if(arg is bool bVal)  // bool converts to T or NIL.
            {
               yield return bVal ? True : Nil;
               continue;
            }

            var converter = TypedValueConverter.GetConverter(arg.GetType());
            if(converter != null && converter.CanConvert(true))
            {
               object converted = converter.ToTypedValues(arg);
               if(converted != null)
                  arg = converted;
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

            if(arg is ResultBuffer rb)
               arg = rb.Cast<TypedValue>();

            /// The result of nested calls to List() and any
            /// conversions to multiple TypedValues is handled 
            /// here. If the IEnumerable<TypedValue> has no 
            /// elements, Nil is returned.

            if(arg is IEnumerable<TypedValue> values)
            {
               if(values is ICollection<TypedValue> tvc && tvc.Count == 0)
               {
                  yield return Nil;
                  continue;
               }
               using(var en = values.GetEnumerator())
               {
                  if(!en.MoveNext())
                  {
                     yield return Nil;
                     continue;
                  }
                  yield return en.Current;
                  while(en.MoveNext())
                     yield return en.Current;
                  continue;
               }
            }

            /// Optimized paths for ObjectIdCollection/IEnumerable<ObjectId>
            /// and Point3dCollection/IEnumerable<Point3d>:

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

            /// Fallback for all IEnumerables not handled above,
            /// such as object[]. 
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
               foreach(var item in ToListWorker(enumerable, depth + 1))
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

      const short tcListBegin = (short)LispDataType.ListBegin;
      const short tcListEnd = (short)LispDataType.ListEnd;
      const short tcDotEnd = (short)LispDataType.DottedPair;

      static bool IsListBegin(TypedValue tv)
      {
         return tv.TypeCode == tcListBegin;
      }

      static bool IsListEnd(TypedValue tv)
      {
         return tv.TypeCode == tcListEnd || tv.TypeCode == tcDotEnd;
      }

      static bool IsEnumerable(object obj)
      {
         return obj is IEnumerable && !(obj is string);
      }

      static bool IsEnumerable(object obj, out IEnumerable result)
      {
         result = null;
         if(obj is IEnumerable enumerable && !(obj is string))
            result = enumerable;
         return result != null;
      }

      static bool IsEmpty(IEnumerable enumerable)
      {
         Assert.IsNotNull(enumerable, nameof(enumerable));  
         if(enumerable is ICollection collection)
            return collection.Count == 0;
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

      /// <summary>
      /// ToLisp() and ToLispXxxxx() extension methods.
      /// 
      /// Produces a TypedValue having a LispDataType 
      /// for the given type of the argument, and the
      /// argument as the value.
      /// </summary>

      public static TypedValue ToLisp(this int value)
      {
         return new TypedValue((short)LispDataType.Int32, value);
      }

      public static TypedValue ToLisp(this short value)
      {
         return new TypedValue((short)LispDataType.Int16, value);
      }

      public static TypedValue ToLisp(this double value, LispDataType type = LispDataType.Double)
      {
         return new TypedValue((short)type, value);
      }

      public static TypedValue ToLispAngle(this double value)
      {
         return new TypedValue((short)LispDataType.Angle, value);
      }

      public static TypedValue ToLispOrientation(this double value)
      {
         return new TypedValue((short)LispDataType.Orientation, value);
      }

      public static TypedValue ToLisp(this ObjectId value)
      {
         return new TypedValue((short)LispDataType.ObjectId, value);
      }

      public static TypedValue ToLisp(this bool value)
      {
         return value ? True : Nil;
      }

      /// <summary>
      /// The validate argument indicates if the collection of
      /// ObjectIds should be checked to ensure that all elements
      /// reference an entity or a derived type.
      /// 
      /// If the caller is sure that all elements reference entities,
      /// they can pass false for this argument, and avoid the extra
      /// overhead involved in checking each ObjectId.
      /// </summary>
      /// <param name="ids"></param>
      /// <param name="validate"></param>
      /// <returns></returns>
      
      public static SelectionSet ToSelectionSet(this IEnumerable<ObjectId> ids, bool validate = false)
      {
         AcRx.ErrorStatus.NotAnEntity.ThrowIf(validate && !ids.IsAllEntities());
         return SelectionSet.FromObjectIds(ids.AsArray());
      }

      public static SelectionSet ToSelectionSet(this ObjectIdCollection ids, bool validate = true)
      {
         AcRx.ErrorStatus.NotAnEntity.ThrowIf(validate && !ids.IsAllEntities());
         ObjectId[] idarray = new ObjectId[ids.Count];
         ids.CopyTo(idarray, 0);
         return SelectionSet.FromObjectIds(idarray);
      }

      public static TypedValue ToLispSelectionSet(this IEnumerable<ObjectId> ids, bool validate = true)
      {
         return ToLisp(ToSelectionSet(ids, validate));
      }

      public static TypedValue ToLispSelectionSet(this ObjectIdCollection ids, bool validate = true)
      {
         return ToLisp(ToSelectionSet(ids, validate));
      }

      public static TypedValue ToLisp(this SelectionSet value)
      {
         return new TypedValue((short)LispDataType.SelectionSet, value);
      }

      public static TypedValue ToLisp(this Point3d value)
      {
         return new TypedValue((short)LispDataType.Point3d, value);
      }

      public static TypedValue ToPoint3d(double x, double y, double z)
      {
         return new TypedValue((short)LispDataType.Point3d, new Point3d(x, y, z));
      }

      public static TypedValue ToLisp(this Point2d value)
      {
         return new TypedValue((short)LispDataType.Point2d, value);
      }

      public static TypedValue ToPoint2d(double x, double y)
      {
         return new TypedValue((short)LispDataType.Point2d, new Point2d(x, y));
      }

      public static TypedValue ToLisp(this string value)
      {
         return new TypedValue((short)LispDataType.Text, value);
      }

      /// <summary>
      /// Returns an 'entsel' list containing the entity name
      /// and the point used to pick it.
      /// </summary>
      /// <param name="per"></param>
      /// <returns></returns>
      
      public static IEnumerable<TypedValue> ToLisp(this PromptEntityResult per)
      {
         Assert.IsNotNull(per, nameof(per));
         return ToLisp(per.ObjectId, per.PickedPoint);
      }

      public static IEnumerable<TypedValue> ToLisp(this ObjectId id, Point3d pickPoint)
      {
         return new TypedValue[] {
            ListBegin,
            ToLisp(id),
            ToLisp(pickPoint),
            ListEnd
         };
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
      /// Produces a sequence of TypedValue from a homogenous 
      /// sequence of T, optionally nested in List delimters.
      /// 
      /// NOTE: bool is not a supported generic argument type,
      /// because its values map to two different LispDataTypes.
      /// 
      /// Returns LispDataType.Nil if the given sequence is empty.
      /// </summary>
      /// <typeparam name="T">The type of the List objects</typeparam>
      /// <param name="source">The List objects</param>
      /// <param name="type">The LispDataType to set each resulting 
      /// TypedValue to. If this value is LispDataType.None, this API
      /// will attempt to deduce the value from the generic argument.</param>
      /// <param name="list">A value indicating if the elements should be
      /// enclosed in a matching pair of ListBegin/ListEnd elements</param>
      /// <returns>The sequence of TypedValues derived from the List</returns>

      public static IEnumerable<TypedValue> ToList<T>(this IEnumerable<T> source, LispDataType type = LispDataType.None, bool list = true)
      {
         /// Refactored to avoid call to Enumerable.Any():
         Assert.IsNotNull(source, nameof(source));
         if(typeof(T) == typeof(bool))
            throw new ArgumentException("ToList(): bool not supported.");
         if(source is ICollection<T> coll2 && coll2.Count == 0)
         {
            yield return Nil;
            yield break;
         }
         using(var e = source.GetEnumerator())
         {
            if(!e.MoveNext())
            {
               yield return Nil;
               yield break;
            }
            if(type == LispDataType.None)
               type = typeof(T).ToLispDataType(true);
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

      public static Type ToType(this LispDataType type, bool throwIfNotFound = false)
      {
         Type result = null;
         if(lispDataTypeToTypeMap.TryGetValue(type, out result))
            return result;
         if(throwIfNotFound)
            throw new ArgumentException($"Unsupported LispDataType: {type}");
         return result;
      }
     
      /// <summary>
      /// Cannot map a bool because its two values
      /// map to two different LispDataTypes. Hence,
      /// mapping to LispDataType requires a value.
      /// </summary>

      public static LispDataType ToLispDataType(this Type type, bool throwIfNotFound = false)
      {
         if(typeof(SelectionSet).IsAssignableFrom(type))
            return LispDataType.SelectionSet;
         if(typeToLispDataTypeMap.TryGetValue(type, out LispDataType result))
            return result;
         if(throwIfNotFound)
            throw new ArgumentException($"Unsupported type {type.CSharpName()}");
         return LispDataType.None;
      }

      public static TypedValue ToTypedValue(object arg, LispDataType type = LispDataType.None, bool throwIfNotFound = false)
      {
         if(arg == null)
            return new TypedValue((int)LispDataType.Nil);
         if(arg is bool b)
            return ToLisp(b);
         if(arg is SelectionSet ss)
            return ToLisp(ss);
         if(type == LispDataType.None)
            type = arg.GetType().ToLispDataType(throwIfNotFound);
         if(type != LispDataType.None)
            return new TypedValue((short)type, arg);
         if(throwIfNotFound)
            throw new ArgumentException($"Unsupported Type {arg.GetType()}");
         return None;
      }

      static readonly Dictionary<LispDataType, Type> lispDataTypeToTypeMap = new()
      {
         { LispDataType.None, null},
         { LispDataType.Double, typeof(double)},
         { LispDataType.Orientation, typeof(double)},
         { LispDataType.Angle, typeof(double)},
         { LispDataType.Point2d, typeof(Point2d) },
         { LispDataType.Int16, typeof(short) },
         { LispDataType.Text, typeof(string) },
         { LispDataType.ObjectId, typeof(ObjectId) },
         { LispDataType.SelectionSet, typeof(SelectionSet) },
         { LispDataType.Point3d, typeof(Point3d) },
         { LispDataType.Int32, typeof(int) },
         { LispDataType.Void, typeof(void) },
         { LispDataType.ListBegin, typeof(ListBeginClass) },
         { LispDataType.ListEnd, typeof(ListEndClass) },
         { LispDataType.DottedPair, typeof(DottedPairClass) },
         { LispDataType.Nil, typeof(NilClass) },
         { LispDataType.T_atom, typeof(bool) }
      };

      /// <summary>
      /// Only maps value types, class types like
      /// SelectionSet (which is abstract) must be
      /// tested using the 'is' operator. bool maps
      /// to two different LispDataTypes, so it must
      /// be handled differently as well.
      /// </summary>

      static readonly Dictionary<Type, LispDataType> typeToLispDataTypeMap = new()
      {
         { typeof(double), LispDataType.Double },
         { typeof(float), LispDataType.Double },
         { typeof(Point2d), LispDataType.Point2d },
         { typeof(short), LispDataType.Int16 },
         { typeof(sbyte), LispDataType.Int16 },
         { typeof(byte), LispDataType.Int16 },
         { typeof(char), LispDataType.Int16 },
         { typeof(string), LispDataType.Text },
         { typeof(ObjectId), LispDataType.ObjectId },
         { typeof(SelectionSet), LispDataType.SelectionSet }, // requires is test (abstract)
         { typeof(Point3d), LispDataType.Point3d },
         { typeof(int), LispDataType.Int32 },
      };

      /// <summary>
      /// LispDataType.ToType() must return a type.
      /// These proxy types are used for List delimiter types:
      /// </summary>

      public class ListBeginClass { }
      public class ListEndClass { }
      public class DottedPairClass { }
      public class NilClass { }

      /// <summary>
      /// ListBegin, ListEnd, and DotEnd are already used as
      /// public members for TypedValue fields. These fields
      /// are intended for use with the LispDataType.ToType()
      /// and Type.ToLispDataType() extension methods.
      /// </summary>
      
      public static readonly ListBeginClass ListBeginType = new ListBeginClass();
      public static readonly ListEndClass ListEndType = new ListEndClass();
      public static readonly DottedPairClass DottedPairType = new DottedPairClass();
      public static readonly NilClass NilType = new NilClass();

      /// <summary>
      /// An extension method that can be used with Linq
      /// operations to convert a sequence of objects to 
      /// a sequence of TypedValues representing a Lisp list.
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static IEnumerable<TypedValue> ToLispList(this IEnumerable arg)
      {
         return ToListWorker(arg);
      }

      /// <summary>
      /// Converts a sequence of objects to a ResultBuffer
      /// representing a LISP list:
      /// </summary>
      /// <param name="args"></param>
      /// <returns></returns>

      public static ResultBuffer ToResultBuffer(this IEnumerable args)
      {
         return ToLispList(args).ToResult();
      }

      static IEnumerable<TypedValue> GetIterator(IEnumerable<TypedValue> source, IteratorType type = IteratorType.List)
      {
         if(type == IteratorType.Default || source is Iterator)
            return source;
         else
            return new Iterator(source, type);
      }

      static ListBuilder()
      {
         TypedValueConverterAttribute.Initialize();
      }


      [Flags]
      public enum IteratorType
      {
         Default = 0,         // Enumerate elements as-is.
         List = 1,            // Enclose all elements in a ListBegin/End sequence.
         Explode = 2,         // Explode top-most nested lists
         DeepExplode = 4      // Explode nested lists at any depth.
      }

      /// <summary>
      /// A type that wraps an instance of this can alter its
      /// behavior after creation, by setting the IteratorType
      /// property.
      /// </summary>
      
      public interface IListIterator : IEnumerable<TypedValue>
      {
         public IteratorType IteratorType { get; set; }
      }

      /// <summary>
      /// Facilitates exploding and/or nesting of lists
      /// 
      /// The default behavior is to enclose the source
      /// sequence in a ListBegin/End sequence.
      /// </summary>
      
      class Iterator : IListIterator
      {
         IEnumerable<TypedValue> source;
         IteratorType type = IteratorType.Default;
         bool enumerated = false;

         public Iterator(IEnumerable<TypedValue> source, IteratorType type = IteratorType.List)
         {
            this.source = source;
            this.type = type;
         }

         public IteratorType IteratorType
         {
            get => type;
            set
            {
               if(enumerated)
                  throw new InvalidOperationException(
                     "Property cannot be asssigned to after enumeration has started.");
               this.type = value;
            }
         }

         public IEnumerator<TypedValue> GetEnumerator()
         {
            if(type == IteratorType.Default)
               return source.GetEnumerator();
            else
               return GetValues().GetEnumerator();
         }

         IEnumerator IEnumerable.GetEnumerator()
         {
            return this.GetEnumerator();
         }

         IEnumerable<TypedValue> GetValues()
         {
            enumerated = true;
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




