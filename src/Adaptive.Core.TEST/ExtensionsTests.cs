using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Globalization;
using Adaptive.Core;
using System.Reflection;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class ExtensionsTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void FindMethodTest()
        {
            MethodInfo a = typeof(Methods).FindMethod("A", typeof(void));
            Assert.IsNotNull(a);

            a = typeof(Methods).FindMethod("A", typeof(void), typeof(int), typeof(string));
            Assert.IsNotNull(a);

            a = typeof(Methods).FindMethod("AAA", typeof(void), typeof(int), typeof(string));
            Assert.IsNotNull(a);

            a = typeof(Methods).FindMethod("AA", typeof(void), typeof(int), typeof(string));
            Assert.IsNotNull(a);

            a = typeof(Methods).FindMethod("AA", typeof(void), typeof(int), typeof(Guid));
            Assert.IsNotNull(a);

            a = typeof(Methods).FindMethod("B", typeof(object));
            Assert.IsNotNull(a);

            a = typeof(Methods).FindMethod("C", typeof(Guid));
            Assert.IsNotNull(a);
        }
    }

    public class Methods
    {
        private void A()
        {
        }

        private void A(int a, object b)
        {
        }

        private void AA<P1>(int a, P1 b)
        {
        }

        private static void AAA(object target, int a, object b)
        {
        }

        private string B()
        {
            return null;
        }

        private T C<T>()
        {
            return default(T);
        }
    }
}
