using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Threading;
using System.Globalization;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class InheritedInterfacesTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void TestInheritedInterfaces()
        {
            var factory = new AdaptiveFactory<Base>();
            factory.ImplementMethods().UsingSharedExecuter("Init", "Exec");

            Type ibase = factory.Implement(typeof(IBase));
            Type iderived = factory.Implement(typeof(IDerived));
            Assert.AreNotSame(ibase, iderived);
            Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(ibase));
            Assert.IsTrue(typeof(IBase).IsAssignableFrom(ibase));
            Assert.IsTrue(typeof(IBase).IsAssignableFrom(iderived));
            Assert.IsTrue(typeof(IDerived).IsAssignableFrom(iderived));

            using (IBase b = (IBase)Activator.CreateInstance(ibase))
            {
                Assert.IsNotNull(b.DoSomething());
                Assert.AreEqual("DoSomething", b.DoSomething().Name);
            }
            using (IDerived d = (IDerived)Activator.CreateInstance(iderived))
            {
                Assert.AreEqual("DoSomething", d.DoSomething().Name);
                Assert.AreEqual("DoMore", d.DoMore().Name);
            }
        }

        [TestMethod]
        public void TestGenericInterface()
        {
            var factory = new AdaptiveFactory<Base>();
            factory.ImplementMethods().UsingSharedExecuter("Init", "Exec");

            Type t = factory.Implement<IGeneric<int>>();
            IGeneric<int> g = (IGeneric<int>)Activator.CreateInstance(t);
            Assert.AreEqual("DoGeneric", g.DoGeneric(42).Name);
        }

        [TestMethod]
        public void TestGenericBase()
        {
            var factory = new AdaptiveFactory<GenericBase<int>>();
            factory.ImplementMethods().UsingSharedExecuter("Init", "Exec");

            IGeneric<int> g = Activator.CreateInstance(factory.Implement<IGeneric<int>>()) as IGeneric<int>;
            Assert.AreEqual("DoGeneric", g.DoGeneric(42).Name);
            Assert.IsTrue(g is GenericBase<int>);
        }

        [TestMethod]
        public void TestMultipleFactories()
        {
            var factory1 = new AdaptiveFactory<GenericBase<int>>();
            factory1.ImplementMethods().UsingSharedExecuter("Init", "Exec");

            var factory2 = new AdaptiveFactory<GenericBase<int>>();
            factory2.ImplementMethods().UsingSharedExecuter("Init", "Exec");

            Type t1 = factory1.Implement<IGeneric<int>>();
            Type t2 = factory2.Implement<IGeneric<int>>();

            Assert.AreNotSame(t1, t2);
        }
    }

    public class Base : IDisposable
    {
        public bool Disposed = false;

        protected MethodInfo Init(MethodInfo method)
        {
            return method;
        }

        protected object Exec(MethodInfo info, object[] parameters)
        {
            return info;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    public class GenericBase<T> : Base
    {
    }

    public interface IBase : IDisposable
    {
        MethodInfo DoSomething();
    }

    public interface IDerived : IBase
    {
        MemberInfo DoMore();
    }

    public interface IGeneric<T> : IDerived
    {
        MemberInfo DoGeneric(T t);
    }
}
