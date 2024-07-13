
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
   /// invalidate the computed value, forcing it to
   /// be recomputed when subsequently requested.
   /// 
   /// The computed value is cached and returned by 
   /// the Value property until the point when the 
   /// Invalidate() method is called. 
   /// 
   /// Each time Invalidate() is called, the supplied 
   /// method will be called to recompute and update 
   /// the cached value the next time it is requested.
   /// </summary>
   /// <typeparam name="T">The type of the cached value</typeparam>

   public struct Cached<T>
   {
      bool dirty = true;
      Func<T> update;
      T value;

      public Cached(Func<T> update)
      {
         Assert.IsNotNull(update, nameof(update));
         this.update = update;
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

      public static implicit operator T(Cached<T> value) => value.Value;
   }

   /// <summary>
   /// A version of Cached that uses a parameter to compute the value.
   /// </summary>
   /// <typeparam name="T">The type of the cached value</typeparam>
   /// <typeparam name="TParameter">The type of the parameter used to compute the value</typeparam>

   public struct Cached<T, TParameter>
   {
      bool valid = false;
      Func<TParameter, T> update;
      T value;
      TParameter parameter;

      public Cached(TParameter parameter, Func<TParameter, T> update)
      {
         Assert.IsNotNull(update, nameof(update));
         this.parameter = parameter;
         this.update = update;
      }

      public void Invalidate()
      {
         this.valid = false;
      }

      public void Invalidate(TParameter parameter)
      {
         this.parameter = parameter;
         this.valid = false;
      }

      public TParameter Parameter
      { 
         get => parameter;
         set
         {
            parameter = value;
            Invalidate();
         }
      }

      public T Value
      {
         get
         {
            if(!valid)
            {
               value = update(parameter);
               valid = true;
            }
            return value;
         }
      }

      public static implicit operator T(Cached<T, TParameter> value) => value.Value;
   }

}


