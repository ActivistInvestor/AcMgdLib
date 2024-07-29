/// IDatabaseTransaction.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Expressions.Predicates;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public interface IDatabaseTransaction
   {
      DBObject this[ObjectId key]
      {
         get
         {
            return Transaction.GetObject<DBObject>(key, OpenMode.ForRead, true, false);
         }
      }

      /// <summary>
      /// All access to the current instance 'this' should
      /// be rerouted through this property. The default
      /// implementation is to simply return 'this', but
      /// for the OpenCloseTransaction variant, it must
      /// return an OpenCloseTransaction, which is not the
      /// base type.
      /// </summary>
      
      Transaction Transaction { get; }

      /// <summary>
      /// For the OpenCloseTransaction variant, what to
      /// do about this must be resolved. 
      /// </summary>
      TransactionManager TransactionManager { get; }

      Database Database { get; }

      DBDictionary GroupDictionary => GetObject<DBDictionary>(Database.GroupDictionaryId);
      DBDictionary DataLinkDictionary => GetObject<DBDictionary>(Database.DataLinkDictionaryId);
      DBDictionary DetailViewStyleDictionary => GetObject<DBDictionary>(Database.DetailViewStyleDictionaryId);
      DBDictionary SectionViewStyleDictionary => GetObject<DBDictionary>(Database.SectionViewStyleDictionaryId);
      DBDictionary MLeaderStyleDictionary => GetObject<DBDictionary>(Database.MLeaderStyleDictionaryId);
      DBDictionary TableStyleDictionary => GetObject<DBDictionary>(Database.TableStyleDictionaryId);
      DBDictionary PlotSettingsDictionary => GetObject<DBDictionary>(Database.PlotSettingsDictionaryId);
      DBDictionary VisualStyleDictionary => GetObject<DBDictionary>(Database.VisualStyleDictionaryId);
      DBDictionary MaterialDictionary => GetObject<DBDictionary>(Database.MaterialDictionaryId);
      DBDictionary LayoutDictionary => GetObject<DBDictionary>(Database.LayoutDictionaryId);
      DBDictionary MLStyleDictionary => GetObject<DBDictionary>(Database.MLStyleDictionaryId);
      DBDictionary NamedObjectsDictionary => GetObject<DBDictionary>(Database.NamedObjectsDictionaryId);

      ObjectId CurrentSpaceId => Database.CurrentSpaceId;
      ObjectId ModelSpaceBlockId =>
         SymbolUtilityServices.GetBlockModelSpaceId(Database);
      ObjectId PaperSpaceBlockId =>
         SymbolUtilityServices.GetBlockPaperSpaceId(Database);
      ObjectId RegAppAcadId =>
         SymbolUtilityServices.GetRegAppAcadId(Database);
      ObjectId TextStyleStandardId =>
         SymbolUtilityServices.GetTextStyleStandardId(Database);
      ObjectId LayerDefpointsId =>
         SymbolUtilityServices.GetLayerDefpointsId(Database);
      ObjectId LayerZeroId => Database.LayerZero;
      bool IsCompatibilityMode =>
         SymbolUtilityServices.IsCompatibilityMode(Database);

      ObjectId ByBlockLinetype => Database.BlockTableId;
      ObjectId ByLayerLinetype => Database.ByLayerLinetype;
      ObjectId ContinuousLinetype => Database.ContinuousLinetype;

      BlockTableRecord ModelSpace => GetObject<BlockTableRecord>(ModelSpaceBlockId);
      BlockTableRecord CurrentSpace => GetObject<BlockTableRecord>(CurrentSpaceId);
      BlockTableRecord PaperSpace => GetObject<BlockTableRecord>(PaperSpaceBlockId);
      
      BlockTable BlockTable => GetObject<BlockTable>(Database.BlockTableId);
      LayerTable LayerTable => GetObject<LayerTable>(Database.LayerTableId);
      LinetypeTable LinetypeTable => GetObject<LinetypeTable>(Database.LinetypeTableId);
      ViewportTable ViewportTable => GetObject<ViewportTable>(Database.ViewportTableId);
      ViewTable ViewTable => GetObject<ViewTable>(Database.ViewTableId);
      DimStyleTable DimStyleTable => GetObject<DimStyleTable>(Database.DimStyleTableId);
      RegAppTable RegAppTable => GetObject<RegAppTable>(Database.RegAppTableId);
      TextStyleTable TextStyleTable => GetObject<TextStyleTable>(Database.TextStyleTableId);
      UcsTable UcsTable => GetObject<UcsTable>(Database.UcsTableId);

      /// <summary>
      /// Auto-implemented properties require a backing store
      /// which means they require state that interfaces cannot
      /// have.
      /// </summary>

      bool ForceOpenOnLockedLayers { get; set; }
      bool IsAutoDelete { get; }
      bool IsCurrentWorkingDatabase { get; }
      bool IsDocTransaction { get; }
      bool IsReadOnly { get; set; }
      OpenMode OpenMode { get; set; }
      bool OwnsDatabase { get; }

      void Abort(); /// ?????????

      void Add(DBObject entity) => Transaction.AddNewlyCreatedDBObject(entity, true);

      public ObjectId Append(Entity entity, BlockTableRecord owner = null)
      {
         AssertIsValid();
         owner = owner ?? CurrentSpaceForWrite;
         Assert.IsNotNullOrDisposed(entity, nameof(entity));
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         AcRx.ErrorStatus.NotOpenForWrite.ThrowIf(!owner.IsWriteEnabled);
         owner.AppendEntity(entity);
         Add(entity);
         return entity.ObjectId;
      }

      public ObjectIdCollection Append(IEnumerable<Entity> entities, bool disposeOnFail = true)
      {
         return Append(entities, CurrentSpaceForWrite, disposeOnFail);
      }

      public ObjectIdCollection Append(IEnumerable<Entity> entities, BlockTableRecord owner, bool disposeOnFail = true)
      {
         AssertIsValid();
         Assert.IsNotNull(entities, nameof(entities));
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         AcRx.ErrorStatus.NotOpenForWrite.ThrowIf(!owner.IsWriteEnabled);
         ObjectIdCollection result = new ObjectIdCollection();
         using(var e = entities.GetEnumerator())
         {
            try
            {
               while(e.MoveNext())
               {
                  var entity = e.Current;
                  Assert.IsNotNullOrDisposed(entity, nameof(entity));
                  owner.AppendEntity(entity);
                  Add(entity);
                  result.Add(entity.ObjectId);
               }
               return result;
            }
            catch(System.Exception ex)
            {
               if(disposeOnFail)
               {
                  while(e.MoveNext())
                     e.Current?.Dispose();
               }
               throw ex;
            }
         }
      }

      public ObjectIdCollection Append(DBObjectCollection entities, BlockTableRecord owner, bool dispose = true)
      {
         IDisposable disposer = dispose ? entities.EnsureDispose(false) : null;
         using(disposer)
         {
            return Append(entities.OfType<Entity>(), owner, true);
         }
      }

      public ObjectIdCollection Append(DBObjectCollection entities, bool dispose = true)
      {
         return Append(entities, CurrentSpaceForWrite, dispose);
      }


      void AssertIsValid();
      /// Uses state
      BlockTableRecord CurrentSpaceForWrite { get; }
      /// Uses state
      public Database Detach();
      BlockTableRecord GetBlock(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      IEnumerable<BlockReference> GetBlockReferences(string pattern, OpenMode mode = OpenMode.ForRead, Func<BlockTableRecord, bool> predicate = null);
      BlockTableRecord GetCurrentSpaceBlock(OpenMode mode = OpenMode.ForRead);
      DataLink GetDataLink(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      DetailViewStyle GetDetailViewStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      ObjectId GetDictionaryEntryId<T>(Func<T, bool> predicate) where T : DBObject;
      ObjectId GetDictionaryEntryId<T>(string key, bool throwIfNotFound = false) where T : DBObject;
      IEnumerable<ObjectId> GetDictionaryEntryIds<T>(Func<T, bool> predicate) where T : DBObject;
      T GetDictionaryObject<T>(Func<T, bool> predicate, OpenMode mode = OpenMode.ForRead) where T : DBObject;
      T GetDictionaryObject<T>(string key, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = true) where T : DBObject;
      IEnumerable<T> GetDictionaryObjects<T>(OpenMode mode = OpenMode.ForRead) where T : DBObject;
      DimStyleTableRecord GetDimStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      IEnumerable<Entity> GetEntities(OpenMode mode = OpenMode.ForRead, bool openLocked = false);
      Group GetGroup(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      LayerTableRecord GetLayer(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      Layout GetLayout(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      IEnumerable<ObjectId> GetLayoutBlockIds(params string[] layoutNames);
      IEnumerable<ObjectId> GetLayoutBlockIdsMatching(string pattern);
      IEnumerable<BlockTableRecord> GetLayoutBlocks(OpenMode mode = OpenMode.ForRead, bool includingModelSpace = false);
      IEnumerable<BlockTableRecord> GetLayoutBlocks(params string[] names);
      IEnumerable<BlockTableRecord> GetLayoutBlocksMatching(string pattern);
      Layout GetLayoutByKey(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      ObjectId GetLayoutId(string layoutName, bool throwIfNotFound = false);
      LinetypeTableRecord GetLinetype(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      Material GetMaterial(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      MLeaderStyle GetMLeaderStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      MlineStyle GetMlineStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      BlockTableRecord GetModelSpaceBlock(OpenMode mode = OpenMode.ForRead);
      IEnumerable<Entity> GetModelSpaceEntities(OpenMode mode = OpenMode.ForRead, bool exact = false, bool openLocked = false);
      IFilteredEnumerable<T, TCriteria> GetModelSpaceObjects<T, TCriteria>(Expression<Func<T, ObjectId>> keySelector, Expression<Func<TCriteria, bool>> predicate)
         where T : Entity
         where TCriteria : DBObject;
      IEnumerable<T> GetModelSpaceObjects<T>(OpenMode mode = OpenMode.ForRead, bool exact = false, bool openLocked = false) where T : Entity;
      IEnumerable<T> GetModelSpaceObjects<T>(IFilter<T> filter) where T : Entity;
      T GetNamedObject<T>(string key, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = true) where T : DBObject;
      IEnumerable<T> GetNamedObjects<T>(OpenMode mode = OpenMode.ForRead) where T : DBObject;
      T GetObject<T>(ObjectId id, OpenMode mode = OpenMode.ForRead, bool openErased = false, bool openOnLockedLayer = false) where T : DBObject;
      T GetObjectChecked<T>(ObjectId id, OpenMode mode = OpenMode.ForRead, bool openErased = false, bool openOnLockedLayer = false) where T : DBObject;
      IEnumerable<T> GetObjects<T, TCriteria>(Expression<Func<T, ObjectId>> keySelector, Expression<Func<TCriteria, bool>> predicate)
         where T : Entity
         where TCriteria : DBObject;
      IEnumerable<T> GetObjects<T>(OpenMode mode = OpenMode.ForRead, bool exact = false, bool openLocked = false) where T : Entity;
      IEnumerable<T> GetObjects<T>(IFilter<T> filter) where T : Entity;
      IEnumerable<T> GetPaperSpaceObjects<T>(OpenMode mode = OpenMode.ForRead, bool exact = false, bool openLocked = false) where T : Entity;
      PlotSettings GetPlotSettings(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      T GetRecord<T>(Func<T, bool> predicate, OpenMode mode = OpenMode.ForRead) where T : SymbolTableRecord;
      T GetRecord<T>(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false) where T : SymbolTableRecord;
      ObjectId GetRecordId(SymbolTable table, string key);
      RegAppTableRecord GetRegApp(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      SectionViewStyle GetSectionViewStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      TableStyle GetTableStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      TextStyleTableRecord GetTextStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      UcsTableRecord GetUcs(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      ViewTableRecord GetView(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      ViewportTableRecord GetViewportTableRecord(Func<ViewportTableRecord, bool> predicate, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      ViewportTableRecord GetViewportTableRecord(int vpnum, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      DBVisualStyle GetVisualStyle(string name, OpenMode mode = OpenMode.ForRead, bool throwIfNotFound = false);
      IEnumerable<T> UpgradeOpen<T>(IEnumerable<T> source, bool upgradeOnLockedLayers = true) where T : DBObject;
   }
}