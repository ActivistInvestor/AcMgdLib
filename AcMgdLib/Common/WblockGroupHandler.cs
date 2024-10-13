using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
/// This example causes groups to be included in a WBLOCK or 
/// a COPYCLIP operation when all member entities in a group
/// are included in the operation. 
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
/// the operation. If only a subset of a group's entities are 
/// involved in a WBLOCK operation, the group is not included.
/// 
/// If COPYCLIP is used to copy entities and groups to the 
/// clipboard, when pasted back into a drawing, the pasted
/// groups become anonymous/unnamed groups. 
/// 
/// Because this operation does not technically Clone existing
/// groups, if there is any type of application-data attached to 
/// a group (e.g., xdata or extension dictionary) it will not be
/// copied/cloned or transformed. Supporting that is beyond the
/// scope of this example.
/// 
/// Updates:
/// 
/// Bug fixes/optimizations since original commit:
/// 
/// -  Modified code to avoid copying empty groups
///    to distination database.
///    
/// -  Modified code to enable forced copy of database
///    on full WBLOCK operation.
///    
/// -  Modified code to not act when source database 
///    contains no clonable groups.
///    
/// </summary>

namespace AcMgdLib.Common.Examples
{
   public class WblockGroupHandler : WblockCloneHandler
   {
      public WblockGroupHandler(Database db) : base(db, true)
      {
      }

      /// <summary>
      /// Revised:
      /// 
      /// Returning false causes further notifications to
      /// be supressed, and the operation is not handled.
      /// 
      /// By opting-out of the operation at this point, a
      /// full database copy can be avoided if there's no
      /// clonable groups in the source database.
      /// </summary>
      /// <param name="sourceDb"></param>
      /// <returns></returns>
      
      protected override bool OnWblockNotice(Database sourceDb)
      {
         return HasClonableGroups(sourceDb);
      }

      protected override void OnDeepCloneEnded(IdMapping map, bool aborted)
      {
         if(!aborted && map != null) 
         {
            int cloned = CloneGroups(map);
            if(cloned > 0)
               DebugWrite($"Copied {cloned} groups");
         }
      }

      /// <summary>
      /// Clones all groups from the source database
      /// to the destination database, only if all of 
      /// the member entities in a group were cloned. 
      /// </summary>
      /// <param name="map"></param>

      int CloneGroups(IdMapping map)
      {
         int cloned = 0;
         try
         {
            using(var tr = new OpenCloseTransaction())
            {
               Database destDb = map.DestinationDatabase;
               var groups = (DBDictionary) tr.GetObject(
                  destDb.GroupDictionaryId, OpenMode.ForWrite);
               foreach(Group srcGroup in GetClonableGroups(map.OriginalDatabase, tr))
               {
                  var cloneIds = GetCloneIds(srcGroup, map);
                  if(cloneIds != null)
                  {
                     Group group = new Group(srcGroup.Description, srcGroup.Selectable);
                     groups.SetAt(srcGroup.Name, group);
                     tr.AddNewlyCreatedDBObject(group, true);
                     if(group.Name.StartsWith('*'))
                        group.SetAnonymous();                     
                     group.Append(cloneIds);
                     ++cloned;
                  }
               }
               tr.Commit();
            }
            return cloned;
         }
         catch(System.Exception ex)
         {
            WriteMessage($"Exception in {nameof(CloneGroups)}(): {ex.ToString()}");
            return 0;
         }
      }

      /// <summary>
      /// If not all source entities exist in the map (e.g., they
      /// were not all cloned), this returns null and the group is 
      /// not cloned. 
      /// </summary>

      static ObjectIdCollection GetCloneIds(Group source, IdMapping map)
      {
         var srcIds = source.GetAllEntityIds();
         if(srcIds.Length == 0)
            return null;
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

      public static IEnumerable<Group> GetClonableGroups(Database db, Transaction tr)
      {
         var groupDict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
         foreach(var entry in groupDict)
         {
            Group group = (Group)tr.GetObject(entry.Value, OpenMode.ForRead);
            if(!group.IsNotAccessible && group.NumEntities > 0)
               yield return group;
         }
      }

      public static ObjectIdCollection GetClonableGroupIds(Database db)
      {
         using(var tr = new OpenCloseTransaction())
         {
            try
            {
               var groups = GetClonableGroups(db, tr);
               if(groups.Any())
                  return new ObjectIdCollection(groups.Select(gr => gr.Id).ToArray());
               else
                  return null;
            }
            finally
            {
               tr.Commit();
            }
         }
      }

      static bool HasClonableGroups(Database db)
      {
         using(var tr = new OpenCloseTransaction())
         {
            var result = GetClonableGroups(db, tr).Any();
            tr.Commit();
            return result;
         }
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
