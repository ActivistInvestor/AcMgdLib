
/// Cached.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

using Autodesk.AutoCAD.DatabaseServices.Extensions;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;

namespace System.Extensions
{
   /// <summary>
   /// Automates storage and updating of a value,
   /// using a caller-supplied factory method that 
   /// computes/updates the value as-needed. 
   /// 
   /// This class works like the framework's Lazy<T>,
   /// with the addition of offering the ability to
   /// invalidate the encapsulated value, forcing it 
   /// to be recomputed when subsequently requested.
   /// 
   /// The encapsulated value is cached and returned 
   /// by the Value property until the point when the 
   /// Invalidate() method is called. 
   /// 
   /// Each time Invalidate() is called, the supplied 
   /// method will be called to recompute and update 
   /// the encapsulated value at the point when it is
   /// subsequently requested.
   /// </summary>
   /// <typeparam name="T">The type of the cached value</typeparam>

   public class Cached<T> : IDisposable
   {
      bool dirty;
      Func<T> update;
      bool disposed = false;
      bool dispose = false;
      bool initialized = false;
      T value;

      /// <summary>
      /// Initializes a new instance of Cached<T>. If
      /// the generic argument implements IDisposable
      /// and the dispose argument is provided and is
      /// true, the cached value is disposed when it
      /// is invalidated and when the instance of this
      /// type is disposed.
      /// </summary>
      /// <param name="update">A function that takes no
      /// arguments and returns the value to be cached
      /// and returned.</param>
      /// <param name="dispose">A value indicating if 
      /// the cached value should be disposed when it 
      /// is invalidated or the instance of this type 
      /// is disposed</param>
      
      public Cached(Func<T> update, bool dispose = false)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
         this.dispose = dispose;
         this.dirty = true;
      }

      public Cached(T initialValue, Func<T> update, bool dispose = false)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
         this.value = initialValue;
         this.dispose = dispose && typeof(IDisposable).IsAssignableFrom(typeof(T));
         this.initialized = true;
         this.dirty = false;
      }

      public void Invalidate()
      {
         CheckDisposed();
         this.dirty = true;
         TryDispose();
      }

      void TryDispose()
      {
         if(dispose)
         {
            if(value is IDisposable disposable)
               disposable.Dispose();
            value = default(T);
         }
      }

      public void Dispose()
      {
         if(!disposed)
         {
            disposed = true;
            TryDispose();
         }
      }

      void CheckDisposed()
      {
         if(disposed)
            throw new ObjectDisposedException(GetType().CSharpName());
      }

      bool HasValue => !(dirty || disposed);

      public T Value
      {
         get
         {
            CheckDisposed();
            if(dirty)
            {
               TryDispose();
               value = update();
               initialized = true;
               dirty = false;
            }
            return value;
         }
      }

      public bool IsValid => !dirty;

      public static implicit operator T(Cached<T> operand) =>
         operand.Value ?? throw new ArgumentNullException(nameof(operand));
   }

   /// <summary>
   /// A version of Cached that encapsulates and
   /// uses a parameter to compute the value.
   /// </summary>
   /// <typeparam name="T">The type of the cached value</typeparam>
   /// <typeparam name="TParam">The type of the parameter used to compute the value</typeparam>

   public class Cached<T, TParam> : IDisposable
   {
      bool dirty;
      Func<TParam, T> update;
      TParam parameter;
      bool disposed = false;
      bool dispose = false;
      bool initialized = false;
      T value;

      /// <summary>
      /// Initializes a new instance of Cached<T>. If
      /// the generic argument implements IDisposable
      /// and the dispose argument is provided and is
      /// true, the cached value is disposed when it
      /// is invalidated and when the instance of this
      /// type is disposed.
      /// </summary>
      /// <typeparam name="T">The type of the cached value</typeparam>
      /// <typeparam name="TParam">The type of the parameter used to compute the value</typeparam>
      /// <param name="update">A function that takes no
      /// arguments and returns the value to be cached.</param>
      /// <param name="dispose">A value indicating if the
      /// cached value should be disposed when invalidated
      /// or when the instance of this type is disposed</param>

      public Cached(TParam parameter, Func<TParam, T> update, bool dispose = false)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
         this.parameter = parameter;
         this.dispose = dispose && typeof(IDisposable).IsAssignableFrom(typeof(T));
         this.dirty = true;
      }

      public Cached(T initialValue, TParam parameter, Func<TParam, T> update, bool dispose = false)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
         this.value = initialValue;
         this.parameter = parameter;
         this.dispose = dispose;
         this.initialized = true;
         this.dirty = false;
      }

      public void Invalidate()
      {
         CheckDisposed();
         this.dirty = true;
         TryDispose();
      }

      void TryDispose()
      {
         if(dispose)
         {
            if(value is IDisposable disposable)
               disposable.Dispose();
            value = default(T);
         }
      }

      public void Dispose()
      {
         if(!disposed)
         {
            disposed = true;
            TryDispose();
         }
      }

      void CheckDisposed()
      {
         if(disposed)
            throw new ObjectDisposedException(GetType().CSharpName());
      }

      bool HasValue => !(dirty || disposed);

      public T Value
      {
         get
         {
            CheckDisposed();
            if(dirty)
            {
               TryDispose();
               value = update(parameter);
               initialized = true;
               dirty = false;
            }
            return value;
         }
      }

      public bool IsValid => !dirty;

      public static implicit operator T(Cached<T, TParam> operand) => 
         operand.Value ?? throw new ArgumentNullException(nameof(operand));
   }


}


