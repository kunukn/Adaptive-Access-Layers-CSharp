using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class PredefinedFactoriesTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void ProxyFactoryTest()
        {
            AdaptiveFactory<ProxyBase> proxyFactory = AdaptiveFactory.CreateProxyFactory<ProxyBase>("Target");
            Assert.AreEqual(1, proxyFactory.MethodHandlers.Count);
            Assert.AreEqual(0, proxyFactory.PropertyHandlers.Count);
            Assert.AreEqual(0, proxyFactory.EventHandlers.Count);

            Target target = new Target();
            ITarget proxy = (ITarget)Activator.CreateInstance(proxyFactory.Implement<ITarget>(), target);
            Assert.IsNotNull(proxy);

            Assert.AreEqual(5, proxy.Add(2, 3));
            target.Value = 42;
            Assert.AreEqual(42, proxy.Value);
            proxy.Value = 21;
            Assert.AreEqual(21, target.Value);
        }

        [TestMethod]
        public void DtoFactoryTest()
        {
            AdaptiveFactory<object> rawFactory = AdaptiveFactory.CreateDtoFactory<object>(null, null);
            IDTO dto = Activator.CreateInstance(rawFactory.Implement<IDTO>()) as IDTO;
            dto.Key = 42;
            Assert.AreEqual(42, dto.Key);

            AdaptiveFactory<NotifyPropertySub> changeFactory = AdaptiveFactory.CreateDtoFactory<NotifyPropertySub>("OnChanging", "OnChanged");
            dto = Activator.CreateInstance(changeFactory.Implement<IDTO>()) as IDTO;
            Assert.IsTrue(dto is INotifyPropertyChanged);
            Assert.IsTrue(dto is INotifyPropertyChanging);

            List<string> changed = new List<string>();
            List<string> changing = new List<string>();
            ((INotifyPropertyChanged)dto).PropertyChanged += (s, e) => changed.Add(e.PropertyName);
            ((INotifyPropertyChanging)dto).PropertyChanging += (s, e) => changing.Add(e.PropertyName);

            dto.Key = 42;
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(1, changing.Count);
            Assert.AreEqual("Key", changed[0]);
        }
    }

    public class ProxyRoot
    {
        protected object Target;
    }

    public class ProxyBase : ProxyRoot
    {
        public ProxyBase(object target)
        {
            Target = target;
        }
    }

    public interface ITarget
    {
        int Value { get; set; }
        int Add(int a, int b);
    }

    public class Target : ITarget
    {
        public int Value { get; set; }

        public int Add(int a, int b)
        {
            return a + b;
        }
    }

    public interface IDTO
    {
        int Key { get; set; }
        string Name { get; set; }
    }

    public class NotifyPropertyBase : INotifyPropertyChanged, INotifyPropertyChanging
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event PropertyChangingEventHandler PropertyChanging;

        protected void OnChanging(PropertyInfo property, object value)
        {
            var handler = PropertyChanging;
            if (handler != null)
                handler(this, new PropertyChangingEventArgs(property.Name));
        }

        protected void OnChanged<T>(PropertyInfo property, T value)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(property.Name));
        }
    }

    public class NotifyPropertySub : NotifyPropertyBase
    {
    }

}
