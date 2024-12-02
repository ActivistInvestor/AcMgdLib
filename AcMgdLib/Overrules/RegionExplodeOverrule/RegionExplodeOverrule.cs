using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
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
      /// Updated:
      /// 
      /// Option 2 above has been implemented and is now the 
      /// default behavior. The overrule will convert all 
      /// contiguous chains of 2 or more lines/arcs to polylines, 
      /// including those that are a part of loops containing 
      /// other types of curves, such as splines or elliptical 
      /// arcs.
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
      /// Parallel Execution disabled by default:
      /// 
      /// Due to some unexplained, intermittent crashes, the
      /// parallel switch is disabled by default, until the
      /// cause of the failure can be identified. At this
      /// point, the suspect is extracting curves from breps,
      /// But, this has yet to be confirmed. If that proves
      /// to be the case, the code will need to be refactored
      /// to extract brep curves serially, and then operate
      /// on them in parallel.
      /// 
      /// Conditional operation only during EXPLODE.
      /// 
      /// Because this overrule can be called for any type of
      /// explosion of Regions (something AutoCAD does routinely
      /// to identify sub-entities, perform object snap, etc.), 
      /// the overrule is constrained to only work during the 
      /// EXPLODE command.
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

         /// <summary>
         /// Notes on optimizations to be done:
         /// 
         ///   1.  Determine if the region contains any geometry
         ///       of interest (edge geometry that is convertable 
         ///       to a polyline), which means looking for two or 
         ///       more contiguous line/arc edges). 
         ///       
         ///       If the region has no converable geometry, then 
         ///       there is nothing to do, and we can defer to the 
         ///       default behavior (e.g., call base.Explode()).
         ///       
         ///       The above optimization has been implemented in
         ///       this commit.
         ///       
         /// </summary>
         /// <param name="entity"></param>
         /// <param name="entitites"></param>

         public override void Explode(Entity entity, DBObjectCollection entitites)
         {
            bool handled = CanExplode(entity);
            if(handled && entity is Region region) 
            {
               try
               {
                  using(var brep = new Brep(region))
                  {
                     var geCurves = brep.Explode(parallel);
                     if(geCurves is null || !geCurves.Any())
                     {
                        /// Didn't find any loops that can be converted
                        /// to polylines, so do nothing. To play it safe,
                        /// the BRep is disposed before making the call 
                        /// to base.Explode()
                        
                        handled = false;
                        return;
                     }
                     ResultBuffer xdata = propagateXdata ? entity.XData : null;
                     bool hasXData = xdata != null && xdata.Cast<TypedValue>().Any();

                     foreach(var geCurve in geCurves as Curve3d[] ?? geCurves.ToArray())
                     {
                        Curve curve = Curve.CreateFromGeCurve(geCurve);
                        entitites.Add(curve);
                        if(hasXData)
                           curve.XData = xdata;
                     }
                  }
               }
               catch(System.Exception)
               {
                  handled = false;
               }
               finally
               {
                  if(!handled)
                     base.Explode(entity, entitites);
               }
            }
            else
            {
               base.Explode(entity, entitites);
            }
         }

         /// <summary>
         /// The overrule alters the default behavior only
         /// for the EXPLODE command. In any other context,
         /// the default behavior prevails.
         /// </summary>

         static bool CanExplode(Entity entity)
         {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            return entity.Database != null
               && doc.CommandInProgress == "EXPLODE"
               && IsEqualTo(doc.Database, entity.Database);
         }

         static RegionExplodeOverrule instance = null;

         static bool parallel = false;
         static bool propagateXdata = false;

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

         /// <summary>
         /// A command that toggles propagation of Xdata from
         /// a region to the entities created by exploding it.
         /// This is disabled by default.
         /// </summary>

         [CommandMethod("REGIONEXPLODEXDATA")]
         public static void ToggleXdata()
         {
            propagateXdata ^= true;
            string what = propagateXdata ? "en" : "dis";
            Application.DocumentManager.MdiActiveDocument
               .Editor.WriteMessage($"\nRegion Explode XData propagation {what}abled");
         }

         /// <summary>
         /// Toggles special handling of Regions by the 
         /// EXPLODE command on/off. This command must be
         /// issued to enable the functionality provided
         /// by this code.
         /// 
         /// When enabled, Exploded region will be comprised
         /// of polylines rather than chains of lines/arcs.
         /// </summary>
         
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
      }


      public static bool IsEqualTo(this Database db, Database other)
      {
         if(db is null)
            return other is null;
         else if(other is null)
            return false;
#if NET8_0_OR_GREATER
         return db.RuntimeId == other.RuntimeId;
#else
         return db.UnmanagedObject == other.UnmanagedObject;
#endif
      }
   }

   /// <summary>
   /// For future use with explode region loops to
   /// a single spline (not implemented yet).
   /// </summary>

   public enum RegionExplodeType
   {
      Default = 0, // Default behavior of EXPLODE command 
      Polylines,   // Convert contiguous lines/arcs to polylines
      Spline       // Convert each loop to a single Spline
   }
}