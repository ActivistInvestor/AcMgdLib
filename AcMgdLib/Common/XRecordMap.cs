using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;

namespace Autodesk.AutoCAD.DatabaseServices.Extensions
{

   /// <summary>
   /// TKey must be a type that can be stored in a ResultBuffer.
   /// </summary>
   /// <typeparam name="TKey"></typeparam>
   public class XRecordMap<TKey>
   {
      DBObject owner;
      string rootKey;
      DBDictionary xdict = null;
      Transaction trans;
      Xrecord root;
      short code;
      // TValue is each Xrecord's DBDictionary key
      Dictionary<TKey, string> records = new Dictionary<TKey, string>();

      public XRecordMap(Transaction tr, DBObject owner, string rootKey, short keyCode)
      {
         Assert.IsNotNullOrDisposed(owner, nameof(owner));
         Assert.IsNotNullOrDisposed(tr, nameof(tr));
         this.owner = owner;
         this.rootKey = rootKey;
         this.trans = tr;
         this.code = keyCode;
         //root = owner.GetXrecord(key, tr);
         //if(root != null)
         //{
         //   records = root.Data.ToDictionary<TKey, string>();
         //}
      }

      Xrecord GetRoot(bool create = false)
      {
         if(root == null)
         {
            root = owner.GetXrecord(rootKey, trans, OpenMode.ForRead, create, true);
         }
         return root;
      }

      Dictionary<TKey, string> GetRecords(bool create = false) 
      {
         if(records == null)
         {
            if(GetRoot(create) != null)
            {
               if(root?.Data != null)
                  records = null; // root.Data.ToDictionary<TKey, string>();
               else
                  records = new Dictionary<TKey, string>();
            }
         }
         return records;
      }

      public bool ContainsKey(TKey key)
      {
         if(GetRecords(false) != null)
            return records.ContainsKey(key);
         else
            return false;
      }

      public ResultBuffer this[TKey key]
      {
         get
         {
            if(GetRecords(false) != null)
            {
               if(records.TryGetValue(key, out string value))
               {
                  owner.GetXRecordData(value);
               }
            }
            return null;
         }
         set
         {
            if(GetRecords(true) == null)
               throw new InvalidOperationException("Failed to get Xrecord data");
            if(records.TryGetValue(key, out string childkey))
            {
               Xrecord child = owner.GetXrecord(childkey, trans, OpenMode.ForWrite, true, true);
               if(child == null)
                  throw new InvalidOperationException("Failed to get child xrecord");
               child.Data = value;
            }
            else
            {
               Xrecord record = new Xrecord();
               record.Data = value;
               record.XlateReferences = true;
               string childKey = Guid.NewGuid().ToString();
               /// Where is the owner DBDictionary ?
               if(root == null)
                  throw new InvalidOperationException("no root");
               var dict = trans.GetObject<DBDictionary>(root.OwnerId, OpenMode.ForWrite);
               dict.SetAt(childKey, record);
               records[key] = childKey;
            }
         }
      }


   }
}
