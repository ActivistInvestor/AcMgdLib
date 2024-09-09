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
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices
{
   public static class AccurateExtentsExtension
   {
      /// <summary>
      /// Get accurate MText extents (multi-column MTEXT
      /// seems to have issues with extents calculation).
      /// </summary>
      /// <param name="mtext"></param>
      /// <returns></returns>

      public static Extents3d GetGeometricExtents(this MText mtext)
      {
         Assert.IsNotNullOrDisposed(mtext);
         if(mtext.ColumnType != ColumnType.NoColumns && mtext.ColumnCount > 1)
         {
            Extents3d extents = new Extents3d();
            using(var ents = TryExplode(mtext))
            {
               foreach(DBObject obj in ents) using(obj)
               {
                  if(obj is MText mtext2)
                     extents.AddExtents(GetMTextExtents(mtext2));
               }
            }
            return extents;
         }
         return GetMTextExtents(mtext);
      }

      static Extents3d GetMTextExtents(MText mtext)
      {
         Assert.IsNotNullOrDisposed(mtext);
         Extents3d extents = new Extents3d();
         double width = mtext.ActualWidth;
         double height = mtext.ActualHeight;
         Point3d pos = mtext.Location;
         extents.AddPoint(pos);
         extents.AddPoint(new Point3d(pos.X, pos.Y - height, pos.Z));
         extents.AddPoint(new Point3d(pos.X + width, pos.Y, pos.Z));
         extents.AddPoint(new Point3d(pos.X + width, pos.Y - height, pos.Z));
         if(!mtext.Rotation.IsEqualTo(0.0))
            extents.TransformBy(Matrix3d.Rotation(mtext.Rotation, mtext.Normal, pos));
         return extents;
      }

      static bool IsDefault(Extents3d ext)
      {
         return ext.IsEqualTo(default(Extents3d));
      }

      static DBObjectCollection TryExplode(this Entity entity)
      {
         Assert.IsNotNullOrDisposed(entity);
         DBObjectCollection coll = new DBObjectCollection();
         try
         {
            entity.Explode(coll);
         }
         catch(AcRx.Exception ex) when(ex.IsExplodeError())
         {
         }
         return coll;
      }

      public static bool IsExplodeError(this AcRx.Exception ex)
      {
         return ex.ErrorStatus == AcRx.ErrorStatus.NotApplicable
            || ex.ErrorStatus == ErrorStatus.CannotExplodeEntity
            || ex.ErrorStatus == ErrorStatus.CannotScaleNonUniformly;
      }

      /// <summary>
      /// Tries to get accurate/best-fit extents of BlockReference,
      /// including MInserted blocks.
      /// 
      /// This API follows the pattern used by TryGetBounds().
      /// It returns ErrorStatus.OK on success, or another
      /// ErrorStatus on failure.
      /// </summary>

      public static AcRx.ErrorStatus TryGetGeomExtentsBestFit(this BlockReference blkref, out Extents3d result)
      {
         Assert.IsNotNull(blkref, "blockReference");
         try
         {
            result = blkref.GeometryExtentsBestFit();
            if(blkref is MInsertBlock mib && mib.Columns != 1 && mib.Rows != 1)
               result = AddMInsertExtents(mib, result);
            return AcRx.ErrorStatus.OK;
         }
         catch(AcRx.Exception ex) when(ex.IsBoundsError())
         {
            result = default(Extents3d);
            return ex.ErrorStatus;
         }
      }

      static Extents3d AddMInsertExtents(MInsertBlock blk, Extents3d input)
      {
         double width = blk.ColumnSpacing * (blk.Columns - 1);
         double height = blk.RowSpacing * (blk.Rows - 1);
         var ecs = blk.BlockTransform.CoordinateSystem3d;
         input.ExpandBy((height * ecs.Yaxis.GetNormal()) + (width * ecs.Xaxis.GetNormal()));
         return input;
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
            if(entity is BlockReference blkref && !(blkref is Table))
               return blkref.TryGetGeomExtentsBestFit(out result);
            result = entity.GeometricExtents;
            return AcRx.ErrorStatus.OK;
         }
         catch(AcRx.Exception ex) when(ex.IsBoundsError())
         {
            result = default(Extents3d);
            return ex.ErrorStatus;
         }
      }

      /// <summary>
      /// Indicates if the Exception's ErrorStatus is one of those
      /// thrown by GeometricExtents and GeometryExtentsBestFit():
      /// </summary>
      /// <param name="ex">The exception that was thrown</param>
      /// <returns>A value indicating if the ErrorStatus is one
      /// that is thrown by methods that compute an extents.
      /// </returns>

      public static bool IsBoundsError(this AcRx.Exception ex)
      {
         var es = ex.ErrorStatus;
         return es == AcRx.ErrorStatus.InvalidExtents
            || es == AcRx.ErrorStatus.NullExtents
            || es == AcRx.ErrorStatus.NotApplicable
            || es == AcRx.ErrorStatus.CannotScaleNonUniformly;
      }

      public static bool IsOk(this AcRx.ErrorStatus es)
      {
         return es == AcRx.ErrorStatus.OK;
      }

      /// <summary>
      /// Computes the geometric extents of a sequence of entities,
      /// with optional extended accuracy.
      /// </summary>
      /// <param name="entities">The sequence of entities whose extents
      /// is to be calculated</param>
      /// <param name="accurate">A value indicating if accurate extents 
      /// computation should be used for MText, MInsertBlocks, and BlockReferences</param>
      /// <returns></returns>

      public static Extents3d GetGeometricExtents(this IEnumerable<Entity> entities, bool accurate = true)
      {
         Assert.IsNotNull(entities, "entities");
         Extents3d extents = new Extents3d();
         Extents3d result;
         using(var e = entities.GetEnumerator())
         {
            if(e.MoveNext())
            {
               using(AccurateExtents.Enable(accurate))
               {
                  Entity ent = e.Current;
                  if(ent != null && ent.TryGetBounds(out result).IsOk())
                     extents = result;
                  while(e.MoveNext())
                  {
                     ent = e.Current;
                     if(ent != null && ent.TryGetBounds(out result).IsOk())
                        extents.AddExtents(result);
                  }
               }
            }
         }
         return extents;
      }

      /// <summary>
      /// Similar to the above overload, but also accepts
      /// an Action that's called when an attempt to obtain
      /// an entity's extents fails. 
      /// </summary>

      public static Extents3d GetGeometricExtents<T>(this IEnumerable<T> entities,
         Action<T, AcRx.ErrorStatus> error,
         bool accurate = true) where T : Entity
      {
         Assert.IsNotNull(entities);
         Assert.IsNotNull(error);
         Extents3d extents = new Extents3d();
         Extents3d result;
         using(var e = entities.GetEnumerator())
         {
            if(e.MoveNext())
            {
               using(AccurateExtents.Enable(accurate))
               {
                  T ent = e.Current;
                  if(ent != null)
                  {
                     var es = ent.TryGetBounds(out result);
                     if(!es.IsOk())
                        error(ent, es);
                     else
                        extents = result;
                  }
                  while(e.MoveNext())
                  {
                     ent = e.Current;
                     if(ent != null)
                     {
                        var es = ent.TryGetBounds(out result);
                        if(es.IsOk())
                           extents.AddExtents(result);
                        else
                           error(ent, es);
                     }
                  }
               }
            }
         }
         return extents;
      }
   }

   public class MTextExtentsOverrule : GeometryOverrule<MText>
   {
      public override Extents3d GetGeomExtents(Entity entity)
      {
         if(AccurateExtents.Enabled)
         {
            try
            {
               return ((MText)entity).GetGeometricExtents();
            }
            catch(AcRx.Exception ex) when(ex.IsBoundsError())
            {
            }
         }
         return base.GetGeomExtents(entity);
      }
   }

   public class BlockReferenceExtentsOverrule : GeometryOverrule<BlockReference>
   {
      public override Extents3d GetGeomExtents(Entity entity)
      {
         if(AccurateExtents.Enabled && IsTrueBlockReference(entity))
         {
            BlockReference blkref = (BlockReference)entity;
            Extents3d result;
            if(blkref.TryGetGeomExtentsBestFit(out result).IsOk())
               return result;
         }
         return base.GetGeomExtents(entity);
      }

      static bool IsTrueBlockReference(DBObject obj)
      {
         return obj != null
            && !DBObject.IsCustomObject(obj.Id)
            && (obj is BlockReference)
            && !(obj is Table);
      }

   }


   /// <summary>
   /// A class that manages the above GeometryOverrules that 
   /// are used to compute more accurate extents for MText, 
   /// MinsertBlocks, and BlockReferences. The Enabled property
   /// is false by default, and must be enabled. It is highly-
   /// recommended that the value is enabled only while it is
   /// absolutely necessary, as the overhead of computing more-
   /// accurate geometric extents can be significant.
   /// </summary>

   public static class AccurateExtents
   {
      static bool enabled => mTextOverrule != null && blockRefOverrule != null;
      static BlockReferenceExtentsOverrule blockRefOverrule;
      static MTextExtentsOverrule mTextOverrule;

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
         return new Enabler(value);
      }

      /// Non-public members

      static void SetEnabled(bool value)
      {
         if(enabled ^ value)
         {
            if(value)
            {
               mTextOverrule = new MTextExtentsOverrule();
               blockRefOverrule = new BlockReferenceExtentsOverrule();
            }
            else
            {
               mTextOverrule?.Dispose();
               mTextOverrule = null;
               blockRefOverrule?.Dispose();
               blockRefOverrule = null;
            }
         }
      }

      class Enabler : IDisposable
      {
         bool oldvalue;
         bool disposed = false;

         public Enabler(bool value)
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
