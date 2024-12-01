using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;

public static partial class RegionExtensions
{
   /// <summary>
   /// An overrule that will explode regions into Polylines
   /// where possible, overriding the default behavior which 
   /// explodes regions to individual line and arc segments.
   /// 
   /// With the overrule running, the EXPLODE command will
   /// create Polylines rather than chains of contiguous
   /// lines and arcs.
   /// 
   /// Currently, the code will only convert loops that are
   /// comprised entirely of lines and arcs into Polylines.
   /// If an ACIS/ASM curve appears in a loop, the behavior
   /// is the same as the default behavior of EXPLODE.
   /// 
   /// Work-in-progress.
   /// 
   /// The goal is to extend the functionality of this code
   /// to allow the following options:
   /// 
   ///   1. Convert loops containing ACIS/ASM curves into 
   ///      a single, closed Spline entity. While this is 
   ///      currently possible, it is not enabled by default
   ///      because it may not produce the desired result in
   ///      every case. The JOIN command can be used on the
   ///      result to create a single, closed Spline if that
   ///      is desired.
   ///      
   ///   2. Convert loops containing ACIS/ASM curves into
   ///      multiple entities with all contiguous line/arc
   ///      sequences converted to Polylines. This is a bit
   ///      more of a challenge but is entirely possible.
   ///      
   /// Usage:
   /// 
   /// Add all files in the repo folder to a project, 
   /// build it, and NETLOAD the compiled assembly into 
   /// AutoCAD.
   /// 
   /// Issue the REGIONEXPLODE command to enable the 
   /// overrule.
   /// 
   /// Issue the EXPLODE command, and select one or more
   /// regions with loops containing no splines or ellptical
   /// arc segments (e.g., loops created from Polylines).
   /// The EXPLODE command will produce Polylines rather 
   /// than lines and arcs.
   /// 
   /// It probably should have worked this way from
   /// the outset, but.....
   /// </summary>

   public class RegionExplodeOverrule : TransformOverrule<Region>
   {
      public override void Explode(Entity entity, DBObjectCollection entitites)
      {
         if(entity is Region region)
         {
            try
            {
               using(var brep = new Brep(region))
               {
                  /// Important: the result of ParallelGetEdgeGeometry()
                  /// must be executed within the scope of the BRep, which 
                  /// is done by calling .ToArray() on the result. This is 
                  /// necessary to cause the code to execute in the scope 
                  /// of the Brep, rather than after it has been disposed.
                  
                  var curves = brep.ParallelGetEdgeGeometry().ToArray();

                  foreach(var item in curves)
                  {
                     entitites.Add(Curve.CreateFromGeCurve(item));
                  }
               }
            }
            catch(System.Exception)
            {
               base.Explode(entity, entitites);
            }
         }
      }

      static RegionExplodeOverrule instance = null;

      [CommandMethod("REGIONEXPLODE")]
      public static void StopStart()
      {
         if(instance == null)
            instance = new RegionExplodeOverrule();
         else
         {
            instance.Dispose();
            instance = null;
         }
         string what = instance != null ? "en" : "dis";
         Application.DocumentManager.MdiActiveDocument
            .Editor.WriteMessage($"\nRegionExplodeOverrule {what}abled");
      }
   }




}

