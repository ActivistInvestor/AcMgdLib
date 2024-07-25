using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Extensions
{
   public abstract class DisposableBase : IDisposable
   {
      private bool disposed;

      public bool IsDisposed => disposed;

      public virtual void CheckDisposed()
      {
         if(this.disposed)
            throw new ObjectDisposedException(this.GetType().FullName);
      }

      protected abstract void Dispose(bool disposing);

      public void Dispose()
      {
         if(!disposed)
         {
            Dispose(true);
            disposed = true;
         }
         GC.SuppressFinalize(this);
      }
   }

   public class DisposeAction : DisposableBase
   {
      Action action;
      public DisposeAction(Action action)
      {
         this.action = action;
      }

      protected override void Dispose(bool disposing)
      {
         if(disposing && action != null)
         {
            action();
            action = null;
         }
      }

      public static IDisposable OnDispose(Action action)
      {
         return new DisposeAction(action);
      }

      /// <summary>
      /// Allows an object that may, or may not implement
      /// IDisposable to be 'disposed' via using(). If the
      /// wrapped object doesn't implement IDisposable,
      /// nothing happens when the IDisposable wrapper is
      /// disposed. Otherwise, the IDisposable wrapper will
      /// dispose the wrapped IDisposable when the wrapper
      /// is disposed.
      /// </summary>
      /// <param name="wrapped"></param>
      /// <returns></returns>

      public static IDisposable Wrap(object wrapped)
      {
         if(wrapped is IDisposable disposable)
            return new DisposeAction(() => disposable.Dispose());
         return Empty;
      }

      static readonly Action emptyAction = delegate () { };

      public static readonly IDisposable Empty = new DisposeAction(emptyAction);
   }

   /// <summary>
   /// Enables arbitrary destructor/finalization
   /// semantics for a variety of purposes. 
   /// </summary>
   /// <typeparam name="T"></typeparam>

   public class Disposable<T> : DisposableBase
   {
      T target;
      Action<T> disposeAction;
      bool disposed;

      /// <summary>
      /// When the instance is disposed,
      /// it invokes disposeAction on the
      /// given target.
      /// 
      /// The instance is implicitly-convertable
      /// to the target.
      /// </summary>
      /// <param name="target"></param>
      /// <param name="disposeAction"></param>

      public Disposable(T target, Action<T> disposeAction)
      {
         if(disposeAction == null)
            throw new ArgumentNullException(nameof(disposeAction));
         if(target == null)
            throw new ArgumentNullException(nameof(target));
         this.target = target;
         this.disposeAction = disposeAction;
      }

      protected virtual T Instance => target;

      protected override void Dispose(bool disposing)
      {
         if(!disposing)
            throw new InvalidOperationException("object must be deterministically-disposed");
         if(disposeAction != null)
         {
            disposeAction(target);
            disposeAction = null;
            target = default(T);
         }
      }

      public static implicit operator T(Disposable<T> operand)
      {
         if(operand == null)
            throw new ArgumentException(nameof(operand));
         operand.CheckDisposed();
         return operand.Instance;
      }
   }


   public static class DisposableExtensions
   {
      public static IDisposable AsDisposable<T>(this T arg, Action<T> disposeAction)
      {
         return new Disposable<T>(arg, disposeAction);
      }
   }
}
