/// EntityVisitor.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Classes that facilitate recursively operating 
/// on nested entites in BlockTableRecords.

using System;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A class that implements the Visitor pattern for
   /// AutoCAD entities, using System.Dynamic as the
   /// primary dispatch mechansim. This class uses a
   /// hybrid model of the Visitor pattern, involving
   /// both dynamic and non-dynamic dispatch. 
   /// 
   /// This class allows a user to operate on individual
   /// entities contained in blocks in a recursive manner.
   /// 
   /// Using this class requires a type to be derived from 
   /// it, and one or more overloads of the Visit() method
   /// be implemented in the derived type.
   /// 
   /// Block entities can be operated on in a type-wise
   /// manner by type or base type by providing overloaded 
   /// versions of the Visit() method taking an argument 
   /// of the type(s) to be visited or a base type of the 
   /// types to be visited. 
   /// 
   /// For example, to visit all Line entities in visited 
   /// blocks, one simply needs to add an overload of the
   /// Visit() method taking a Line as its only argument:
   /// 
   ///    public void Visit(Line line)
   ///    {
   ///       // TODO: Operate on the line argument
   ///    }
   /// 
   /// Abstract base types can be specified as an argument
   /// to an overload of Visit(), which causes any type that
   /// is derived from the specified base type to be passed 
   /// to the method. For example, to visit all Curve-based
   /// entities, one only need to do this:
   /// 
   ///    public void Visit(Curve curve)
   ///    {
   ///       // TODO: Operate on the curve argument
   ///    }
   ///    
   /// Overloads of the Visit() method can 'override' other
   /// overloads taking a less-specific type. For example,
   /// if a derived type had both of the above example Visit()
   /// methods, the one that takes a Curve argument would be
   /// passed all types of Curve entities, EXCEPT for Lines,
   /// which would be passed to the overload taking a Line
   /// argument.
   /// 
   /// If there is no interest in visiting a certain type
   /// of entity, they will not be exposed to a derived
   /// type that does not implement an overloaded Visit()
   /// method taking an argument of those types or of any
   /// base types.
   /// 
   /// Operational modes:
   /// 
   /// The EntityVisitor class has two modes of operation.
   /// 
   /// 1. Non-Contextual: 
   /// 
   /// In this mode, blocks and the entities they contain
   /// are visited once and only once, regardless of how 
   /// many insertions of a block exist. This mode can be
   /// used to visit every entity that a block reference
   /// is visually-dependent on, exactly once.
   /// 
   /// 2. Contextual:
   /// 
   /// In this mode, blocks and the entities they contain
   /// can be visited multiple times. Every block definition 
   /// is visited once for each reference to it, recursively. 
   /// This mode can be used to visit the contents of one or
   /// more containing block references, recursively in their
   /// nesting context. 
   /// 
   /// When a Visit() overload taking a type derived from the
   /// Entity class is called, the Containers property will
   /// hold the entity's containing block references, and the
   /// BlockTransform property will be set to the compound
   /// object transform that describes the transformation of
   /// the current entity to the space containing the outer-
   /// most container block reference (or in the case in which
   /// the outer-most block is a layout, the coordinate system
   /// of the layout).
   /// 
   /// The Contextual property controls this mode of operation,
   /// and must be set prior to calling the Visit() main entry 
   /// point method taking an ObjectId. The default value of the
   /// Contextual property is true.
   /// 
   /// See the MyEntityVisitor, DeepExplodeVisitor, and
   /// HighlightCirclesVisitor example classes for examples
   /// showing the use of this class.
   /// 
   /// The EntityVisitor<T> class:
   /// 
   /// The primary abstract base type, taking a generic
   /// argument that defines what type(s) of entities are
   /// visited by an instance of a derived type.
   /// 
   /// Note that the generic argument defines what types
   /// of entities can be visited by this type, but it is
   /// overloads of the Visit() method that determine which
   /// entities are actually visited. In derived types, if 
   /// no overload of the Visit() method matches the type of
   /// an entity, it will not be visited in a derived type.
   /// </summary>
   /// <typeparam name="T">A type derived from Entity that
   /// determines what types are elegible to be visited. The
   /// instance will visit instances of the generic argument
   /// and instances of any types derived from it. Note that
   /// BlockReference is not a valid generic argument.
   /// </typeparam>

   public abstract class EntityVisitor<T> : BlockReferenceVisitor where T:Entity
   {
      static bool includeAttributes = typeof(T).IsAssignableFrom(typeof(AttributeReference));

      public EntityVisitor(bool exactMatch = false)
         : base(exactMatch, RXClass<T>.GetMatchExpression(exactMatch))
      {
      }

      /// <summary>
      /// AttributeReferences can only be visited if the
      /// VisitAttributes property is true, AND the type
      /// used as the generic argument can be assigned to
      /// an AttributeReference (which means that it must
      /// be AttributeReference, DBText, or Entity).
      /// </summary>
      
      public override bool VisitAttributes 
      { 
         get => includeAttributes && base.VisitAttributes; 
      }

      /// <summary>
      /// The generic argument cannot be BlockReference.
      /// There is no generic constraint that can enforce 
      /// that restriction:
      /// </summary>

      static EntityVisitor()
      {
         if(typeof(T) == typeof(BlockReference))
         {
            throw new ArgumentException(typeof(EntityVisitor<T>).CSharpName() +
               ": Generic argument cannot be BlockReference");
         }
      }

      /// <summary>
      /// Currently unused, included for future optimizations
      /// when the generic argument type is AttributeReference,
      /// which will allow all other types to be skipped without
      /// having to open them.
      /// </summary>

      protected static bool isAttributeVisitor 
         = typeof(T) == typeof(AttributeReference);

   }

   /// This non-generic derived type operates on all entities.

   public abstract class EntityVisitor : EntityVisitor<Entity>
   {
   }
}
