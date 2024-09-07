using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using AcRx = Autodesk.AutoCAD.Runtime;
using AcBr = Autodesk.AutoCAD.BoundaryRepresentation;
using AcGe = Autodesk.AutoCAD.Geometry;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

using Assert = System.Diagnostics.Assert;

namespace Autodesk.AutoCAD.DatabaseServices
{
   public static class AccurateExtentsExtension
   {
      /// <summary>
      /// Get accurate MText extents
      /// </summary>
      /// <param name="mtext"></param>
      /// <returns></returns>

      public static Extents3d GetGeometricExtents(this MText mtext)
      {
         if(mtext.ColumnType != ColumnType.NoColumns && mtext.ColumnCount > 1)
         {
            Extents3d extents = new Extents3d();
            using(DBObjectList<Entity> ents = mtext.TryExplode())
            {
               foreach(MText mtext2 in ents.OfType<MText>())
               {
                  extents.AddExtents(GetTrueMTextExtents(mtext2));
               }
            }
            if(extents != new Extents3d())
               return extents;
         }
         return GetTrueMTextExtents(mtext);
      }

      /// <summary>
      /// Get accurate extents of MInsertBlock
      /// </summary>

      static Extents3d GetGeometricExtents(this MInsertBlock block)
      {
         Extents3d extents = block.TryGetGeometricExtentsBestFit();
         if(block.Columns != 1 && block.Rows != 1)
         {
            double width = block.ColumnSpacing * (block.Columns - 1);
            double height = block.RowSpacing * (block.Rows - 1);
            CoordinateSystem3d matrix = block.BlockTransform.CoordinateSystem3d;
            extents.ExpandBy((height * matrix.Yaxis.GetNormal()) + (width * matrix.Xaxis.GetNormal()));
         }
         return extents;
      }

      /// <summary>
      /// Get accurate/best-fit extents of BlockReference
      /// </summary>

      public static Extents3d TryGetGeometricExtentsBestFit(this BlockReference blkref)
      {
         Assert.IsNotNull(blkref, "blockReference");
         try
         {
            return blkref.GeometryExtentsBestFit();
         }
         catch(AcRx.Exception ex)
         {
            ex.Allow(AcRx.ErrorStatus.InvalidInput, AcRx.ErrorStatus.InvalidExtents);
         }
         return blkref.GeometricExtents;
      }

      static Extents3d GetTrueMTextExtents(MText mtext)
      {
         if(mtext == null)
            throw new ArgumentNullException("mtext");
         Extents3d extents = new Extents3d();
         double width = mtext.ActualWidth;
         double height = mtext.ActualHeight;
         Point3d pos = mtext.Location;
         extents = extents.AddPoints(
            pos,
            new Point3d(pos.X, pos.Y - height, pos.Z),
            new Point3d(pos.X + width, pos.Y, pos.Z),
            new Point3d(pos.X + width, pos.Y - height, pos.Z));
         if(!mtext.Rotation.IsZero())
            extents.TransformBy(Matrix3d.Rotation(mtext.Rotation, mtext.Normal, pos));
         return extents;
      }

      public static IEnumerable<Entity> SetPropertiesFrom(this DBObjectCollection entities, Entity source)
      {
         Assert.IsNotNull(entities, "entities");
         Assert.IsNotNull(source, "source");
         return SetPropertiesFrom(entities.OfType<Entity>(), source);
      }

      public static IEnumerable<Entity> SetPropertiesFrom(this IEnumerable<Entity> entities, Entity source)
      {
         Assert.IsNotNull(entities, "entities");
         Assert.IsNotNull(source, "source");
         foreach(Entity ent in entities)
         {
            ent.SetPropertiesFrom(source);
            yield return ent;
         }
      }
   }

   public class MTextExtentsOverrule : GeometryOverrule
   {
      bool disposed = false;
      public MTextExtentsOverrule()
      {
         AddOverrule(GetClass(typeof(MText)), this, true);
      }

      protected override void Dispose(bool disposing)
      {
         if(!disposed)
         {
            disposed = true;
            RemoveOverrule(GetClass(typeof(MText)), this);
         }
         base.Dispose(disposing);
      }

      public override Extents3d GetGeomExtents(Entity entity)
      {
         if(AccurateExtents.Enabled)
         {
            return ((MText)entity).GetGeometricExtents();
         }
         return base.GetGeomExtents(entity);
      }
   }

   public class MInsertBlockExtentsOverrule : GeometryOverrule
   {
      bool disposed = false;

      public MInsertBlockExtentsOverrule()
      {
         AddOverrule(GetClass(typeof(MInsertBlock)), this, true);
      }

      protected override void Dispose(bool disposing)
      {
         if(!disposed)
         {
            disposed = true;
            RemoveOverrule(GetClass(typeof(MInsertBlock)), this);
         }
         base.Dispose(disposing);
      }

      public override Extents3d GetGeomExtents(Entity entity)
      {
         Extents3d extents = base.GetGeomExtents(entity);
         if(AccurateExtents.Enabled)
         {
            MInsertBlock block = entity as MInsertBlock;
            if(block != null && block.Columns > 1 || block.Rows > 1)
            {
               double width = block.ColumnSpacing * (block.Columns - 1);
               double height = block.RowSpacing * (block.Rows - 1);
               CoordinateSystem3d cs = block.BlockTransform.CoordinateSystem3d;
               extents.ExpandBy((height * cs.Yaxis.GetNormal()) + (width * cs.Xaxis.GetNormal()));
            }
         }
         return extents;
      }
   }

   public class BlockReferenceExtentsOverrule : GeometryOverrule
   {
      bool disposed = false;

      public BlockReferenceExtentsOverrule()
      {
         AddOverrule(GetClass(typeof(BlockReference)), this, true);
      }

      protected override void Dispose(bool disposing)
      {
         if(!disposed)
         {
            disposed = true;
            RemoveOverrule(GetClass(typeof(BlockReference)), this);
         }
         base.Dispose(disposing);
      }

      public override Extents3d GetGeomExtents(Entity entity)
      {
         if(AccurateExtents.Enabled && !(entity is Table))
         {
            BlockReference blkref = (BlockReference)entity;
            try
            {
               return blkref.GeometryExtentsBestFit();
            }
            catch(AcRx.Exception ex)
            {
               if(ex.ErrorStatus != AcRx.ErrorStatus.InvalidInput
                  && ex.ErrorStatus != AcRx.ErrorStatus.InvalidExtents)
                  throw;
            }
         }
         return base.GetGeomExtents(entity);
      }

   }

   /// <summary>
   /// A global switch that enables/disables the above three
   /// GeometryOverrules used to compute more accurate extents
   /// for MText, MinsertBlocks, and BlockReferences. The value
   /// is false by default, and must be enabled. It is highly-
   /// recommended that the value is enabled only while it is
   /// absolutely necessary, as the overhead of computing more-
   /// accurate geometric extents can be significant.
   /// </summary>

   public static class AccurateExtents
   {
      static bool enabled = false;
      static Overrule[] overrules = new Overrule[3];

      /// <summary>
      /// Enables or disables accurate extents computation
      /// </summary>

      public static bool Enabled
      {
         get
         {
            return enabled;
         }
         set
         {
            SetEnabled(value);
         }
      }

      /// <summary>
      /// Temporarily enables or disables accurate extents 
      /// computation.
      /// </summary>
      /// <remarks>The Enable() method presumes that accurate 
      /// extents computation is currently not enabled when it 
      /// is called without an argument. If accurate extents 
      /// computation is already enabled when Enable() is called, 
      /// it will remain enabled after the object returned by 
      /// Enable() is disposed.
      /// 
      /// The Enable() method should be used as shown in the 
      /// following example pattern:
      /// 
      /// <example>
      /// 
      /// public static void UsingAccurateExtentsExample()
      /// {
      ///    // at this point accurate extents 
      ///    // computation is not enabled.
      ///    
      ///    using(AccurateExtents.Enable())
      ///    {
      ///	    // Accurate extents computation is enabled, 
      ///	    // perform operations requiring accurate extents 
      ///	    // computation here.
      ///    }
      ///    
      ///    // at this point accurate extents 
      ///    // computation is no-longer enabled.
      /// }
      /// 
      /// </example>
      /// </remarks>
      /// <param name="value">An optional boolean indicating 
      /// if accurate extents computation should be temporarily 
      /// enabled or temporarily disabled. If true, accurate 
      /// extents computation is temporarily enabled. If this
      /// argument is not provided, true is inferred.</param>
      /// <returns>An object that when disposed restores 
      /// the previous value of Enabled</returns>

      public static IDisposable Enable(bool value = true)
      {
         return new Scope(value);
      }

      /// Non-public members

      static void SetEnabled(bool value)
      {
         if(enabled ^ value)
         {
            if(enabled = value)
            {
               overrules[0] = new MTextExtentsOverrule();
               overrules[1] = new MInsertBlockExtentsOverrule();
               overrules[2] = new BlockReferenceExtentsOverrule();
               Autodesk.AutoCAD.ApplicationServices.Application.BeginQuit += beginQuit;
            }
            else
            {
               for(int i = 0; i < overrules.Length; i++)
               {
                  overrules[i].Dispose();
                  overrules[i] = null;
               }
               Application.BeginQuit -= beginQuit;
            }
         }
      }

      static void beginQuit(object sender, EventArgs e)
      {
         Enabled = false;
      }

      class Scope : IDisposable
      {
         bool oldvalue;
         bool disposed = false;

         public Scope(bool value)
         {
            this.oldvalue = Enabled;
            Enabled = value;
         }

         public void Dispose()
         {
            if(!disposed)
            {
               disposed = true;
               Enabled = this.oldvalue;
            }
         }
      }

   }
}
