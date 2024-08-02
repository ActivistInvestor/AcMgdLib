using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Utility
{
   public static class SysUtils
   {
      public static T OrEmpty<T>(this T value) where T : new()
      {
         if(value != null)
            return value;
         else
            return new T();
      }

      public static string Remove(this string value, string str)
      {
         return value.Replace(str, "");
      }
   }
}
