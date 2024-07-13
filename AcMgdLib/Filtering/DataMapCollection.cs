/// DataMapCollection.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Implements a graph of DBObjectDataMap instances. 

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Expressions.Extensions;
using System.Diagnostics.Extensions;


namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   public class DataMapCollection : KeyedCollection<(Type, Expression), DataMap>
   {
      DataMap owner;

      public DataMapCollection(DataMap owner) : base(DefaultComparer)
      {
         Assert.IsNotNull(owner, nameof(owner));
         this.Add(owner);
         this.owner = owner; 
      }

      protected override (Type, Expression) GetKeyForItem(DataMap item)
      {
         return (item.TValueSourceType, item.KeySelectorExpression);
      }

      public DataMap this[Type type, Expression expression]
      {
         get
         {
            DataMap map = null;
            base.Dictionary.TryGetValue((type, expression), out map);
            return map;
         }
      }

      public bool ReferenceContains(DataMap map)
      {
         foreach(var item in base.Items)
         {
            if(object.ReferenceEquals(item, map)) 
               return true;
         }
         return false;
      }

      public IEnumerable<DataMap> Children => this.Where(item => item != owner);

      public int ChildCount => base.Count - 1;

      public bool HasChildren => base.Count > 1;

      static ItemComparer DefaultComparer = new ItemComparer();

      class ItemComparer : IEqualityComparer<(Type, Expression)>
      {
         static ExpressionEqualityComparer comparer = ExpressionEqualityComparer.Instance;

         public bool Equals((Type, Expression) x, (Type, Expression) y)
         {
            return x.Item1 == y.Item1 && comparer.Equals(x.Item2, y.Item2);
         }

         public int GetHashCode((Type, Expression) obj)
         {
            return HashCode.Combine(obj.Item1.GetHashCode(),
               comparer.GetHashCode(obj.Item2));
         }
      }
   }


}



