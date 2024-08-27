/// DeepCloneOverrule.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using System;
using System.Diagnostics.Extensions;
using System.Linq;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A specialization of ObjectOverrule that provides an entry
   /// point for operating on cloned objects at the point when
   /// the objects are cloned. This class can eliminate the need
   /// to subsequently open newly-created clones to perform various
   /// operations on them. The constructor accepts a delegate that 
   /// will be called at the point when an object has been cloned, 
   /// and it is a primary clone. 
   /// 
   /// The delegate is called after the clone has been added to
   /// to its owner and to the Database.
   /// 
   /// Note that the generic argument type can be any type, but 
   /// this overrule will only be called for cloned objects that 
   /// are instances of the generic argument type, regardless of 
   /// what other type(s) of objects are also cloned.
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class DeepCloneOverrule<T> : ObjectOverrule<T> where T : DBObject
   {
      Action<T, T> action;
      ObjectId ownerId;

      /// <summary>
      /// Accepts an Action<T, T> that is passed each source object 
      /// and the clone of the source object. The action can perform 
      /// operations on both the Source object and the clone of it.
      /// 
      /// The Source object will be open for read, and the clone will
      /// be open for write. 
      /// </summary>
      /// <param name="action">The delegate that accepts the Source
      /// object and its clone of the given generic argument type.
      /// If objects that are not of the generic argument type are 
      /// also cloned, this method is not called for those objects.
      /// </param>
      /// <param name="ownerId">The ObjectId of the owner which the
      /// clones are to belong to. If this argument is ObjectId.Null,
      /// there is no filtering by owner.</param>

      public DeepCloneOverrule(Action<T, T> action)
         : this(ObjectId.Null, action)
      {
      }

      /// <summary>
      /// A derived type can supermessage this constructor
      /// and provide a null Action, since derived types will
      /// usually override OnCloned() to do the equivalent of
      /// what the Action does. Instances of this type must
      /// supply an Action argument.
      /// </summary>
      /// <param name="ownerId">The ObjectId of the new
      /// owner of the cloned objects. This is the same
      /// value that is supplied to the DeepCloneObjects()
      /// method's owner argument. 
      /// 
      /// If a value is supplied, the OnCloned() method and 
      /// the Action delegate are called only if the cloned 
      /// object is owned by the specified owner.
      /// 
      /// If this value is not provided or is ObjectId.Null, 
      /// there is no filtering by ownership.</param>
      /// <param name="action">An Action that must accept
      /// two arguments of the generic argument type T.
      /// The first argument is the Source object that was
      /// cloned. The second argument is the clone of the 
      /// Source object. The Source object is read-enabled,
      /// and the clone is write-enabled. The delegate that
      /// is psssed as this argument should not cache any
      /// reference to its arguments, as they are no longer
      /// be usable once the delegate returns.
      /// </param>
      
      public DeepCloneOverrule(ObjectId ownerId, Action<T, T> action)
         : base(true)
      {
         if(this.GetType() == typeof(DeepCloneOverrule<T>))
            Assert.IsNotNull(action, nameof(action));
         this.action = action;
         this.ownerId = ownerId;
      }

      public override DBObject DeepClone(DBObject dbObj, DBObject owner, IdMapping idMap, bool isPrimary)
      {
         DBObject result = base.DeepClone(dbObj, owner, idMap, isPrimary);
         if(isPrimary)
            Clone(dbObj, result, owner, idMap);
         return result;
      }

      public override DBObject WblockClone(DBObject dbObject, RXObject ownerObject, IdMapping idMap, bool isPrimary)
      {
         DBObject result = base.WblockClone(dbObject, ownerObject, idMap, isPrimary);
         if(isPrimary && ownerObject is DBObject owner)
            Clone(dbObject, result, owner, idMap);
         return result;
      }

      void Clone(DBObject src, DBObject result, DBObject owner, IdMapping idMap)
      {
         if(ownerId.IsNull || owner.ObjectId == ownerId)
         {
            if(src is T source && result is T clone)
            {
               OnCloned(source, clone);
            }
         }
      }

      /// <summary>
      /// Called for each object that is cloned, and 
      /// passed the Source and the clone of the Source.
      /// 
      /// When called, the Source object is read-enabled
      /// and the clone is write-enabled. The Source can
      /// be upgraded to OpenMode.ForWrite and modified 
      /// if needed.
      /// 
      /// Do not cache references to the arguments, as
      /// they will be unusable after this method has
      /// returned.
      /// 
      /// The default implementation of this method 
      /// calls the Action supplied to the constructor.
      /// 
      /// In derived types, an override of this method
      /// serves the same basic purpose as the action, so 
      /// overrides shouldn't need to super-message this. 
      /// </summary>
      /// <param name="source">The Source object that 
      /// has been cloned, open for read.</param>
      /// <param name="clone">The clone of the Source
      /// object, open for write</param>

      protected virtual void OnCloned(T source, T clone)
      {
         action?.Invoke(source, clone);
      }
   }
}
