using Autodesk.AutoCAD.Runtime;

/// ObjectOverrule.cs  ActivistInvestor / Tony T.
/// 
/// Source: https://github.com/ActivistInvestor/Overrules/blob/main/ObjectOverrule.cs

namespace Autodesk.AutoCAD.DatabaseServices
{
   /// <summary>
   /// A common base type for ObjectOverrule-based types.
   /// 
   /// This common base type targets a single runtime class,
   /// and automates adding and removing the overrule from
   /// the class, provides a means to enable or disable the 
   /// overrule, and automatically removes the overrule from
   /// the runtime class when an instance is disposed.
   /// 
   /// You can derive your ObjectOverrule from this type and
   /// avoid the need to to manually add/remove the overrule 
   /// from the runtime class.
   /// 
   /// Note: Due to a bug in AutoCAD, you cannot enable or
   /// disable an instance of this type from within one of 
   /// the ObjectOverrule overrides. Doing that will crash
   /// AutoCAD.
   /// </summary>
   /// <typeparam name="T">The targeted managed wrapper type</typeparam>

   public abstract class ObjectOverrule<T> : ObjectOverrule where T : DBObject
   {
      bool enabled = false;
      bool isDisposing = false;
      protected static readonly RXClass rxclass = RXClass.GetClass( typeof( T ) );

      public ObjectOverrule( bool enabled = true )
      {
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
            if( this.enabled ^ value )
            {
               this.enabled = value;
               if( value )
                  AddOverrule( rxclass, this, true );
               else
                  RemoveOverrule( rxclass, this );
               OnEnabledChanged( this.enabled );
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
      
      protected virtual void OnEnabledChanged( bool enabled )
      {
      }

      protected bool IsDisposing => isDisposing;

      protected override void Dispose( bool disposing )
      {
         if(disposing)
         {
            isDisposing = true;
            Enabled = false;
         }
         base.Dispose( disposing );
      }
   }
  
}
