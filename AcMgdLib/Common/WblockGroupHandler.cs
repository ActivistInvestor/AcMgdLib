using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;

/// <summary>
/// An example showing the use of the WblockCloneHandler
/// base type, which automates the grunt work required to
/// intervene in a wblock clone operation. 
/// 
/// This example causes groups to be included in a WBLOCK or a
/// COPYCLIP operation when alll of their member entities are 
/// included in the operation. 
/// 
/// To enable the functionality provided by this example, one 
/// only need to issue the WBLOCKGROUPS command defined below. 
/// 
/// In a realistic usage scenario, the initialization done by
/// the WBLOCKGROUPS command might instead by done within the
/// Initialize() method of an IExtensionApplication.
/// 
/// Once WBLOCKGROUPS is issued, you can use the COPYCLIP and
/// PASTECLIP commands to copy and paste entities, including 
/// any groups they belong to.
/// 
/// In order for a group to be included in a WBLOCK operation,
/// all of the member entities in the group must be included in
/// the operation. If only a subset of member entities of a group
/// are involved in a WBLOCK operation, the group(s) they belong 
/// to are not included.
/// 
/// If COPYCLIP is used to copy entities and groups they are
/// members of the clipboard, when pasted back into the same
/// drawing or another drawing, the groups will be pasted as
/// anonymous/unnamed groups. 
/// 
/// Because this operation does not technically Clone existing
/// groups, if there is any type of application-data attached to 
/// a group (e.g., xdata or extension dictionary) it will not be
/// copied/cloned or transformed. Supporting that is beyond the
/// scope of this example.
/// </summary>

namespace AcMgdLib.Common.Examples
{
   public class WblockGroupHandler : WblockCloneHandler
   {
      public WblockGroupHandler(Database db) : base(db)
      {
      }

      protected override void OnDeepCloneEnded(Database sender)
      {
         CloneGroups(this.IdMap);
      }

      /// <summary>
      /// Clones all groups from the source database
      /// to the destination database, only if all of 
      /// the entities in the group were cloned. 
      /// </summary>
      /// <param name="map"></param>

      static void CloneGroups(IdMapping map)
      {
         if(map == null)
            throw new ArgumentNullException(nameof(map));
         try
         {
            using(var tr = new OpenCloseTransaction())
            {
               Database sourceDb = map.OriginalDatabase;
               Database destDb = map.DestinationDatabase;
               ObjectId destGroupDictionaryId = destDb.GroupDictionaryId;
               var groupDictionary = tr.GetObject<DBDictionary>(
                  destGroupDictionaryId, OpenMode.ForWrite);
               foreach(var srcGroup in sourceDb.GetGroups(tr))
               {
                  if(srcGroup.IsNotAccessible)
                     continue;
                  var cloneIds = GetCloneIds(srcGroup, map);
                  if(cloneIds != null)
                  {
                     Group group = new Group(srcGroup.Description, srcGroup.Selectable);
                     groupDictionary.SetAt(srcGroup.Name, group);
                     tr.AddNewlyCreatedDBObject(group, true);
                     group.Append(cloneIds);
                     DebugWrite($"Copied group {srcGroup.Name} ({cloneIds.Count} entities)");
                  }
               }
               tr.Commit();
            }
         }
         catch(System.Exception ex)
         {
            WriteMessage($"Exception in {nameof(CloneGroups)}(): {ex.ToString()}");
            return;
         }
      }

      /// <summary>
      /// If not all source entities exist in the map (e.g., they
      /// were not cloned), then this returns null and the group
      /// is not cloned.
      /// </summary>

      public static ObjectIdCollection GetCloneIds(Group source, IdMapping map)
      {
         var srcIds = source.GetAllEntityIds();
         var cloneIds = new ObjectId[srcIds.Length];
         for(int i = 0; i < srcIds.Length; i++)
         {
            var id = srcIds[i];
            if(!map.Contains(id))
               return null;
            cloneIds[i] = map[id].Value;
         }
         return new ObjectIdCollection(cloneIds);
      }

   }

   public static class WBlockGroupExtensions
   {
      public static IEnumerable<Group> GetGroups(this Database db, Transaction tr)
      {
         var groupDict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
         foreach(DictionaryEntry entry in groupDict)
         {
            yield return Unsafe.As<Group>(tr.GetObject((ObjectId)entry.Value, OpenMode.ForRead));
         }
      }

      public static T GetObject<T>(this Transaction tr, ObjectId id, OpenMode mode = OpenMode.ForRead)
         where T: DBObject
      {
         return Unsafe.As<T>(tr.GetObject(id, mode));
      }


   }

   public static class WblockGroupHandlers
   {
      static bool initialized = false;

      public static void Initialize()
      {
         if(!initialized)
         {
            initialized = true;
            foreach(Document doc in Application.DocumentManager)
            {
               doc.UserData[typeof(WblockGroupHandler)] =
                  new WblockGroupHandler(doc.Database);
            }

            Application.DocumentManager.DocumentCreated += documentCreated;
         }
      }

      private static void documentCreated(object sender, DocumentCollectionEventArgs e)
      {
         e.Document.UserData[typeof(WblockGroupHandler)] =
            new WblockGroupHandler(e.Document.Database);
      }
   }

   public static class TestCommand
   {
      [CommandMethod("WBLOCKGROUPS")]
      public static void Initialize()
      {
         WblockGroupHandlers.Initialize();
      }
   }

}