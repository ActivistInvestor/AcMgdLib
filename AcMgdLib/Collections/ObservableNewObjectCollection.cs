/// ObservableNewObjectCollection.cs
/// 
/// ActivistInvestor / Tony Tanzillo
/// 
/// Distributed under the terms of the MIT license

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
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
      bool WaitForQuiescence { get; set; }
   }

   public class ObservableNewObjectCollection<T> : NewObjectCollection<T>
      where T : DBObject
   {
      int added = 0;

      public ObservableNewObjectCollection(ObjectId ownerId = default(ObjectId),
            bool exactMatch = false,
            bool enabled = true)
         : base(ownerId, exactMatch, enabled)
      {
      }

      public bool NotifyOnQuiscence { get; set; }

      public event EventHandler<CollectionChangedEventArgs<T>> CollectionChanged;

      protected override void Add(ObjectId id)
      {
         base.Add(id);
         ++added;
         Idle.Distinct.Invoke(NotifyCollectionChanged, NotifyOnQuiscence);
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
            if(added > 0)
            {
               bool erased = IncludingErased;
               for(int i = Count - added; i < Count; i++)
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
      /// </summary>

      public int AddedCount
      {
         get
         {
            if(IncludingErased)
               return added;
            int result = 0;
            for(int i = Count - added; i < Count; i++)
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
      /// This only happens if there is at least one
      /// handler listening to the notification. 
      /// </summary>

      public bool ClearOnNotify { get; set; }

      /// <summary>
      /// Returns a sequence consisting of the Objects
      /// added since the last time the CollectionChanged 
      /// event was raised, using the given Transaction 
      /// and OpenMode.
      /// </summary>
      /// <param name="trans"></param>
      /// <param name="mode"></param>
      /// <returns></returns>

      public IEnumerable<T> GetNewObjects(Transaction trans, OpenMode mode = OpenMode.ForRead)
      {
         Assert.IsNotNullOrDisposed(trans, nameof(trans));
         return NewObjectIds.GetObjects<T>(trans, mode);
      }

      void NotifyCollectionChanged()
      {
         if(CollectionChanged != null)
         {
            CollectionChanged(this, new EventArgs(this));
            added = 0;
            if(ClearOnNotify)
               Clear();
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
