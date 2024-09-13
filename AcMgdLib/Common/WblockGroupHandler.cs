using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;
using DeepCloneMappingExample;

/// <summary>
/// An example showing the use of the WblockCloneHandler
/// base type, which automates the grunt work required to
/// intervene in a wblock clone operation. 
/// 
/// This example causes groups to be included in a WBLOCK or a
/// COPYCLIP operation when all of their member entities are 
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
/// If COPYCLIP is used to copy entities and groups to the 
/// clipboard, when pasted back into a drawing the groups will 
/// be pasted as anonymous/unnamed groups. 
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
      public WblockGroupHandler(Database db) : base(db, true)
      {
      }

      //protected override void OnDeepCloneEnded(IdMapping map, bool aborted)
      //{
      //   if(!aborted)
      //   {
      //      int cloned = CloneGroups(map);
      //      if(cloned > 0)
      //         DebugWrite($"Copied {cloned} groups");
      //   }
      //}

      protected override void OnDeepCloneEnded(IdMapping map, bool aborted)
      {
         if(!aborted)
         {
            deepCloneGroups(Destination, map);
         }
      }

      private void deepCloneGroups(Database destination, IdMapping map)
      {
         IEnumerable<ObjectId> ids;
         using(var tr = new OpenCloseTransaction())
         {
            ids = Source.GetNamedObjects<Group>(tr)
               .Where(group => !group.IsNotAccessible)
               .Select(group => group.ObjectId);

            tr.Commit();
         }
         if(ids.Any())
         {
            ids.CopyTo<Group>(Destination.GroupDictionaryId,
               OnGroupCloned);
         }

         void OnGroupCloned(Group source, Group clone)
         {
            var cloneIds = GetCloneIds(source, map);
            AcConsole.ReportMsg($"clone.NumEntities = {clone.NumEntities}");
            //if(cloneIds != null)
            //{
            //   clone.Append(cloneIds);
            //}
         }
      }

      /// <summary>
      /// Clones all groups from the source database
      /// to the destination database, only if all of 
      /// the entities in the group were cloned. 
      /// </summary>
      /// <param name="map"></param>

      static int CloneGroups(IdMapping map)
      {
         if(map == null)
            throw new ArgumentNullException(nameof(map));
         int cloned = 0;
         try
         {
            using(var tr = new OpenCloseTransaction())
            {
               Database sourceDb = map.OriginalDatabase;
               Database destDb = map.DestinationDatabase;
               ObjectId destGroupDictionaryId = destDb.GroupDictionaryId;
               var groupDictionary = (DBDictionary) tr.GetObject(
                  destGroupDictionaryId, OpenMode.ForWrite);
               foreach(var srcGroup in sourceDb.GetAccessibleGroups(tr))
               {
                  var cloneIds = GetCloneIds(srcGroup, map);
                  if(cloneIds != null)
                  {
                     Group group = new Group(srcGroup.Description, srcGroup.Selectable);
                     groupDictionary.SetAt(srcGroup.Name, group);
                     tr.AddNewlyCreatedDBObject(group, true);
                     group.Append(cloneIds);
                     ++cloned;
                     destDb.RegisterApplication(ACMGDLIB_GROUPDATA, tr);
                     group.XData = GetXDataForSource(srcGroup);
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

      DeepCloneOverrule<Group> overrule;

      protected override void OnBeginInsert(Database from)
      {
         /// Get the names of all groups in the destination
         /// document, to determine if the incoming groups
         /// can have their name restored.
         base.OnBeginInsert(from);
         overrule = new DeepCloneOverrule<Group>(OnCloned);
      }

      void OnCloned(Group src, Group clone)
      {
         AcConsole.ReportMsg($" {src.ToDebugString()}" +
          $" => {clone.ToDebugString()} (writeEnabled = {clone.IsWriteEnabled})");
      }

      protected override void OnInsertEnded(Database db, bool aborted)
      {
         overrule?.Dispose();
         overrule = null;
         ResolveGroups(db, InsertMapping);
         base.OnInsertEnded(db, aborted);
      }

      void ResolveGroups(Database db, IdMapping map)
      {
         using(var tr = new OpenCloseTransaction())
         {
            var cloneIds = map.GetPrimaryCloneIds<Group>();
            foreach(var cloneId in cloneIds)
            {
               Group group = tr.GetObject<Group>(cloneId);
               AcConsole.Write($"Group {group.Name} IsWriteEnabled = {group.IsWriteEnabled}");
            }
         }
      }

      static TypedValueList GetXDataForSource(Group group)
      {
         TypedValueList list = new TypedValueList();
         list.AddRange(
            (DxfCode.ExtendedDataRegAppName, ACMGDLIB_GROUPDATA),
            (DxfCode.ExtendedDataAsciiString, group.Name),
            (DxfCode.ExtendedDataInteger16, group.Selectable ? 1 : 0)
         );
         return list;
      }

      public const string ACMGDLIB_GROUPDATA = "ACMGDLIB_GROUPDATA";

      /// <summary>
      /// If not all source entities exist in the map (e.g., they
      /// were not all cloned), this returns null and the group is 
      /// not cloned.
      /// </summary>

      public static ObjectIdCollection GetCloneIds(Group source, IdMapping map)
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

   }

   public static class WBlockCloneGroupExtensions
   {
      public static IEnumerable<Group> GetAccessibleGroups(this Database db, Transaction tr)
      {
         var groupDict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
         foreach(var entry in groupDict)
         {
            Group group = (Group)tr.GetObject(entry.Value, OpenMode.ForRead);
            if(!group.IsNotAccessible)
               yield return group;
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