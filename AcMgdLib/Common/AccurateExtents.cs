/// AccurateExtents.cs 
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

/// This file has not yet been fully-integrated into
/// AcMgdLib, and as such it should be regarded as a
/// 'Preview' distribution, with additional changes
/// and additions pending.

using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;
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
            using(var ents = TryExplode(mtext))
            {
               foreach(DBObject obj in ents)
               {
                  try
                  {
                     if(obj is MText mtext2)
                        extents.AddExtents(GetTrueMTextExtents(mtext2));
                  }
                  finally
                  {
                     obj.Dispose();
                  }
               }
            }
            if(extents != new Extents3d())
               return extents;
         }
         return GetTrueMTextExtents(mtext);
      }

      static DBObjectCollection TryExplode(this Entity entity)
      {
         Assert.IsNotNull(entity, "entity");
         DBObjectCollection coll = new DBObjectCollection();
         try
         {
            entity.Explode(coll);
         }
         catch(AcRx.Exception ex) when(ex.ErrorStatus == AcRx.ErrorStatus.NotApplicable)
         {
         }
         return coll;
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
         if(!mtext.Rotation.IsEqualTo(0.0))
            extents.TransformBy(Matrix3d.Rotation(mtext.Rotation, mtext.Normal, pos));
         return extents;
      }

      /// <summary>
      /// Tries to retrieve an entity's GeometricExtents, and
      /// catches exceptions thrown by the call to that property. 
      /// 
      /// If the call fails, the result is the ErrorStatus that
      /// was thrown. 
      /// 
      /// If the call succeeds, the result is ErrorStatus.OK.
      /// 
      /// This API is intended to serve use cases where how to
      /// deal with failures to get geometric extents depends on
      /// how the extents is to be used, which can vary based on 
      /// each use case.
      /// </summary>
      /// <param name="entity">The Entity whose extents is being
      /// requested</param>
      /// <param name="result">The output Extents3d</param>
      /// <returns>ErrorStatus.OK if the call succeeds, or
      /// another ErrorStatus value returned by an exception.</returns>
      
      public static AcRx.ErrorStatus TryGetBounds(this Entity entity, out Extents3d result)
      {
         Assert.IsNotNull(entity);
         if(entity is Ray or Xline)
         {
            result = default(Extents3d);
            return AcRx.ErrorStatus.NotApplicable;
         }
         try
         {
            result = entity.GeometricExtents;
            return AcRx.ErrorStatus.OK;
         }
         catch(AcRx.Exception ex) when (ex.IsGeomExtentsError())
         {
            result = default(Extents3d);
            return ex.ErrorStatus;
         }
      }

      public static bool IsOk(this AcRx.ErrorStatus es)
      {
         return es == AcRx.ErrorStatus.OK;
      }

      /// <summary>
      /// Filters for all known ErrorStatus values thrown by
      /// GeometricExtents and GeometryExtentsBestFit():
      /// </summary>
      /// <param name="ex">The exception that was thrown</param>
      /// <returns>A value indicating if the error status is one
      /// that is thrown by the methods that obtain an extents.
      /// </returns>

      public static bool IsGeomExtentsError(this AcRx.Exception ex)
      {
         var es = ex.ErrorStatus;
         return es == AcRx.ErrorStatus.InvalidExtents
            || es == AcRx.ErrorStatus.NullExtents
            || es == AcRx.ErrorStatus.NotApplicable
            || es == AcRx.ErrorStatus.CannotScaleNonUniformly;
      }

      /// <summary>
      /// Computes the geometric extents of a sequence of entities,
      /// with optional extended accuracy.
      /// </summary>
      /// <param name="entities"></param>
      /// <param name="useAccurateExtents">A value indicating if accurate extents 
      /// computation is to be used for MText, MInsertBlocks, and BlockReferences</param>
      /// <returns></returns>

      public static Extents3d GetGeometricExtents(this IEnumerable<Entity> entities, bool useAccurateExtents = true)
      {
         Assert.IsNotNull(entities, "entities");
         Extents3d extents = new Extents3d();
         Extents3d result;
         using(var e = entities.GetEnumerator())
         {
            if(e.MoveNext())
            {
               using(AccurateExtents.Enable(useAccurateExtents))
               {
                  Entity ent = e.Current;
                  if(ent != null)
                  {
                     if(ent.TryGetBounds(out result).IsOk())
                        extents = result;
                  }
                  while(e.MoveNext())
                  {
                     ent = e.Current;
                     if(ent != null)
                     {
                        if(ent.TryGetBounds(out result).IsOk())
                           extents.AddExtents(result);
                     }
                  }
               }
            }
         }
         return extents;
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
