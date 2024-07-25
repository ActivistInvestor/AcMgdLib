
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace System.Extensions
{

   /// <summary>
   /// A class that manages the disposal of an ordered
   /// set of IDisposable instances.
   /// 
   /// Contract: IDisposable instances managed by this
   /// class are disposed in the reverse order in which 
   /// they are added. Care must be taken in cases where
   /// there is an inter-dependence between objects added 
   /// to the disposal queue. Dependent objects should be
   /// added AFTER the objects they're dependent on have 
   /// been added.
   /// 
   /// Contract: An instance of an IDisposable cannot be 
   /// added to the queue multiple times. If there is an 
   /// attempt to add the same instance multiple times, 
   /// all but the initial attempt is ignored and there 
   /// is no error. If the instance is a struct, a copy
   /// of it is added to the instance on each call to the
   /// Add() method, and each copy will be disposed.
   /// 
   /// Following that rule will serve to ensure that when 
   /// a dependent object is disposed, the object(s) that
   /// it depends on have not been disposed yet.
   /// 
   /// </summary>

   public static class Disposables
   {
      static object lockObj = new object();
      static OrderedSet<IDisposable> list = new OrderedSet<IDisposable>();
      static Disposer disposer = null;
      static bool isDisposing = false;

      static Disposables()
      {
         Application.QuitWillStart += quitWillStart;
      }

      private static void quitWillStart(object sender, EventArgs e)
      {
         Clear(true);  
      }

      /// <summary>
      /// Disposables.Add(IDisposable item,...)
      /// 
      /// This method can be passed any number of IDisposables.
      /// When the Dispose() method is called, the arguments will
      /// be disposed and dequeued.
      /// 
      /// e.g.:
      /// 
      ///    Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 1.0);
      ///    Disposables.Add(circle);
      ///    
      /// Note that in lieu of calling Add(), the AutoDispose() 
      /// extension method can instead be used thusly:
      ///
      ///    Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 1.0);
      ///    circle.AutoDispose();
      ///    
      /// or with this one-liner:
      /// 
      ///    Circle circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 1.0).AutoDispose();
      ///    
      /// If for some reason, you no longer want an object
      /// that was previously-queued for disposal to not
      /// be disposed, you can remove it from the queue via
      /// either the Remove() method, or the AutoDispose()
      /// method, like so:
      /// 
      ///    Disposables.Remove(circle);
      ///    
      /// or using the AutoDispose() extension method, by
      /// passing false as the argument:
      /// 
      ///    circle.AutoDispose(false);
      /// 
      /// If the Remove() or AutoDispose(false) methods are 
      /// used to dequeue an IDisposable, the caller becomes 
      /// responsible for disposing it..
      /// 
      /// </summary>
      /// <param name="disposable">The items to be disposed at shutdown
      /// or when the Dispose() method is called</param>

      public static void Add(params IDisposable[] args)
      {
         if(args == null)
            throw new ArgumentNullException(nameof(args));
         lock(lockObj)
         {
            list.UnionWith(args);
         }
      }

      /// <summary>
      /// Removes an IDisposable that is queued for 
      /// disposiong without disposing it.
      /// </summary>

      public static bool Remove(IDisposable disposable)
      {
         lock(lockObj)
         {
            return list.Remove(disposable);
         }
      }

      /// <summary>
      /// Checks if a given IDisposable is already queued 
      /// for disposal:
      /// </summary>

      public static bool Contains(IDisposable item)
      {
         lock(lockObj)
         {
            return list.Contains(item);
         }
      }

      /// <summary>
      /// The count of elements currently queued for disposal:
      /// </summary>
      
      public static int Count => list.Count;

      /// <summary>
      /// Disposes all elements queued for disposal. This method
      /// is autonomously called when AutoCAD shuts down, so there
      /// is no need to explicitly call it at shutdown. 
      /// 
      /// This method can also be called at any time to dispose and 
      /// dequeue IDisposable instances that were previously-queued
      /// using the Add() method or the AutoDispose() method.
      /// </summary>

      public static void Dispose()
      {
         Clear(false);
      }

      static void Clear(bool terminating = false)
      {
         System.Exception exception = null;
         int exceptionCount = 0;
         lock(lockObj)
         {
            isDisposing = true;
            try
            {
               for(int i = list.Count - 1; i > -1; i--)
               {
                  try
                  {
                     IDisposable item = list[i];
                     if(item != null && ! IsDisposed(item))
                        item.Dispose();
                  }
                  catch(System.Exception ex)
                  {
                     exception = exception ?? ex;
                     exceptionCount++;
                  }
               }
            }
            finally
            {
               list.Clear();
               isDisposing = false;
            }
         }
         if(exception != null)
         {
            if(!terminating)
               throw exception;
            else
            {
               Debug.WriteLine($"Dispose() threw {exception} (total exceptions: {exceptionCount})");
               Console.Beep();
            }
         }
      }

      /// <summary>
      /// This method is typically used only in specialized
      /// use cases. It will remove any elements derived from
      /// DisposableWrapper whose IsDisposed property is true.
      /// If a DisposableWrapper's IdDisposed property is true,
      /// the instance has been disposed, making a call to its
      /// Dispose() method unecessary. Calling this method will
      /// also make any otherwise-unreachable elements elegible
      /// for garbage collection.
      /// </summary>

      public static void PurgeDisposableWrappers()
      {
         var remove = new List<IDisposable>(list.Count);
         for(int i = 0; i < list.Count; i++)
         { 
            IDisposable disposable = list[i];
            if(IsDisposed(disposable))
               remove.Add(disposable);
         }
         if(remove.Count > 0)
         {
            lock(lockObj)
            {
               list.ExceptWith(remove);
            }
         }
      }

      static bool IsDisposed(IDisposable disposable)
      {
         return disposable is DisposableWrapper dw && dw.IsDisposed;
      }

      /// <summary>
      /// An extension method targeting IDisposable, that can be used
      /// in lieu of the Add() method. If the add argument is true, the
      /// target is queued for disposal. If the add argument is false
      /// the target is dequeued for disposal and is not disposed, and
      /// the caller becomes responsible for disposing the target.
      /// </summary>
      /// <param name="item">The item to be queued/dequeued for 
      /// automatic disposal</param>
      /// <param name="add">true to queue the item for disposal,
      /// or false to dequeue the item (default = true)</param>
      /// <exception cref="ArgumentNullException"></exception>

      public static T AutoDispose<T>(this T item, bool add = true)
         where T : IDisposable
      {
         if(item == null)
            throw new ArgumentNullException(nameof(item));
         if(add)
            Add(item);
         else
            Remove(item);
         return item;
      }

      /// <summary>
      /// An extension method that can be used in lieu of the
      /// Disposables.Contains() method to indicate if a given
      /// IDisposable has been queued for disposal.
      /// </summary>
      /// <param name="item">The IDisposable to query for</param>
      /// <returns>true if the argument is queued for disposal</returns>

      public static bool IsAutoDispose(this IDisposable item)
      {
         return Disposables.Contains(item);
      }

      /// <summary>
      /// Returns an object that can be controlled by a
      /// using() directive, that will call the Dispose()
      /// method of this class when the returned instance 
      /// is disposed.
      /// 
      /// Example:
      /// <code>
      ///           using(Disposables.GetDisposer())
      ///           {
      ///              Disposables.Add(disposable1);
      ///              Disposables.Add(disposable2);
      ///              Disposables.Add(disposable3);....
      ///              
      /// 
      ///           }  // All objects added to the Disposable 
      ///              // are disposed here, without having
      ///              // to call Disposables.Dispose().
      /// </code>
      /// </summary>
      /// <returns></returns>

      public static IDisposable GetDisposer()
      {
         if(disposer == null)
            disposer = new Disposer();
         return disposer;
      }

      class Disposer : IDisposable
      {
         public void Dispose()
         {
            if(Disposables.disposer != null)
            {
               Disposables.Dispose();
               Disposables.disposer = null;
            }
         }
      }
   }

   /// <summary>
   /// Disposes objects on the main thread on the next idle event.
   /// This is primarily intended to be called from a finalizer
   /// that calls Dispose(false), to avoid deleting an unmanaged 
   /// resource whose destructor is not thread-safe. Instead of
   /// deleting the unmanaged resource in Dispose(false), it is
   /// deleted on the next idle event using a delegate.
   /// 
   /// </summary>

   public static class AsyncDisposer
   {
      static OrderedSet<IDisposable> items = new OrderedSet<IDisposable>();
      static bool enabled = false;

      public static void Add(IDisposable disposable)
      {
         items.Add(disposable);
      }

      public static void DisposeAsync(this IDisposable disposable)
      {
         if(Application.DocumentManager != null)
         {
            disposable.Dispose();
            return;
         }
         Add(disposable);
         Enabled = items.Count > 0;
      }

      static bool Enabled 
      { 
         get 
         {
            return enabled;
         } 
         set 
         {
            if(enabled ^ value)
            {
               enabled = value;
               if(enabled)
                  Application.Idle += idle;
               else
                  Application.Idle -= idle;
            }
         } 
      }

      private static void idle(object sender, EventArgs e)
      {
         foreach(IDisposable disposable in items.Reverse())
            disposable.Dispose();
         items.Clear();
         Enabled = false;
      }
   }
}

