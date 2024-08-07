/// TypedValueListExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Diagnostic and validation helper methods.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Extensions;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Utility;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime.Extensions;

namespace Autodesk.AutoCAD.Runtime.Extensions
{
   /// <summary>
   /// Partial conversion of TypedValueList methods to extension 
   /// methods that can target TypedValueList, or any type that 
   /// implements IList<TypedValue> (such as List<TypedValue>).
   /// 
   /// These classes are essentially the evolution of the original 
   /// TypedValueList class that was initially published here:
   /// 
   ///    http://www.theswamp.org/index.php?topic=14495.msg186823#msg186823
   /// 
   /// Over the years, new functionality has been 
   /// added, implemented as extension methods.
   /// 
   /// Notes: Most of these extension methods target IList<TypedValue>,
   /// which includes List<TypedValue>, TypedValueList, and TypedValue[].
   /// However, functions that are not read-only operations, such as the
   /// AddRange() overloads cannot be used on arrays of TypedValue, as 
   /// they are not resizable. A runtime check is performed that rejects
   /// arrays in all methods that add/remove list to/from the target.
   /// 
   /// LINQ-less implementation.
   /// 
   /// Many of the operations performed by this class can be easily-
   /// performed by LINQ operations of varying-complexity, but only 
   /// at the cost of performance. That was in-part, the motivation 
   /// behind including non-LINQ implmementations of those operations.
   /// </summary>

   public static class TypedValueListExtensions
   {

      /// <summary>
      /// Validates an IList<TypedValue> as not being fixed-size 
      /// (e.g., it is not an array) and as having elements that 
      /// can be modified via the set indexer.
      /// 
      /// While arrays support IList<T>, they cannot be expanded.
      /// </summary>

      static IList<T> CheckIsFixedSize<T>(IList<T> list)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         if(list is T[] || list.IsReadOnly)
            throw new InvalidOperationException("the collection is read-only or not expandable");
         return list;
      }

      /// <summary>
      /// The Add() overloads from the original TypedValueList:
      /// </summary>

      public static void Add(this IList<TypedValue> list, short typeCode, object value)
      {
         CheckIsFixedSize(list).Add(new TypedValue(typeCode, value));
      }

      public static void Add(this IList<TypedValue> list, LispDataType type, object value)
      {
         CheckIsFixedSize(list).Add(new TypedValue((short)type, value));
      }

      public static void Add(this IList<TypedValue> list, DxfCode code, object value)
      {
         CheckIsFixedSize(list).Add(new TypedValue((short)code, value));
      }

      /// <summary>
      /// New overload of AddRange() that adds one or more List 
      /// with a type code determined by ResultBuffer.ObjectsToResbuf();
      /// </summary>

      public static void AddRange(this IList<TypedValue> list, params object[] values)
      {
         AddObjects(list, values);
      }

      /// <summary>
      /// Overload of the above that accepts List as an IEnumerable
      /// </summary>

      public static void AddRange(this IList<TypedValue> list, IEnumerable values)
      {
         if(values == null)
            throw new ArgumentNullException(nameof(values));
         object[]? array = values as object[];
         if(array == null)
         {
            ICollection? collection = values as ICollection;
            if(collection != null)
            {
               array = new object[collection.Count];
               collection.CopyTo(array, 0);
            }
            else
            {
               array = values.Cast<object>().ToArray();
            }
         }
         AddObjects(list, array);
      }

      public static void AddObjects(this IList<TypedValue> list, object[] values)
      {
         CheckIsFixedSize(list);
         if(values != null && values.Length > 0)
         {
            var ptr = Marshaler.ObjectsToResbuf(values);
            if(ptr == IntPtr.Zero)
               throw new InvalidOperationException($"failed to convert {nameof(values)} to resbuf");
            ResultBuffer buffer = (ResultBuffer)DisposableWrapper.Create(typeof(ResultBuffer), ptr, true);
            AddRange(list, buffer.AsArray());
         }
      }

      /// Adds a range of elements expressed as IEnumerable<TypedValue>

      static void AddRange(IList<TypedValue> list, IEnumerable<TypedValue> values)
      {
         CheckIsFixedSize(list);
         if(list is List<TypedValue> tmp)
         {
            tmp.AddRange(values);
         }
         else
         {
            foreach(TypedValue tv in values)
               list.Add(tv);
         }
      }

      public static void AddRange(this IList<TypedValue> list, ResultBuffer rb)
      {
         AddRange(list, rb.Cast<TypedValue>());
      }

      /// <summary>
      /// Adds a range of elements expressed as ValueTyple(short, object):
      /// </summary>

      public static void AddRange(this IList<TypedValue> list, params (short code, object value)[] args)
      {
         CheckIsFixedSize(list);
         if(args == null)
            throw new ArgumentNullException(nameof(args));
         if(args.Length > 0)
         {
            AddRange(list, args.ToTypedValues());
         }
      }

      /// <summary>
      /// Adds a range of elements all having the same 
      /// given type code, each having one of the given 
      /// List:
      /// 
      /// e.g.:
      /// <code>
      ///  
      ///    var list = new List<TypedValue>();
      ///    
      ///    list.AddRange(DxfCode.Text, "Moe", "Larry", "Curly");
      ///    
      /// Which is equivlaent to
      /// 
      ///    list.Add(new TypedValue((short) DxfCode.Text, "Moe")));
      ///    list.Add(new TypedValue((short) DxfCode.Text, "Larry")));
      ///    list.Add(new TypedValue((short) DxfCode.Text, "Curly")));
      ///    
      /// </code>
      /// 
      /// This method is overloaded with 2 variants x 3 versions, 
      /// varying by the type of the type code (LispDataType, DxfCode,
      /// and short), and by the form in which the List are provided
      /// (one as params T[], and one as IEnumerable<T>).
      /// 
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="list"></param>
      /// <param name="code"></param>
      /// <param name="values"></param>

      public static void AddRange<T>(this IList<TypedValue> list, short code, params T[] values)
      {
         CheckIsFixedSize(list);
         if(values == null)
            throw new ArgumentNullException(nameof(values));
         if(values.Length > 0)
            AddRange(list, values.Select(v => new TypedValue(code, v)));
      }

      public static void AddRange<T>(this IList<TypedValue> list, short code, IEnumerable<T> values)
      {
         CheckIsFixedSize(list);
         if(values == null)
            throw new ArgumentNullException(nameof(values));
         if(values.Any())
            AddRange(list, values.Select(value => new TypedValue(code, value)));
      }

      public static void AddRange<T>(this IList<TypedValue> list, DxfCode code, params T[] values)
      {
         AddRange(list, (short)code, values);
      }

      public static void AddRange<T>(this IList<TypedValue> list, DxfCode code, IEnumerable<T> values)
      {
         AddRange(list, (short)code, values);
      }

      public static void AddRange<T>(this IList<TypedValue> list, LispDataType code, params T[] values)
      {
         AddRange(list, (short)code, values);
      }

      public static void AddRange<T>(this IList<TypedValue> list, LispDataType code, IEnumerable<T> values)
      {
         AddRange(list, (short)code, values);
      }

      /// <summary>
      /// Gets an instance of an interface type that
      /// provides direct access to the value of each
      /// TypedValue element in the list via an indexer.
      /// 
      /// For example:
      /// <code>
      ///           
      ///    IList<TypedValue> list = new List<TypedValue>();
      ///    list.AddRange(DxfCode.Text, "Moe", "Larry", "Curly");
      ///    ITypedValueList List = list.GetValueList();
      ///    
      ///    /// The value of each element can be
      ///    /// accessed via the indexer:
      ///    
      ///    string first = List[0];
      ///    List[1] = "Felix";
      ///           
      ///    
      /// </code>
      /// Note that the returned ITypedValueList does not
      /// represent a copy of the List/input list, it is
      /// just a wrapper around it.
      /// </summary>
      /// <param name="list">The list of TypedValues</param>
      /// <returns>An instance of an ITypedValueList that
      /// provides access to the TypedValues of each TypedValue
      /// element in the given list via an indexer.</returns>

      public static ITypedValueList GetValueList(this IList<TypedValue> list)
      {
         return new ValueList(list);
      }

      /// <summary>
      /// Returns a sequence of IList<TypedValue> where each
      /// element represents a repeating sequence that starts
      /// with an element having the given typeCode.
      /// 
      /// Each returned List<TypedValue> contains the elements
      /// that start with an element having the given typeCode,
      /// and all elements following it, up to the next element
      /// having the given typeCode.
      /// 
      /// This method assumes that all repeating sequences have 
      /// an equal number of elements following each element that
      /// starts with the given typeCode.
      /// 
      /// Elements that precede the first occurence of an element
      /// with the given typeCode, and elements following the last 
      /// repeating sequence are excluded.
      /// 
      /// Example:
      /// <code>
      /// 
      /// static void GroupByExample()
      /// {
      ///    TypedValueList list = new TypedValueList(
      /// 
      ///       (1, "Moe"),
      ///       (2, "Larry"),
      ///       (3, "Curly"),
      ///       (4, "Foo"),
      ///       (5, "Bar"),
      ///       (330, "Group 1"), // start of first sequence
      ///       (10, 0.0),
      ///       (40, 0.25),
      ///       (210, 1.0),
      ///       (330, "Group 2"), // start of second sequence
      ///       (10, 0.0),
      ///       (40, 0.25),
      ///       (210, 2.0),
      ///       (330, "Group 3"),
      ///       (10, 0.0),
      ///       (40, 0.25),
      ///       (210, 3.0),
      ///       (330, "Group 4"),
      ///       (10, 0.0),
      ///       (40, 0.25),
      ///       (210, 4.0),
      ///       (330, "Group 5"),
      ///       (10, 0.0),
      ///       (40, 0.25),
      ///       (210, 5.0),
      ///       (7, "Seven"),
      ///       (8, "Eight"),
      ///       (9, "Nine"),
      ///       (10, "Ten")
      ///    );
      /// 
      ///    var groups = list.GroupBy(330);
      /// 
      ///    foreach(var item in groups)
      ///    {
      ///       Console.WriteLine(item.ToString<short>());
      ///    }
      /// }
      /// 
      /// The above code will produce the following output:
      /// 
      ///   (330: Group 1) (10: 0) (40: 0.25) (210: 1)
      ///   (330: Group 2) (10: 0) (40: 0.25) (210: 2)
      ///   (330: Group 3) (10: 0) (40: 0.25) (210: 3)
      ///   (330: Group 4) (10: 0) (40: 0.25) (210: 4)
      ///   (330: Group 5) (10: 0) (40: 0.25) (210: 5)
      /// 
      /// In the above example, each repeating sequence starts
      /// with an element having the TypeCode 330, and ends with 
      /// an element having the TypeCode 210.
      /// </code>
      /// 
      /// </summary>

      /// Helper for IEnumerable<T> advances the enumerator
      /// while the given function returns true.

      static bool Seek<T>(this IEnumerator<T> e, Func<T, bool> func)
      {
         while(e.MoveNext())
            if(func(e.Current))
               return true;
         return false;
      }

      public static IEnumerable<IList<TypedValue>> GroupBy(
         this IList<TypedValue> list,
         short typeCode)
      {
         int len = -1;
         TypedValueList? sublist = null;
         using(var e = list.GetEnumerator())
         {
            if(e.Seek(tv => tv.TypeCode == typeCode))
            {
               sublist = new TypedValueList(e.Current);
               while(e.MoveNext())
               {
                  TypedValue tv = e.Current;
                  if(tv.TypeCode != typeCode)
                  {
                     sublist.Add(tv);
                     continue;
                  }
                  len = sublist.Count;
                  yield return sublist;
                  sublist = new TypedValueList(tv);
                  sublist.Capacity = len;
                  break;
               }
               while(e.MoveNext())
               {
                  TypedValue tv = e.Current;
                  if(tv.TypeCode == typeCode)
                  {
                     yield return sublist;
                     sublist = new TypedValueList(tv);
                     sublist.Capacity = len;
                     continue;
                  }
                  else if(sublist.Count == len)
                  {
                     yield return sublist;
                     break;
                  }
                  else
                  {
                     sublist.Add(tv);
                  }
               }
            }
         }
      }

      /// <summary>
      /// Returns a sequence containing all elements of the given
      /// list starting with the first element, up to but exluding
      /// the first element having the given type code.
      /// </summary>
      /// <param name="list"></param>
      /// <param name="code"></param>
      /// <returns></returns>

      public static IEnumerable<TypedValue> TakeBefore(this IList<TypedValue> list, short code)
      {
         return list.TakeWhile(tv => tv.TypeCode != code);
      }

      /// <summary>
      /// Returns a sequence of elements from the given list, 
      /// starting with the first element that follows the
      /// last element having the given type code, followed
      /// by all remaining elements in the list.
      /// </summary>
      /// <param name="list"></param>
      /// <param name="code"></param>
      /// <returns></returns>

      public static IEnumerable<TypedValue> TakeAfter(this IList<TypedValue> list, short code)
      {
         return list.TakeAfter(tv => tv.TypeCode == code);
      }

      public static IEnumerable<T> TakeAfter<T>(this IList<T> list, Func<T, bool> predicate)
      {
         int next = list.IndexOfLast(predicate);
         if(next > -1 && next < list.Count - 1)
         {
            for(int i = next + 1; i < list.Count; i++)
               yield return list[i];
         }
      }

      public static IEnumerable<TypedValue> Ungroup(this IEnumerable<IList<TypedValue>> list)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         foreach(var sublist in list)
         {
            if(sublist != null)
            {
               foreach(TypedValue tv in sublist)
                  yield return tv;
            }
         }
      }

      /// <summary>
      /// Returns the index of the first element having the given type code
      /// or -1 if no element having the given type code exists.
      /// </summary>

      public static int IndexOf(this IList<TypedValue> list, short code)
      {
         return list.IndexOf(tv => tv.TypeCode == code);
      }

      /// <summary>
      /// Returns the index of the first element in the list that
      /// satisfies the given predicate, or -1 if no element in
      /// the list satisfies the predicate.
      /// </summary>

      public static int IndexOf<T>(this IList<T> list, Func<T, bool> predicate)
      {
         for(int i = 0; i < list.Count; i++)
         {
            if(predicate(list[i]))
               return i;
         }
         return -1;
      }

      /// TODO: Move to generic list extension class

      /// <summary>
      /// Returns the index of the last element in the list that
      /// satisfies the given predicate, or -1 if no element in
      /// the list satisfies the predicate.
      /// </summary>

      public static int IndexOfLast<T>(this IList<T> list, Func<T, bool> predicate)
      {
         for(int i = list.Count - 1; i >= 0; i--)
         {
            if(predicate(list[i]))
               return i;
         }
         return -1;
      }

      /// <summary>
      /// Returns the index of the last element in the list having
      /// a TypeCode equal to the given code, or -1 if no element in 
      /// the list has a TypeCode equal to the given code.
      /// </summary>

      public static int IndexOfLast(this IList<TypedValue> list, short code)
      {
         return list.IndexOfLast(tv => tv.TypeCode == code);
      }

      /// <summary>
      /// Following along with the conventions used in the overloaded
      /// AddRange(DxfCode, value, value, value, ...), this method will
      /// insert one or more new elements into the existing liSt, all of
      /// which have the given type code, and each of which has one of 
      /// the given List.
      /// 
      /// One element having the given code is inserted into the list 
      /// for each provided value.
      /// 
      /// Note: This method is only applicable to List<TypedValue> or 
      /// TypedValueList. It cannot be used on any IList<TypedValue>.
      /// </summary>
      /// <param name="list">The List<TypedValue> to insert the list into</param>
      /// <param name="index">The index at which to insert the new item(s)</param>
      /// <param name="code">The type code assigned to all newly-inserted list</param>
      /// <param name="values">The List assigned to each newly-inserted item</param>

      public static void InsertRange(this List<TypedValue> list, int index, short code, params object[] values)
      {
         CheckIndex(list, index);
         IEnumerable<TypedValue> newItems = values.Select(val => new TypedValue(code, val));
         list.InsertRange(index, newItems);
      }

      /// <summary>
      /// Inserts the specified number of new elements into the list starting
      /// at the specified index. Each newly-inserted element has the specified
      /// type code, and a value produced by the given function, which takes the
      /// integer offset of the newly-inserted item relative to the index argument.
      /// </summary>
      /// <param name="list">The List<TypedValue> to insert the list into</param>
      /// <param name="index">The index at which to insert the new item(s)</param>
      /// <param name="count">The number of elements to be inserted</param>
      /// <param name="code">The type code assigned to all newly-inserted list</param>
      /// <param name="func">A function that takes an integer offset from the
      /// index parameter, and returns the value to be assigned to the element</param>

      public static void InsertRange(this List<TypedValue> list, int index, int count, Func<int, TypedValue> func)
      {
         CheckIndex(list, index);
         list.InsertRange(index, Enumerable.Range(0, count).Select(i => func(i)));
      }

      /// <summary>
      /// Inserts a sequence of TypedValues immediately after
      /// the last existing element having the specified code.
      /// </summary>

      public static void InsertAfterLast(this List<TypedValue> list,
         short code, IEnumerable<TypedValue> values)
      {
         int last = IndexOfLast(list, code);
         if(last > -1)
            list.InsertRange(last + 1, values);
         else
            throw new InvalidOperationException("item not found");
      }

      /// <summary>
      /// Inserts a sequence of TypedValues all having the same
      /// specified typeCode, and each having one of the specified 
      /// List, immediately after the last existing element 
      /// having the specified code.
      /// </summary>

      public static void InsertAfterLast<T>(this List<TypedValue> list,
         short code, short typeCode, IEnumerable<T> values)
      {
         int last = IndexOfLast(list, code);
         if(last > -1)
            list.InsertRange(last + 1, values.Select(v => new TypedValue(typeCode, v)));
         else
            throw new InvalidOperationException("item not found");
      }

      /// <summary>
      /// Returns a sequence of the List of all
      /// elements having the given type code.
      /// 
      /// While this can easily be done using Linq, 
      /// (e.g., Where(...).Select(...)), this is a 
      /// tad more effecient.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="list"></param>
      /// <param name="code"></param>
      /// <returns></returns>

      public static IEnumerable<T> ValuesOfType<T>(this IList<TypedValue> list, short code)
      {
         for(int i = 0; i < list.Count; i++)
         {
            TypedValue item = list[i];
            if(item.TypeCode == code)
               yield return (T)item.Value;
         }
      }

      /// <summary>
      /// Returns the number of elements in the given list
      /// having a TypeCode equal to the specified value.
      /// 
      /// While this can be easily accomplished using the
      /// LINQ Count() method, that method doesn't optimize
      /// for IList types and requires the use of a delegate.
      /// </summary>
      /// <param name="list"></param>
      /// <param name="code"></param>
      /// <returns></returns>

      public static int CountOfType(this IList<TypedValue> list, short code)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         int count = 0;
         int end = list.Count;
         List<TypedValue>? implist = list as List<TypedValue>;
         if(implist != null)
         {
            var span = CollectionsMarshal.AsSpan(implist);
            for(int i = 0; i < end; i++)
            {
               if(span[i].TypeCode == code)
                  ++count;
            }
         }
         else
         {
            for(int i = 0; i < end; i++)
            {
               if(list[i].TypeCode == code)
                  ++count;
            }
         }
         return count;
      }

      /// <summary>
      /// Returns a sequence containing the indices of 
      /// every element whose TypeCode is equal to the
      /// specified code.
      /// </summary>

      public static IEnumerable<int> IndicesOfType(this IList<TypedValue> list, short code)
      {
         return list.IndicesOf(tv => tv.TypeCode == code);
      }

      /// TODO: Move to generic list extension class

      /// <summary>
      /// Returns a sequence containing the indices of
      /// all elements that satisify the given predicate.
      /// </summary>

      public static IEnumerable<int> IndicesOf<T>(this IList<T> list, Func<T, bool> predicate)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         for(int i = 0; i < list.Count; i++)
         {
            if(predicate(list[i]))
               yield return i;
         }
      }

      /// <summary>
      /// Gets the index of the nth occurence of an element 
      /// having the specified type code.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="list"></param>
      /// <param name="code">The type code to search for</param>
      /// <param name="index">The 0-based sub-index of the element.
      /// A value of 0 returns the first occurence of an element
      /// having the given type code. A negative value returns the 
      /// last occurence of an element having the given type code.</param>
      /// <returns>The index of the requested element having the given
      /// type code, or -1 if no element was found with the given code.</returns>

      public static int GetIndexOfTypeAt(this IList<TypedValue> list, short code, int index)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         int idx = -1;
         if(index < 0)
         {
            for(int i = list.Count - 1; i >= 0; i--)
            {
               if(list[i].TypeCode == code)
                  return i;
            }
            return -1;
         }
         else
         {
            for(int i = 0; i < list.Count; i++)
            {
               if(list[i].TypeCode == code && ++idx == index)
                  return i;
            }
            return -1;
         }
      }

      static void CheckIndex(this IList<TypedValue> list, int index, bool checkFixedSize = false)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         if(checkFixedSize)
            CheckIsFixedSize(list);
         if(index < 0 || index >= list.Count)
            throw new IndexOutOfRangeException(index.ToString());
      }

      public static T SetValueAt<T>(this IList<TypedValue> list, int index, T value)
      {
         CheckIndex(list, index, true);
         var tv = list[index];
         if(!(tv.Value is T))
            throw new ArgumentException("type mismatch");
         list[index] = new TypedValue(tv.TypeCode, value);
         return (T)tv.Value;
      }

      /// <summary>
      /// Replaces the List of all elements having the given type code.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="list"></param>
      /// <param name="code"></param>
      /// <param name="converter">A function that takes the subindex of
      /// the element and the existing value, and returns the new value
      /// for the element. The subindex is the nth occurence of an element 
      /// having the given type code. The subindex of the first element
      /// having the given type code is 0</param>

      public static void ReplaceValues<T>(this IList<TypedValue> list, short code, Func<int, T, T> converter)
      {
         CheckIsFixedSize(list);
         int subindex = -1;
         for(int i = 0; i < list.Count; i++)
         {
            var tv = list[i];
            if(tv.TypeCode == code)
            {
               list[i] = new TypedValue(tv.TypeCode, converter(++subindex, (T)tv.Value));
            }
         }
      }

      /// <summary>
      /// Converts the target to a new Xrecord;
      /// </summary>

      public static Xrecord ToXrecord(this IList<TypedValue> list)
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         Xrecord xrecord = new Xrecord();
         xrecord.Data = new ResultBuffer(list.AsArray());
         return xrecord;
      }

      public static string ToString(this IList<TypedValue> list, string delimiter = "\n")
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         return ToString<DxfCode>(list, delimiter);
      }

      public static string ToString<T>(this IList<TypedValue> list, string delimiter = " ") where T : struct
      {
         if(list == null)
            throw new ArgumentNullException(nameof(list));
         Type type = typeof(T);
         if(!(type == typeof(DxfCode) || type == typeof(LispDataType) || type == typeof(short)))
            throw new ArgumentException("Invalid type");
         StringBuilder sb = new StringBuilder();
         bool flag = type.IsEnum;
         foreach(var tv in list)
         {
            object o = flag ? Enum.ToObject(type, tv.TypeCode) : tv.TypeCode;
            if(o != null)
               sb.Append($"({o}: {tv.Value})");
            else
               sb.Append($"({tv.TypeCode}: {tv.Value})");
            sb.Append(delimiter);
         }
         return sb.ToString();
      }

      /// <summary>
      /// Creates an XRecord containing the specified sequence
      /// of ObjectIds as hard/soft owner/pointer references,
      /// according to the specified DxfCode. The default is
      /// DxfCode.SoftPointerId.
      /// 
      /// This version is somewhat faster than the version 
      /// that takes an IEnumerable<T>, because it avoids 
      /// use of LINQ, which is not recommended for dealing 
      /// with conversion of collections of determinant size.
      /// </summary>
      /// <param name="ids">The ObjectIds to store in the Xrecord</param>
      /// <param name="code">The dxf code to assign to each element</param>
      /// <returns>An Xrecord containing the given ObjectIds</returns>

      public static Xrecord ToXrecord(this ObjectIdCollection ids,
         short code = 330, bool xlateReferences = true)
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         TypedValue[] array = new TypedValue[ids.Count];
         for(int i = 0; i < array.Length; i++)
            array[i] = new TypedValue(code, ids[i]);
         Xrecord xrecord = new Xrecord();
         xrecord.XlateReferences = xlateReferences;
         xrecord.Data = new ResultBuffer(array);
         return xrecord;
      }

      /// <summary>
      /// Slower version that takes an IEnumerable<ObjectId>,
      /// and uses helper methods from this class.
      /// </summary>
      /// <param name="ids">The ObjectIds to store in the Xrecord</param>
      /// <param name="code">The dxf code to assign to each element</param>
      /// <returns>An Xrecord containing the given ObjectIds</returns>

      public static Xrecord ToXrecord(this IEnumerable<ObjectId> ids,
         short code = 330, bool xlateReferences = true)
      {
         if(ids == null)
            throw new ArgumentNullException(nameof(ids));
         TypedValueList list = new TypedValueList();
         list.AddRange(code, ids);
         Xrecord xrecord = new Xrecord();
         xrecord.XlateReferences = xlateReferences;
         xrecord.Data = list;
         return xrecord;
      }

      public static ResultBuffer ToResultBuffer(this IEnumerable<TypedValue> items)
      {
         Assert.IsNotNull(items);
         return new ResultBuffer(items.AsArray());
      }

      /// <summary>
      /// Extensions targeting ValueTuple(short/DxfCode.LispDataType, object)
      /// that convert those types to one or more TypedValues.
      /// 
      /// EDIT: Moved to TupleExtensions.cs
      /// </summary>

      /// ResultBuffer conversion to/from Dictionary<TKey, TValue>:

      public static ResultBuffer ToResultBuffer<TKey, TValue>(
         this Dictionary<TKey, TValue> items,
         short keyCode,
         short valueCode)
      {
         var result = new ResultBuffer();
         foreach(var pair in items)
         {
            result.Add(new TypedValue(keyCode, pair.Key));
            result.Add(new TypedValue(valueCode, pair.Value));
         }
         return result;
      }

      public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
         this ResultBuffer rb,
         Func<TValue> factory = null) where TValue : new()
      {
         var result = new Dictionary<TKey, TValue>();
         var e = rb.GetEnumerator();
         while(e.MoveNext())
         {
            var key = e.Current.Value;
            if(!e.MoveNext())
               throw new InvalidOperationException("Malformed sequence: value expected");
            result.Add((TKey)key, (TValue)e.Current.Value);
         }
         return result;
      }

      /// ResultBuffer conversion to/from Dictionary<TKey, List<TValue>>:

      public static ResultBuffer ToResultBuffer<TKey, TValue>(
         this Dictionary<TKey, IEnumerable<TValue>> items,
         DxfCode keyCode,
         DxfCode valueCode)
      {
         return ToResultBuffer<TKey, TValue>(items, (short) keyCode, (short) valueCode);
      }

      public static ResultBuffer ToResultBuffer<TKey, TValue>(
         this Dictionary<TKey, IEnumerable<TValue>> items,
         short keyCode,
         short valueCode)
      {
         Assert.IsNotNull(items, nameof(items));
         var result = new ResultBuffer();
         foreach(var pair in items)
         {
            result.Add(new TypedValue(keyCode, pair.Key));
            result.Add(dxfListBegin);
            if(pair.Value != null)
            {
               foreach(TValue value in pair.Value)
                  result.Add(new TypedValue(valueCode, value));
            }
            result.Add(dxfListEnd);
         }
         return result;
      }

      /// <summary>
      /// Converts a Dictionary<TKey, IEnumerable<TValue>> to a
      /// LISP-compatible ResultBuffer that converts to a proper
      /// association list in LISP, with each sublist having the 
      /// key as the first element (car) and the List as the 
      /// remaining elements. If there is only a single TValue
      /// element in an entry, it is returned as the cdr of a 
      /// dotted-pair with the car being the key.
      /// 
      /// The counterpart to this method is ToLispDictionary(),
      /// which performs the reverse conversion.
      /// 
      /// </summary>
      /// <typeparam name="TKey"></typeparam>
      /// <typeparam name="TValue"></typeparam>
      /// <param name="items"></param>
      /// <param name="keyCode"></param>
      /// <param name="valueCode"></param>
      /// <returns></returns>

      public static ResultBuffer ToResultBuffer<TKey, TValue>(
         this Dictionary<TKey, IEnumerable<TValue>> items,
         LispDataType dtKeyCode,
         LispDataType dtValueCode)
      {
         Assert.IsNotNull(items, nameof(items));
         var result = new ResultBuffer();
         int cnt = 0;
         short keyCode = (short)dtKeyCode;
         short valueCode = (short)dtValueCode;
         foreach(var pair in items)
         {
            cnt = 0;
            result.Add(lispListBegin);
            result.Add(new TypedValue(keyCode, pair.Key));
            if(pair.Value != null)
            {
               foreach(TValue value in pair.Value)
               {
                  result.Add(new TypedValue(valueCode, value));
                  ++cnt;
               }
            }
            if(cnt == 1)
               result.Add(lispDotEnd);
            else
               result.Add(lispListEnd);
         }
         return result;
      }

      // public static Dictionary<TKey, IEnumerable<TValue>> ToDictionary


      /// <summary>
      /// Converts a ResultBuffer to a Dictionary<TKey, ICollection<TValue>>.
      /// 
      /// Constraints:
      /// 
      /// TKey and TValue must both be simple value types (or strings)
      /// that can be assigned to a TypedValue's Value property and be 
      /// written to an XRecord. Reference types other than string are 
      /// not supported. Arrays are not supported.
      /// </summary>
      /// <typeparam name="TKey">The type of the Dictionary's Keys</typeparam>
      /// <typeparam name="TValue">The type of the List stored in each
      /// dictionary entry's ICollection<TValue> value.</typeparam>
      /// <param name="resbuf">The result buffer to convert</param>
      /// <param name="factory">A function that returns an ICollection<TValue>
      /// that will hold each Dictionary entry's List. If not provided, the
      /// default container is a List<TValue></param>
      /// <returns>A dictionary holding the contents of the ResultBuffer</returns>
      /// <exception cref="InvalidOperationException"></exception>

      public static Dictionary<TKey, IEnumerable<TValue>> 
      ToListDictionary<TKey, TValue>(this ResultBuffer resbuf, 
         Func<ICollection<TValue>> factory = null)
      {
         Assert.IsNotNull(resbuf, nameof(resbuf));
         if(factory == null)
            factory = () => new List<TValue>();
         var result = new Dictionary<TKey, IEnumerable<TValue>>();
         var e = resbuf.GetEnumerator();
         while(e.MoveNext())
         {
            var key = e.Current.Value;
            if(!e.MoveNext())
               throw new InvalidOperationException("Count mismatch");
            TypedValue cur = e.Current;
            if(!cur.IsListBegin())
               throw new InvalidOperationException(
                  $"Malformed list: expecting List Begin: {cur.TypeCode}, {cur.Value}");
            ICollection<TValue> list = factory();
            while(true)
            {
               if(!e.MoveNext())
                  throw new InvalidOperationException(
                     $"Malformed list: expecting List End");
               cur = e.Current;
               if(cur.IsListEnd())
                  break;
               list.Add((TValue) cur.Value);
            }
            result.Add((TKey)key, list);
         }
         return result;
      }

      public static Dictionary<TKey, IEnumerable<TValue>>
      ToLispDictionary<TKey, TValue>(this ResultBuffer resbuf,
         Func<ICollection<TValue>> factory = null)
      {
         Assert.IsNotNull(resbuf, nameof(resbuf));
         if(factory == null)
            factory = () => new List<TValue>();
         var result = new Dictionary<TKey, IEnumerable<TValue>>();
         var e = resbuf.GetEnumerator();
         int i = 0;
         while(e.MoveNext())
         {
            // ($"[{i++}]: " + e.Current.Format()).WriteLine();
            if(!e.Current.IsListBegin())
               throw new InvalidOperationException("Error Expecting List Begin");
            if(!e.MoveNext())
               throw new InvalidOperationException("Malformed List");
            var key = e.Current.Value;
            if(! (key is TKey))
               throw new InvalidOperationException($"Invalid Key type: {key.GetType().Name}");
            // ($"[{i++}]: " + e.Current.Format()).WriteLine();
            ICollection<TValue> list = factory();
            while(e.MoveNext())
            {
               // ($"[{i++}]: " + e.Current.Format()).WriteLine();
               TypedValue cur = e.Current;
               if(cur.IsListEnd())
                  break;
               list.Add((TValue)cur.Value);
            }
            if(!e.Current.IsListEnd())
               throw new InvalidOperationException("Malformed list: Expecting List End");
            result.Add((TKey)key, list);
         }
         return result;
      }



      /// <summary>
      /// Converts a sequence of TypedValues that use LISP list 
      /// semantics (LispDataType.ListBegin/End) to an equivalent
      /// DXF-compatible sequence using (DxfCode.ControlString "{")
      /// and (DxfCode.ConstrolString "}") as list delimiters.
      /// 
      /// Storing a ResultBuffer in an Xrecord requires this
      /// conversion.
      /// </summary>

      public static IEnumerable<TypedValue> ConvertToDxfList(this IEnumerable<TypedValue> items)
      {
         foreach(TypedValue item in items)
         {
            switch(item.TypeCode)
            {
               case 5016:
                  yield return dxfListBegin;
                  break;
               case 5017:
                  yield return dxfListEnd;
                  break;
               default:
                  yield return item;
                  break;
            }
         }
      }

      /// <summary>
      /// Performs the inverse conversion done by ConvertToDxfList()
      /// </summary>

      public static IEnumerable<TypedValue> ConvertToLispList(this IEnumerable<TypedValue> items)
      {
         foreach(TypedValue item in items)
         {
            if(item.IsEqualTo(dxfListBegin))
               yield return lispListBegin;
            else if(item.IsEqualTo(dxfListEnd))
               yield return lispListEnd;
            else
               yield return item;
         }
      }


      static readonly TypedValue dxfListBegin = 
         new TypedValue((short) DxfCode.ControlString, "{");
      static readonly TypedValue dxfListEnd = 
         new TypedValue((short) DxfCode.ControlString, "}");
      static readonly TypedValue lispListBegin = 
         new TypedValue((short) LispDataType.ListBegin);
      static readonly TypedValue lispListEnd =
         new TypedValue((short) LispDataType.ListEnd);
      static readonly TypedValue lispDotEnd =
         new TypedValue((short)LispDataType.DottedPair);


      public static bool IsListBegin(this TypedValue value) =>
         value.TypeCode == 5016 || value.IsEqualTo(dxfListBegin);

      public static bool IsListEnd(this TypedValue value)
      {
         short code = value.TypeCode;
         return code == 5017 || code == 5018 || value.IsEqualTo(dxfListEnd);
      } 

      /// A Fix for TypedValue.Equals()

      static TypedValueComparer valueComparer = TypedValueComparer.Instance;

      /// <summary>
      /// Performs a stable comparison of the TypedValues of 
      /// two TypedValues. 
      /// </summary>
      /// <param name="left"></param>
      /// <param name="right"></param>
      /// <returns></returns>
      
      public static bool IsEqualTo(this TypedValue left, TypedValue right)
      {
         return valueComparer.Equals(left, right);
      }

      /// Diagnostics support

      const short LispDataTypeMin = (short)LispDataType.None - 1;

      public static string Format(this ResultBuffer rb, string format = "{0} = {1}")
      {
         return rb.Cast<TypedValue>().Format(format);
      }

      public static string Format(this TypedValue typedValue, string format = "{0} = {1}")
      {
         format = format ?? "{0} = {1}";
         object val = typedValue.Value;
         short code = typedValue.TypeCode;
         if(val == null)
         {
            format = format.Remove(" = ");
            val = "";
         }
         if(code > LispDataTypeMin)
            return string.Format(format, (LispDataType)code, val);
         else
            return string.Format(format, (DxfCode)code, val);
      }

      static readonly string spaces = new string((char)32, 80);

      static string Pad(int level, int width = 2)
      {
         return spaces.Substring(0, width * Math.Max(level, 0));
      }

      public static string Format(this IEnumerable<TypedValue> items, string format = "{0} = {1}")
      {
         if(items == null)
            return "(null)\n";
         StringBuilder sb = new StringBuilder();
         int depth = 1;
         foreach(TypedValue tv in items)
         {
            TypedValue value = tv;
            bool isLispType = tv.TypeCode > LispDataTypeMin;
            bool flag = tv.IsListBegin();
            if(tv.IsListEnd())
            {
               --depth;
               if(isLispType)
                  value = new TypedValue(tv.TypeCode, depth);
            }
            else if(flag)
            {
               if(isLispType)
                  value = new TypedValue(tv.TypeCode, depth);
            }
            sb.Append(value.Format($"\n{Pad(depth-1)}{format}"));
            if(flag)
               ++depth;
         }
         sb.AppendLine("\n");
         return sb.ToString();
      }
   }

   public interface ITypedValueList : IReadOnlyList<object>
   {
      public new object this[int index] { get; set; }
      public short GetTypeCodeAt(int index);
      public void SetTypeCodeAt(int index, short value);
   }

   class ValueList : ITypedValueList
   {
      private readonly IList<TypedValue> source;

      internal ValueList(IList<TypedValue> source)
      {
         if(source == null)
            throw new ArgumentNullException(nameof(source));
         this.source = source;
      }

      object IReadOnlyList<object>.this[int index]
      {
         get { return source[index].Value; }
      }

      public object this[int index]
      {
         get
         {
            return source[index].Value;
         }
         set
         {
            source[index] = new TypedValue(source[index].TypeCode, value);
         }
      }

      public int Count => source.Count;

      public short GetTypeCodeAt(int index)
      {
         return source[index].TypeCode;
      }

      public void SetTypeCodeAt(int index, short value)
      {
         source[index] = new TypedValue(value, source[index].Value);
      }

      /// <summary>
      /// Enumerates the objects, rather than the TypedValues
      /// </summary>

      public IEnumerator<object> GetEnumerator()
      {
         return source.Select(tv => tv.Value).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }
   }


}




