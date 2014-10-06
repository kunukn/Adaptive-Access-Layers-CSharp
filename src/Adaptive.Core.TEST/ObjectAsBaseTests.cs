using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class ObjectAsBaseTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void ObjectAsBaseTest()
        {
            //TODO: Same for props and events
            //Better naming
            // Comments
            // out and ref params
            // rewrite test, one big with all features and lots of handlers

            // Create factory using plain object as base
            var factory = new AdaptiveFactory<object>();
            factory.ImplementMethods(m => m.Name == "Max").UsingSharedExecuter(GetMaxHandler);
            factory.ImplementMethods().UsingSharedExecuter(InitObjectAsBase, ExecObjectAsBase);
            factory.ImplementProperties(p => p.PropertyType==typeof(int)).UsingGetterAndSetter(InitProp, GetIntProp, SetIntProp);
            factory.ImplementEvents().UsingAdderAndRemover(InitEvent, AddEvent, RemoveEvent);
            //factory.AddMethodHandler<string>().ForAllMethods().WithInitializer(InitObjectAsBase).WithExecutor(ExecObjectAsBase);

            // Create implementation
            Type type = factory.Implement(typeof(ILayer));
            Assert.AreEqual(typeof(object), type.BaseType);
            Assert.AreNotEqual(typeof(object), type);

            // Verify same implementation next time
            Assert.AreSame(type, factory.Implement(typeof(ILayer)));

            // Create instance and call it
            ILayer layer = (ILayer)Activator.CreateInstance(type);
            Assert.AreEqual(3, layer.Add(1, 2));
            Assert.AreEqual(30, layer.Add(10, 20));

            var d = layer.GetOnly;
            layer.GetAndSet = 22;
            layer.Fire += layer_Fire;
            layer.Fire -= layer_Fire;
            // TODO: Impl setter, check box/unbox scenarios, generic setter/getter, generic TInfo

            int max = layer.Max(1, 3, 2);
            Assert.AreEqual(3, max);
        }

        void layer_Fire(object sender, ConsoleCancelEventArgs e)
        {
            throw new NotImplementedException();
        }

        public static object GetMaxHandler(object instance, MethodInfo method, object[] parameters)
        {
            return parameters.OfType<int>().Max();
        }

        public static PropertyInfo InitProp(object instance, PropertyInfo prop)
        {
            return prop;
        }

        public static void SetIntProp(object instance, PropertyInfo prop, object value)
        {
        }

        public static object GetIntProp(object instance, PropertyInfo prop)
        {
            return 42;
        }

        public class EventData
        {
            public EventInfo Evt;
            public readonly HashSet<EventHandler> Handlers = new HashSet<EventHandler>();
        }

        public static EventData InitEvent(object instance, EventInfo evt)
        {
            return new EventData() { Evt = evt };
        }

        public static void AddEvent(object instance, EventData info, EventHandler handler)
        {
            info.Handlers.Add(handler);
        }

        public static void RemoveEvent(object instance, EventData info, EventHandler handler)
        {
            info.Handlers.Remove(handler);
        }

        /// <summary>
        /// Must be public to be callable from emitted type
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public static object InitObjectAsBase(object layer, MethodInfo method)
        {            
            return 42;
        }

        /// <summary>
        /// Must be public to be callable from emitted type
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="info"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static object ExecObjectAsBase(object layer, object info, object[] parameters)
        {
            Assert.AreEqual(42, info);
            return parameters.OfType<int>().Sum();
        }

        public interface ILayer
        {
            int GetAndSet { get; set; }
            int GetOnly { get; }
            //string SetOnly { set; }
            int Add(int a, int b);
            int Max(int a, int b, int c);
            event EventHandler<ConsoleCancelEventArgs> Fire;
        }
    }
}
