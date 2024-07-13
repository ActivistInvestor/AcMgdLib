using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Extensions;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

/// DBObjectDataMapExample.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Example code showing how to use/extend the
/// DBObjectDataMap and various other classes from 
/// the AcDbLinq library.

namespace AutoCAD.AcDbLinq.Examples
{

   /// <summary>
   /// A specialization of DBObjectDataMap that calculates 
   /// and returns an entity's 'effective' Color, in an
   /// efficient way.
   /// 
   /// An entity's effective color is the entity's color if not 
   /// BYLAYER, or the effective color of the layer which the 
   /// entity resides on. 
   /// 
   /// The effective color of a layer is the layer's color, or 
   /// if a viewport is specified, the overridden color in the 
   /// viewport, if a color override exists.
   /// 
   /// AutoCAD's properties palette displays the effective color
   /// of selected entities in the swatch of the Color property.
   /// 
   /// Nested entities (e.g. 'BYBLOCK') are not supported.
   /// 
   /// This class is mainly intended to serve as an example
   /// showing how to specialize the underlying classes for 
   /// various purposes. Required error checking has been
   /// omitted for brevity and clarity.
   /// 
   /// Using this class to compute an entity's effective color
   /// is as simple as this:
   /// 
   /// <code>
   /// 
   ///   Document doc = Application.DocumentManager.MdiActiveDocument;
   ///
   ///   var colors = new EffectiveColorMap(doc.Editor.ActiveViewportId);
   ///   
   ///   Entity entity = // assign to an Entity
   ///   
   ///   Color effectiveColor = colors[entity];
   /// 
   /// </code>
   /// </summary>

   public class EffectiveColorMap : DBObjectDataMap<Entity, LayerTableRecord, Color>
   {
      /// <summary>
      /// Optionally pass the ObjectId of a model space viewport
      /// that should be used to calculate the effective color of
      /// entities used with the instance.
      /// </summary>
      /// <param name="viewportId"></param>

      public EffectiveColorMap(ObjectId viewportId = default(ObjectId))

         : base(ent => ent.ColorIndex == 256 ? ent.LayerId : ObjectId.Null,
              layer => GetEffectiveLayerColor(layer, viewportId))
      {
         if(!viewportId.IsNull)
            AcRx.ErrorStatus.WrongObjectType.Requires<Viewport>(viewportId);
      }

      /// <summary>
      /// The keySelector delegate passed to the constructor
      /// will return the entity's LayerId, or ObjectId.Null
      /// if the entity's color is not BYLAYER. If the result
      /// is ObjectId.Null, this method is called and returns
      /// the value of the entity's Color property.
      /// </summary>

      protected override Color GetDefaultValue(Entity entity)
      {
         return entity.Color;
      }

      /// <summary>
      /// This method is called and passed a LayerTableRecord.
      /// It returns the layer's effective color, which could
      /// be an overridden color for the specified viewport, or
      /// the layer's color if no viewport is specified, or no
      /// color override exists for the given viewport.
      /// </summary>

      static Color GetEffectiveLayerColor(LayerTableRecord layer, ObjectId viewportId)
      {
         if(!viewportId.IsNull && !viewportId.IsErased && layer.HasOverrides)
         {
            var overrides = layer.GetViewportOverrides(viewportId);
            if(overrides.IsColorOverridden)
               return overrides.Color;
         }
         return layer.Color;
      }
   }

   /// <summary>
   /// This class is a wrapper for EffectiveColorMap
   /// that can be used to obtain the effective color 
   /// of an entity in the active viewport.
   /// 
   /// Internally, it marshals multiple per-viewport
   /// instances of EffectiveColorMap, and uses the
   /// instance associated with the active viewport.
   /// </summary>
   
   public class EffectiveColors
   {
      EffectiveColorMap defaultMap = new EffectiveColorMap();
      Dictionary<ObjectId, EffectiveColorMap> maps = 
         new Dictionary<ObjectId, EffectiveColorMap>();

      EffectiveColors()
      {
      }

      static EffectiveColors instance = null;
      public static EffectiveColors Instance
      { 
         get 
         { 
            if (instance == null)
               instance = new EffectiveColors();
            return instance; 
         } 
      }

      /// <summary>
      /// The viewport that's used to compute the effective
      /// color, is the viewport that's active at the point
      /// when this indexer is referenced.
      /// </summary>
      /// <param name="entity"></param>
      /// <returns></returns>

      public Color this[Entity entity] => GetEffectiveColor(entity);

      /// <summary>
      /// The viewport that's used to compute the effective
      /// color, is the viewport whose ObjectId is provided,
      /// or the viewport that's active at the point when this 
      /// method is called without a viewport argument.
      /// </summary>
      /// <param name="entity"></param>
      /// <param name="vportId"></param>
      /// <returns></returns>

      public Color GetEffectiveColor(Entity entity, ObjectId vportId = default(ObjectId))
      {
         if(entity.ColorIndex != 256)
            return entity.Color;
         Document doc = Application.DocumentManager.MdiActiveDocument;
         if(doc == null)
            return defaultMap[entity];
         if(vportId.IsNull)
            vportId = doc.Editor.CurrentViewportObjectId;
         if(vportId.IsNull)
            return defaultMap[entity];
         AcRx.ErrorStatus.WrongObjectType.Requires<Viewport>(vportId);
         EffectiveColorMap vportMap;
         if(!maps.TryGetValue(vportId, out vportMap))
            maps[vportId] = vportMap = new EffectiveColorMap(vportId);
         return vportMap[entity];
      }
   }


   public static class EffectiveColorTestCommands
   {
      /// <summary>
      /// Example usage of EffectiveColorMap
      /// 
      /// This command repeatedly prompts you to select
      /// an entity, and displays its 'effective' color
      /// in the viewport that was active when the
      /// command was issued. 
      /// 
      /// See the VPORTEFFECTIVECOLOR example below, which
      /// calculates the effective color of an entity in
      /// any viewport, using the viewport that was active 
      /// when each entity is selected.
      /// </summary>


      [CommandMethod("EFFECTIVECOLOR")]
      public static void GetEffectiveColor()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var effectiveColors = new EffectiveColorMap(ed.ActiveViewportId);
         using(var tr = new DocumentTransaction(doc))
         {
            tr.IsReadOnly = true;
            while(true)
            {
               var per = ed.GetEntity("\nSelect an entity: ");
               if(per.Status != PromptStatus.OK)
                  return;
               Entity entity = tr.GetObject<Entity>(per.ObjectId);
               var color = effectiveColors[entity];
               ed.WriteMessage($"\nEffective color: {color.ColorNameForDisplay}");
            }
         }
      }

      /// <summary>
      /// Example use of EffectiveColors
      /// 
      /// Demonstrates the EffectiveColors class that computes
      /// the effective color of an entity in the active viewport.
      /// The viewport that's active when the entity is selected
      /// is used to compute the entity's effective color.
      /// </summary>

      [CommandMethod("VPORTEFFECTIVECOLOR")]
      public static void GetEffectiveColorInActiveViewport()
      {
         Document doc = Application.DocumentManager.MdiActiveDocument;
         Editor ed = doc.Editor;
         var effectiveColors = EffectiveColors.Instance;
         using(var tr = new DocumentTransaction(doc))
         {
            tr.IsReadOnly = true;
            while(true)
            {
               var per = ed.GetEntity("\nSelect an entity: ");
               if(per.Status != PromptStatus.OK)
                  return;
               Entity entity = tr.GetObject<Entity>(per.ObjectId);
               var color = effectiveColors[entity];
               ed.WriteMessage($"\nEffective color: {color.ColorNameForDisplay}");
            }
         }
      }
   }

}



