/// BlockExportOverrule.cs  
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under terms of the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

namespace AcMgdLib.DatabaseServices
{
   /// <summary>
   /// A class that automates the exploding and
   /// exporting of entities to a new Database.
   /// 
   /// Put more-simply, this class combines the
   /// functionality of the WBLOCK and EXPLODE 
   /// commands, to automate a series of complex
   /// operations. To do what this class does 
   /// maually would involve using the WBLOCK
   /// command to export selected objects to a
   /// new DWG file, and then opening that file
   /// and exploding all block references.
   /// 
   /// </summary>

   public class BlockExportOverrule : ObjectOverrule
   {
      Matrix3d transform;
      bool hasTransform = false;
      bool disposed = false;
      static RXClass entityClass = RXObject.GetClass(typeof(Entity));
      static RXClass blockRefClass = RXObject.GetClass(typeof(BlockReference));
      Database originalWorkingDb;

      BlockExportOverrule()
      {
         AddOverrule(entityClass, this, true);
      }

      /// Required to update non-default-justified text.
      /// Pass null to restore previous working db.
      void SetWorkingDatabase(Database db = null)
      {
         if(db is null)
         {
            if(originalWorkingDb != null && originalWorkingDb != HostApplicationServices.WorkingDatabase)
               HostApplicationServices.WorkingDatabase = originalWorkingDb;
         }
         else if(originalWorkingDb is null)
         {
            originalWorkingDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;
         }
      }

      public override DBObject WblockClone(DBObject dbObject, RXObject ownerObject, IdMapping idMap, bool isPrimary)
      {
         SetWorkingDatabase(idMap.DestinationDatabase);
         var result = base.WblockClone(dbObject, ownerObject, idMap, isPrimary);
         if(hasTransform && isPrimary && result is Entity entity)
            entity.TransformBy(transform);
         return result;
      }

      internal Matrix3d Transform
      {
         get { return transform; }
         set
         {
            transform = value;
            hasTransform = !transform.IsEqualTo(Matrix3d.Identity);
         }
      }

      protected override void Dispose(bool disposing)
      {
         if(!disposed)
         {
            disposed = true;
            SetWorkingDatabase(null);
            RemoveOverrule(entityClass, this);
         }
         base.Dispose(disposing);
      }

      /// <summary>
      /// Exports the selection to a Database, and
      /// explodes all exported block references that
      /// can be exploded. XRef block references are
      /// not exported.
      /// 
      /// </summary>
      /// <remarks>This operation has the potential to raise 
      /// ErrorStatus.CannotScaleNonUniformly if an NUS block
      /// reference is included in the set of ObjectIds and it
      /// cannot be exploded. This implementation currently 
      /// does not support all NUS block references.
      /// 
      /// Roadmap: Support for recursive exploding of nested
      /// block references (this is somewhat-complicated by 
      /// the fact that objects must be database-resident in 
      /// order to deep-clone them, precluding the direct use 
      /// of Entity.Explode() to do the work). 
      /// 
      /// Revisions:
      /// 
      /// As a result of stumbling onto a bug in the ObjectId 
      /// class (the CompareTo() method) that has probably been 
      /// there since day 1, the original version of this class 
      /// was not doing what was expected. That has been resolved 
      /// using a workaround for the aformentioned bug.
      /// 
      /// For the curious, the bug results in a failure to sort or
      /// order objects using ObjectIds as sort keys. The included
      /// ObjectIdComparer class is the workaround.
      /// 
      /// Code was revised to support exporting  any type of
      /// entity. Entities that are not block references are
      /// exported in the same way they are by AutoCAD's WBLOCK
      /// (objects) command. Block references are exploded and
      /// are replaced with the entities produced by exploding
      /// them.
      /// 
      /// Another revision causes all objects resulting from 
      /// exploding each block to be placed into an anonymous 
      /// Group in the destination file.
      /// 
      /// Yet another revision causes groups in the source file
      /// to be exported to the destination file, provided that
      /// all entities in the group are exported.
      /// 
      /// </remarks>
      /// <param name="objectIds">The ObjectIds of the
      /// entities to be exploded and cloned. All entities 
      /// whose Ids are included must have the same owner.
      /// block references whose ids are included in this
      /// argument are exploded in the output database.
      /// If a block reference cannot be exploded, it will
      /// be exported as-is.
      /// </param>
      /// <param name="db">The destination database to output 
      /// to. If null or not provided, a new Database is created 
      /// and output to.
      /// </param>
      /// <param name="basePoint">An optional basePoint for 
      /// translating objects in the destination database. If
      /// not provided, the WCS origin is used.</param>
      /// <param name="exportGroups">A value indicating if a group
      /// should be created from the entities produced by exploding
      /// each block reference in the destination Database.</param>
      /// <returns>The Database argument with objects added to
      /// it, or a new Database with objects added to it if no
      /// Database argument was provided. If no Database argument
      /// was provided, the new Database returned by this method
      /// must be disposed.
      /// </returns>

      public static Database Export(IEnumerable<ObjectId> objectIds, 
         Point3d basePoint = default(Point3d),
         bool exportGroups = true,
         Database db = null)
      {
         if(objectIds is null)
            throw new ArgumentNullException(nameof(objectIds));
         if(!objectIds.Any())
            return null;
         Database source = objectIds.First().Database;
         if(source is null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NotInDatabase);
         Matrix3d transform = Matrix3d.Identity;
         bool hasBasePoint = !basePoint.IsEqualTo(Point3d.Origin);
         if(hasBasePoint)
            transform = Matrix3d.Displacement(basePoint.GetVectorTo(Point3d.Origin));
         Database result = db ?? new Database(true, true);
         ObjectId target = SymbolUtilityServices.GetBlockModelSpaceId(result);
         try
         {
            using(var tr = new OpenCloseTransaction())
            {
               try
               {
                  var blockRefs = GetBlockReferences(objectIds, tr)
                     .OrderBy(br => br.BlockTableRecord, ObjectIdComparer.Instance);
                  var entities = GetEntities(objectIds, tr).ToList();
                  if(!(blockRefs.Any() || entities.Any()))
                     return null;
                  BlockTableRecord btr = null;
                  ObjectId btrId = ObjectId.Null;
                  ObjectIdCollection items = null;
                  using(var exporter = new BlockExportOverrule())
                  {
                     if(blockRefs.Any())
                     {
                        HashSet<ObjectId> rejected = new HashSet<ObjectId>();
                        foreach(BlockReference br in blockRefs)
                        {
                           if(rejected.Contains(br.BlockTableRecord))
                           {
                              entities.Add(br);
                              continue;
                           }
                           if(btrId != br.BlockTableRecord)
                           {
                              btrId = br.BlockTableRecord;
                              btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                              if(IsXref(btr))
                              {
                                 btrId = ObjectId.Null;
                                 continue;
                              }
                              if(!btr.Explodable)
                              {
                                 entities.Add(br);
                                 rejected.Add(br.BlockTableRecord);
                                 continue;
                              }
                              var array = btr.Cast<ObjectId>().ToArray();
                              items = new ObjectIdCollection(array);
                           }
                           if(hasBasePoint)
                              exporter.Transform = transform * br.BlockTransform;
                           else
                              exporter.Transform = br.BlockTransform;
                           IdMapping map = new IdMapping();
                           source.WblockCloneObjects(items, target, map,
                              DuplicateRecordCloning.Ignore, false);
                           if(exportGroups)
                           {
                              var ids = map.Cast<IdPair>().Where(p => p.IsPrimary)
                                 .Select(p => p.Value).ToArray();
                              GroupAPI.CreateGroup(ids, tr, btr.Name);
                           }
                        }
                     }
                     if(entities.Any())
                     {
                        IdMapping map = new IdMapping();
                        var collection = new ObjectIdCollection(
                           entities.Select(e => e.ObjectId).ToArray());
                        exporter.Transform = transform;
                        source.WblockCloneObjects(collection, target, map,
                           DuplicateRecordCloning.Ignore, false);
                        GroupAPI.CloneGroups(map, tr);
                     }
                  }
               }
               finally
               {
                  tr.Commit();
               }
            }
         }
         catch(System.Exception)
         {
            if(db != result)
               result.Dispose();
            throw;
         }
         return result;
      }

      static bool IsXref(BlockTableRecord btr) =>
         (btr.IsFromExternalReference | btr.IsFromOverlayReference | btr.IsDependent);

      /// <summary>
      /// Includes only BlockReferences but not Tables:
      /// </summary>

      private static IEnumerable<BlockReference> GetBlockReferences(IEnumerable<ObjectId> ids, Transaction tr)
      {
         foreach(ObjectId id in ids)
         {
            if(id.ObjectClass == blockRefClass)
            {
               var result = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
               if(IsExportableBlockReference(result))
                  yield return result;
            }
         }
      }

      /// <summary>
      /// Excludes BlockReferences, but includes Tables:
      /// </summary>

      private static IEnumerable<Entity> GetEntities(IEnumerable<ObjectId> ids, Transaction tr)
      {
         foreach(ObjectId id in ids)
         {
            if(id.ObjectClass.IsDerivedFrom(entityClass))
            {
               var result = (Entity)tr.GetObject(id, OpenMode.ForRead);
               if(!IsExportableBlockReference(result))
                  yield return result;
            }
         }
      }

      internal static bool IsExportableBlockReference(DBObject obj)
      {
         return obj is not null
            && !DBObject.IsCustomObject(obj.ObjectId)
            && obj is BlockReference
            && obj is not Table;
      }

      /// <summary>
      /// ObjectId.CompareTo() contains a bug that causes
      /// sorting/ordering to fail. This class can be used
      /// in any case where ObjectIds are sorted or used as
      /// sort keys.
       /// </summary>

      class ObjectIdComparer : IComparer<ObjectId>
      {
         static ObjectIdComparer instance;
         public static ObjectIdComparer Instance =>
            instance ?? (instance = new ObjectIdComparer());

         public int Compare(ObjectId x, ObjectId y)
         {
            return x.OldId.CompareTo(y.OldId);
         }
      }
   }

   public static partial class GroupAPI
   {
      static RXClass groupClass = RXObject.GetClass(typeof(Group));
      static RXClass entityClass = RXObject.GetClass(typeof(Entity));
      public static ObjectId CreateGroup(this IEnumerable<ObjectId> ids, Transaction tr, string name = "")
      {
         if(ids is null || !ids.Any())
            return ObjectId.Null;
         if(tr is null)
            throw new ArgumentNullException(nameof(tr));
         Database db = ids.First().Database;
         if(db is null)
            throw new AcRx.Exception(AcRx.ErrorStatus.NotInDatabase);
         var groupDictionarId = db.GroupDictionaryId;
         var groups = (DBDictionary)tr.GetObject(groupDictionarId, OpenMode.ForWrite);
         Group group = new Group("Exploded block reference {name}", true);
         groups.SetAt("*U", group);
         var collection = new ObjectIdCollection(ids as ObjectId[] ?? ids.ToArray());
         group.Append(collection);
         tr.AddNewlyCreatedDBObject(group, true);
         return group.ObjectId;
      }

      /// <summary>
      /// Begin proposed encapsulation of reusable WblockGroupHandler APIs
      /// </summary>
      public static int CloneGroups(IdMapping idMap, Transaction trans)
      {
         if(idMap is null)
            throw new ArgumentNullException(nameof(idMap));
         var groupIds = GetGroupIds(idMap, trans);
         if(groupIds.Count == 0)
            return 0;
         int cloned = 0;
         try
         {
            Database destDb = idMap.DestinationDatabase;
            var groups = (DBDictionary)trans.GetObject(
               destDb.GroupDictionaryId, OpenMode.ForWrite);
            var map = ToDictionary(idMap);
            foreach(ObjectId id in groupIds)
            {
               var source = (Group)trans.GetObject(id, OpenMode.ForRead);
               var cloneIds = GetCloneIds(source, map);
               if(cloneIds is not null && cloneIds.Count > 0)
               {
                  if(CloneGroup(groups, source, cloneIds, trans) != null)
                     ++cloned;
               }
            }
            return cloned;
         }
         catch(System.Exception ex)
         {
            DebugOutput.Write($"Exception in {nameof(CloneGroups)}(): {ex.ToString()}");
            return 0;
         }
      }
      static Group CloneGroup(DBDictionary owner, Group source, ObjectIdCollection cloneIds, Transaction tr)
      {
         Group group = new Group(source.Description, source.Selectable);
         try
         {
            group.Append(cloneIds);
            if(source.Name.StartsWith('*'))
               group.SetAnonymous();
            owner.SetAt(source.Name, group);
            tr.AddNewlyCreatedDBObject(group, true);
            return group;
         }
         catch
         {
            group.Dispose();
            throw;
         }
      }

      static Dictionary<ObjectId, ObjectId> ToDictionary(IdMapping map, bool primaryOnly = false)
      {
         return map.Cast<IdPair>().Where(primaryOnly ? primaryEntitiesOnly : entitiesOnly)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
      }

      static bool primaryEntitiesOnly(IdPair pair) =>
         pair.IsPrimary && pair.Key.ObjectClass.IsDerivedFrom(entityClass);

      static bool entitiesOnly(IdPair pair) =>
         pair.Key.ObjectClass.IsDerivedFrom(entityClass);

      public static ICollection<ObjectId> GetGroupIds(IdMapping map, Transaction tr)
      {
         HashSet<ObjectId> ids = new HashSet<ObjectId>();
         foreach(IdPair pair in map)
         {
            ObjectId key = pair.Key;
            if(key.ObjectClass.IsDerivedFrom(entityClass))
            {
               Entity entity = (Entity)tr.GetObject(key, OpenMode.ForRead);
               var rids = entity.GetPersistentReactorIds();
               if(rids != null && rids.Count > 0)
               {
                  int cnt = rids.Count;
                  IntPtr ptr = groupClass.UnmanagedObject;
                  for(int i = 0; i < cnt; i++)
                  {
                     if(rids[i].ObjectClass.UnmanagedObject == ptr)
                        ids.Add(rids[i]);
                  }
               }
            }
         }
         return ids;
      }

      static ObjectIdCollection GetCloneIds(Group source, Dictionary<ObjectId, ObjectId> map)
      {
         var srcIds = source.GetAllEntityIds();
         if(srcIds.Length == 0)
            return null;
         var cloneIds = new ObjectId[srcIds.Length];
         for(int i = 0; i < srcIds.Length; i++)
         {
            if(!map.TryGetValue(srcIds[i], out cloneIds[i]))
               return null;
         }
         return new ObjectIdCollection(cloneIds);
      }

      static class DebugOutput
      {
         [Conditional("DEBUG")]
         internal static void Write(string msg, [CallerMemberName] string caller = null)
         {
            if(!string.IsNullOrWhiteSpace(caller))
               msg = caller + "(): " + msg;
            Application.DocumentManager.MdiActiveDocument
               .Editor.WriteMessage(msg);
            Debug.WriteLine(msg);
         }
      }

   }




   public static class BlockExportCommands
   {
      /// <summary>
      /// Current issues:
      /// 
      ///    The resulting IdMap sometimes throws an exception
      ///    when there is either no entities or no blocks, and
      ///    it fails when accessing the DestinationDatabase.
      ///    
      ///    A solution for capturing the audit trail of source 
      ///    to clone mapping is needed.
      ///    
      /// </summary>
      
      [CommandMethod("EXPORTEXPLODED", CommandFlags.UsePickSet)]
      public static void BlockExportCommand()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         var editor = doc.Editor;
         var psfo = new PromptSaveFileOptions("\nDestination Drawing file: ");
         psfo.Filter = "Drawing Files|*.dwg";
         psfo.FilterIndex = 0;
         if(doc.IsNamedDrawing)
            psfo.InitialDirectory = Path.GetDirectoryName(doc.Name);
         var pfsr = editor.GetFileNameForSave(psfo);
         if(pfsr.Status != PromptStatus.OK)
            return;
         string outputFile = pfsr.StringResult;
         if(!DwgIOUtils.IsWriteEnabled(outputFile, out string error))
         {
            editor.WriteMessage($"\nFile {outputFile}:\n  {error}");
            return;
         }
         PromptSelectionOptions pso = new PromptSelectionOptions();
         pso.RejectObjectsFromNonCurrentSpace = true;
         pso.RejectPaperspaceViewport = true;
         var psr = editor.GetSelection(pso);
         ObjectId[] selection = null;
         if(psr.Status != PromptStatus.OK)
            return;
         var ppo = new PromptPointOptions("\nBasepoint <UCS Origin>: ");
         ppo.AllowNone = true;
         var ppr = editor.GetPoint(ppo);
         if(ppr.Status == PromptStatus.Cancel)
            return;
         Point3d basePoint = ppr.Value.TransformBy(editor.CurrentUserCoordinateSystem);
         selection = psr.Value.GetObjectIds();
         int cnt = selection.Length;
         try
         {
            using(var db = BlockExportOverrule.Export(selection, basePoint, true))
            { 
               if(db is null)
               {
                  editor.WriteMessage("\nExport failed.");
                  return;
               }
               db.SaveAs(outputFile, DwgVersion.Current);
               editor.WriteMessage($"\nExported selected objects to {outputFile}");
            }
         }
         catch(System.Exception ex)
         {
            editor.WriteMessage(ex.ToString());
         }
      }

   }

   static class DwgIOUtils
   {
      public static bool IsWriteEnabled(string filePath, out string error)
      {
         error = string.Empty;

         try
         {
            string? directory = Path.GetDirectoryName(filePath);
            if(string.IsNullOrEmpty(directory))
            {
               error = "Error: Invalid file path.";
               return false;
            }

            if(!Directory.Exists(directory))
            {
               try
               {
                  Directory.CreateDirectory(directory);
               }
               catch(UnauthorizedAccessException)
               {
                  error = "Error: Insufficient directory create permissions.";
                  return false;
               }
               catch(SecurityException)
               {
                  error = "Error: directory creation failed (Security).";
                  return false;
               }
               catch(IOException ex)
               {
                  error = $"Error: Failed to create directory - {ex.Message}";
                  return false;
               }
            }

            if(File.Exists(filePath))
            {
               using(FileStream fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None))
               {
                  return true;
               }
            }
            else
            {
               using(FileStream fs = File.Create(filePath, 1, FileOptions.DeleteOnClose))
               {
                  return true;
               }
            }
         }
         catch(UnauthorizedAccessException)
         {
            error = "Error: cannot write to the file or directory.";
         }
         catch(SecurityException)
         {
            error = "Error: cannot write to the file or directory (Security).";
         }
         catch(IOException ex) when(IsFileLocked(ex))
         {
            error = "Error: The file is locked or in use by another process.";
         }
         catch(System.Exception ex)
         {
            error = $"Error: {ex.Message}";
         }
         return false;
      }

      private static bool IsFileLocked(IOException ex)
      {
         int errorCode = Marshal.GetHRForException(ex) & 0xFFFF;
         return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION & ERROR_LOCK_VIOLATION
      }
   }


}

#pragma warning restore CS0618 // Type or member is obsolete

