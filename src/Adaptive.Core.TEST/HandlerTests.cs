using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Threading;
using System.Globalization;

namespace Adaptive.Core.TEST
{
    [TestClass]
    public class HandlerTests
    {
        [TestInitialize]
        public void SetCulture()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void HandlerTest()
        {
            ITestLayer layer = TestAccessLayer.Create<ITestLayer>();
            TestAccessLayer tal = layer as TestAccessLayer;
            Assert.IsNotNull(tal);

            // A tests
            layer.A();
            layer.A(42);
            layer.A("A", "B", "C");

            // B tests
            Assert.AreEqual(42, layer.B(42));
            Assert.AreEqual(DayOfWeek.Wednesday.ToString(), layer.B(DayOfWeek.Wednesday));
            Assert.AreEqual(Guid.Empty.ToString(), layer.B(Guid.Empty));
            Assert.AreEqual("B", layer.B("B"));

            // C tests
            Assert.AreEqual(0, layer.C());
            Assert.AreEqual(4, layer.C(1, 2));
            Assert.AreEqual(4, layer.C(1));

            // P tests
            Assert.AreEqual(0, layer.P1);
            layer.P1 = 42;
            Assert.AreEqual(42, layer.P1);
            Assert.IsNull(layer.P2);
            layer.P2 = "42";
            Assert.AreEqual("42", layer.P2);
            Assert.IsNull(layer.P3);
            layer.P3 = Guid.Empty;
            Assert.AreEqual(Guid.Empty, layer.P3);

            // Q tests
            Assert.AreEqual(0, layer.Q);
            layer.Q = 21;
            Assert.AreEqual(21, layer.Q);

            // Backing field tests with get inspector
            Assert.IsNull(tal.LastGetProperty);
            Assert.IsNull(layer.R);
            Assert.IsNotNull(tal.LastGetProperty);
            Assert.AreEqual("R", tal.LastGetProperty.Item1.Name);

            layer.R = Guid.Empty;
            Assert.AreEqual(Guid.Empty, layer.R);
            Assert.AreEqual(0, layer.R2);
            layer.R3 = 42;
            Assert.IsNull(layer.R4);
            layer.R4 = "42";
            Assert.AreEqual("42", layer.R4);
            Assert.AreEqual("R4", tal.LastGetProperty.Item1.Name);
            Assert.AreEqual("42", tal.LastGetProperty.Item2);

            Assert.IsFalse(layer.IsDisposed);
            layer.Dispose();
            Assert.IsTrue(layer.IsDisposed);
        }
    }

    public interface IEmptyInterface : IDisposable
    {
    }

    public interface IMasterInterface : IEmptyInterface
    {
        string B(string b);
        int P1 { get; set; }
        string P2 { get; set; }
        Guid? P3 { get; set; }
    }

    public interface IIsDisposed
    {
        bool IsDisposed { get; }
    }

    public interface ITestLayer : IEmptyInterface, IMasterInterface, IIsDisposed
    {
        // Overloads by same name with params using static handlers
        void A();
        void A(int a);
        void A(params string[] inputs);

        // Return values
        int B(int a);
        string B(Guid b);
        string B(DayOfWeek? b);

        // Instance handlers
        int C();
        int C(int a, int b = 42);

        // Generic property handlers
        int Q { get; set; }

        // Using backing field
        Guid? R { get; set; }
        int R2 { get; }
        int R3 { set; }
        string R4 { get; set; }
    }

    public class TestAccessLayer : IDisposable, IIsDisposed
    {
        private static readonly AdaptiveFactory<TestAccessLayer> s_Factory;

        static TestAccessLayer()
        {
            s_Factory = new AdaptiveFactory<TestAccessLayer>();

            // Method handlers
            s_Factory.ImplementMethods(m => m.Name == "A").UsingSharedExecuter(InitA, ExecA);
            s_Factory.ImplementMethods(m => m.Name == "B").UsingSharedExecuter(InitB, ExecB);
            s_Factory.ImplementMethods(m => m.Name == "C").UsingSharedExecuter("InitC", "ExecC");

            // Property handlers
            s_Factory.ImplementProperties(p => p.Name.StartsWith("P")).UsingGetterAndSetter(InitP, GetP, SetP);
            s_Factory.ImplementProperties(p => p.Name.StartsWith("Q")).UsingGetterAndSetter(InitQ, GetP, SetP);
            s_Factory.ImplementProperties(p => p.Name.StartsWith("R"))
                .UsingBackingField()
                .WithGetInspector(GetInspector)
                .WithGetInspector("GenericGetInspector")
                .WithSetInspector("SetInspector", "SetInspector");

            // Event handlers
            s_Factory.ImplementEvents().UsingAdderAndRemover("InitEvent", "AddEvent", "RemoveEvent");

            // TODO: State without initializer, generic getter/setter, setter does not work, split getter/setter
        }

        public static void GetInspector(TestAccessLayer layer, PropertyInfo property, object value)
        {
            layer.LastGetProperty = Tuple.Create(property, value);
        }

        protected static void GenericGetInspector<T>(TestAccessLayer layer, PropertyInfo property, T value)
        {
            Assert.AreEqual(typeof(T), property.PropertyType);
        }

        public void SetInspector<T>(PropertyInfo property, T value)
        {
            Assert.AreEqual(typeof(T), property.PropertyType);
        }

        public static T Create<T>() where T : class
        {
            return (T)Activator.CreateInstance(s_Factory.Implement<T>());
        }

        public Tuple<PropertyInfo, object> LastGetProperty;

        #region Method handlers

        protected static object InitA(TestAccessLayer layer, MethodInfo m)
        {
            return null;
        }

        protected static object ExecA(TestAccessLayer layer, object state, object[] parameters)
        {
            return null;
        }

        protected static MethodInfo InitB(TestAccessLayer layer, MethodInfo m)
        {
            return m;
        }

        protected static object ExecB(TestAccessLayer layer, MethodInfo state, object[] parameters)
        {
            if (state.ReturnType == typeof(string))
                return parameters[0].ToString();

            return parameters[0];
        }

        protected int InitC(MethodInfo m)
        {
            return m.GetParameters().Length;
        }

        protected object ExecC(int state, object[] parameters)
        {
            return state + parameters.Length;
        }

        #endregion

        #region Property handlers

        protected class PropValue
        {
            public object Value;
        }

        protected static PropValue InitP(TestAccessLayer layer, PropertyInfo prop)
        {
            return new PropValue() { Value = prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null };
        }

        protected static void SetP(TestAccessLayer layer, PropValue state, object value)
        {
            state.Value = value;
        }

        protected static object GetP(TestAccessLayer layer, PropValue state)
        {
            return state.Value;
        }

        protected static PropValue InitQ(TestAccessLayer layer, PropertyInfo prop)
        {
            return new PropValue() { Value = prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null };
        }

        protected static void SetQ<T>(TestAccessLayer layer, PropValue state, T value)
        {
            state.Value = value;
        }

        protected static T GetQ<T>(TestAccessLayer layer, PropValue state)
        {
            return (T)state.Value;
        }

        #endregion

        protected EventInfo InitEvent(EventInfo info)
        {
            return info;
        }

        protected void AddEvent(EventInfo info, EvtHandler handler)
        {
        }

        protected void RemoveEvent(EventInfo info, EvtHandler handler)
        {
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
