/// DocData.cs
/// 
/// ActivistInvestor / Tony T
/// 
/// Distributed unter the terms of the MIT license

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics.Extensions;
using System.Reflection;

namespace Autodesk.AutoCAD.ApplicationServices.Extensions
{
   /// <summary>
   /// Manages per-document instances of a specified Type.
   /// 
   /// Unlike the AutoCAD managed runtime's PerDocumentClass 
   /// attribute, creation of instances is eager rather than 
   /// lazy, allowing the managed class to add event handlers 
   /// at the point when the instance is created.
   /// 
   /// Instances of the managed type are created by this type
   /// in one of two ways:
   /// 
   /// 1. The Initialize() method is passed a delegate that
   ///    takes a Document as its only argument, and returns
   ///    the instance to be associated with the document.
   ///   
   /// 2. The managed type implements a constructor that takes
   ///    a Document as its only argument. In this case, the
   ///    Initialize() method is not passed a delegate.
   ///    
   /// Initialization:
   /// 
   /// This class is typically used by calling its Initialize()
   /// method once and only once, and is usually done from within
   /// an IExtensionApplication's Initialize() method.
   /// 
   /// For example, if one wants to associate an instance of the
   /// the class 'MyClass' with each document, this is all that's 
   /// required:
   /// 
   ///   DocData<MyClass>.Initialize(doc => new MyClass());
   ///   
   /// The above line causes instances of MyClass to be created
   /// for each currently-open document, and all subsequently-
   /// opened documents.
   ///   
   /// The above example does not require that MyClass have a 
   /// constructor taking a Document as an argument.
   /// 
   /// If MyClass does have such a constructor, the delegate
   /// can be omitted, and the DocData class will look for and
   /// use the constructor:
   /// 
   ///   DocData<MyClass>.Initialize();
   /// 
   /// IDisposable support:
   /// 
   /// If the type that is managed by this class implements the
   /// IDisposable interface, it's Dispose() method will be called
   /// when the associated document is about to be destroyed.
   /// 
   /// Note: This is a minimal/watered-down implementation of 
   /// earlier works having no dependence on other library code.
   /// </summary>
   /// <typeparam name="T">The type that is to be managed</typeparam>

   public static class DocData<T> where T : class
   {
      static bool canDispose = typeof(IDisposable).IsAssignableFrom(typeof(T));
      static readonly BindingFlags flags = BindingFlags.Instance 
         | BindingFlags.NonPublic | BindingFlags.Public;
      static ConstructorInfo ctor;
      static Func<Document, T> factory = null;
      static DocumentCollection docs = Application.DocumentManager;
      static bool initialized = false;

      public static void Initialize(Func<Document, T> factory = null)
      {
         if(initialized)
            return;
         initialized = true;
         if(factory == null)
         {
            ctor = typeof(T).GetConstructor(flags, null, new Type[] { typeof(Document) }, null);
            if(ctor == null)
               throw new MissingMemberException(
                  $"{typeof(T).Name} requires a constructor taking a Document argument");
            factory = doc => ctor.Invoke(new[] { doc }) as T;
         }
         DocData<T>.factory = factory;
         foreach(Document doc in docs)
            Add(doc);
         docs.DocumentCreated += documentCreated;
         if(canDispose)
            docs.DocumentToBeDestroyed += documentToBeDestroyed;
      }

      public static void Uninitialize()
      {
         if(initialized)
         {
            foreach(Document doc in docs)
            {
               if(doc.UserData.Contains(typeof(T)))
               {
                  var disp = doc.UserData[typeof(T)] as IDisposable;
                  if(disp != null)
                     disp.Dispose();
                  doc.UserData.Remove(typeof(T));
               }
            }
            docs.DocumentCreated -= documentCreated;
            if(canDispose)
               docs.DocumentToBeDestroyed += documentToBeDestroyed;
            initialized = false;
         }
      }

      static void Add(Document doc)
      {
         T instance = factory(doc);
         if(instance == null)
            throw new InvalidOperationException($"Instance creation failed: {typeof(T).Name}");
         doc.UserData[typeof(T)] = instance;
      }

      /// <summary>
      /// Gets the instance associated with the given document
      /// </summary>

      public static T GetObject(Document doc)
      {
         if(doc == null)
            throw new ArgumentNullException(nameof(doc));
         return doc.UserData[typeof(T)] as T;
      }

      /// <summary>
      /// Gets the instance associated with the active document
      /// </summary>

      public static T Current
      {
         get
         {
            Document doc = docs.MdiActiveDocument;
            if(doc == null)
               throw new Autodesk.AutoCAD.Runtime.Exception(ErrorStatus.NoDocument);
            return GetObject(doc);
         }
      }

      static void documentCreated(object sender, DocumentCollectionEventArgs e)
      {
         Add(e.Document);
      }

      static void documentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
      {
         if(e.Document.UserData[typeof(T)] is IDisposable disposable)
            disposable.Dispose();
      }

   }

   public static partial class DocumentExtensions
   {
      /// <summary>
      /// Exposes the DocData.GetObject() method as an extension
      /// method of the Document class.
      /// </summary>
      /// <typeparam name="T">The type of the object to obtain,
      /// which is also the key used to obtain it from the given
      /// document's extension dictionary.</typeparam>
      /// <param name="doc">The Document from which to obtain 
      /// the result</param>
      /// <returns>The value of the item from the Document's
      /// UserData dictionary for which the generic argument
      /// type is the key</returns>

      public static T GetObject<T>(this Document doc, bool throwIfNotFound) where T: class
      {
         Assert.IsNotNull(doc);
         if(doc.UserData.ContainsKey(typeof(T)))
            return (T) doc.UserData[typeof(T)];
         if(throwIfNotFound)
            throw new KeyNotFoundException(typeof(T).Name);
         return default(T);
      }

      public static T GetObject<T>(this Document doc, Func<Document, T> factory) where T: class
      {
         Assert.IsNotNull(doc);
         Assert.IsNotNull(factory);
         if(doc.UserData.ContainsKey(typeof(T)))
            return (T) doc.UserData[typeof(T)];
         T result = factory(doc);
         doc.UserData[typeof(T)] = result;
         return result;
      }
   }


}