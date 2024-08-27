/// ContextualEntityVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Classes that facilitate recursively operating 
/// on nested entites in BlockTableRecords.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Extensions;
using System.Linq;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// EntityVisitor<T, TContext>
   /// 
   /// The TContext generic argument is a user-defined type 
   /// that is pushed/popped onto/off of a stack each time 
   /// the instance begins/ends visiting the contents of a 
   /// block reference.
   /// 
   /// This class provides a means for consumers to manage
   /// data whose scope is the scope of the BlockReference 
   /// that is currently being visited.
   /// 
   /// Each time a block reference is visited, the caller
   /// provides a new instance of T, within which they can
   /// store data that's associated with that specific block 
   /// reference.
   /// 
   /// If the generic argument implements IDisposable, its
   /// Dispose() method is called just after the instance 
   /// has been popped off of the reference stack.
   /// 
   /// The instance of T representing the data associated
   /// with the block reference currently being visited can
   /// be accessed directly via the Current property.
   /// 
   /// Derived types will typically override BeginVisit(),
   /// to perform initialization (or do that in the override
   /// of Create()), and override EndVisit() to do whatever 
   /// finalization is required, using the value of the
   /// Current property.
   /// 
   /// 
   /// </summary>
   /// <typeparam name="TContext">The type of the object that
   /// is to be associated with each visited block reference
   /// </typeparam>

   public abstract class EntityVisitor<T, TContext> : EntityVisitor<T>, IEnumerable<TContext>
      where TContext : class
      where T : Entity
   {
      readonly Stack<Cached<TContext, BlockReference>> context =
         new Stack<Cached<TContext, BlockReference>>();

      public EntityVisitor()
      {
         Contextual = true;
      }

      /// <summary>
      /// This method must be overridden in derived types,
      /// and must return an instance of TContext, that will
      /// be permanently associated with the BlockReference 
      /// argument. 
      /// 
      /// Instances of TContext have a 1-to-1 relationship
      /// with BlockReferences, allowing them to store data
      /// whose scope is the same as the associated block 
      /// reference. The instance of TContext at the top of 
      /// the stack is always the instance associated with 
      /// the block reference whose entities are currently
      /// being visited.
      /// 
      /// The simplicity this design provides can be seen in 
      /// the DeepExplodeVisitor class, where the TContext is
      /// an ObjectIdCollection that holds the ObjectIds of 
      /// block entities be cloned and transformed.
      /// 
      /// Note that creation of instances is fully-lazy and
      /// deferred until the instance is requested through 
      /// the Current property.
      /// </summary>
      /// <param name="blockref">The BlockReference that is
      /// to be associated with the result</param>
      /// <returns>The instance of T that is to be associated
      /// with the given BlockReference</returns>

      protected abstract TContext GetContext(BlockReference blockref);

      public TContext Current
      {
         get
         {
            if(context.Count == 0)
               throw new InvalidOperationException("empty stack");
            return context.Peek();
         }
      }

      public bool HasContext => context.Count > 0;

      protected sealed override void Push(BlockReference blkref)
      {
         base.Push(blkref);
         context.Push(new Cached<TContext, BlockReference>(blkref, GetContext, true));
      }

      protected sealed override void Pop()
      {
         if(context.Count == 0)
            throw new InvalidOperationException("Pop(): Empty stack");
         context.Pop().Dispose();
         base.Pop();
      }

      /// <summary>
      /// Forces creation of instances of TContext
      /// for each Block reference currently on the
      /// reference stack.
      /// </summary>

      public IEnumerator<TContext> GetEnumerator()
      {
         return context.Select(item => item.Value).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }

      public override bool Contextual
      {
         get => base.Contextual;
         set 
         {
            if(!value)
               throw new NotSupportedException("Value cannot be false");
            base.Contextual = value;
         }
      }
   }
}
