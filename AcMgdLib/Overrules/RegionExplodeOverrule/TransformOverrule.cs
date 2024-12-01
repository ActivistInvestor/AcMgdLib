/// ObjectOverrule.cs  
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under terms of the MIT license.

using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices
{
   public abstract class TransformOverrule<T> : TransformOverrule where T : Entity
   {
      bool enabled = false;
      static RXClass targetClass = RXObject.GetClass(typeof(T));
      bool isDisposing = false;

      public TransformOverrule(bool enabled = true)
      {
         this.IsOverruling = enabled;
      }

      /// <summary>
      /// This property can be used to enable/disable
      /// overruling for the instance. 
      /// 
      /// The static Overruling property of the Overrule
      /// base type does nothing (it was disabled because
      /// it was enabling/disabling all overrules, rather
      /// than a specific overrule).
      /// 
      /// </summary>

      public virtual bool IsOverruling
      {
         get
         {
            return this.enabled;
         }
         set
         {
            if(this.enabled ^ value)
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

      protected virtual void OnEnabledChanged(bool enabled)
      {
         // AcConsole.ReportThis(this, enabled);
      }

      protected bool IsDisposing => isDisposing;

      protected override void Dispose(bool disposing)
      {
         if(disposing)
         {
            isDisposing = true;
            IsOverruling = false;
         }
         base.Dispose(disposing);
      }
   }





}
