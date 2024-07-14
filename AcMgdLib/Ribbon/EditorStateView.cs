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
      static Cached<bool> quiescent = new Cached<bool>(GetIsQuiescent);
      static object lockObj = new object();

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

      event PropertyChangedEventHandler propertyChanged = null;

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

      void InvalidateQuiescentState()
      {
         quiescent.Invalidate();
         NotifyIsQuiescentDocumentChanged();
      }

      void NotifyIsQuiescentDocumentChanged()
      {
         propertyChanged?.Invoke(this,
            new PropertyChangedEventArgs(nameof(IsQuiescentDocument)));
      }

      /// <summary>
      /// Note: Returns false if there is no active document
      /// </summary>

      public bool IsQuiescentDocument
      {
         get
         {
            return quiescent.Value; 
         }
      }

      static bool GetIsQuiescent()
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
      /// quiescent state may have changed. 
      /// </summary>

      void documentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
      {
         if(e.Document == docs.MdiActiveDocument && !e.GlobalCommandName.ToUpper().Contains("ACAD_DYNDIM"))
            InvalidateQuiescentState();
      }

      void documentEvent(object sender, EventArgs e)
      {
         InvalidateQuiescentState();
      }

      public void Dispose()
      {
         if(!disposed)
         {
            this.disposed = true;
            EnableSourceEvents(false);
         }
         GC.SuppressFinalize(this);
      }
   }
}

