using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class DefineInterfaceTests
    {
        [TestMethod]
        public void DefineInterfaceTest()
        {
            Type t = typeof(II);

            Type type = AdaptiveFactory.DefaultModule.DefineInterface("X", null, new PropertyDefinition("A", typeof(int)));
            Assert.IsTrue(type.IsInterface);
            Assert.IsTrue(type.IsPublic);
            Assert.IsNotNull(type.GetProperty("A"));
            Assert.IsTrue(type.GetProperty("A").CanRead);
            Assert.IsTrue(type.GetProperty("A").CanWrite);

            AdaptiveFactory<object> factory = new AdaptiveFactory<object>();
            factory.ImplementProperties().UsingBackingField();

            Type dtoType = factory.Implement(type);
            Assert.IsTrue(type.IsAssignableFrom(dtoType));
            Assert.IsNotNull(dtoType.GetProperty("A"));
        }

        public interface II
        {
            int A { get; set; }
        }

    }
}
