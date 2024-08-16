/// ObservableNewObjectCollection.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime.Extensions;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A specialization of NewObjectCollection<T> that can notify observers
   /// that items have been added to the collection. The notification does
   /// not fire for every addition. It will not fire until the Application
   /// reaches the Idle state, which is after one or more elements have been
   /// added while the application was busy. It is recommended that observers
   /// clear the collection when handling the CollectionChanged event, so that
   /// on each notification, the collection will contain only items that were 
   /// added since the last notification occurred.
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public interface IObservableNewObjecCollection<T> : INewObjectCollection<T>
      where T : DBObject
   {
      event EventHandler CollectionChanged;
      int AddedCount { get; }
      bool NotifyOnQuiescent { get; set; }
   }

   public class ObservableNewObjectCollection<T> : NewObjectCollection<T>
      where T : DBObject
   {
      int addedCount = 0;
      bool clearCalled = false;
      bool notifying = false;

      public ObservableNewObjectCollection(ObjectId ownerId = default(ObjectId),
         bool includingErased = false,
         bool exactMatch = false,
         bool enabled = true) : base(ownerId, includingErased, exactMatch, enabled)
      {
         NotifyOnQuiscent = true;
      }

      /// <summary>
      /// Specifies if CollectionChanged notifications should
      /// be deferred until AutoCAD is in a quiescent state.
      /// 
      /// If false, CollectionChanged notifications will be
      /// raised on the next idle event following the addition
      /// of one or more objects, but can occur while commands
      /// are in progress (for example, idle notifications can
      /// occur whenever AutoCAD is polling for graphical or
      /// keyboard input while a command is in-progress).
      /// 
      /// If this value is true, CollectionChanged notifications
      /// are not sent until the next idle event that is raised
      /// while AutoCAD is in a quiescent state.
      /// 
      /// In most common use-cases, the recommended value for 
      /// this property is true.
      /// </summary>
      
      public bool NotifyOnQuiscent { get; set; }

      /// <summary>
      /// Asynchronously signals that one or more objects 
      /// have been added to the collection. 
      /// 
      /// See the NotifyOnQuiescent property for details on
      /// when this event is raised.
      /// </summary>
      
      public event EventHandler<CollectionChangedEventArgs<T>> CollectionChanged;


      /// <summary>
      /// Synchronously signals that an object has been 
      /// added to the collection.
      /// </summary>
      
      public event ObjectAddedEventHandler ObjectAdded;

      protected override void Add(ObjectId id)
      {
         ++addedCount;
         bool veto = false;
         if(ObjectAdded != null)
         {
            var args = new ObjectAddedEventArgs(id);
            ObjectAdded(this, args);
            veto = args.vetoed;
         }
         if(!veto)
         {
            base.Add(id);
            Idle.Distinct.Invoke(NotifyCollectionChanged, NotifyOnQuiscent);
         }
      }

      /// <summary>
      /// Returns a sequence consisting of the ObjectIds
      /// of all objects that were added since the last
      /// time the CollectionChanged event was raised.
      /// 
      /// If the IncludingErased property is false, this
      /// sequence will not include the ObjectIds of erased
      /// objects.
      /// </summary>

      public IEnumerable<ObjectId> NewObjectIds
      {
         get
         {
            if(addedCount > 0)
            {
               bool erased = IncludingErased;
               int cnt = this.CountIncludingErased;
               for(int i = cnt - addedCount; i < cnt; i++)
               {
                  if(erased || !this[i].IsErased)
                     yield return this[i];
               }
            }
         }
      }

      /// <summary>
      /// This value will hold the number of elements
      /// that were added to the collection since the
      /// last time the CollectionChanged event was
      /// raised.
      /// 
      /// If the IncludingErased property is false, the
      /// result excludes erased elements.
      /// 
      /// Because there is work required to produce the
      /// result, it is highly-recommended that it be
      /// stored in a variable and that used in prefernce
      /// to repeatedly accessing this property when its
      /// value could not have changed, such as within a
      /// CollectionChanged event handler.
      /// </summary>

      public int AddedCount
      {
         get
         {
            if(IncludingErased)
               return addedCount;
            int result = 0;
            int cnt = CountIncludingErased;
            for(int i = cnt - addedCount; i < cnt; i++)
            {
               if(!this[i].IsErased)
                  ++result;
            }
            return result;
         }
      }

      /// <summary>
      /// Specifies if the instance should be cleared
      /// after each CollectionChanged notification.
      /// That only happens if there is at least one
      /// handler listening to the notification. 
      /// </summary>

      public bool ClearOnNotify { get; set; }

      /// <summary>
      /// Returns a sequence consisting of the Objects
      /// added since the last time the CollectionChanged 
      /// event was raised, opened in the given Transaction 
      /// and with the specified OpenMode.
      /// </summary>
      /// <param name="trans"></param>
      /// <param name="mode"></param>
      /// <returns></returns>

      public IEnumerable<T> GetNewObjects(Transaction trans, OpenMode mode = OpenMode.ForRead)
      {
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         return NewObjectIds.GetObjects<T>(trans, mode);
      }

      public bool IsNotifying => notifying;

      void NotifyCollectionChanged()
      {
         if(CollectionChanged != null)
         {
            notifying = true;
            clearCalled = false;
            try
            {
               CollectionChanged(this, new EventArgs(this));
            }
            finally
            {
               addedCount = 0;
               notifying = false;
               if(ClearOnNotify || clearCalled)
                  Clear();
            }
         }
      }

      /// <summary>
      /// If called from a handler of the CollectionChanged
      /// event, the instance is not cleared until after the
      /// handler returns. Hence, CollectionChanged handlers 
      /// should not rely on the instance having been cleared
      /// immediately after this is called.
      /// 
      /// If the ClearOnNotify property is true, the instance
      /// is cleared after all handlers of the CollectionChanged
      /// event have been notified.
      /// </summary>
      public override void Clear()
      {
         clearCalled = notifying;
         if(!notifying)
         {
            addedCount = 0;
            base.Clear();
         }
      }

      class EventArgs : CollectionChangedEventArgs<T>
      {
         public EventArgs(ObservableNewObjectCollection<T> sender)
            : base(sender)
         {
         }
      }
   }

   /// <summary>
   /// The current roadmap is to make this type
   /// non-generic, but that will require a major
   /// refactoring of the associated types, and
   /// require event handlers to cast from a base
   /// type to a generic derived type.
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class CollectionChangedEventArgs<T> : EventArgs where T: DBObject
   {
      ObservableNewObjectCollection<T> sender;

      protected CollectionChangedEventArgs(ObservableNewObjectCollection<T> sender)
      {
         this.sender = sender;
      }

      public ObservableNewObjectCollection<T> Sender => sender;
      public IEnumerable<ObjectId> NewObjectIds => sender.NewObjectIds;
      public IEnumerable<T> GetNewObjects(Transaction trans, OpenMode mode = OpenMode.ForRead)
         => sender.GetNewObjects(trans, mode);
      public void Clear() => sender.Clear();
      public int NonErasedCount => sender.NonErasedCount;
      public int Count => sender.Count;
      public int AddedCount => sender.AddedCount;
   }
}

public delegate void ObjectAddedEventHandler(object sender, ObjectAddedEventArgs e);

public class ObjectAddedEventArgs : EventArgs
{
   internal bool vetoed = false;
   public ObjectAddedEventArgs(ObjectId id)
   {
      this.ObjectId = id;
   }

   public void Veto() {vetoed = true;}

   public ObjectId ObjectId { get; private set; }
}
