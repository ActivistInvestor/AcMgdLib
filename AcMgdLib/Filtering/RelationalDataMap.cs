/// RelationalDataMap.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

using System;
using System.Diagnostics.Extensions;
using System.Linq.Expressions.Predicates;
using System.Security.Cryptography.X509Certificates;
using System.Text;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// Non-AutoCAD dependent types that exist primiarly
   /// for the purpose of allowing implicit conversions
   /// to delegate types that use the generic arguments
   /// defined in these types.
   /// 
   /// These classs can have no dependence on AutoCAD.
   /// 
   /// Planned but not completed: 
   /// 
   /// - Promote non AutoCAD-dependent members from 
   ///   derived types to this type.
   ///   
   /// This class is slated for refactoring, to include methods from 
   /// derived types that are dependent only on the generic arguments
   /// defined in this type.
   /// </summary>
   /// <typeparam name="TKeySource"></typeparam>
   /// <typeparam name="TValue"></typeparam>

   public abstract class DataMap<TKeySource, TValue> : DataMap
   {
      /// <summary>
      /// The type of the cache key:
      /// </summary>
      public override Type TKeyType => typeof(TKeySource);

      /// <summary>
      /// The type of the cached value:
      /// </summary>
      public override Type TValueType => typeof(TValue);

      public abstract Func<TKeySource, TValue> Accessor { get; }

      public static implicit operator Func<TKeySource, TValue>(DataMap<TKeySource, TValue> dataMap)
      {
         Assert.IsNotNull(dataMap, nameof(dataMap));
         return dataMap.Accessor;
      }
   }

   /// <summary>
   /// A RelationalDataMap is a DataMap wherein values are obtained 
   /// from a 'related' object, rather than directly from the object
   /// for which the value is being requested. The related objects are 
   /// indirectly referenced using a key (the TKey generic argument in 
   /// this type) that is obtained from the object for which the mapped 
   /// value is to be obtained for (the TKeySource generic argument in 
   /// this type).
   /// 
   /// Hence, given a TKeySource object, this class obtains a TKey from
   /// the TKeySource object using a user-supplied delegate. The TKey is
   /// then used to access a TValueSource object, which is then used to
   /// produce the resulting TValue using a second user-supplied delegate.
   /// Derived types typically cache TValues, which are associated with 
   /// and lookup up using TKeys.
   /// 
   /// At its core, this type is a wrapper for a Dictionary<TKey, TValue> 
   /// and typically uses an instance of same as the storage medium.
   /// 
   /// This base class does not know how to obtain a TValueSource object
   /// from a TKey, which must be implemented by derived types.
   /// 
   /// This class is slated for refactoring to include methods from 
   /// derived types that are dependent only on the generic arguments
   /// defined in this type.
   /// </summary>
   /// <typeparam name="TKeySource">The type of the object from which 
   /// a TKey is obtained</typeparam>
   /// <typeparam name="TKey">The type of the key obtained from a
   /// TKeySource, that is used to obtain a TValueSource</typeparam>
   /// <typeparam name="TValueSource">The type of the object from 
   /// which a TValue is obtained</typeparam>
   /// <typeparam name="TValue">The type of the associated value</typeparam>

   public abstract class RelationalDataMap<TKeySource, TKey, TValueSource, TValue> 
      : DataMap<TKeySource, TValue>
   {
      /// <summary>
      /// The type of the object from which cache keys are obtained:
      /// </summary>
      public override Type TKeySourceType => typeof(TKeySource);

      /// <summary>
      /// The type of the object from which cached values are obtained:
      /// </summary>
      public override Type TValueSourceType => typeof(TValueSource);

      /// <summary>
      /// Diagnostics function that displays the
      /// type of generic arguments.
      /// </summary>

      public override string Dump(string label = null, string pad = "")
      {
         StringBuilder sb = new StringBuilder(base.Dump(label, pad));
         if(string.IsNullOrWhiteSpace(label))
            label = this.ToIdString();
         else
            label += $" {this.ToIdString()}";
         sb.AppendLine($"{pad}{label}: ");
         sb.AppendLine($"{pad}KeySouce Type:      {TKeySourceType.CSharpName}");
         sb.AppendLine($"{pad}Key Type:           {TKeyType.CSharpName}");
         sb.AppendLine($"{pad}ValueSource Type:   {TValueSourceType.CSharpName}");
         sb.AppendLine($"{pad}Value Type:         {TValueType.CSharpName}");
         string s = Parent?.ToIdString() ?? "(none)";
         sb.AppendLine($"{pad}Parent filter:      {s}");
         return sb.ToString();
      }
   }


}



