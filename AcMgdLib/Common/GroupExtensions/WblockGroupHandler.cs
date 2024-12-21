/// WblockGroupHandler.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using System;
using System.Collections.Generic;
using System.Linq;
using AcMgdLib.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

/// <summary>
/// An example showing the use of the WblockCloneHandler
/// base type, which automates the grunt work required to
/// intervene in a wblock operation. 
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
/// cloned or have references to/from it translated. Supporting 
/// that is beyond the scope of this example.
/// 
/// Updates:
/// 
/// Bug fixes/optimizations since original commit:
/// 
/// -  Modified code to avoid copying empty groups
///    to distination database.
///    
/// -  Modified code to enable forced copy of database
///    on full WBLOCK operation, only if there are one
///    or more cloneable groups in the database.
///    
/// -  Modified code to not act when source database 
///    contains no clonable groups.
///    
/// </summary>

namespace AcMgdLib.Common.Examples
{
   public class WblockGroupHandler : WblockCloneHandler
   {
      ObjectIdCollection clonableGroupIds = null;

      public WblockGroupHandler(Database db) : base(db, true)
      {
      }

      public WblockGroupHandler(Document doc) : this(doc.Database) 
      {
      }

      /// <summary>
      /// Revised:
      /// 
      /// Returning false causes further notifications to
      /// be suppressed, and the operation is not handled.
      /// 
      /// By opting-out of the operation at this point, a
      /// full database copy can be avoided if there's no
      /// clonable groups in the source database.
      /// </summary>
      /// <param name="sourceDb"></param>
      /// <returns></returns>

      protected override bool OnWblockNotice(Database sourceDb)
      {
         clonableGroupIds = GetClonableGroupIds(sourceDb);
         return clonableGroupIds is not null && clonableGroupIds.Count > 0;
      }

      protected override void OnDeepCloneEnded(IdMapping map, bool aborted)
      {
         if(!aborted && clonableGroupIds is not null)
         {
            int cloned = CloneGroups(map);
            if(cloned > 0)
               DebugWrite($"Exported {cloned} groups");
         }
      }

      /// <summary>
      /// Clones all groups from the source database
      /// to the destination database, only if all of 
      /// the entities in the group were cloned. 
      /// </summary>
      /// <param name="idMap"></param>

      int CloneGroups(IdMapping idMap)
      {
         if(idMap == null)
            throw new ArgumentNullException(nameof(idMap));
         if(clonableGroupIds == null || clonableGroupIds.Count == 0)
            return 0;
         int cloned = 0;
         try
         {
            using(var tr = new OpenCloseTransaction())
            {
               Database destDb = idMap.DestinationDatabase;
               var groupDictionary = (DBDictionary) tr.GetObject(
                  destDb.GroupDictionaryId, OpenMode.ForWrite);
               var map = ToDictionary(idMap);
               foreach(ObjectId id in clonableGroupIds)
               {
                  var source = (Group)tr.GetObject(id, OpenMode.ForRead);
                  var cloneIds = GetCloneIds(source, map);
                  if(cloneIds is not null)
                  {
                     using(Group group = new Group(source.Description, source.Selectable))
                     {
                        groupDictionary.SetAt(source.Name, group);
                        if(group.Name.StartsWith('*'))
                           group.SetAnonymous();
                        group.Append(cloneIds);
                        tr.AddNewlyCreatedDBObject(group, true);
                        ++cloned;
                     }
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

      static ObjectIdCollection GetCloneIds(Group source, 
         Dictionary<ObjectId, ObjectId> map)
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

      public static IEnumerable<Group> GetClonableGroups(Database db, Transaction tr = null)
      {
         bool flag = tr == null;
         IntPtr dbImpObj = db.UnmanagedObject;
         if(flag)
            tr = new OpenCloseTransaction();
         try
         {
            var groupDict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
            foreach(var entry in groupDict)
            {
               Group group = (Group)tr.GetObject(entry.Value, OpenMode.ForRead);
               if(group.NumEntities > 0 && !group.IsNotAccessible)
                  yield return group;
            }
         }
         finally
         {
            if(flag)
            {
               tr.Commit();
               tr.Dispose();
            }
         }
      }

      static Dictionary<ObjectId, ObjectId> ToDictionary(IdMapping map, bool primaryOnly = false)
      {
         return map.Cast<IdPair>().Where(primaryOnly ? primaryEntitiesOnly : entitiesOnly)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
      }

      static readonly RXClass entityClass = RXObject.GetClass(typeof(Entity));

      static Func<IdPair, bool> primaryEntitiesOnly =
         static pair => pair.IsPrimary && pair.Key.ObjectClass.IsDerivedFrom(entityClass);

      static Func<IdPair, bool> entitiesOnly =
         static pair => pair.Key.ObjectClass.IsDerivedFrom(entityClass);

      public static ObjectIdCollection GetClonableGroupIds(Database db)
      {
         var groups = GetClonableGroups(db);
         if(groups.Any())
            return new ObjectIdCollection(groups.Select(group => group.Id).ToArray());
         else
            return null;
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