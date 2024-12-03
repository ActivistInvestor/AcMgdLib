using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Extensions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace AcMgdLib.BoundaryRepresentation
{
   /// <summary>
   /// An overrule that will explode regions into Polylines
   /// where possible, overriding the default behavior which 
   /// explodes regions to individual line and arc segments.
   /// 
   /// With the overrule running, the EXPLODE command will
   /// create Polylines rather than interconnected chains 
   /// of lines and arcs.
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
   /// Both of the above have been implemented. Exploding to
   /// polylines is the default behavior. The overrule will 
   /// convert all contiguous chains of 2 or more lines/arcs 
   /// to polylines, including those that are a part of loops 
   /// containing other types of curves, such as splines or 
   /// elliptical arcs.
   ///      
   /// Usage:
   /// 
   /// Add all files in the repo folder to a project, 
   /// build it, and NETLOAD the compiled assembly into 
   /// AutoCAD.
   /// 
   /// Issue the REGIONEXPLODE command and choose Polyline.
   /// 
   /// Issue the EXPLODE command, and select one or more
   /// regions with loops containing no splines or ellptical
   /// arc segments (e.g., loops created from Polylines).
   /// The EXPLODE command will produce Polylines rather 
   /// than interconnected lines and arcs.
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
   /// explosion of Regions (something AutoCAD might do when
   /// needs to look at sub-entities, perform object snap, 
   /// trim/extend, etc.), the overrule is constrained to 
   /// only work during the EXPLODE command.
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

   [StartupClass]
   public class RegionExplodeOverrule : TransformOverrule<Region>
   {

      static RegionExplodeOverrule()
      {
         instance = new RegionExplodeOverrule();
      }

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
      /// <param name="entitySet"></param>

      public override void Explode(Entity entity, DBObjectCollection entitySet)
      {
         bool handled = CanExplode(entity);
         try
         {
            if(handled && entity is Region region)
            {
               using(var brep = new Brep(region))
               {
                  var geCurves = brep.Explode(explodeType, parallel);
                  if(geCurves is null || !geCurves.Any())
                  {
                     AcConsole.ReportThis("Explode() return null or empty array");
                     /// Didn't find any loops that can be converted
                     /// to polylines, so do nothing. To play it safe,
                     /// the BRep is disposed before making the call 
                     /// to base.Explode()

                     handled = false;
                     return;
                  }
                  ResultBuffer xdata = propagateXdata ? entity.XData : null;
                  bool hasXData = xdata != null && xdata.Cast<TypedValue>().Any();
                  
                  // Convert all Curve3ds to Curves and store them in
                  // an array, in case a call to CreateFromGeCurve()
                  // fails.
                  //
                  // In that case, the exception will be thrown before
                  // any curves have been added to the DBObjectCollection,
                  // and base.ExpLode() can be called to fallback to the
                  // default EXPLODE behavior without polluting the result.

                  Curve3d[] array = geCurves as Curve3d[] ?? geCurves.ToArray();
                  var curves = Array.ConvertAll(array, Curve.CreateFromGeCurve);
                  foreach(var curve in curves)
                  {
                     entitySet.Add(curve);
                     if(hasXData)
                        curve.XData = xdata;
                  }
               }
            }
         }
         catch(System.Exception ex)
         {
            DebugWrite(ex.ToString());
            handled = false;
         }
         finally
         {
            if(!handled)
               base.Explode(entity, entitySet);
         }
      }

      /// <summary>
      /// The overrule alters the default behavior only
      /// for the EXPLODE command with Regions that are
      /// database-resident. In any other context, the 
      /// default behavior prevails.
      /// </summary>

      static bool CanExplode(Entity entity)
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         return entity.Database != null
            && doc.CommandInProgress == "EXPLODE";
      }

      static RegionExplodeOverrule instance = null;
      static bool parallel = false;
      static bool propagateXdata = false;
      static RegionExplodeType explodeType = RegionExplodeType.Polylines;


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
         Write($"Region explode parallel exection {what}abled");
      }

      static bool Enabled
      {
         get => instance != null && instance.IsOverruling;
         set
         {
            if(instance == null)
               instance = new RegionExplodeOverrule();
            instance.IsOverruling = value;
         }
      }

      static void Write(string fmt, params object[] args)
      {
         Application.DocumentManager.MdiActiveDocument?
            .Editor.WriteMessage("\n" + fmt, args);
      }

      [Conditional("DEBUG")]
      static void DebugWrite(string fmt, params object[] args)
      {
         Application.DocumentManager.MdiActiveDocument?
            .Editor.WriteMessage("\n" + fmt, args);
      }

      /// <summary>
      /// To enable or disable the overrule functionality
      /// specify Polylines, Spline, or Default.
      /// 
      /// Select Polylines to convert all sequences of 
      /// 2 or more interconnected line and arc segments 
      /// to a polylines.
      /// 
      /// Select Spline to convert each loop to a single
      /// closed Spline.
      /// 
      /// Select Default to disable the overrule and revert
      /// to the default behavior of the EXPLODE command.
      /// 
      /// Select Xdata to toggle propagation of Xdata from
      /// the region being exploded to the objects created
      /// by exploding it (experimental).
      /// </summary>

      [CommandMethod("REGIONEXPLODE")]
      public static void RegionExplodeMode()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         var ed = doc.Editor;
         var pko = new PromptKeywordOptions("Region explode mode");
         pko.Keywords.Add("Default");
         pko.Keywords.Add("Polylines");
         pko.Keywords.Add("Spline");
         pko.Keywords.Add("Xdata");
         pko.Keywords.Default = explodeType.ToString();
         var pr = ed.GetKeywords(pko);
         if(pr.Status != PromptStatus.OK)
            return;
         switch(pr.StringResult)
         {
            case "Default":
               Enabled = false;
               break;
            case "Polylines":
               Enabled = true;
               explodeType = RegionExplodeType.Polylines;
               break;
            case "Spline":
               Enabled = true;
               explodeType = RegionExplodeType.Spline;
               break;
            case "Xdata":
               propagateXdata ^= true;
               break;
            default:
               break;
         }
      }
   }

#if !LOCALENV

   [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
   public class StartupClassAttribute : System.Attribute
   {
   }

#endif

}