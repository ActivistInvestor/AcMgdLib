/// GeometryOverrule.cs 
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.


using Autodesk.AutoCAD.Runtime;
using System.Reflection;
using System.Linq;

namespace Autodesk.AutoCAD.DatabaseServices
{
   /// <summary>
   /// A common base type for GeometryOverrule-based types.
   /// 
   /// This common base type targets a single runtime class,
   /// and automates adding and removing the overrule from
   /// the class, provides a means to enable or disable the 
   /// overrule, and automatically removes the overrule from
   /// the runtime class when an instance is disposed.
   /// 
   /// You can derive your GeometryOverrule from this type and
   /// avoid the need to manually add/remove the overrule from 
   /// the target runtime class.
   /// 
   /// Note: Due to a bug in AutoCAD's Overrule API, you can't
   /// enable or disable an instance of this type from within 
   /// one of the GeometryOverrule overrides. Doing that will 
   /// crash AutoCAD.
   /// </summary>
   /// <typeparam name="T">The targeted managed wrapper type</typeparam>

   public class GeometryOverrule<T> : GeometryOverrule where T:Entity
   {
      bool enabled = false;
      bool isDisposing = false;

      /// <summary>
      /// Indicates if the instance overrides at least one
      /// virtual method of the GeometryOverrule base type.
      /// </summary>

      protected readonly bool IsOverruled;

      protected static readonly RXClass targetClass = RXClass.GetClass(typeof(T));

      public GeometryOverrule(bool enabled = true)
      {
         IsOverruled = isOverrule;
         Enabled = enabled;
      }

      public virtual bool Enabled
      {
         get
         {
            return this.enabled;
         }
         set
         {
            if(IsOverruled && this.enabled ^ value)
            {
               this.enabled = value;
               if(value)
                  AddOverrule(targetClass, this, true);
               else
                  RemoveOverrule(targetClass, this);
               OnEnabledChanged(this.enabled);
            }
         }
      }

      /// <summary>
      /// Can be overridden in derived types to be notified
      /// when the enabled state of the instance changes.
      /// 
      /// The IsDisposing property can be used to detect if
      /// the enabled state is changing because the instance
      /// is being disposed.
      /// </summary>
      /// <param name="enabled">True if the instance was enabled</param>

      protected virtual void OnEnabledChanged(bool enabled)
      {
      }

      protected bool IsDisposing => isDisposing;

      protected override void Dispose(bool disposing)
      {
         if(disposing)
         {
            isDisposing = true;
            Enabled = false;
         }
         base.Dispose(disposing);
      }

      /// <summary>
      /// This property indicates if the instance overrides
      /// any virtual method of the GeometryOverrule base type. 
      /// If it doesn't, overrulling is entirely pointless 
      /// and will not be enabled.
      /// </summary>

      bool isOverrule
      {
         get
         {
            return this.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
               .Any(m => m.IsVirtual && m.GetBaseDefinition().DeclaringType == typeof(GeometryOverrule)
                  && m.DeclaringType != typeof(GeometryOverrule));
         }
      }


   }
}
