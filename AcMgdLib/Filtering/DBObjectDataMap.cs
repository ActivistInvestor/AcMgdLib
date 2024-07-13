/// DBObjectDataMap.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics.Extensions;
using AcRx = Autodesk.AutoCAD.Runtime;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{
   /// <summary>
   /// A class that caches data obtained from DBObjects that 
   /// are referenced by other DBObjects, and provides access 
   /// to the cached data through the referencing objects.
   /// 
   /// The generic description above doesn't adequately convey
   /// how this class can be extremely useful for the purpose
   /// of executing queries against many DBObjects, in the same
   /// way that AutoCAD's filtered selection mechanism does. 
   /// 
   /// See the practical examples and discussion below for more 
   /// on how this class can be used to perform relational data
   /// queries. 
   /// 
   /// While one can say that this class is, at its essence, an
   /// glorified wrapper around the .NET Dictionary class, there
   /// is a bit more to it than that. 
   /// 
   /// This class automates and simplifies a common pattern of
   /// usage of the Dictionary class, by providing a simplified
   /// way to cache data obtained from referenced objects, and
   /// to easily and quicly access that data through referencing 
   /// objects.
   /// 
   /// Consider the relationship between Entities and Layers.
   /// Every entity references a layer. Every layer can be
   /// referenced by many entities, and so the Entity is the 
   /// referencing object and LayerTableRecord is the object
   /// that's referenced by the referencing object. 
   /// 
   /// This class allows you to access data obtained from a 
   /// referenced object (a LayerTableRecord), through any
   /// referencing object (entities). Because the referencing 
   /// objects have a many-to-one relationship with referenced 
   /// objects (e.g., many entities reference the same layer), 
   /// this class models that relationship, providing an access 
   /// layer allowing code to access data of referenced objects 
   /// through objects that reference them.
   /// 
   /// The <typeparamref name="TFiltered"/>> object is used to
   /// obtain an ObjectId that is used as the key to store the
   /// cached data. The <typeparamref name="TCriteria"/>> is
   /// used to produce the cached data. In a typical use case,
   /// the <typeparamref name="TFiltered"/> is an Entity, and
   /// the <typeparamref name="TCriteria"/>is an object that
   /// is referenced by an entity (such as the LayerTableRecord
   /// of the layer which the entity resides on/references).
   /// 
   /// <typeparamref name="TFiltered"/>usually has a many-to-one
   /// relationship with the <typeparamref name="TCriteria"/>
   /// (e.g., many entities can reference the same layer). While
   /// a <typeparamref name="TCriteria"/> always has a one-to-one
   /// relationship with the cached data.
   /// 
   /// This simple example uses Entity as the <typeparamref name="TFiltered"/>
   /// and LayerTableRecord as the <typeparamref name="TCriteria"/>,
   /// and bool as the <typeparamref name="TValue"/>, and enables
   /// caching of a layer's IsLocked property value, avoiding the
   /// need to repeatedly open the LayerTableRecord to get it:
   /// <code>
   /// 
   ///  var lockedLayers = new DBObjectDataMap<Entity, LayerTableRecord, bool>(
   ///        entity => entity.LayerId, 
   ///        layer => layer.IsLocked
   ///  );
   ///      
   /// </code>
   /// DBObjectDataMap doesn't know anything about what it caches;
   /// where it comes from; or where the keys that are used to lookup 
   /// cached data come from, so it requires two delegates that do
   /// those things. In the above constructor call, the first delegate
   /// takes an Entity and returns the value of its LayerId property,
   /// which is an ObjectId. That ObjectId is used as the key to store
   /// the cached data. The cache takes the ObjectId and looks for data
   /// in the cache using that ObjectId as a key. 
   /// 
   /// If data for the ObjectId is found in the cache, it is returned. 
   /// Otherwise, the DBObjectDataMap opens the DBObject referenced by 
   /// the ObjectId and passes the opened DBObject to the second delegate 
   /// (which takes a LayerTableRecord as its argument) which returns the 
   /// data that is to be cached, keyed to the ObjectId returned by the 
   /// first delegate. It then adds the resulting data to the cache and 
   /// returns it. 
   /// 
   /// On subsequent requests for data for that same ObjectId/key, it is 
   /// retrieved from the cache very quickly, and without having to open 
   /// the referenced LayerTableRecord again.
   /// 
   /// Given an instance created via the above constructor call, one
   /// can find out if an entity resides on a locked layer with this:
   /// 
   ///    Entity someEntity = /// assign to an Entity
   ///    
   ///    bool isOnLockedLayer = lockedLayers[someEntity];
   ///    
   /// Lifetime and automated cache management:
   /// 
   /// The life of a DBObjectDataMap is typically the life of whatever
   /// operations it is used with, and should not extend beyond the point 
   /// where the possiblity that objects whose data has been cached can be 
   /// modified. Once a DBObject whose data has been cached is modified, 
   /// the cached data is no-longer considered valid.
   /// 
   /// ****************************************************************
   /// Note: The following documentation applies to a specialization of
   /// DBObjectDataMap that is not yet included in this distribution.
   /// 
   /// However, in specialized/advanced use cases it is possible to allow
   /// an instance of this type to persist across changes to DBObjects whose
   /// data has been cached in an instance, by setting the ObserveChanges
   /// property to true. Doing that enables an ObjectOverrule that monitors
   /// changes to the DBObjects whose data is cached, and invalidates that 
   /// cached data whenever the source DBObject is modified.
   /// 
   /// If the ObservedChanges property is set to true, the instance <em>must 
   /// be deterministically-disposed when no longer needed</em>, to disable 
   /// the ObjectOverrule that monitors changes. If ObserveChanges is false
   /// (which it is by default) there's no need to dispose the instance.
   /// 
   /// Generic characteristics:
   /// 
   /// 1. An ObjectId that references a TCriteria must be 
   ///    reachable using an instance of a TFiltered.
   ///    
   /// 2. A TValue must be reachable using an instance of a 
   ///    TCriteria.
   /// 
   /// 3. The ObjectId obtained from a TFiltered is a key that is
   ///    permanently associated with the TValue obtained from the
   ///    TCriteria referenced by the ObjectId key.
   /// 
   /// </summary>
   /// <typeparam name="TFiltered">The type of DBObject that is
   /// used to produce the key used to retrieve cached data</typeparam>
   /// <typeparam name="TCriteria">The type of DBObject from
   /// which the cached data is derived</typeparam>
   /// <typeparam name="TValue">The type of the cached data</typeparam>

   public class DBObjectDataMap<TKeySource, TValueSource, TValue> : 
         RelationalDataMap<TKeySource, ObjectId, TValueSource, TValue>
      where TKeySource : DBObject
      where TValueSource : DBObject
   {
      Dictionary<ObjectId, TValue> map = null;

      Expression<Func<TKeySource, TValue>> getValueExpression = null;
      protected readonly Expression<Func<TKeySource, ObjectId>> keySelectorExpression = null;
      Func<TKeySource, TValue> getValue = null;
      protected Func<TKeySource, ObjectId> keySelector;
      protected Func<TValueSource, TValue> valueSelector;
      static readonly bool isPredicate = typeof(TValue) == typeof(bool);
      protected Dictionary<ObjectId, TValue> Map => map;

      public DBObjectDataMap(
         Expression<Func<TKeySource, ObjectId>> keySelector,
         Func<TValueSource, TValue> valueSelector) 
      {
         Assert.IsNotNull(keySelector, nameof(keySelector));
         Assert.IsNotNull(valueSelector, nameof(valueSelector));
         this.map = CreateMap();
         this.keySelectorExpression = keySelector;
         this.keySelector = keySelector.Compile();
         this.valueSelector = valueSelector;
         /// Critical for optimized performance in unmodified filters
         this.getValueExpression = arg => GetValue(arg);
         this.getValue = GetValue;
      }

      /// <summary>
      /// Indicates if the getValue delegate represents the
      /// default GetValue() method, or a delegate that was
      /// compiled from one or more added expressions.
      /// </summary>

      protected bool IsGetValueModified => getValue != GetValue;

      protected virtual Dictionary<ObjectId, TValue> CreateMap()
      {
         return new Dictionary<ObjectId, TValue>();
      }

      /// <summary>
      /// Used as part of the key in the dictionary that 
      /// holds child filter nodes within a parent filter.
      /// </summary>
      
      public override Expression KeySelectorExpression => keySelectorExpression;

      /// <summary>
      /// Assigning a value to this expression requires that the
      /// current value be included in the new expression (typically
      /// combined using a logical and/or operation). 
      /// 
      /// Without including a call to the initial value of this 
      /// expression in a modifed version, there'll be a failure.
      /// This requirement may not make sense at this level, but 
      /// does at the DBObjectFilter level, where TValue is a bool.
      /// 
      /// Example (when TValue is bool):
      ///   
      ///   filter.GetValueExpression = filter.GetValueExpression.And(newExpression);
      ///    
      /// The above line combines the existing expression and another 
      /// expression in a logical 'and' operation, ensuring the initial 
      /// value of this property is invoked.
      /// 
      /// DBObjectFilter performs this operation internally, but does not
      /// allow the value of this property to be assigned from the outside.
      ///   
      /// </summary>
      protected Expression<Func<TKeySource, TValue>> GetValueExpression
      {
         get => getValueExpression; 
         set
         {
            Assert.IsNotNull(value, nameof(value));
            if(getValueExpression != value)
            {
               CheckInitialized();
               if(value.CanReduce)
                  value.ReduceAndCheck();
               getValueExpression = value;
               getValue = getValueExpression.Compile();
            }
         }
      }

      protected void CheckInitialized()
      {
         if(initialized)
            throw new InvalidOperationException("Cannot modify criteria after use");
      }

      /// <summary>
      /// Produces a value for the given <typeparamref name="TKeySource"/>> 
      /// instance.
      /// </summary>
      /// <param name="key">A <typeparamref name="TKeySource"/> whose
      /// associated value is to be retrieved.</param>
      /// <returns>The associated value</returns>

      public TValue this[TKeySource key] => getValue(key);

      /// <summary>
      /// The implicit conversion operator returns the value
      /// of this, which is the method called by the indexer.
      /// </summary>

      public Func<TKeySource, TValue> Accessor => getValue;

      /// <summary>
      /// This method is not directly callable from the 
      /// outside. It is the default value of the getValue
      /// delegate and as such, may be called as part of
      /// a more-complex expression that can result from 
      /// logically-combining this method with one or more
      /// other expressions. The getValue delegate always
      /// contains the complete predicate that calls this 
      /// method internally.
      /// </summary>
      /// <param name="keySource">The <typeparamref name="TKeySource"/> instance
      /// for which a TValue is being requested.</param>
      /// <returns>The TValue for the given <paramref name="keySource"/></returns>
      /// <exception cref="AcRx.Exception"></exception>

      bool initialized = false;
      TValue GetValue(TKeySource keySource)
      {
         if(!initialized)
         {
            initialized = true;
            OnInitialized();
         }
         ObjectId id = keySelector(keySource);
         if(id.IsNull)
            return GetDefaultValue(keySource);
         if(!map.TryGetValue(id, out TValue value))
            value = GetValueForKey(id, keySource);
         return value;
      }

      protected virtual void OnInitialized()
      {
         if(Trace && this.Parent == null)
            AcConsole.WriteLine(this.Dump());
      }

      /// <summary>
      /// If the keySelector returns ObjectId.Null, this
      /// will be called to provide a value (or throw an
      /// exception, if appropriate).
      /// 
      /// In the EffectiveColorMap, the keySelector returns
      /// ObjectId.Null to signal that the entity argument's
      /// color is not 'BYLAYER', causing the entity's color
      /// to be returned by an override of this method.
      /// </summary>
      /// <param name="keySource"></param>
      /// <returns></returns>
      /// <exception cref="NotImplementedException"></exception>

      protected virtual TValue GetDefaultValue(TKeySource keySource)
      {
         throw new AcRx.Exception(AcRx.ErrorStatus.NullObjectId);
      }

      /// <summary>
      /// Opens the DBObject from which the value is obtained,
      /// gets the value using the valueSelector delegate, and
      /// caches and returns it.
      /// 
      /// This method is only called if there is no existing
      /// cached value associated with the argument.
      /// 
      /// For performance reasons, this method avoids using
      /// transactions and opens the DBObject directly.
      /// 
      /// </summary>
      /// <param name="id">The ObjectId of a TCriteria object</param>
      /// <param name="keySource">The TFiltered instance from which the 
      /// <paramref name="id"/> argument was obtained.</param>
      /// <returns>The result of invoking the valueSelector delegate
      /// on an instance of a TCriteria having the given ObjectId</returns>

      protected virtual TValue GetValueForKey(ObjectId id, TKeySource keySource)
      {
         AcRx.ErrorStatus.NullObjectId.ThrowIf(id.IsNull);
         using(TValueSource source = (TValueSource)id.Open(OpenMode.ForRead))
         {
            TValue value = GetValueFromSource(source);
            if(value != null)
               Add(source, value);
            return value;
         }
      }

      /// <summary>
      /// Can be overridden to access the TCriteria object, and/or
      /// override the valueSelector delegate. The default implementation
      /// returns the result of invoking the valueSelector delegate on 
      /// the argument.
      /// 
      /// Overrides of this can supermessage this base method to return
      /// the value, before and/or after they operate on the argument.
      /// </summary>
      /// <param name="source">The <typeparamref name="TValueSource"/> used
      /// to produce the result</param>
      /// <returns>The result of invoking the valueSelector delegate
      /// on the <typeparamref name="TValueSource"/> instance</returns>

      protected virtual TValue GetValueFromSource(TValueSource source)
         => valueSelector(source);

      /// <summary>
      /// Adds the given value to the cache.
      /// </summary>
      /// <param name="source"></param>
      /// <param name="value"></param>

      protected virtual void Add(TValueSource source, TValue value)
      {
         if(value != null)
         {
            map[source.ObjectId] = value;
            OnMapChanged(MapChangeType.ItemAdded, source.ObjectId);
         }
      }

      /// <summary>
      /// Removes an entry from the cache
      /// </summary>

      protected virtual void Remove(ObjectId id)
      {
         if(map.Remove(id))
            OnMapChanged(MapChangeType.ItemRemoved, id);
      }

      /// <summary>
      /// Clears the cache.
      /// </summary>

      protected virtual void Clear()
      {
         if(map.Count > 0)
         {
            map.Clear();
            OnMapChanged(MapChangeType.Clear);
         }
      }

      /// <summary>
      /// Indicates if the cache contains data associated with 
      /// the TCriteria instance having the given ObjectId.
      /// </summary>
      /// <param name="id">The ObjectId of a TCriteria instance</param>
      /// <returns>A value indicating if the cache contains data
      /// associated with the TCriteria instance having the 
      /// given ObjectId</returns>

      public bool ContainsKey(ObjectId id) => map.ContainsKey(id);

      /// <summary>
      /// Attempts to get an existing entry from the 
      /// cache given its ObjectId key.
      /// </summary>

      public bool TryGetValue(ObjectId key, out TValue result)
      {
         return map.TryGetValue(key, out result);
      }

      /// <summary>
      /// Invalidates the cache entry having the
      /// specified key.
      /// </summary>
      /// <param name="id">The ObjectId key of a <typeparamref name="TValueSource"/>
      /// instance whose associated cache entry is to be invalidated</param>

      public override void Invalidate(ObjectId id)
      {
         Remove(id);
      }

      /// <summary>
      /// Invalidates the entire cache
      /// </summary>

      public override void Invalidate()
      {
         Clear();
      }

      /// <summary>
      /// Obtains the keys of all cache entries.
      /// </summary>

      public ICollection<ObjectId> ObjectIds => map.Keys;

      public override Type TKeySourceType => typeof(TKeySource);
      public override Type TValueSourceType => typeof(TValueSource);
      public override Type TValueType => typeof(TValue);

      /// <summary>
      /// Invalidates cached data associated with all 
      /// TCriteria instances that are owned by the 
      /// given Database.
      /// </summary>
      /// <param name="db">The database that owns the
      /// TCriteria instances whose cache entries
      /// are to be invalidated.</param>

      public void Invalidate(Database db)
      {
         int cnt = map.Count;
         Purge(id => id.Database == db);
      }

      bool Purge(Func<ObjectId, bool> predicate)
      {
         int cnt = map.Count;
         map = map.Where(p => !predicate(p.Key)).ToDictionary(p => p.Key, p => p.Value);
         bool result = cnt != map.Count;
         if(result)
            OnMapChanged(MapChangeType.Clear);
         return result;
      }

      /// <summary>
      /// Converts the instance to a delegate that takes an
      /// instance of a TFiltered and returns its associated
      /// TValue. Calling the returned delegate and passing it
      /// an instance of a <typeparamref name="TKeySource"/>
      /// is equivalent to using the indexer.
      /// </summary>
      /// <param name="map"></param>

      public static implicit operator Func<TKeySource, TValue>(DBObjectDataMap<TKeySource, TValueSource, TValue> map)
      {
         Assert.IsNotNull(map, nameof(map));
         return map.Accessor;
      }
   }

   public enum MapChangeType
   {
      ItemAdded,
      ItemRemoved,
      ItemModified,
      Clear
   }
}



