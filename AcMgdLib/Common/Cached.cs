
/// Cached.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.

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

   public struct Cached<T>
   {
      bool dirty;
      Func<T> update;
      T value;

      public Cached(Func<T> update)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
         value = default(T);
         dirty = true;
         Invalidate();
      }

      public Cached(T initialValue, Func<T> update)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
         this.value = initialValue;
         this.dirty = false;
      }

      public void Invalidate()
      {
         this.dirty = true;
      }

      public T Value
      {
         get
         {
            if(dirty)
            {
               value = update();
               dirty = false;
            }
            return value;
         }
      }

      public bool IsValid => !dirty;

      public static implicit operator T(Cached<T> cached) => cached.Value;
   }

   /// <summary>
   /// A version of Cached that encapsulates and
   /// uses a parameter to compute the value.
   /// </summary>
   /// <typeparam name="T">The type of the cached value</typeparam>
   /// <typeparam name="TParam">The type of the parameter used to compute the value</typeparam>

   public struct Cached<T, TParam>
   {
      bool dirty;
      T value;
      TParam parameter;
      Func<TParam, T> update;

      public Cached(TParam parameter, Func<TParam, T> update)
      {
         Assert.IsNotNull(update, nameof(update));
         this.parameter = parameter;
         this.value = default(T);
         this.update = update;
         this.dirty = true;
      }

      public Cached(T initialValue, TParam parameter, Func<TParam, T> update)
      {
         Assert.IsNotNull(update, nameof(update));
         this.parameter = parameter;
         this.value = initialValue;
         this.update = update;
         this.dirty = false;
      }

      public void Invalidate()
      {
         this.dirty = true;
      }

      public void Invalidate(TParam parameter)
      {
         this.parameter = parameter;
         this.dirty = true;
      }

      public TParam Parameter
      { 
         get => parameter;
         set
         {
            Invalidate(value);
         }
      }

      public T Value
      {
         get
         {
            if(dirty)
            {
               value = update(parameter);
               dirty = false;
            }
            return value;
         }
      }

      public static implicit operator T(Cached<T, TParam> value) => value.Value;
   }

}


