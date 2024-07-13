/// CollectionHelpers.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   /// <summary>
   /// Helper classes that allow a consumer to obtain 
   /// the ObjectId of a SymbolTable from a specified
   /// Database, given the type of the contained/owned 
   /// SymbolTableRecord expressed as a generic argument.
   /// 
   /// For example, to get the ObjectId of the BlockTable
   /// in the current database:
   /// 
   ///   Database db = HostApplicationServices.WorkingDatabase;
   ///   
   ///   var blockTableId = db.GetSymbolTableId<BlockTableRecord>();
   ///   
   /// To get the ObjectId of the LayerTable in the same
   /// database:
   /// 
   ///   var layerTableId = db.GetSymbolTableId<LayerTableRecord>();
   ///   
   /// If you're wondering why all of this is needed when the 
   /// Database class already exposes dedicated methods that 
   /// return the ObjectIds of all symbol tables, it is because 
   /// this class is used by included extension methods that 
   /// generically deal with SymbolTableRecords, and as such, 
   /// cannot directly call the methods of the Database class 
   /// that return specific symbol table ids without using a
   /// switch statement or a dictionary.
   /// 
   /// Instead, this class provides a generic way to obtain 
   /// the ObjectId of a SymbolTable given the type of the
   /// records it contains with minimal overhead.
   /// </summary>
   /// <typeparam name="T">The type of the SymbolTableRecords
   /// contained in the requested SymbolTable</typeparam>

   public static class SymbolTable<T> where T : SymbolTableRecord
   {
      /// <summary>
      /// Gets the ObjectId of the owning SymbolTable for
      /// the SymbolTableRecord-based generic argument type,
      /// in the given Database.
      /// 
      /// The generic argument must not be SymbolTableRecord,
      /// <em>it must be a concrete derived type.</em>
      /// </summary>

      internal static readonly Func<Database, ObjectId> GetObjectId =
         SymbolTableAccessors.GetAccessor<T>();

      /// <summary>
      /// Opens and returns the SymbolTable that contains
      /// SymbolTableRecords of the given generic argument type.
      /// </summary>
      /// <param name="db">The Database to access</param>
      /// <param name="tr">The Transaction to use to open the result</param>
      /// <param name="mode">The OpenMode to use to open the result</param>
      /// <returns>The requested SymbolTable</returns>

      public static SymbolTable GetSymbolTable(Database db, Transaction tr, OpenMode mode = OpenMode.ForRead)
      {
         db.CheckTransaction(tr);
         return GetObjectId(db).GetObject<SymbolTable>(tr, mode);
      }
   }

   internal static class SymbolTableAccessors
   {
      readonly static Dictionary<Type, Func<Database, ObjectId>> map
         = new Dictionary<Type, Func<Database, ObjectId>>();

      public static Func<Database, ObjectId> GetAccessor<T>() where T : SymbolTableRecord
      {
         if(!map.ContainsKey(typeof(T)))
            throw new ArgumentException($"Accessor not defined for type {typeof(T).Name}");
         return map[typeof(T)];
      }

      public static Func<Database, ObjectId> GetAccessor(Type type)
      {
         if(map.TryGetValue(type, out Func<Database, ObjectId> func))
            return func;
         return null;
      }

      static SymbolTableAccessors()
      {
         map[typeof(BlockTableRecord)] = db => db.BlockTableId;
         map[typeof(LayerTableRecord)] = db => db.LayerTableId;
         map[typeof(LinetypeTableRecord)] = db => db.LinetypeTableId;
         map[typeof(ViewportTableRecord)] = db => db.ViewportTableId;
         map[typeof(ViewTableRecord)] = db => db.ViewTableId;
         map[typeof(DimStyleTableRecord)] = db => db.DimStyleTableId;
         map[typeof(RegAppTableRecord)] = db => db.RegAppTableId;
         map[typeof(TextStyleTableRecord)] = db => db.TextStyleTableId;
         map[typeof(UcsTableRecord)] = db => db.UcsTableId;
         map[typeof(SymbolTableRecord)] = db =>
            throw new InvalidOperationException(
               $"requires a concrete type derived from {nameof(SymbolTableRecord)}");
      }
   }

   public static partial class DatabaseExtensions
   {
      /// <summary>
      /// Returns the ObjectId of a symbol table from the
      /// given Database, given the type of the contained
      /// SymbolTableRecord expressed as a generic argument.
      /// </summary>
      /// <typeparam name="T">The type of the SymbolTableRecord
      /// owned/contained by the requested SymbolTable</typeparam>
      /// <param name="database">The Database to get the result from</param>
      /// <returns>The ObjectId of the SymbolTable that contains/owns
      /// instances of the generic argument type.
      /// </returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static ObjectId GetSymbolTableId<T>(this Database database)
         where T : SymbolTableRecord
      {
         Assert.IsNotNullOrDisposed(database, nameof(database));
         return SymbolTable<T>.GetObjectId(database);
      }

      /// <summary>
      /// Opens and returns a SymbolTable from the given Database, 
      /// given the type of the owned/contained SymbolTableRecord 
      /// expressed as a generic argument.
      /// </summary>
      /// <typeparam name="T">The type of the SymbolTableRecord
      /// owned/contained by the requested SymbolTable</typeparam>
      /// <param name="database">The Database to get the result from</param>
      /// <param name="trans">The Transaction to use to open the result.</param>
      /// <param name="mode">The OpenMode to open the result with.</param>
      /// <returns>The SymbolTable that contains/owns instances of the 
      /// generic argument type.
      /// </returns>
      /// <exception cref="ArgumentNullException"></exception>

      public static SymbolTable GetSymbolTable<T>(this Database database,
            Transaction trans,
            OpenMode mode = OpenMode.ForRead) where T : SymbolTableRecord
      {
         if(typeof(T).IsAbstract)
            throw new ArgumentException("Requires a non-abstract type derived from SymbolTableRecord");
         database.CheckTransaction(trans);
         return GetSymbolTableId<T>(database).GetObject<SymbolTable>(trans, mode);
      }
   }

   /// <summary>
   /// This class follows the same pattern used by
   /// SymbolTable<T> to provide the ObjectIds of
   /// various standard or "built-in" dictionaries, 
   /// given the type of the value contained in the 
   /// dictionary.
   /// </summary>
   /// <typeparam name="T">The type of the values
   /// stored in the DBDictionary</typeparam>

   public static class DBDictionary<T> where T : DBObject
   {
      /// <summary>
      /// Gets the ObjectId of the owning DBDictionary that
      /// contains instances of the generic argument type,
      /// in the given Database.
      /// 
      /// The generic argument must be one of the types that
      /// are assigned by the DBDictionaryAccessors class below.
      /// </summary>

      public static readonly Func<Database, ObjectId> GetObjectId =
         DBDictionaryAccessors.GetAccessor<T>();

      public static DBDictionary GetDictionary(Database db, Transaction tr, OpenMode mode = OpenMode.ForRead)
      {
         return GetObjectId(db).GetObject<DBDictionary>(tr, mode);
      }

      /// Non-DBObject or ambiguous/abstract types not usable via this method:
      /// DBDictionary<DBObject>.GetObjectId = db => db.ColorDictionaryId;
      /// DBDictionary<string>.GetObjectId = db => db.PlotStyleNameDictionaryId;
   }

   static class DBDictionaryAccessors
   {
      static Dictionary<Type, Func<Database, ObjectId>> map =
         new Dictionary<Type, Func<Database, ObjectId>>();

      public static Func<Database, ObjectId> GetAccessor<T>() where T : DBObject
      {
         if(!map.ContainsKey(typeof(T)))
            throw new ArgumentException($"Unsupported DBDictionary accessor type {typeof(T).Name}");
         return map[typeof(T)];
      }

      static DBDictionaryAccessors()
      {
         map[typeof(Group)] = db => db.GroupDictionaryId;
         map[typeof(DataLink)] = db => db.DataLinkDictionaryId;
         map[typeof(DetailViewStyle)] = db => db.DetailViewStyleDictionaryId;
         map[typeof(SectionViewStyle)] = db => db.SectionViewStyleDictionaryId;
         map[typeof(MLeaderStyle)] = db => db.MLeaderStyleDictionaryId;
         map[typeof(TableStyle)] = db => db.TableStyleDictionaryId;
         map[typeof(PlotSettings)] = db => db.PlotSettingsDictionaryId;
         map[typeof(DBVisualStyle)] = db => db.VisualStyleDictionaryId;
         map[typeof(Material)] = db => db.MaterialDictionaryId;
         map[typeof(Layout)] = db => db.LayoutDictionaryId;
         map[typeof(MlineStyle)] = db => db.MLStyleDictionaryId;
      }
   }
}




