using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class MethodSignatureTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void ValidMethodSignaturesTest()
        {
            var factory = new AdaptiveFactory<LayerBase>();
            factory.ImplementMethods().UsingSharedExecuter("Init", "Exec");
            factory.ImplementProperties(p => true).UsingGetterAndSetter("Init", "Get", "Set");

            Type type = factory.Implement(typeof(IValidInterface));
            IValidInterface layer = (IValidInterface)Activator.CreateInstance(type);
            layer.VoidMethod();
            layer.SingleArgument(0);
            layer.DefaultArgument();
            Assert.AreEqual(42, layer.ValueTypeReturn());
            Assert.AreEqual("ABC", string.Join("", layer.RefTypeReturn()));

            layer.Number = 42;
            Assert.AreEqual(42, layer.Number);
        }

        [TestMethod]
        public void InvalidMethodSignaturesTest()
        {
            var factory = new AdaptiveFactory<LayerBase>();
            factory.ImplementMethods().UsingSharedExecuter("Init", "Exec2");

            try
            {
                factory.Implement<IRefParamInterface>();
                Assert.Fail();
            }
            catch (ArgumentException)
            {
            }
        }

        public class LayerBase
        {
            protected Type Init(MethodInfo method)
            {
                return method.ReturnType;
            }

            protected object Exec(Type returnType, object[] parameters)
            {
                if (returnType == typeof(void))
                    return null;
                else if (returnType.IsValueType)
                    return 42;
                else
                    return new string[] { "A", "B", "C" };
            }

            protected object Exec2(Type returnType, object[] parameters)
            {
                parameters[0] = 84;
                return null;
            }

            private readonly IDictionary<string, object> _values = new Dictionary<string, object>();

            protected string Init(PropertyInfo prop)
            {
                return prop.Name;
            }

            protected object Get(string name)
            {
                if (_values.ContainsKey(name))
                    return _values[name];
                return null;
            }

            protected void Set(string name, object value)
            {
                _values[name] = value;
            }
        }
    }

    public interface IValidInterface
    {
        void VoidMethod();

        int ValueTypeReturn();

        IEnumerable<string> RefTypeReturn();

        void SingleArgument(int input);

        void DefaultArgument(int input = 42);

        int Number { get; set; }
    }

    public interface IRefParamInterface
    {
        void Action(ref int data);
    }
}
