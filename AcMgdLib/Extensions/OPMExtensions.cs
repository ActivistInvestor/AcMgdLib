/// OPMExtensions.cs  
/// 
/// ActivistInvestor / Tony T.
/// 
/// Distributed under the terms of the MIT license.
/// 
/// Supporting APIs for OPM-related operations.
/// 
/// This code is largely derived from example 
/// code written by Cyrille Fauvel of Autodesk.
/// 
/// Note: Recent refactorings may require C# 10.0.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Internal.PropertyInspector;

namespace AcMgdLib.DatabaseServices
{
   public static class OPMExtensions
   {
      /// <summary>
      /// Returns an object contining the names and values of
      /// the OPM properties for the database object having the
      /// given ObjectId. 
      /// 
      /// This method is implemented as an extension to the
      /// ObjectId class, making it simpler to access.
      /// 
      /// The result is a specialized Dictionary<string,object>
      /// that when disposed, will release any property values 
      /// that are COM objects.
      /// </summary>
      /// <param name="id"></param>
      /// <returns></returns>
      
      public static OPMPropertyMap GetOPMProperties(this ObjectId id)
      {
         OPMPropertyMap map = new OPMPropertyMap();
         IntPtr pUnk = ObjectPropertyManagerPropertyUtility.GetIUnknownFromObjectId(id);
         if(pUnk != IntPtr.Zero)
         {
            try
            {
               using(CollectionVector properties = ObjectPropertyManagerProperties.GetProperties(id, false, false))
               {
                  int cnt = properties.Count();
                  if(cnt != 0)
                  {
                     using(CategoryCollectable category = properties.Item(0) as CategoryCollectable)
                     {
                        CollectionVector props = category.Properties;
                        int propCount = props.Count();
                        for(int j = 0; j < propCount; j++)
                        {
                           using(PropertyCollectable prop = props.Item(j) as PropertyCollectable)
                           {
                              if(prop == null)
                                 continue;
                              object value = null;
                              if(prop.GetValue(pUnk, ref value) && value != null)
                              {
                                 if(!map.ContainsKey(prop.Name))
                                    map[prop.Name] = value;
                              }
                           }
                        }
                     }
                  }
               }
            }
            finally
            {
               Marshal.FinalReleaseComObject(pUnk);
            }
         }
         return map;
      }
   }

   /// <summary>
   /// A specialized Dictionary that holds the results of the
   /// GetOPMProperties() method, that when disposed, will 
   /// release any property values that are COM objects.
   /// </summary>
   
   public class OPMPropertyMap : DisposableDictionary<string, object>
   {
      protected override void Dispose(bool disposing)
      {
         if(disposing)
         {
            foreach(var pair in this)
            {
               if(Marshal.IsComObject(pair.Value))
                  Marshal.FinalReleaseComObject(pair.Value);
            }
         }
      }
   }



   public abstract class DisposableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable
   {
      private bool disposed;

      protected abstract void Dispose(bool disposing);

      public void Dispose()
      {
         if(!disposed)
         {
            disposed = true;
            Dispose(true);
            GC.SuppressFinalize(this);
         }
      }
   }

}



