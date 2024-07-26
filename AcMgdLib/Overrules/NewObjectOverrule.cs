
using Autodesk.AutoCAD.Runtime;

/// NewObjectOverrule.cs
/// 
/// Activist Investor / Tony T
/// 
/// Distributed under the terms of the MIT license

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// This class can be used to gain access to newly-created
   /// objects at the point just before/after they are appended
   /// to the database. 
   /// 
   /// It can be used in lieu of NewObjectCollection when the
   /// derived type only needs to gain access to objects at
   /// the points when it is about to be closed, or after it is
   /// closed, and doesn't require the ObjectIds of those objects
   /// to be collected, as is done by NewObjectCollection.
   /// 
   /// The advantage of this class is that it has far-less 
   /// overhead than NewObjectCollection.
   /// </summary>
   /// <typeparam name="T">The type of DBObjects that are to be
   /// observed by an instance.</typeparam>

   public class NewObjectOverrule<T> : ObjectOverrule<T> where T: DBObject
   {
      public NewObjectOverrule(bool enabled = true) : base(enabled)
      {
         SetCustomFilter();
      }

      public override bool IsApplicable(RXObject subject)
      {
         return subject is T obj && obj.IsNewObject;
      }

      public override void Close(DBObject dbObject)
      {
         T subject = dbObject as T;
         bool flag = subject != null && subject.IsNewObject;
         if(flag)
            OnClosing(subject);
         base.Close(dbObject);
         if(flag)
            OnClosed(subject);
      }

      /// <summary>
      /// Only called when the object that is about to be
      /// closed is a new object. The ObjectId of the new
      /// object is not available from this override.
      /// </summary>

      protected virtual void OnClosing(T obj)
      {
      }

      /// <summary>
      /// Only called when the object that was closed is
      /// a new object. The ObjectId of the new object is 
      /// available from this override.
      /// </summary>

      protected virtual void OnClosed(T obj)
      {
      }

   }
  
}
