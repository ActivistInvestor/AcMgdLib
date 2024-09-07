/// BlockReferenceVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Classes that facilitate recursively operating 
/// on nested entites in BlockTableRecords.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Extensions;
using System.Extensions;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Expressions.Predicates;
using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   /// <summary>
   /// A non-generic abstract base class that provides most 
   /// of the functionality exposed by the types that derive 
   /// from it.
   /// </summary>

   public abstract class BlockVisitor : BlockVisitor<Entry>
   {
      public BlockVisitor(bool exactMatch = false,
            Expression<Func<ObjectId, bool>> expression = null)
         : base(exactMatch, expression)
      {
      }

      protected override Entry GetStackEntry(BlockReference blkref)
      {
         return new Entry(stack, blkref);
      }
   }

   public class Entry : BlockVisitor<Entry>.StackEntry
   {
      public Entry(BlockVisitor<Entry>.ReferenceStack owner, BlockReference blkref) 
         : base(owner, blkref)
      {
      }
   }

   public abstract partial class BlockVisitor<T> where T: BlockVisitor<T>.StackEntry
   {
      dynamic dynamicThis;
      bool contextual = true;
      bool dynamic = false;
      int blockCount = 0;
      int visitedCount = 0;
      bool visiting = false;
      int depth = 0;
      long elapsed = 0L;
      HashSet<ObjectId> visitedBlocks = new HashSet<ObjectId>();
      Stack<BlockReference> containers = new Stack<BlockReference>();
      protected readonly ReferenceStack stack;
      List<string> path = new List<string>();
      Transaction trans;
      Func<BlockReference, ObjectId> getBlockDefId;
      BlockReference root = null;
      ObjectId rootBlockId = ObjectId.Null;
      bool ownerIsLayout = false;
      Entity currentEntity = null;
      Cached<Matrix3d> blockTransform = null;
      bool userBlocksOnly = false;
      bool visitAttributes = false;
      List<ObjectId> subentPath = new List<ObjectId>() { ObjectId.Null };
      protected Func<ObjectId, bool> predicate;
      protected static readonly SubentityId nullSubEntityId =
         new SubentityId(SubentityType.Null, 0);
      protected static readonly RXClass blockRefClass =
         RXObject.GetClass(typeof(BlockReference));
      protected DBObjectDataMap<BlockReference, BlockTableRecord, string> effectiveNames
         = new(br => br.DynamicBlockTableRecord, btr => btr.Name);


      /// <summary>
      /// When Visit(AttributeReference) is called, the owning
      /// BlockReference has already been popped off the stack. 
      /// It can be obtained via this property.
      /// </summary>

      private BlockReference attributeOwner;

      /// <summary>
      /// Creates an instance of EntityVisitor
      /// </summary>
      /// <param name="exactMatch">indicates if types derived 
      /// from the generic argument type should be excluded. 
      /// If the generic argument is abstract, this argument 
      /// is ignored and is effectively-false.</param>

      public BlockVisitor(bool exactMatch = false, 
         Expression<Func<ObjectId, bool>> expression = null)
      {
         stack = new ReferenceStack(this);
         dynamicThis = (dynamic)this;
         if(expression == null)
            expression = RXClass<Entity>.GetMatchExpression(exactMatch);
         this.predicate = expression.Or(id => id.ObjectClass == blockRefClass).Compile();
         this.getBlockDefId = GetBlockDefId;
         blockTransform = new Cached<Matrix3d>(GetBlockTransform);
      }

      /// <summary>
      /// Readers must not commit, abort, or dispose 
      /// the result:
      /// </summary>

      protected Transaction Transaction
      {
         get
         {
            AssertIsVisiting();
            Assert.MustBeFalse(trans == null || !trans.AutoDelete);
            return trans;
         }
      }

      public T GetObject<T>(ObjectId id, OpenMode mode = OpenMode.ForRead)
         where T : DBObject
      {
         return Transaction.GetObject<T>(id, mode);
      }

      /// <summary>
      /// Controls if a block's elements are enumerated only 
      /// once. If this is true, the block's elements will be 
      /// enumerated once for each reference to the block that 
      /// is encountered, in its nesting context.
      /// 
      /// If this property is false, a block's entities will 
      /// be visited no more than once, and the Containers
      /// property, the Path property, and the BlockId property
      /// are undefined and will trigger an exception if they
      /// are accessed.
      /// 
      /// When this property is true, the Containers collection 
      /// holds the container BlockReferences of the entity that
      /// is currently being visited, with sole the exception of 
      /// AttributeReferences, which are visited at the same level 
      /// of nesting as their owning BlockReference is visited at.
      /// 
      /// The default value of this property is true.
      /// </summary>

      public virtual bool Contextual
      {
         get => contextual;
         set
         {
            AssertIsNotVisiting();
            contextual = value;
         }
      }

      protected void AssertIsContextual()
      {
         if(!contextual)
            throw new InvalidOperationException("Invalid context.");
      }

      protected void AssertIsNotVisiting()
      {
         if(visiting)
            throw new InvalidOperationException("Value must be set prior to calling Visit()");
      }

      protected void AssertIsVisiting()
      {
         if(!visiting)
            throw new InvalidOperationException("Operation only available while visiting is in progress");
      }

      protected void AssertHasContext()
      {
         AssertIsContextual();
         if(containers.Count == 0)
            throw new InvalidOperationException("Reference stack is empty");
      }

      /// <summary>
      /// If true, the instance will not visit contents
      /// of xref/overlay, anonymous, or xref-dependent 
      /// blocks.
      /// 
      /// This property is to be replaced with individual
      /// boolean properties for each type of block.
      /// </summary>

      public bool UserBlocksOnly
      {
         get => userBlocksOnly;
         set
         {
            AssertIsNotVisiting();
            userBlocksOnly = value;
         }
      }

      /// <summary>
      /// If set to false, block reference attributes
      /// are not visited. In derived types that use
      /// a generic argument for the type of entity to
      /// visit, this property's value is determined
      /// by both the assigned value, and whether the
      /// the generic argument type can be assigned to 
      /// an AttributeReference.
      /// </summary>

      public virtual bool VisitAttributes
      {
         get => visitAttributes;
         set
         {
            AssertIsNotVisiting();
            visitAttributes = value;
         }
      }

      /// <summary>
      /// AttributeReferences are not visited as block entities,
      /// but rather, are visited at the same level as the owning 
      /// block reference, after it and its contents have been 
      /// visited.
      /// 
      /// Because of this, the reference stack will not hold the
      /// owning block reference when Visit() is called and passed
      /// an AttributeReference. This property exists to provide a
      /// means for a Visit() method that is elegible to receive an
      /// AttributeReference to access the owning block reference.
      /// 
      /// This property is only usable when visiting attributes of
      /// a block reference. In any other context, accessing it will
      /// throw an exception.
      /// </summary>

      public BlockReference AttributeOwner
      {
         get
         {
            Assert.IsNotNullOrDisposed(attributeOwner, nameof(attributeOwner));
            return attributeOwner;
         }
      }

      /// <summary>
      /// The following filtering properties were 
      /// planned but never implemented, so they have
      /// proposed status. All of these properties will
      /// declaratively provide more-granular control
      /// over what block references are visited.
      /// 
      /// Currently, the CanVisit() virtual method can
      /// be overridden to indicate if a BlockReference
      /// should be visited or not. And, an overload that
      /// takes a BlockTableRecord can also be overridden 
      /// to indicate if ALL references to the specified
      /// BlockTableRecord should be visited or not.
      /// </summary>

      /// <summary>
      /// Applies to individual BlockReferences, based
      /// on whether they are effectively-explodable.
      /// </summary>
      /// public bool VisitNonExplodable { get; set; } = true;
      /// <summary>
      /// Visit non-dynamic anonymous blocks.
      /// </summary>
      /// public bool VisitAnonymous { get; set; }
      /// <summary>
      /// Visit xref/overlay references
      /// </summary>
      /// public bool VisitXrefAndOverlay { get; set; }
      /// <summary>
      /// Visit dependents of xref/overlay reference 
      /// </summary>
      /// public bool VisitDependent { get; set; }
      /// <summary>
      /// User-defined filter visits only blocks whose
      /// names match the pattern.
      /// </summary>
      /// public string BlockNamePattern { get; set; }
      /// <summary>
      /// Specifies a collection of block name 
      /// patterns that are applied to individual 
      /// path elements. See the PathMatches()
      /// method for details.
      /// </summary>
      /// public string[] PathPatterns { get; set; }

      /// <summary>
      /// Controls how dynamic blocks are handled by this
      /// class. If this property is true, the entities of 
      /// the defining dynamic block are visited when a 
      /// reference to the dynamic block is encountered, 
      /// including anonymous references.
      /// 
      /// If false, references to anonymous dynamic blocks
      /// will visit the anonymous block definition.
      /// 
      /// The default for this property is false, and this
      /// class not been fully-tested with a value of true.
      /// 
      /// Disabled: This property has been disabled pending
      /// further testing with Contextual = false. 
      /// 
      /// When Contextual == true, this property is always 
      /// effectively-false.
      /// </summary>

      //public bool Dynamic
      //{
      //   get => dynamic;
      //   set
      //   {
      //      if(value ^ dynamic)
      //      {
      //         AssertIsNotVisiting();
      //         dynamic = value;
      //         getBlockDefId = GetBlockDefId;
      //      }
      //   }
      //}

      /// Gets the effective name of an anonymous dynamic
      /// block reference (the name of the defining dynamic
      /// block), and caches it for subsequent use.

      public string GetEffectiveName(BlockReference blkref)
      {
         return blkref.IsDynamicBlock ? effectiveNames[blkref] : blkref.Name;
      }

      /// <summary>
      /// The number of BlockReferences that have been visited. 
      /// This value is only meaningful when Contextual = true.
      /// When Contextual is false, the result is undefined.
      /// </summary>

      public int Count => blockCount;

      protected ObjectId GetBlockDefId(BlockReference block)
      {
         return block.BlockTableRecord;
      }

      /// Support for resolving anonymous dynamic block
      /// references to the dynamic block definition is
      /// disabled pending further testing. - DISABLED.

      //protected ObjectId GetBlockDefId(BlockReference block)
      //{
      //   if(dynamic)
      //      getBlockDefId = br => br.DynamicBlockTableRecord;
      //   else
      //      getBlockDefId = br => br.BlockTableRecord;
      //   return getBlockDefId(block);
      //}

      /// <summary>
      /// Primary entry point that triggers visiting of 
      /// the entities of the specified block, and all 
      /// blocks having insertions nested within it.
      /// 
      /// The id argument can be either the ObjectId of a 
      /// BlockReference, or the id of a BlockTableRecord.
      /// 
      /// If the argument is the id of a BlockTableRecord,
      /// the nested argument determines if the entities of
      /// that BlockTableRecord are visited. 
      /// 
      /// If the id argument is the ObjectId of a Layout 
      /// block, nested is always true, and aside from block 
      /// references, this class never visits entities that 
      /// are directly-inserted into layout blocks.
      /// 
      /// If nested is false, entities in the definition of 
      /// the BlockTableRecord whose id is passed as the id
      /// argument are not visited, and only entities within 
      /// blocks inserted into that block are visited.
      /// 
      /// The purpose of the nested property is to allow the 
      /// id of the model or a paper space block to be passed, 
      /// without visiting entities that are directly-owned 
      /// by those blocks.
      /// 
      /// The default value for nested is false, which causes
      /// entities directly inserted into the argument block
      /// to be visited, if it is not a Layout block.
      /// 
      /// If the argument is the ObjectId of a BlockReference,
      /// the nested property is ignored, and entities in the 
      /// BlockTableRecord that is referenced by the argument 
      /// block reference are always visited.
      /// </summary>
      /// <param name="id">The ObjectId of the BlockTableRecord
      /// or BlockReference whose contents is to be visited.</param>
      /// <param name="trans">The Transaction to use in the 
      /// operation</param>
      /// <param name="nested">A value indicating if the entities 
      /// directly inserted into the BlockTableRecord whose Id 
      /// is passed as the id argument should be visited. If this 
      /// value is false, only entities in blocks inserted into 
      /// the argument BlockTableRecord will be visited. If true, 
      /// the entities of the argument block are visited. If the 
      /// id argument is the ObjectId of a BlockReference, this 
      /// argument is ignored and is effectively-false. If the id 
      /// argument is the id of a layout block, this argument is
      /// ignored and is effectively-true.</param>

      public void Visit(ObjectId id, Transaction trans, bool nested = false)
      {
         id.CheckTransaction(trans);
         this.trans = trans;
         this.root = null;
         this.containers.Clear();
         this.path.Clear();
         Stopwatch stopwatch = Stopwatch.StartNew();
         Initialize(id);
         bool failed = true;
         visiting = true;
         try
         {
            if(id.IsA<BlockTableRecord>())
               VisitBlockTableRecord(id, NestedOnly ? true : nested);
            else if(id.IsA<BlockReference>())
               VisitBlockReference(trans.GetObject<BlockReference>(id));
            else
               AcRx.ErrorStatus.WrongObjectType.ThrowIf(true, id.ObjectClass.Name);
            failed = false;
         }
         finally
         {
            stopwatch.Stop();
            elapsed = stopwatch.ElapsedMilliseconds;
            visiting = false;
            Finalize(id, failed);
         }
      }

      /// <summary>
      /// These overridables allow derived types to perform
      /// operation-scoped initialization and finalization.
      /// 
      /// Initialize() is called just before visiting starts,
      /// and is passed the ObjectId supplied to the Visit() 
      /// entry point method.
      /// </summary>

      protected virtual void Initialize(ObjectId id)
      {
      }

      /// <summary>
      /// Can be overridden in a derived type to perform
      /// finalization after the entire operation has
      /// completed. This method is always called, even
      /// if the operation failed prior to completing.
      /// </summary>
      /// <param name="id">The ObjectId passed to the
      /// entry point Visit() method.</param>
      /// <param name="failed">A value indicating if the
      /// operation did not complete successfully</param>

      protected virtual void Finalize(ObjectId id, bool failed)
      {
      }

      /// <summary>
      /// Derived types that depend on the reference stack may 
      /// require nested to be true, to avoid a case where the 
      /// ObjectId argument to Visit() is a BlockTableRecord.
      /// 
      /// In that case, if nested is false, the entities of the
      /// BlockTableRecord are visited with no block reference
      /// on the stack (there cannot be a reference to a layout
      /// block). 
      /// 
      /// The main purpose of allowing the entry point to accept
      /// a BlockTableRecord is to accomodate Layout blocks as 
      /// the starting point. Since there are no references to a 
      /// Layout block, the reference stack will be empty when 
      /// visiting a layout block's entities.
      /// 
      /// Classes that specialize EntityVisitor need to be able
      /// to deal with an empty reference stack if they need to
      /// visit layout entities.
      /// 
      /// APIs that are dependent on the reference stack include
      /// the Transform and IsUniformlyScaled properties.
      /// </summary>

      protected virtual bool NestedOnly => false;

      public void VisitBlockReference(ObjectId blockRefId, Transaction trans)
      {
         AcRx.ErrorStatus.WrongObjectType.Requires<BlockReference>(blockRefId);
         blockRefId.CheckTransaction(trans);
         Visit(getBlockDefId(trans.GetObject<BlockReference>(blockRefId)), trans, false);
      }

      /// <summary>
      /// This overload will not visit directly-owned entities of
      /// the given block argument, and is equivalent to calling 
      /// Visit() and passing true as the nested argument.
      /// </summary>
      /// <param name="btrId">The ObjectId of the BlockTableRecord
      /// whose contents is to be visited.</param>
      /// <param name="trans">The Transaction to use in the operation</param>

      public void VisitNested(ObjectId btrId, Transaction trans)
      {
         AcRx.ErrorStatus.WrongObjectType.Requires<BlockTableRecord>(btrId);
         Visit(btrId, trans, true);
      }

      /// <summary>
      /// Holds the containing block references of the entity
      /// currently being visited, from the inner-most to the
      /// outer-most.
      /// </summary>

      public IReadOnlyCollection<BlockReference> Containers => containers;

      /// <summary>
      /// Returns the BlockReference at the top of the reference stack.
      /// If the reference stack is empty, this will throw an exception.
      /// The HasContext property can be used to check if there is at
      /// least one BlockReference on the stack.
      /// </summary>

      public BlockReference Top
      {
         get
         {
            AssertHasContext();
            return containers.Peek();
         }
      }

      public bool HasContext => contextual && containers.Count > 0;

      /// <summary>
      /// Returns the BlockReference at the bottom of the reference stack.
      /// If the reference stack is empty, this will throw an exception.
      /// The HasContext property can be used to check if there is at
      /// least one BlockReference on the stack.
      /// </summary>

      public BlockReference Root
      {
         get
         {
            AssertHasContext();
            return root;
         }
      }

      /// <summary>
      /// The effective names of all container block references
      /// ordered from outer-most to inner-most.
      /// 
      /// This collection contains the effective names of the
      /// current container block references. If one or more
      /// container block references is an anonymous dynamic
      /// block, the defining dynamic block's name appears in
      /// place of the anonymous block name.
      /// </summary>

      public IReadOnlyList<string> Path => path.AsReadOnly();

      /// <summary>
      /// Describes the transformation from the current entity
      /// being visited to the space containing the outer-most
      /// container block reference. 
      /// 
      /// The result is calculated lazily and is cached and used
      /// for requests for the current container block reference, 
      /// and is invalidated after all of that block's contents 
      /// have been visited, or visiting recurses into or out of 
      /// another nested block reference.
      /// </summary>

      public Matrix3d Transform
      {
         get
         {
            AssertIsContextual();
            return blockTransform;
         }
      }

      void TraceContainers()
      {
         AcConsole.Write($"Containers: {string.Join(" > ", containers.Select(br => br.Name))}");
      }

      Matrix3d GetBlockTransform()
      {
         var matrix = Matrix3d.Identity;
         if(containers.Count > 0)
         {
            var array = new BlockReference[containers.Count];
            containers.CopyTo(array, 0);
            matrix = array[array.Length - 1].BlockTransform;
            if(array.Length > 1)
            {
               for(int i = array.Length - 2; i > -1; i--)
                  matrix *= array[i].BlockTransform;
            }
         }
         return matrix;
      }

      /// <summary>
      /// Returns a FullSubentityPath representing the entity
      /// nested in the current container block references.
      /// </summary>
      /// <param name="entity">The Entity whose path is to be 
      /// returned.</param>
      /// <returns>The FullSubentityPath representing the
      /// entity nested in its container block references.
      /// </returns>
      /// <remarks>
      /// Calling this method is only valid from an overload
      /// of Visit(), as it depends on the current reference
      /// stack, and the entity argument must be directly-
      /// owned by the BlockReference at the top of the stack.
      /// 
      /// Developement notes:
      /// 
      /// Using this method with AttributeReferences may have
      /// issues, and has not been tested with that type.
      /// </remarks>

      public FullSubentityPath GetFullSubEntityPath(Entity entity)
      {
         return GetFullSubEntityPath(entity, nullSubEntityId);
      }

      /// <summary>
      /// Returns a FullSubentityPath representing a component
      /// part of the entity nested in the current container 
      /// block references. The subEntityId argument identifies
      /// the component part of the entity. If this argument is
      /// a null SubEntityId, the result identifies the entity
      /// argument itself.
      /// </summary>
      /// <param name="entity">The Entity whose path is to be 
      /// returned. The Entity argument must be owned by the 
      /// BlockTableRecord referenced by the BlockReference 
      /// currently at the top of the reference stack (e.g.,
      /// an Entity currently being visited, referenced from
      /// within a call to a Visit() overload).</param>
      /// <param name="subEntityId">A SubentityId representing
      /// the component part of the given Entity</param>
      /// <returns></returns>

      public FullSubentityPath GetFullSubEntityPath(Entity entity, SubentityId subEntityId)
      {
         AssertIsValid(entity);
         ObjectId[] array = subentPath.ToArray();
         array[array.Length - 1] = entity.ObjectId;
         return new FullSubentityPath(array, subEntityId);
      }

      /// <summary>
      /// Asserts that the given entity is being visited
      /// within a visit operation, and is directly-owned
      /// by a BlockTableRecord that is referenced by the
      /// BlockReference at the top of the reference stack.
      /// </summary>
      /// <param name="entity"></param>
      /// <exception cref="ArgumentException"></exception>
      void AssertIsValid(Entity entity)
      {
         Assert.IsNotNullOrDisposed(entity);
         AssertIsVisiting();
         AssertHasContext();
         if(entity.BlockId != Top.BlockTableRecord)
            throw new ArgumentException("Invalid entity");
      }

      /// <summary>
      /// Calls the given Entity's Highlight() method with
      /// the FullSubentityPath of the current container
      /// block references. Docs for PushHighlight() and
      /// PopHighlight() are typical, and mirror the same-
      /// named methods of the Entity class, with the
      /// exception of the FullSubentityPath argument that
      /// is provided by these methods.
      /// </summary>
      /// <remarks>
      /// All of the following methods that operate on an
      /// entity are only valid when called from an overload 
      /// of a Visit() method. Calling these methods in any 
      /// other context will trigger an exception.
      /// </remarks>
      /// <param name="entity">The Entity to highlight.
      /// The Entity argument must be directly owned by 
      /// the BlockTableRecord that's referenced by the 
      /// BlockReference currently at the top of the 
      /// reference stack.</param>
      /// <param name="highlightAll"></param>

      public virtual void Highlight(Entity entity, bool highlightAll = false)
      {
         AssertIsValid(entity);
         Root?.Highlight(GetFullSubEntityPath(entity), highlightAll);
      }

      /// <summary>
      /// This method differs from the same-named method 
      /// of the Entity class in that it will return a 
      /// FullSubentityPath that can subsequently be passed 
      /// to PopHighlight() to unhighlight the same entity. 
      /// See the docs for the Entity.PushHighlight() method 
      /// for the info on the arguments.
      /// </summary>
      /// <returns>A FullSubentityPath that can be passed 
      /// to the Entity.PopHighlight() method to unhighlight 
      /// the entity.</returns>
      
      public virtual FullSubentityPath PushHighlight(Entity entity, HighlightStyle style = HighlightStyle.DashedAndThicken)
      {
         AssertIsValid(entity);
         var path = GetFullSubEntityPath(entity);
         Root?.PushHighlight(path, style);
         return path;
      }

      public virtual void PopHighlight(Entity entity)
      {
         AssertIsValid(entity);
         Root?.PopHighlight(GetFullSubEntityPath(entity));
      }

      /// <summary>
      /// Matches against the current Path. 
      /// 
      /// The arguments are case-insensitive wcmatch-style 
      /// wildcards. Each pattern must match a path element 
      /// in the order given. Partial matches are supported. 
      /// The result is true if each of the supplied patterns 
      /// matches the corresponding name in the path, starting
      /// with the last path element, which is compared to the
      /// last wildcard. 
      /// 
      /// There can be fewer patterns than path elements, 
      /// but not more. If the number of patterns is less
      /// than the number of path elements, the patterns
      /// are compared to the last n path elements, where 
      /// n is the number of patterns.
      /// 
      /// The default behavior in cases where there are 
      /// fewer patterns than path elements is to compare
      /// the last pattern to the last path element and
      /// compare preceding elements of the path and the
      /// patterns until all patterns have been compared.
      /// 
      /// To cause comparisons to start with the first path
      /// and pattern elements, add the special pattern "|" 
      /// as the last pattern argument. If the "|" special
      /// pattern appears in the argument list, arguments
      /// that follow it are ignored and not compared to
      /// the path.
      /// 
      /// If the special "|" character is used as a pattern,
      /// and the number of patterns is less than the number
      /// of path elements, the use of "|" effective acts as
      /// a wildcard, where there is a match if all supplied
      /// pattern elements match a corresponding path element,
      /// regardless of how many additional elements there are
      /// in the path.
      /// 
      /// For the purpose of this API, dynamic block names
      /// always appear in the path, in lieu of anonymous 
      /// block names.  
      /// </summary>
      /// <param name="patterns"></param>
      /// <returns></returns>

      public bool PathMatches(params string[] patterns)
      {
         if(patterns == null || patterns.Length == 0
            || patterns.Length > path.Count)
            return false;
         int offset = 0;
         if(patterns[patterns.Length - 1] != "|")
            offset = path.Count - patterns.Length;
         for(int i = 0; i < patterns.Length - 1; i++)
         {
            if(string.IsNullOrEmpty(patterns[i]))
               throw new ArgumentException("Invalid pattern element");
            if(patterns[i] == "+")
               return true;
            if(!path[i + offset].Matches(patterns[i]))
               return false;
         }
         return true;
      }

      /// <summary>
      /// Should indicate if the entity being visited
      /// can be exploded and transformed to the space 
      /// containing the outer-most container block 
      /// reference.
      /// </summary>

      public bool IsUniformlyScaled
      {
         get
         {
            AssertIsContextual();
            return Transform.IsUniscaledOrtho();
         }
      }

      public Scale3d CompoundScaleFactors
      {
         get
         {
            AssertIsContextual();
            if(!containers.Any())
               return new Scale3d(1.0);
            else
               return containers.Aggregate<BlockReference, Scale3d>(
                  new Scale3d(1.0), (s, br) => s * br.ScaleFactors);
         }
      }

      void VisitBlockTableRecord(ObjectId btrId, bool nested = false)
      {
         BlockTableRecord btr = trans.GetObject<BlockTableRecord>(btrId);
         if(btr.IsLayout)
            nested = true;
         if(CanVisit(btr))
         {
            ++blockCount;
            foreach(ObjectId id in btr)
            {
               if(CanVisit(id))
               {
                  var entity = Unsafe.As<Entity>(trans.GetObject(id, OpenMode.ForRead, false));
                  if(IsBlockReference(entity))
                  {
                     BlockReference blkref = Unsafe.As<BlockReference>(entity);
                     ObjectId blockDefId = getBlockDefId(blkref);
                     if(CanVisit(blkref) && contextual || visitedBlocks.Add(blockDefId))
                     {
                        VisitBlockReference(blkref);
                     }
                  }
                  else if(!nested)
                  {
                     DynamicDispatch(entity);
                  }
               }
            }
         }
      }

      /// <summary>
      /// The Visit(BlockReference) method can be overridden in
      /// a derived type to control if the argument's definition
      /// is visited or not. If overridden and the base method is 
      /// not supermessaged, the contents of the referenced block 
      /// will not be visited.
      /// 
      /// This method allows more granular control over what block
      /// definitions are visited, and allows the overriding type
      /// to establish/maintain scope or context.
      /// 
      /// To avoid visiting the argument, overrides can simply not
      /// supermessage this base method.
      /// </summary>

      protected virtual void VisitBlockReference(BlockReference blkref)
      {
         Push(blkref);
         OnVisiting(blkref);
         VisitBlockTableRecord(getBlockDefId(blkref), false);
         OnVisited(blkref);
         Pop();
         if(VisitAttributes)
         {
            var attributes = blkref.AttributeCollection;
            if(attributes.Count > 0)
            {
               attributeOwner = blkref;
               OnVisitingAttributes(blkref);
               try
               {
                  foreach(ObjectId id in attributes)
                  {
                     DynamicDispatch(trans.GetObject<AttributeReference>(id));
                  }
               }
               finally
               {
                  attributeOwner = null;
               }
               OnVisitedAttributes(blkref);
            }
         }
      }

      bool CanVisit(ObjectId id)
      {
         return predicate(id);
      }

      /// <summary>
      /// OnVisiting()
      /// OnVisited()
      /// OnVisitingAttributes()
      /// OnVisitedAttributes()
      /// 
      /// Provides a means for derived types to get control just
      /// before a BlockReference is visited after that block 
      /// reference has been pushed onto stack, and just before it 
      /// is popped off of the stack.
      /// 
      /// If the Visit(BlockReference) overload is implemented,
      /// that overload is called before the block reference has 
      /// been pushed onto the reference stack. These methods are 
      /// provided for use cases that require the block reference 
      /// to be on the reference stack.
      /// </summary>
      /// <param name="blkref">The BlockReference that is about 
      /// to be visited.</param>

      protected virtual void OnVisiting(BlockReference blkref)
      {
      }

      /// <summary>
      /// Called just before the block reference that was just
      /// visited is popped off of the reference stack. 
      /// 
      /// See the DeepExplodeVisitor class for an example that
      /// shows how this method can be leveraged to operate on 
      /// multiple entities from a single block's definition in 
      /// the context of each insertion of the block, after all 
      /// of the block's entities have been visited.
      /// </summary>
      /// <param name="blkref">The BlockReference that was 
      /// just visited</param>

      protected virtual void OnVisited(BlockReference blkref)
      {
      }

      /// <summary>
      /// If AttributeReferences are visited, this method will
      /// be called after all attributes of the current block
      /// reference have been visited.
      /// </summary>
      /// <param name="blkref">The BlockReference that owns the
      /// AttributeReferences that were visited.</param>

      protected virtual void OnVisitedAttributes(BlockReference blkref)
      {
      }

      /// <summary>
      /// If AttributeReferences are visited, this method will
      /// be called just before attributes of the current block
      /// reference are visited.
      /// </summary>
      /// <param name="blkref">The BlockReference that owns the
      /// AttributeReferences that are to be visited.</param>

      protected virtual void OnVisitingAttributes(BlockReference blkref)
      {
      }

      /// <summary>
      /// Allows derived types to specialize StackEntry and
      /// provide instances of that specialization:
      /// </summary>
      /// <param name="blkref"></param>
      /// <returns></returns>
      
      protected virtual T GetStackEntry(BlockReference blkref)
      {
         return (T) new BlockVisitor<T>.StackEntry(stack, blkref);
      }

      protected virtual void Push(BlockReference blkref)
      {
         if(root == null)
            root = blkref;
         /// Begin retrofit 
         stack.Push(GetStackEntry(blkref));
         /// End retrofit
         containers.Push(blkref);
         path.Add(GetEffectiveName(blkref));
         subentPath.Insert(subentPath.Count - 1, blkref.ObjectId);
         blockTransform.Invalidate();
         ++depth;
      }

      protected virtual void Pop()
      {
         Assert.MustBeTrue(containers.Count > 0);
         containers.Pop();
         if(containers.Count == 0)
            root = null;
         subentPath.RemoveAt(subentPath.Count - 2);
         blockTransform.Invalidate();
         if(path.Count > 0) // should never be false
            path.RemoveAt(path.Count - 1);
         --depth;
         Assert.MustBeFalse(depth < 0);
         /// Begin retrofit 
         stack.Pop();
         /// End retrofit
      }

      /// <summary>
      /// The ObjectId of the BlockTableRecord that
      /// directly owns the outer-most containing
      /// BlockReference, which is the value of the 
      /// Root property.
      /// 
      /// If the outer-most container block reference
      /// is directly inserted into a Layout, the result
      /// returned will be the ObjectId of the layout's
      /// BlockTableRecord.
      /// </summary>

      public ObjectId BlockId
      {
         get
         {
            AssertIsContextual();
            if(rootBlockId.IsNull)
            {
               rootBlockId = Root.BlockId;
               ownerIsLayout = rootBlockId.GetValue<BlockTableRecord, bool>(
                  btr => btr.IsLayout);
            }
            return rootBlockId;
         }
      }

      /// <summary>
      /// Returns a value indicating if the BlockTableRecord
      /// whose ObjectId is the value of the BlockId property
      /// is a Layout block.
      /// </summary>

      public bool OwnerIsLayout => ownerIsLayout;

      /// <summary>
      /// The number of entities that were not handled because 
      /// there is no specific handler implemented for their types:
      /// </summary>

      /// <summary>
      /// The number of entities that were visited
      /// </summary>

      public int VisitedCount => visitedCount;

      // The current depth of nesting (and the number
      // of block references on the reference stack)
      public int Depth => depth;

      /// <summary>
      /// Dynamically dispatches a call to Visit() to
      /// the overload of that method having the most-
      /// closely matching argument type. 
      /// </summary>

      protected void DynamicDispatch(Entity entity)
      {
         currentEntity = entity;
         dynamicThis.Visit((dynamic)entity);
         ++visitedCount;
      }

      /// <summary>
      /// Can be overridden in derived types to control what
      /// BlockTableRecords are visited. If an override of
      /// this method returns false, the argument will not be
      /// visited at all, including nested block references.
      /// 
      /// Typically, overrides of this method return the result
      /// of calling the IsUserBlock() extension method, which
      /// excludes Xref/Overlay, anonymous, layout, and tables.
      /// 
      /// The result is NOT cached and this method is called
      /// every time a BlockTableRecord is encountered.
      /// </summary>
      /// <param name="btr">The BlockTableRecord whose contents
      /// are to be visited.</param>
      /// <returns>A value indicating if the BlockTableRecord's
      /// contents should be visited.</returns>

      protected virtual bool CanVisit(BlockTableRecord btr)
      {
         return IsElegibleBlock(btr); // Default: all blocks are visited
      }

      /// <summary>
      /// Can be overridden in a derived type to control if
      /// the argument is visited, on a per-instance basis.
      /// 
      /// Unlike the overload taking a BlockTableRecord, this
      /// method can selectively decide if the argument should
      /// be visited. 
      /// </summary>
      /// <param name="btr">The BlockReference whose contents
      /// are to be visited.</param>
      /// <returns>A value indicating if the BlockReference's
      /// contents should be visited.</returns>

      protected virtual bool CanVisit(BlockReference blockref)
      {
         return true;
      }

      protected bool IsElegibleBlock(BlockTableRecord btr)
      {
         if(UserBlocksOnly)
            return !(btr.IsAnonymous
               || btr.IsFromExternalReference
               || btr.IsFromOverlayReference
               || btr.IsDependent);
         return true;
      }

      /// <summary>
      /// The default handler for types of entities for 
      /// which a more type-specific Visit() handler has 
      /// not been implemented.
      /// 
      /// This handler is called if no overload of Visit() 
      /// taking an argument that more-closely matches the 
      /// runtime type of the argument exists.
      /// 
      /// This method does nothing other than serve as a
      /// default handler that will be called by the DLR if 
      /// no other Visit() method is found, and is needed 
      /// to avoid a runtime binding failure.
      /// </summary>

      public virtual void Visit(Entity entity)
      {
      }

      public long Elapsed => elapsed;

      /// <summary>
      /// Custom Objects and Tables are not treated
      /// as BlockReferences. The exactMatch argument
      /// provided to the constructor does not apply
      /// to Tables, and as such they can be visited 
      /// by implementing a Visit(Table) handler.
      /// </summary>

      static bool IsBlockReference(DBObject obj)
      {
         if(obj == null)
            return false;
         if(DBObject.IsCustomObject(obj.Id))
            return false;
         if(obj is BlockReference)
            return !(obj is Table);
         return false;
      }
   }
}
