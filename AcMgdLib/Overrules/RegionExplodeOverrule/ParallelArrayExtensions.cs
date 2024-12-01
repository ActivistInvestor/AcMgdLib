/// EntityExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Extension methods targeting the Entity class.

using System;
using System.Threading.Tasks;

public static class ParallelArrayExtensions
{
   /// <summary>
   /// Conditional parallel execution based on array size:
   /// 
   /// The ParallelizationThreshold property determines the 
   /// point at which the operation is done in parallel. If 
   /// the array length is > ParallelizationThreshold, the 
   /// operation is done in parallel.
   /// 
   /// The threshold can also be passed as an argument.
   /// 
   /// If the operation is not done in parallel, it uses a
   /// Span<T> to access the array elements.
   /// </summary>

   /// User-tunable threshold

   public static int ParallelizationThreshold
   {
      get;set;
   }

   public static void ForEach<T>(this T[] array, Action<T> action)
   {
      ForEach<T>(array, ParallelizationThreshold, action);
   }

   /// <summary>
   /// Allows caller to explicitly specify/override
   /// the parallelization threshold.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   /// <param name="array"></param>
   /// <param name="threshold">The number of array
   /// elements required for the operation to execute
   /// in parallel, or -1 to use the current value of
   /// the ParallelizationThreshold property, or 0 to
   /// unconditionally disable parallel execution.</param>
   /// <param name="action"></param>
   /// <exception cref="ArgumentNullException"></exception>
   
   public static void ForEach<T>(this T[] array, int threshold, Action<T> action)
   {
      if(array is null)
         throw new ArgumentNullException(nameof(array));
      if(action is null)
         throw new ArgumentNullException(nameof(action));
      if(array.Length == 0)
         return;
      if(threshold < 0)
         threshold = ParallelizationThreshold;
      if(threshold != 0 && array.Length > threshold)
      {
         var options = new ParallelOptions
         {
            MaxDegreeOfParallelism = Environment.ProcessorCount
         };
         Parallel.For(0, array.Length, options, i => action(array[i]));
      }
      else
      {
         var span = array.AsSpan();
         for(int i = 0; i < span.Length; i++)
            action(span[i]);
      }
   }

   /// <summary>
   /// Same as above except the action also takes the index
   /// of the array element.
   /// </summary>

   public static void ForEach<T>(this T[] array, Action<T, int> action)
   {
      ForEach<T>(array, ParallelizationThreshold, action);
   }

   public static void ForEach<T>(this T[] array, int threshold, Action<T, int> action)
   {
      if(array is null)
         throw new ArgumentNullException(nameof(array));
      if(action is null)
         throw new ArgumentNullException(nameof(action));
      if(array.Length == 0)
         return;
      if(threshold < 0)
         threshold = ParallelizationThreshold;
      if(threshold != 0 && array.Length > threshold)
      {
         var options = new ParallelOptions
         {
            MaxDegreeOfParallelism = Environment.ProcessorCount
         };
         Parallel.For(0, array.Length, options, i => action(array[i], i));
      }
      else
      {
         var span = array.AsSpan();
         for(int i = 0; i < span.Length; i++)
            action(span[i], i);
      }

   }

}
