using AcMgdLib.Interop.Examples;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Diagnostics.Extensions;
using Autodesk.AutoCAD.Runtime;

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
/// If one or more groups are copied to the clipboard, when 
/// pasted back into a drawing, the groups will be pasted as 
/// anonymous/unnamed groups. 
/// 
/// Because this operation does not technically clone existing
/// groups, if there is any type of application-data attached to 
/// a group (e.g., xdata or extension dictionary) it will not be
/// copied/cloned or transformed. Supporting that is beyond the
/// scope of this example.
/// </summary>

namespace AcMgdLib.Common.Examples
{
   public class WblockGroupHandler : WblockCloneHandler
   {
      GroupOverrule overrule;

      public WblockGroupHandler(Database db) 
         : base(db, true)
      {
      }

      protected override void OnBeginWblock(Database destDb, IdMapping idMap)
      {
         base.OnBeginWblock(destDb, idMap);
         overrule = new GroupOverrule();
      }

      protected override void OnBeginDeepCloneTranslation(IdMapping map)
      {
         try
         {
            // map.Dump();
            map.CloneGroups();
         }
         catch(System.Exception ex)
         {
            AcConsole.WriteLine(ex.ToString());
         }
         base.OnBeginDeepCloneTranslation(map);
      }

      protected override void OnDeepCloneEnded(Database sender, IdMapping map, bool aborted)
      {
         // map.CopyGroups();
         overrule?.Dispose();
         overrule = null;
      }

      public class GroupOverrule : ObjectOverrule<Group>
      {
         public override DBObject DeepClone(DBObject dbObject, DBObject ownerObject, IdMapping idMap, bool isPrimary)
         {
            var result = base.DeepClone(dbObject, ownerObject, idMap, isPrimary);
            AcConsole.WriteLine($"*** Group.DeepClone(): {result.Format()}");
            return result;
         }

         public override DBObject WblockClone(DBObject dbObject, RXObject ownerObject, IdMapping idMap, bool isPrimary)
         {
            DBObject result = base.WblockClone(dbObject, ownerObject, idMap, isPrimary);
            AcConsole.WriteLine($"*** Group.WblockClone(): {result.Format()}");
            return result;
         }
      }

   }

   public static class TestCommand
   {
      [CommandMethod("WBLOCKGROUPS")]
      public static void Initialize()
      {
         DocData<WblockGroupHandler>.Initialize(
            doc => new WblockGroupHandler(doc.Database));
      }

      /// <summary>
      /// List the names of *all* groups in a drawing file,
      /// along with their count, and selectable, anonymous 
      /// and erased status.
      /// 
      /// Using this command reveals that when inserting
      /// a file containing groups into a drawing, empty
      /// anonymous groups are created. 
      /// 
      /// It also shows that those empty groups are removed 
      /// when the file is saved (or subsequently-reopened).
      /// 
      /// Note that this command is dependent on AcMgdLib, 
      /// and will not work without it.
      /// </summary>

      [CommandMethod("LISTGR")]
      public static void ListGroups()
      {
         using(var tr = new DocumentTransaction(true, true))
         {
            foreach(var gr in tr.GetNamedObjects<Group>())
            {
               AcConsole.Write($"{gr.Name}  Count: {gr.NumEntities}  " +
                  $"Selectable: {gr.Selectable}  " + 
                  $"Anonymous: {gr.IsAnonymous}  " +
                  $"Erased: {gr.IsErased}");
            }
         }
      }


   }

}