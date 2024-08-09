/// EditorStateViewe.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license


using System;
using System.ComponentModel;
using System.Diagnostics.Extensions;
using System.Extensions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Runtime.Extensions;

namespace Autodesk.AutoCAD.EditorInput.Extensions
{

   /// <summary>
   /// A class that monitors the state of the drawing
   /// editor, and provides notifications about changes
   /// to the editor's 'quiescent' state. Typically used
   /// to synchronize the enabled state of UIs with the
   /// quiescent state of the drawing editor.
   /// </summary>

   [DefaultBindingProperty(nameof(IsQuiescentDocument))]
   public class EditorStateView : IDisposable, INotifyPropertyChanged
   {
      static DocumentCollection docs = Application.DocumentManager;
      static EditorStateView instance;
      private bool disposed;
      bool observing = false;
      int refcount = 0;
      Cached<bool> quiescent = new Cached<bool>(GetIsQuiescentDocument);
      static object lockObj = new object();
      static bool isQuitting = false;
      event PropertyChangedEventHandler propertyChanged = null;

      EditorStateView()
      {
         NotifyAsync = true;
         Application.QuitWillStart += OnQuit;
      }

      /// <summary>
      /// If set to true, property changed notifications 
      /// are sent asynchronously on the next Idle event.
      /// </summary>

      public bool NotifyAsync { get; set; }

      public int AddRef()
      {
         EnableSourceEvents(true);
         return ++refcount;
      }

      public bool Release()
      {
         refcount = Math.Max(--refcount, 0);
         EnableSourceEvents(refcount > 0);
         return refcount == 0;
      }

      public event PropertyChangedEventHandler PropertyChanged
      {
         add
         {
            Assert.IsNotNull(value, nameof(value));
            int cnt = HandlerCount;
            propertyChanged += value;
            if(HandlerCount > cnt)
               AddRef();
         }
         remove
         {
            Assert.IsNotNull(value, nameof(value));
            if(propertyChanged != null)
            {
               int cnt = HandlerCount;
               propertyChanged -= value;
               if(HandlerCount < cnt)
                  Release();
            }
         }
      }

      int HandlerCount => propertyChanged?.GetInvocationList().Length ?? 0;

      public static EditorStateView Instance
      {
         get
         {
            lock(lockObj)
            {
               if(instance == null)
                  instance = new EditorStateView();
               return instance;
            }
         }
      }

      void EnableSourceEvents(bool value)
      {
         if(value ^ observing)
         {
            observing = value;
            if(value)
            {
               docs.DocumentLockModeChanged += documentLockModeChanged;
               docs.DocumentActivated += documentEvent;
               docs.DocumentDestroyed += documentEvent;
            }
            else
            {
               docs.DocumentLockModeChanged -= documentLockModeChanged;
               docs.DocumentActivated -= documentEvent;
               docs.DocumentDestroyed -= documentEvent;
            }
            quiescent.Invalidate();
         }
      }

      void IsQuiescentDocumentChanged()
      {
         if(propertyChanged != null)
         {
            if(NotifyAsync)
               Idle.Distinct.Invoke(NotifyIsQuiescentDocumentChanged);
            else
               NotifyIsQuiescentDocumentChanged();
         }
         else
         {
            quiescent.Invalidate();
         }
      }

      void NotifyIsQuiescentDocumentChanged()
      {
         quiescent.Invalidate();
         propertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(IsQuiescentDocument)));
      }

      /// <summary>
      /// Note: Returns false if there is no active document
      /// </summary>

      public bool IsQuiescentDocument => quiescent.Value;

      /// <summary>
      /// The result of this is cached in the quiescent variable
      /// returned by the above property. When one of the events
      /// that signals that the editor's quiescent state may have
      /// changed is raised, the cached value is invalidated and
      /// this method will be called the next time the property
      /// value is accessed to recompute and cache the value again.
      /// 
      /// This caching scheme is called for due to the frequency 
      /// at which the above property can be referenced, and the 
      /// fact that always returns the same result until one of 
      /// the signaling events is raised. 
      /// </summary>
      /// <returns></returns>
      
      static bool GetIsQuiescentDocument()
      {
         Document doc = docs.MdiActiveDocument;
         if(doc != null)
         {
            return doc.Editor.IsQuiescent
               && !doc.Editor.IsDragging
               && (doc.LockMode() & DocumentLockMode.NotLocked)
                   == DocumentLockMode.NotLocked;
         }
         return false;
      }

      /// <summary>
      /// Handlers of driving events:
      /// 
      /// These events signal that the Editor's 
      /// quiescent state might have changed. 
      /// </summary>

      void documentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
      {
         if(e.Document == docs.MdiActiveDocument)
         {
            string cmd = e.GlobalCommandName?.ToUpper();
            if(cmd != null && cmd != "" && cmd != "#" && !cmd.StartsWith("ACAD_DYNDIM"))
               IsQuiescentDocumentChanged();
         }
      }

      void documentEvent(object sender, EventArgs e)
      {
         IsQuiescentDocumentChanged();
      }

      public void Dispose()
      {
         if(!disposed)
         {
            this.disposed = true;
            if(!isQuitting)
            {
               Application.BeginQuit -= OnQuit;
               EnableSourceEvents(false);
            }
         }
         GC.SuppressFinalize(this);
      }

      void OnQuit(object sender, EventArgs e)
      {
         Application.QuitWillStart -= OnQuit;
         isQuitting = true;
      }


   }
}

