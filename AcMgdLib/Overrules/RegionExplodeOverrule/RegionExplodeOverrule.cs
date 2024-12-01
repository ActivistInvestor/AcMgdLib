using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.DatabaseServices
{
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
      /// 
      /// Development notes:
      /// 
      /// The overrule will propagate XData from the Region
      /// being exploded to the resulting entities (this is
      /// purely experimental and may only be useful in very
      /// specialized use-cases).
      /// 
      /// Disclaimer: This is experimental code that is not
      /// recommended for production AutoCAD use. Any use of
      /// this code is undertaken entirely at your own risk,
      /// and the author is not responsible for damages that
      /// arise out of using this code.
      /// 
      /// If you choose to experiment with this code, please
      /// report bugs, feature enhancement requests, or other 
      /// issues via the repository discussion at:
      /// 
      ///   https://github.com/ActivistInvestor/AcMgdLib/discussions
      /// 
      /// </summary>

      public class RegionExplodeOverrule : TransformOverrule<Region>
      {
         public override void Explode(Entity entity, DBObjectCollection entitites)
         {
            if(entity is Region region && IsExplodeCommand)
            {
               try
               {
                  using(var brep = new Brep(region))
                  {
                     var geCurves = brep.Explode();
                     var xdata = entity.XData;
                     bool hasXData = xdata != null && xdata.Cast<TypedValue>().Any();

                     foreach(var geCurve in geCurves)
                     {
                        Entity curve = Curve.CreateFromGeCurve(geCurve);
                        entitites.Add(curve);
                        if(hasXData)
                           curve.XData = xdata;
                     }
                  }
               }
               catch(System.Exception)
               {
                  base.Explode(entity, entitites);
               }
            }
            else
            {
               base.Explode(entity, entitites);
            }
         }

         static RegionExplodeOverrule instance = null;

         static bool parallel = false;

         /// <summary>
         /// A command that enables/disables parallel
         /// execution of work done by this overrule.
         /// 
         /// Note: parallel execution is disabled by
         /// default, and must be enabled using this
         /// command. Unless you are dealing with very
         /// complex or dense regions comprised of many
         /// curves or vertices, you don't really need
         /// parallel execution, and probably will not
         /// benefit from it. Also remember that because
         /// this code is experimental and has not been
         /// thoroughly tested in parallel, there is a
         /// possiblity of failure in that mode.
         /// </summary>
         
         [CommandMethod("REGIONEXPLODEPARALLEL")]
         public static void ToggleParallel()
         {
            parallel ^= true;
            string what = parallel ? "en" : "dis";
            Application.DocumentManager.MdiActiveDocument
               .Editor.WriteMessage($"\nRegion Explode parallelization {what}abled");
         }

         [CommandMethod("REGIONEXPLODE")]
         public static void StopStart()
         {
            if(instance == null)
               instance = new RegionExplodeOverrule();
            else
            {
               instance.IsOverruling ^= true;
            }
            string what = instance.IsOverruling ? "en" : "dis";
            Application.DocumentManager.MdiActiveDocument
               .Editor.WriteMessage($"\nExplode Regions to Polylines {what}abled");
         }

         static bool IsExplodeCommand
         {
            get
            {
               return Application.DocumentManager.MdiActiveDocument?
                  .CommandInProgress == "EXPLODE";
            }
         }
      }

   }
}