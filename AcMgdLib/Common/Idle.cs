﻿/// Idle.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed under the terms of the MIT license
/// 

using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using Autodesk.AutoCAD.ApplicationServices;

namespace Autodesk.AutoCAD.Runtime.Extensions
{

   public class Idle
   {
      Action action = null;
      Func<bool> func = null;
      static Idle current = null;
      static object currentLock = new object();

      Idle(Action action)
      {
         Assert.IsNotNull(action, nameof(action));
         this.action = action;
         Application.Idle += idle;
      }

      /// <summary>
      /// Continues invoking the given func on each
      /// idle event while the func returns true.
      /// </summary>
      /// <param name="func"></param>

      Idle(Func<bool> func)
      {
         Assert.IsNotNull(func, nameof(func));
         this.func = func;
         Application.Idle += idle;
      }

      /// <summary>
      /// If this method is called from an action that
      /// was passed to a previous call to this method,
      /// and the deferred argument is false, the action 
      /// executes immediately and is not deferred until 
      /// the next idle event.
      /// </summary>

      public static void Invoke(Action action, bool deferred = false)
      {
         Assert.IsNotNull(action, nameof(action));
         lock(currentLock)
         {
            if(current != null && !deferred)
               action();
            else
               new Idle(action);
         }
      }

      public static void InvokeWhile(Func<bool> func, bool deferred = false)
      {
         Assert.IsNotNull(func, nameof(func));
         new Idle(func);
      }

      private void idle(object sender, EventArgs e)
      {
         if(action != null)
         {
            Application.Idle -= idle;
            var temp = action;
            action = null;
            current = this;
            try
            {
               temp();
            }
            finally 
            { 
               current = null; 
            }
         }
         else if(func != null)
         {
            bool done = true;
            try
            {
               done = func();
            }
            finally
            {
               if(done)
               {
                  func = null;
                  Application.Idle -= idle;
               }
            }
         }
      }

      //private void idle(object sender, EventArgs e)
      //{
      //   current = this;
      //   try
      //   {
      //      if(action != null)
      //      {
      //         Application.Idle -= idle;
      //         var temp = action;
      //         action = null;
      //         temp();
      //      }
      //      else if(func != null)
      //      {
      //         bool done = true;
      //         try
      //         {
      //            done = func();
      //         }
      //         finally
      //         {
      //            if(done)
      //            {
      //               func = null;
      //               Application.Idle -= idle;
      //            }
      //         }
      //      }
      //   }
      //   finally
      //   {
      //      current = null;
      //   }
      //}

      public static class Distinct
      {
         static HashSet<Action> actions = new HashSet<Action>();

         /// <summary>
         /// If the specified Action has already been passed
         /// to this method but has not yet executed, the call
         /// is ignored, and the action will execute only once 
         /// on the next idle event.
         /// 
         /// Hence, this method can be called multiple times
         /// with the same action argument, but regardless of
         /// the number of times called with that action, it
         /// will only execute only once on the next idle event.
         /// 
         /// To specify that an action be invoked on the next
         /// Idle event only once, regardless of how many times
         /// this method is called and passed that action, use
         /// this:
         /// 
         ///    Idle.Distinct.Invoke(action);
         ///    
         /// The above statement can be called repeatedly with
         /// the same argument, but regardless of how many times
         /// it's called the action will execute only once on the 
         /// next idle event.
         ///    
         /// </summary>
         /// <param name="action"></param>
         /// <returns>True if execution of the given action is
         /// not already pending, or false if it is.</returns>

         public static bool Invoke(Action action)
         {
            Assert.IsNotNull(action, nameof(action));
            bool result = actions.Add(action);
            if(result)
               Idle.Invoke(() => Remove(action));
            return result;
         }

         static void Remove(Action action)
         {
            actions.Remove(action);
            action();
         }
      }
   }
}

