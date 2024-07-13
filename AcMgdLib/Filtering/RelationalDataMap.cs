/// DBObjectDataMapBase.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

using System;
using System.Text;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// Non-AutoCAD dependent placeholder for future 
   /// extensions of this class hierarchy.
   /// 
   /// This class can have no dependence on AutoCAD.
   /// 
   /// Planned but not completed: 
   /// 
   /// - Promote non AutoCAD-dependent members from 
   ///   derived types to this type.
   ///   
   /// </summary>

   public abstract class RelationalDataMap<TKeySource, TKey, TValueSource, TValue> 
      : DataMap
   {
      /// <summary>
      /// The type of the object from which cache keys are obtained:
      /// </summary>
      public override Type TKeySourceType => typeof(TKeySource);

      /// <summary>
      /// The type of the cache key:
      /// </summary>
      public override Type TKeyType => typeof(TKey);

      /// <summary>
      /// The type of the object from which cached values are obtained:
      /// </summary>
      public override Type TValueSourceType => typeof(TValueSource);

      /// <summary>
      /// The type of the cached values:
      /// </summary>
      public override Type TValueType => typeof(TValue);

      /// <summary>
      /// Diagnostics function that displays the
      /// type of generic arguments.
      /// </summary>

      public override string Dump(string label = null, string pad = "")
      {
         StringBuilder sb = new StringBuilder(base.Dump(label, pad));
         if(string.IsNullOrWhiteSpace(label))
            label = this.ToIdentity();
         else
            label += $" {this.ToIdentity()}";
         sb.AppendLine($"{pad}{label}: ");
         sb.AppendLine($"{pad}KeySouce Type:      {TKeySourceType.Name}");
         sb.AppendLine($"{pad}Key Type:           {TKeyType.Name}");
         sb.AppendLine($"{pad}ValueSource Type:   {TValueSourceType.Name}");
         sb.AppendLine($"{pad}Value Type:         {TValueType.Name}");
         string s = Parent?.ToIdentity() ?? "(none)";
         sb.AppendLine($"{pad}Parent filter:      {s}");
         return sb.ToString();
      }
   }


}



