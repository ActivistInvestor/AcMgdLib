/// DwgDataList.cs  -  Tony Tanzillo
/// Distributed under the terms of the
/// MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AcRx = Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Ac2025Project.Test;
using System.Text;
using Autodesk.AutoCAD.Runtime.NativeInterop;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A custom DwgFiler that collects the data that a 
   /// DBObject writes to a DWG file into a List.
   /// 
   /// Use the included DBObject.DwgOut() extension method 
   /// to read the DWG data of a DBObject:
   /// 
   ///    DBObject someDBObject = (assign to an open DBObject)
   ///    IList<DwgDataItem> data = someDBObject.DwgOut();
   ///    
   /// The resulting list will contain all data that is
   /// returned by the acdbEntGet() function, and possibly
   /// additional data as well, and hence, can be used in
   /// lieu of P/Invoking acdbEntGet(). Since DWG files are
   /// not documented, interpretation of the resulting data
   /// is up to the user of this class.
   /// 
   /// Note that while DwgDataList implements both read and
   /// write operations, read operations (e.g., DwgIn) have
   /// never been needed or used, are untested, and probably
   /// will not work as implemented.
   /// 
   /// Revisions:
   /// 
   /// 1. DwgDataList is a read-only collection. To modify
   ///    its contents, use the Data property to get a copy
   ///    and modify the copy.
   ///    
   /// 2. Use of TypedValue to represent elements is flawed,
   ///    because the translation from DwgDataType to DxfCode
   ///    is incoherent and not straight-forword (e.g., how
   ///    the translation is done depends on the object type
   ///    and what the data represents). Additionally, there
   ///    are no DxfCodes representing some data types that
   ///    can appear in DWG output.
   ///    
   /// 3. DwgDataList is not reusable on multiple objects. 
   ///    The constructor was made non-public, and creating 
   ///    an instance must be done via a call to the static
   ///    DwgOut() method.
   ///    
   /// 4. The IncludeBinaryData property can be used to avoid 
   ///    collecting binary data in cases where the caller is 
   ///    not interested in, or doesn't require it (XData is
   ///    rendered as binary data, but typically isn't needed
   ///    in that form, since it is accessable via the DBObject 
   ///    XData property without requiring interpretation). If 
   ///    set to false (default is true), no binary data is 
   ///    collected or included in the instance.
   ///    
   /// </summary>


   public class DwgDataList : DwgFiler, IReadOnlyCollection<DwgDataItem>
   {
      AcRx.ErrorStatus status = AcRx.ErrorStatus.OK;
      FilerType filerType = FilerType.CopyFiler;
      List<DwgDataItem> data = new List<DwgDataItem>();
      HashSet<DwgDataType> includedTypes = new HashSet<DwgDataType>();
      int position = 0;

      /// <summary>
      /// Primary entry point. 
      /// 
      /// This method must be used to obtain the DWG data
      /// for a DBObject. 
      /// 
      /// The code has not been tested with FilerTypes other
      /// than FilerType.CopyFiler, and probably should not
      /// be specified (the default is FilerType.CopyFiler).
      /// </summary>

      public static IList<DwgDataItem> DwgOut(DBObject dbObject,
         bool includeBinaryData = true,
         FilerType filerType = FilerType.CopyFiler)
      {
         if(dbObject == null)
            throw new ArgumentNullException(nameof(dbObject));
         using(DwgDataList list = new DwgDataList(filerType, includeBinaryData))
         {
            dbObject.DwgOut(list);
            return list.Data;
         }
      }

      DwgDataList(FilerType filerType = FilerType.CopyFiler, bool includeBinaryData = true)
      {
         this.filerType = filerType;
         this.IncludeBinaryData = includeBinaryData;
      }

      public void Rewind()
      {
         position = 0;
      }

      //public DwgDataItem? Peek()
      //{
      //   if(data.Count > 0 && position < data.Count - 1)
      //   {
      //      return data[position];
      //   }
      //   return null;
      //}

      public IList<DwgDataItem> Data
      {
         get
         {
            return data.ToList();
         }
      }

      /// <summary>
      /// If set to true (the default), binary data is
      /// collected. If set to false, binary data is not 
      /// collected.
      /// </summary>

      public bool IncludeBinaryData { get; set; }

      public override AcRx.ErrorStatus FilerStatus
      {
         get
         {
            return status;
         }
         set
         {
            status = value;
         }
      }

      public override FilerType FilerType
      {
         get
         {
            return this.filerType;
         }
      }

      public override long Position
      {
         get
         {
            return position;
         }
      }

      public int Count
      {
         get
         {
            return data.Count;
         }
      }

      public bool IsReadOnly
      {
         get
         {
            return true;
         }
      }

      public bool IsEndOfData
      {
         get
         {
            return position > this.data.Count - 1;
         }
      }

      public DwgDataItem this[int index]
      {
         get
         {
            return data[CheckIndex(index)];
         }
      }

      static T TypeMismatch<T>(int index, Type type)
      {
         string name = type?.Name ?? "(null)";
         throw new InvalidCastException(
            $"Type mismatch at {index}: Found {name}, expecting {typeof(T).Name})");
      }

      public override IntPtr ReadAddress()
      {
         return Read<IntPtr>();
      }

      public override byte[] ReadBinaryChunk()
      {
         byte[] array = Read<byte[]>();
         if(array == null)
            return new byte[0];
         byte[] result = new byte[array.Length];
         array.CopyTo(result, 0);
         return result;
      }

      protected virtual T Read<T>()
      {
         if(IsEndOfData)
            throw new AcRx.Exception(AcRx.ErrorStatus.EndOfObject);
         if(FilerStatus != AcRx.ErrorStatus.OK)
            throw new AcRx.Exception(FilerStatus);
         object value = data[position].Value;
         if(!(value is T))
            TypeMismatch<T>(position, value?.GetType());
         ++position;
         if(IsEndOfData)
            FilerStatus = AcRx.ErrorStatus.EndOfObject;
         return (T) value;
      }

      public override bool ReadBoolean()
      {
         return Read<bool>();
      }

      public override byte ReadByte()
      {
         return Read<Byte>();
      }

      public override void ReadBytes(byte[] value)
      {
         byte[] array = Read<byte[]>();
         Array.Copy(array, value, value.Length);
      }

      public override double ReadDouble()
      {
         return Read<double>();
      }

      public override Handle ReadHandle()
      {
         return Read<Handle>();
      }

      public override ObjectId ReadHardOwnershipId()
      {
         return Read<ObjectId>();
      }

      public override ObjectId ReadHardPointerId()
      {
         return Read<ObjectId>();
      }

      public override short ReadInt16()
      {
         return Read<short>();
      }

      public override int ReadInt32()
      {
         return Read<int>();
      }

      public override long ReadInt64()
      {
         return Read<long>();
      }

      public override Point2d ReadPoint2d()
      {
         return Read<Point2d>();
      }

      public override Point3d ReadPoint3d()
      {
         return Read<Point3d>();
      }

      public override Scale3d ReadScale3d()
      {
         return Read<Scale3d>();
      }

      public override ObjectId ReadSoftOwnershipId()
      {
         return Read<ObjectId>();
      }

      public override ObjectId ReadSoftPointerId()
      {
         return Read<ObjectId>();
      }

      public override string ReadString()
      {
         return Read<string>();
      }

      public override ushort ReadUInt16()
      {
         return Read<ushort>();
      }

      public override uint ReadUInt32()
      {
         return Read<uint>();
      }

      public override ulong ReadUInt64()
      {
         return Read<ulong>();
      }

      public override Vector2d ReadVector2d()
      {
         return Read<Vector2d>();
      }

      public override Vector3d ReadVector3d()
      {
         return Read<Vector3d>();
      }

      public override void ResetFilerStatus()
      {
         status = AcRx.ErrorStatus.OK;
      }

      public override void Seek(long offset, int method)
      {
         throw new NotSupportedException();  // TODO
      }

      public override void WriteAddress(IntPtr value)
      {
         Add(DwgDataType.Ptr, value);
      }

      public override void WriteBinaryChunk(byte[] chunk)
      {
         if(IncludeBinaryData)
            WriteBytes(chunk, DwgDataType.BChunk);
      }

      public override void WriteBoolean(bool value)
      {
         this.Add(DwgDataType.Bool, value);
      }

      public override void WriteByte(byte value)
      {
         this.Add(DwgDataType.Byte, value);
      }

      public override void WriteBytes(byte[] chunk)
      {
         if(IncludeBinaryData)
            WriteBytes(chunk, DwgDataType.ByteArray);
      }

      void WriteBytes(byte[] chunk, DwgDataType dataType)
      {
         if(chunk == null)
            throw new ArgumentNullException(nameof(chunk));
         byte[] data = new byte[chunk.Length];
         chunk.CopyTo(data, 0);
         this.Add(dataType, data);
      }

      public override void WriteDouble(double value)
      {
         this.Add(DwgDataType.Real, value);
      }

      public override void WriteHandle(Handle handle)
      {
         this.Add(DwgDataType.Handle, handle);
      }

      public override void WriteHardOwnershipId(ObjectId value)
      {
         this.Add(DwgDataType.HardOwnershipId, value);
      }

      public override void WriteHardPointerId(ObjectId value)
      {
         this.Add(DwgDataType.HardPointerId, value);
      }

      public override void WriteInt16(short value)
      {
         this.Add(DwgDataType.Int16, value);
      }

      public override void WriteInt32(int value)
      {
         this.Add(DwgDataType.Int32, value);
      }

      public override void WriteInt64(long value)
      {
         this.Add(DwgDataType.Int64, value);
      }

      public override void WritePoint2d(Point2d value)
      {
         this.Add(DwgDataType.Point2d, value);
      }

      public override void WritePoint3d(Point3d value)
      {
         this.Add(DwgDataType.Point3d, value);
      }

      public override void WriteScale3d(Scale3d value)
      {
         this.Add(DwgDataType.Scale3d, value);
      }

      public override void WriteSoftOwnershipId(ObjectId value)
      {
         this.Add(DwgDataType.SoftOwnershipId, value);
      }

      public override void WriteSoftPointerId(ObjectId value)
      {
         this.Add(DwgDataType.SoftPointerId, value);
      }

      public override void WriteString(string value)
      {
         this.Add(DwgDataType.Text, value);
      }

      public override void WriteUInt16(ushort value)
      {
         this.Add(DwgDataType.UInt16, value);
      }

      public override void WriteUInt32(uint value)
      {
         this.Add(DwgDataType.UInt32, value);
      }

      public override void WriteUInt64(ulong value)
      {
         this.Add(DwgDataType.UInt64, value);
      }

      public override void WriteVector2d(Vector2d value)
      {
         this.Add(DwgDataType.Vector2d, value);
      }

      public override void WriteVector3d(Vector3d value)
      {
         this.Add(DwgDataType.Vector3d, value);
      }

      public List<object> Values
      {
         get
         {
            return data.ConvertAll(d => d.Value);
         }
      }

      /// <summary>
      /// Returns a subset of elements having the specified DwgDataType
      /// </summary>

      public IEnumerable<DwgDataItem> OfType(DwgDataType dataType)
      {
         return data.Where(item => item.DataType == dataType);
      }

      /// <summary>
      /// Returns the Nth occurence of an item having the given 
      /// DataType, or null if there are fewer occurences of an
      /// item having the given DataType 
      /// 
      /// For example, to get the 3rd ToPoint3d in the list:
      /// 
      ///   dwgDataList.OfTypeAt(DwgDataType.ToPoint3d, 2);
      ///   
      /// </summary>
      /// <param name="type"></param>
      /// <param name="index"></param>
      /// <returns></returns>
      
      public DwgDataItem? OfTypeAt(DwgDataType type, int index)
      {
         int i = -1;
         foreach(var item in data)
         {
            if(item.DataType == type)
               ++i;
            if(i == index)
               return item;
         }
         return null;
      }

      /// <summary>
      /// The distinct set of DwgDataTypes contained in the instance.
      /// </summary>

      public ICollection<DwgDataType> IncludedTypes => includedTypes;

      protected int CheckIndex(int index)
      {
         if(index < 0 || index > data.Count - 1)
            throw new IndexOutOfRangeException($"index = {index} count = {data.Count}");
         return index;
      }

      public object GetValueAt(int index)
      {
         return data[CheckIndex(index)].Value;
      }

      public DwgDataType GetTypeAt(int index)
      {
         return data[CheckIndex(index)].DataType;
      }

      public bool ContainsType(DwgDataType type)
      {
         return includedTypes.Contains(type);
      }

      public int IndexOfType(DwgDataType type)
      {
         return IndexOf(d => d.DataType == type);
      }

      public int IndexOfValue(object value)
      {
         return IndexOf(d => object.Equals(d.Value, value));
      }

      public int IndexOf(DwgDataItem item)
      {
         return data.IndexOf(item);
      }

      public int IndexOf(Func<DwgDataItem, bool> predicate)
      {
         for(int i = 0; i < data.Count; i++)
         {
            if(predicate(data[i]))
               return i;
         }
         return -1;
      }

      /// <summary>
      /// Returns the Nth occurence of the specified DataType,
      /// where the first occurence has a positional index of 1.
      /// 
      /// NOTE: The index is 1-based, not 0-based. 
      /// 
      /// For example, to get the 3rd ToPoint3d value that was 
      /// filed out by the DBObject, you would use:
      /// <code>
      /// 
      ///    ToPoint3d thirdPoint;
      ///    if(myDwgDataList.TryGetValueAt(DwgDataType.ToPoint3d, 3, out thirdPoint))
      ///    {
      ///        Debug.WriteLine("\nFound 3rd point: ", thirdPoint);
      ///    }
      /// 
      /// </code>
      /// Note that this method should only be used when you
      /// need a specific type and you know the occurence of
      /// the type. For example, the Nth vertex of a Polyline,
      /// etc. To get all occurences of a given type, use the
      /// OfType() method instead.
      /// </summary>
      /// <param name="dataType"></param>
      /// <param name="index"></param>
      /// <param name="value"></param>
      /// <returns></returns>

      public bool TryGetValueAt(DwgDataType dataType, int index, out object value)
      {
         int cnt = 0;
         for(int i = 0; i < data.Count; i++)
         {
            DwgDataItem item = data[i];
            if(item.DataType == dataType && ++cnt == index)
            {
               value = item.Value;
               return true;
            }
         }
         value = null;
         return false;
      }

      /// <summary>
      /// Derived types can override this to selectively
      /// collect only the data or data types they are
      /// interested in.
      /// </summary>
      /// <param name="type"></param>
      /// <param name="value"></param>
      /// <exception cref="AcRx.Exception"></exception>

      protected virtual void Add(DwgDataType type, object value)
      {
         if(FilerStatus != AcRx.ErrorStatus.OK)
            throw new AcRx.Exception(FilerStatus);
         includedTypes.Add(type);
         data.Add(new DwgDataItem(type, value));
      }

      public bool Contains(DwgDataItem item)
      {
         return data.Contains(item);
      }

      public void CopyTo(DwgDataItem[] array, int arrayIndex)
      {
         data.CopyTo(array, arrayIndex);
      }

      public IEnumerator<DwgDataItem> GetEnumerator()
      {
         return data.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }
   }

   public struct DwgDataItem : IEquatable<DwgDataItem>
   {
      public DwgDataItem(DwgDataType type, object value)
      {
         if(value == null)
            throw new ArgumentNullException("value");
         this.DataType = type;
         this.Value = value;
      }

      public DwgDataType DataType
      {
         get;
         private set;
      }

      /// <summary>
      /// Holds the 1-based index of this item for the 
      /// given DataType. Note: The value represents a
      /// 1-based index rather than a 0-based index.
      /// 
      /// Hence, to get the 3rd double that was filed
      /// out, you would use a value of 3 with the
      /// TryGetValueAt() method of the DwgDataList.
      /// 
      /// This value represents the Nth occurence of a
      /// value with the DataType in the output list.
      /// 
      /// E.g., the first ToPoint3d written out will have a 
      /// TypeIndex of 0. The second ToPoint3d written out 
      /// will have a TypeIndex of 1, and so on. 
      /// 
      /// This value is distinct for each DataType.
      /// </summary>

      public object Value
      {
         get; private set;
      }

      public bool Equals(DwgDataItem other)
      {
         return this.DataType == other.DataType && object.Equals(this.Value, other.Value);
      }

      public override bool Equals(object obj)
      {
         if(obj is DwgDataItem)
            return Equals((DwgDataItem)obj);
         else
            return false;
      }

      public override int GetHashCode()
      {
         return HashCode.Combine(this.DataType.GetHashCode(), this.Value.GetHashCode());
      }

      public override string ToString()
      {
         return string.Format("{0}: {1}", this.DataType, Value.SafeToString());
      }
   }

   /* Native definition
      
      enum AcDb::DwgDataType {
        kDwgNull = 0,
        kDwgReal = 1,
        kDwgInt32 = 2,
        kDwgInt16 = 3,
        kDwgInt8 = 4,
        kDwgText = 5,
        kDwgBChunk = 6,
        kDwgHandle = 7,
        kDwgHardOwnershipId = 8,
        kDwgSoftOwnershipId = 9,
        kDwgHardPointerId = 10,
        kDwgSoftPointerId = 11,
        kDwg3Real = 12,
        kDwgInt64 = 13,
        kDwgNotRecognized = 19
      };    
   */

   public enum DwgDataType
   {
      Null = 0,
      Real = 1,
      Int32 = 2,
      Int16 = 3,
      Int8 = 4,
      Text = 5,
      BChunk = 6,
      Handle = 7,
      HardOwnershipId = 8,
      SoftOwnershipId = 9,
      HardPointerId = 10,
      SoftPointerId = 11,
      Real3 = 12,
      Int64 = 13,
      NotRecognized = 19,

      /// <summary>
      /// Application-specific, not all source are supported.
      /// </summary>
      Point3d = 20,
      Point2d = 21,
      ByteArray = 22,
      Byte = 23,
      Bool = 24,
      Ptr = 25,
      Scale3d = 26,
      Vector3d = 27,
      Vector2d = 28,
      UInt16 = 29,
      UInt32 = 30,
      UInt64 = 31
   };

   public static class DwgDataListExtensions
   {
      /// <summary>
      /// Extension method targeting DBObject that returns a
      /// List<DwgDataItem> containing the data the given 
      /// DBObject persists in a DWG file.
      /// </summary>
      /// <param name="obj">The DBObject whose data is to be obtained</param>
      /// <param name="filerType">The FilerType to use (default: CopyFiler)</param>
      /// <param name="IncludeBinaryData">true to include binary data in the
      /// result, or false to exclude binary data output</param>
      /// <returns>A list of DwgDataItem objects, each describing
      /// an element of data that is filed out to a .DWG file</returns>

      public static IList<DwgDataItem> DwgOut(this DBObject obj,
         bool IncludeBinaryData = false,
         FilerType filerType = FilerType.CopyFiler)
      {
         if(obj == null)
            throw new ArgumentNullException(nameof(obj));
         return DwgDataList.DwgOut(obj, IncludeBinaryData, filerType);
      }

      /// <summary>
      /// A synonym for DwgOut() included mainly for the purpose
      /// of maintaining-compatibility with older releases of this
      /// class.
      /// </summary>
      /// <param name="obj"></param>

      public static IList<DwgDataItem> ToList(this DBObject obj,
         bool IncludeBinaryData = false,
         FilerType filerType = FilerType.CopyFiler)
      {
         return DwgOut(obj);
      }

      /// <summary>
      /// Example: An extension method targeting DBObject,
      /// that dumps the result of DwgOut() to the console:
      /// </summary>

      public static void DwgDump(this DBObject obj)
      {
         var data = obj.DwgOut();
         Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
         int i = 0;
         foreach(var item in data)
            ed.WriteMessage("\n[{0}] {1}", i++, item.ToString());
      }

      public static string DxfDump(this DBObject obj)
      {
         StringBuilder sb = new StringBuilder();
         int i = 0;
         var rb = obj.ObjectId.EntGet();
         if(rb != null)
         {
            foreach(var tv in rb)
               sb.AppendLine($"[{i} {(DxfCode)tv.TypeCode}]: {tv.Value}");
         }
         return sb.ToString();
      }

      public static string Dump(this TypedValue tv)
      {
         return $"{(DxfCode) tv.TypeCode} = {tv.Value}";
      }

      public static Type GetRuntimeType(this DwgDataType dataType, bool nullcheck = false)
      {
         if(typeMap.ContainsKey(dataType))
            return typeMap[dataType];
         else if(nullcheck)
            throw new KeyNotFoundException($"Type for {dataType} not found.");
         return null;
      }

      /// <summary>
      /// May fail on some UInt types
      /// </summary>

      public static void CheckType(this DwgDataItem item, int index = -1)
      {
         string idx = index > -1 ? $"[{index}] " : "";
         Type type = item.DataType.GetRuntimeType(true);
         Type valueType = item.Value?.GetType();
         if(valueType == null)
            throw new InvalidOperationException($"{idx} Result is null");
         if(type != valueType)
            throw new InvalidOperationException(
               $"{idx}Type mismatch DwgDataType: {type.Name} Result: {valueType.Name}");
      }


      /// <summary>
      /// Returns the managed Type that represents a value
      /// corresponding to the given DwgDataType.
      /// </summary>

      public static Type GetCLRType(this DwgDataType dataType)
      {
         return typeMap[dataType];
      }

      static Dictionary<DwgDataType, Type> typeMap = new Dictionary<DwgDataType, System.Type>();

      static DwgDataListExtensions()
      {
         typeMap[DwgDataType.Text] = typeof(string);
         typeMap[DwgDataType.Bool] = typeof(bool);
         typeMap[DwgDataType.Int8] = typeof(char);
         typeMap[DwgDataType.Int16] = typeof(short);
         typeMap[DwgDataType.Int32] = typeof(int);
         typeMap[DwgDataType.Int64] = typeof(long);
         typeMap[DwgDataType.Real3] = typeof(Point3d);
         typeMap[DwgDataType.Byte] = typeof(byte);
         typeMap[DwgDataType.Handle] = typeof(Handle); // string in dxf
         typeMap[DwgDataType.HardOwnershipId] = typeof(ObjectId);
         typeMap[DwgDataType.SoftOwnershipId] = typeof(ObjectId);
         typeMap[DwgDataType.HardPointerId] = typeof(ObjectId);
         typeMap[DwgDataType.SoftPointerId] = typeof(ObjectId);
         typeMap[DwgDataType.UInt32] = typeof(UInt32);
         typeMap[DwgDataType.UInt16] = typeof(UInt16);
         typeMap[DwgDataType.UInt64] = typeof(UInt64);
         typeMap[DwgDataType.Vector2d] = typeof(Vector2d);
         typeMap[DwgDataType.Vector3d] = typeof(Vector3d);
         typeMap[DwgDataType.Scale3d] = typeof(Scale3d);
         typeMap[DwgDataType.Real] = typeof(double);
         typeMap[DwgDataType.Point3d] = typeof(Point3d);
         typeMap[DwgDataType.Point2d] = typeof(Point2d);
         typeMap[DwgDataType.BChunk] = typeof(byte[]);
         typeMap[DwgDataType.Byte] = typeof(byte);
      }


   }

}
