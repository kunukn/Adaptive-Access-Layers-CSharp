using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Common base class for all AdaptiveFactory classes.
    /// </summary>
    public abstract class AdaptiveFactory
    {
        /// <summary>
        /// Gets/creates dynamic assembly with given name.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public static AssemblyBuilder GetOrCreateDynamicAssembly(string assemblyName)
        {
            AssemblyName asmName = new AssemblyName(assemblyName);
            AssemblyBuilder assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.GetName() == asmName) as AssemblyBuilder;

            return assembly ?? AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
        }

        /// <summary>
        /// Gets/creates dynamic module within given dynamic assembly.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static ModuleBuilder GetOrCreateDynamicModule(AssemblyBuilder assembly)
        {
            string moduleName = assembly.FullName + "Module";

            return assembly.GetDynamicModule(moduleName) ?? assembly.DefineDynamicModule(moduleName);
        }

        /// <summary>
        /// Holds default module if used
        /// </summary>
        private static readonly Lazy<ModuleBuilder> s_DefaultModule = new Lazy<ModuleBuilder>(CreateDefaultModule);

        /// <summary>
        /// Gets/creates default runtime module for given factory. The created module is private to the factory instance.
        /// </summary>
        public static ModuleBuilder DefaultModule
        {
            get { return s_DefaultModule.Value; }
        }

        /// <summary>
        /// Creates default module
        /// </summary>
        /// <returns></returns>
        private static ModuleBuilder CreateDefaultModule()
        {
            return GetOrCreateDynamicModule(GetOrCreateDynamicAssembly("AdaptiveFactory"));
        }

        /// <summary>
        /// Creates a factory that can create proxies around any interface.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <param name="targetMemberName">Name of field/property/method on TBase that yields inner instance to forward all calls to.</param>
        /// <returns></returns>
        public static AdaptiveFactory<TBase> CreateProxyFactory<TBase>(string targetMemberName) where TBase : class
        {
            AdaptiveFactory<TBase> factory = new AdaptiveFactory<TBase>();
            factory.ImplementSecondaryByMethods = true;
            factory.ImplementMethods().UsingTarget(targetMemberName);

            return factory;
        }

        /// <summary>
        /// Creates a factory that implements any property-only interface using backing fields with optional notification before and after sets.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <param name="propertyChangingHandlerName"></param>
        /// <param name="propertyChangedHandlerName"></param>
        /// <returns></returns>
        public static AdaptiveFactory<TBase> CreateDtoFactory<TBase>(string propertyChangingHandlerName, string propertyChangedHandlerName) where TBase : class
        {
            AdaptiveFactory<TBase> factory = new AdaptiveFactory<TBase>();
            factory.ImplementProperties().UsingBackingField().WithSetInspector(propertyChangingHandlerName, propertyChangedHandlerName);
            return factory;
        }
    }

    /// <summary>
    /// Factory for interface implementations derived from TBase base class.
    /// </summary>
    /// <typeparam name="TBase"></typeparam>
    public class AdaptiveFactory<TBase> : AdaptiveFactory
        where TBase : class
    {
        /// <summary>
        /// The unique factory instance id
        /// </summary>
        private readonly string _id = Guid.NewGuid().ToString("N");

        /// <summary>
        /// The real base class derived from or equal to TBase.
        /// </summary>
        private readonly Type _baseClass;

        /// <summary>
        /// The module being used by factory instance when no other module is specified.
        /// </summary>
        public ModuleBuilder Module { get; set; }

        /// <summary>
        /// Creates new factory using TBase as base class for runtime types. Instance implements in AdaptiveFactory.DefaultModule.
        /// <param name="baseClass">Actual base class, must derive from TBase.</param>
        /// </summary>
        public AdaptiveFactory(Type baseClass = null)
            : this(DefaultModule, baseClass)
        {
        }

        /// <summary>
        /// Creates new factory that implements in given module.
        /// </summary>
        /// <param name="module">Module to implement in.</param>
        /// <param name="baseClass">Actual base class, must derive from TBase.</param>
        public AdaptiveFactory(ModuleBuilder module, Type baseClass = null)
        {
            Module = module;
            _baseClass = baseClass ?? typeof(TBase);

            if (!typeof(TBase).IsAssignableFrom(_baseClass))
                throw new ArgumentException("baseClass must be derived from TBase.");

            if (!_baseClass.IsPublic && !_baseClass.IsNestedPublic)
                throw new ArgumentException(string.Format("Base class {0} must be public.", _baseClass.Name));
            if (_baseClass.IsSealed)
                throw new ArgumentException(string.Format("Base class {0} must not be sealed.", _baseClass.Name));

            CacheTypes = true;
        }

        #region Registration

        /// <summary>
        /// Set to true to implement properties and events by implementing their underlying methods. Default is false.
        /// </summary>
        public bool ImplementSecondaryByMethods { get; set; }

        /// <summary>
        /// Set to false to create fresh new type in every call to Create/Implement even when produced type will be identical to existing type. Default is true.
        /// </summary>
        public bool CacheTypes { get; set; }

        /// <summary>
        /// All registered methods
        /// </summary>
        private readonly List<MethodHandler> _methodHandlers = new List<MethodHandler>();

        /// <summary>
        /// Returns list of method handlers.
        /// </summary>
        public IList<MethodHandler> MethodHandlers { get { return _methodHandlers; } }

        /// <summary>
        /// Starts new method handler registration
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public MethodHandler<TBase> ImplementMethods(Func<MethodInfo, bool> predicate = null)
        {
            var handler = new MethodHandler<TBase>() { BaseClass = _baseClass, Predicate = predicate };
            _methodHandlers.Add(handler);
            return handler;
        }

        /// <summary>
        /// Starts a new method handler registration for methods with given attribute.
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public MethodHandler<TBase> ImplementAttributedMethods<TAttribute>(Func<MethodInfo, TAttribute, bool> predicate = null) where TAttribute : Attribute
        {
            return ImplementMethods(m => m.GetCustomAttributes(false).OfType<TAttribute>().Any(a => predicate == null || predicate(m, a)));
        }

        /// <summary>
        /// All property handlers
        /// </summary>
        private readonly List<PropertyHandler> _propertyHandlers = new List<PropertyHandler>();

        /// <summary>
        /// Returns list of property handlers.
        /// </summary>
        public IList<PropertyHandler> PropertyHandlers { get { return _propertyHandlers; } }

        /// <summary>
        /// Starts new handler registration for all properties that matches predicate.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public PropertyHandler<TBase> ImplementProperties(Func<PropertyInfo, bool> predicate = null)
        {
            PropertyHandler<TBase> handler = new PropertyHandler<TBase>() { Predicate = predicate, BaseClass = _baseClass };
            _propertyHandlers.Add(handler);
            return handler;
        }

        /// <summary>
        /// All event handlers.
        /// </summary>
        private readonly List<EvtHandler> _eventHandlers = new List<EvtHandler>();

        /// <summary>
        /// Returns list of event handlers
        /// </summary>
        public IList<EvtHandler> EventHandlers { get { return _eventHandlers; } }

        /// <summary>
        /// Adds an event implementor
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public Adaptive.Core.EvtHandler<TBase> ImplementEvents(Func<EventInfo, bool> predicate = null)
        {
            EvtHandler<TBase> handler = new EvtHandler<TBase>() { Predicate = predicate, BaseClass = _baseClass };
            _eventHandlers.Add(handler);
            return handler;
        }

        #endregion

        /// <summary>
        /// Implements given interface using TBase as base class in given dynamic module.
        /// </summary>
        /// <param name="interfaceType">Interface to implement.</param>
        /// <returns>Implemented type. Multiple calls with same input yields same type.</returns>
        public Type Implement(Type interfaceType)
        {
            if (!interfaceType.IsInterface || (!interfaceType.IsPublic && !interfaceType.IsNestedPublic))
                throw new ArgumentException("interfaceType must be a public interface: " + interfaceType.FullName);

            // Create name of new type and see if it already has been created
            string typeName = string.Format("AdaptiveFactory_{0}_{1}_{2}", _id, _baseClass.Name, interfaceType.GetHashCode())
                .Replace('+', '_');

            // Make typename unique if not caching types
            if (!CacheTypes)
                typeName = typeName + Guid.NewGuid().ToString("N");

            Type type = Module.Assembly.GetType(typeName);
            if (type != null)
                return type;

            // Create type that implements interface and inherits from base
            TypeBuilder typeBuilder = Module.DefineType(typeName, TypeAttributes.Class, _baseClass);

            // Define single Init method to call in ctor
            MethodBuilder initMethod = typeBuilder.DefineMethod("Init", MethodAttributes.Private, typeof(void), Type.EmptyTypes);
            ILGenerator initMethodGenerator = initMethod.GetILGenerator();

            // Define static ctor
            ConstructorBuilder staticCtor = typeBuilder.DefineConstructor(MethodAttributes.Static, CallingConventions.Standard, Type.EmptyTypes);
            ILGenerator staticCtorGenerator = staticCtor.GetILGenerator();

            // Go through all interfaces and implement them
            foreach (Type itfType in interfaceType.GetInterfaces().Concat(new[] { interfaceType }))
            {
                // Skip if already implemented by base class
                if (itfType.IsAssignableFrom(_baseClass))
                    continue;

                typeBuilder.AddInterfaceImplementation(itfType);

                // Go through all methods
                foreach (MethodInfo method in itfType.GetMethods())
                {
                    // Skip getters/setters
                    if (method.IsSpecialName && !ImplementSecondaryByMethods)
                        continue;

                    // Find handler
                    MethodHandler handler = _methodHandlers.FirstOrDefault(h => h.Predicate == null || h.Predicate(method));
                    if (handler == null)
                        throw new ArgumentException("No handler found for method: " + method.Name);

                    // Validate signature
                    if (method.GetParameters().Any(p => p.IsOut))
                        throw new ArgumentException(string.Format("Method {0} contains unsupported out parameters.", method.Name));
                    if (method.GetParameters().Any(p => p.ParameterType.IsByRef))
                        throw new ArgumentException(string.Format("Method {0} contains unsupported ref parameters.", method.Name));

                    // Call checker if any
                    if (handler.Checker != null)
                        handler.Checker(method);

                    // Implement
                    MethodBuilderContext context = new MethodBuilderContext()
                        {
                            BaseClass = _baseClass,
                            TypeBuilder = typeBuilder,
                            Method = method,
                            InitMethodGenerator = initMethodGenerator,
                            StaticCtorGenerator = staticCtorGenerator
                        };
                    BuildMethod(context, handler);
                }

                // Go through all properties
                if (!ImplementSecondaryByMethods)
                {
                    foreach (PropertyInfo prop in itfType.GetProperties())
                    {
                        if (prop.GetIndexParameters().Any())
                            throw new ArgumentException(string.Format("Indexed properties as {0} are not supported.", prop.Name));

                        PropertyHandler handler = _propertyHandlers.FirstOrDefault(h => h.Predicate == null || h.Predicate(prop));
                        if (handler == null)
                            throw new ArgumentException("No handler found for property: " + prop.Name);

                        if (handler.Checker != null)
                            handler.Checker(prop);

                        PropertyBuilderContext context = new PropertyBuilderContext()
                        {
                            BaseClass = _baseClass,
                            TypeBuilder = typeBuilder,
                            StaticCtorGenerator = staticCtorGenerator,
                            InitMethodGenerator = initMethodGenerator,
                            Property = prop
                        };
                        BuildProperty(context, handler);
                    }

                    // Go through all events
                    foreach (EventInfo evt in itfType.GetEvents())
                    {
                        EvtHandler handler = _eventHandlers.FirstOrDefault(h => h.Predicate == null || h.Predicate(evt));
                        if (handler == null)
                            throw new ArgumentException("No handler found for event: " + evt.Name);

                        if (handler.Checker != null)
                            handler.Checker(evt);

                        EventBuilderContext context = new EventBuilderContext()
                        {
                            BaseClass = _baseClass,
                            TypeBuilder = typeBuilder,
                            StaticCtorGenerator = staticCtorGenerator,
                            InitMethodGenerator = initMethodGenerator,
                            Event = evt
                        };

                        BuildEvent(context, handler);
                    }
                }
            }

            // End Init method
            initMethodGenerator.Emit(OpCodes.Ret);

            // End static ctro
            staticCtorGenerator.Emit(OpCodes.Ret);

            // Define constructors
            foreach (ConstructorInfo ctor in _baseClass.GetConstructors())
            {
                BuildCtor(typeBuilder, ctor, initMethod);
            }

            // If none, define default ctor
            if (_baseClass.GetConstructors().Length == 0)
            {
                BuildCtor(typeBuilder, null, initMethod);
            }

            // Create new type
            return typeBuilder.CreateType();
        }

        /// <summary>
        /// Implements given interface type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public Type Implement<T>()
        {
            return Implement(typeof(T));
        }

        #region Member implement methods

        /// <summary>
        /// Implements ctor with same signature as base class ctor
        /// </summary>
        /// <param name="typeBuilder"></param>
        /// <param name="baseClassCtor"></param>
        /// <param name="initMethod"></param>
        private static void BuildCtor(TypeBuilder typeBuilder, ConstructorInfo baseClassCtor, MethodBuilder initMethod)
        {
            ConstructorBuilder ctor = typeBuilder.DefineConstructor(
                MethodAttributes.Public, 
                CallingConventions.Standard, 
                baseClassCtor != null ? baseClassCtor.GetParameters().Select(p => p.ParameterType).ToArray() : new Type[0]);
            ILGenerator generator = ctor.GetILGenerator();

            // Call base ctor
            if (baseClassCtor != null)
            {
                generator.Emit(OpCodes.Ldarg_0); // Load instance as first param to execute
                for (int i = 0; i < baseClassCtor.GetParameters().Length; i++)
                    generator.Emit(OpCodes.Ldarg, i + 1);
                generator.Emit(OpCodes.Call, baseClassCtor);
            }

            // Call Init method
            generator.Emit(OpCodes.Ldarg_0);
            generator.EmitCall(OpCodes.Call, initMethod, null);
            generator.Emit(OpCodes.Ret);
        }

        private static void BuildMethod(MethodBuilderContext context, MethodHandler handler)
        {
            // Define method
            context.MethodInfoField = context.TypeBuilder.DefineField("__methodInfo__" + context.Method.Name + context.Method.GetHashCode().ToString(), typeof(MethodInfo), FieldAttributes.Static | FieldAttributes.Private);
            context.StaticCtorGenerator.EmitLoadMethodInfo(context.Method);
            context.StaticCtorGenerator.Emit(OpCodes.Stsfld, context.MethodInfoField);

            context.MethodBuilder = context.TypeBuilder.DefineMethod(context.Method.Name, MethodAttributes.Virtual | MethodAttributes.Public, context.Method.ReturnType, context.Method.GetParameters().Select(p => p.ParameterType).ToArray());
            context.TypeBuilder.DefineMethodOverride(context.MethodBuilder, context.Method);

            // Build method body leaving return value at stack
            context.ExecMethodGenerator = context.MethodBuilder.GetILGenerator();
            handler.Implement(context);

            // Return return value
            context.ExecMethodGenerator.Emit(OpCodes.Ret);
        }

        private static void BuildProperty(PropertyBuilderContext context, PropertyHandler handler)
        {
            // Define static field to hold PropertyInfo in efficient way
            context.PropertyInfoField = context.TypeBuilder.DefineField("__propertyInfo__" + context.Property.Name, typeof(PropertyInfo), FieldAttributes.Private | FieldAttributes.Static);
            context.StaticCtorGenerator.EmitLoadPropertyInfo(context.Property);
            context.StaticCtorGenerator.Emit(OpCodes.Stsfld, context.PropertyInfoField);

            context.PropertyBuilder = context.TypeBuilder.DefineProperty(context.Property.Name, PropertyAttributes.None, context.Property.PropertyType, null);

            if (context.Property.CanRead)
            {
                MethodBuilder getMethod = context.TypeBuilder.DefineMethod("get_" + context.Property.Name, MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, context.Property.PropertyType, Type.EmptyTypes);
                context.PropertyBuilder.SetGetMethod(getMethod);

                context.GetMethodGenerator = getMethod.GetILGenerator();
                context.TypeBuilder.DefineMethodOverride(getMethod, context.Property.GetGetMethod());
            }

            if (context.Property.CanWrite)
            {
                MethodBuilder setMethod = context.TypeBuilder.DefineMethod("set_" + context.Property.Name, MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, typeof(void), new[] { context.Property.PropertyType });
                context.PropertyBuilder.SetSetMethod(setMethod);

                context.SetMethodGenerator = setMethod.GetILGenerator();
                context.TypeBuilder.DefineMethodOverride(setMethod, context.Property.GetSetMethod());
            }

            handler.Implement(context);

            if (context.GetMethodGenerator != null)
                context.GetMethodGenerator.Emit(OpCodes.Ret);
            if (context.SetMethodGenerator != null)
                context.SetMethodGenerator.Emit(OpCodes.Ret);
        }

        private static void BuildEvent(EventBuilderContext context, EvtHandler handler)
        {
            context.EventBuilder = context.TypeBuilder.DefineEvent(context.Event.Name, context.Event.Attributes, context.Event.EventHandlerType);

            context.EventInfoField = context.TypeBuilder.DefineField("__eventInfo__" + context.Event.Name, typeof(EventInfo), FieldAttributes.Static | FieldAttributes.Private);
            context.StaticCtorGenerator.EmitLoadEventInfo(context.Event);
            context.StaticCtorGenerator.Emit(OpCodes.Stsfld, context.EventInfoField);

            // Add method
            MethodBuilder addMethod = context.TypeBuilder.DefineMethod("add_" + context.Event.Name, MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual, typeof(void), new[] { context.Event.EventHandlerType });
            context.EventBuilder.SetAddOnMethod(addMethod);
            context.TypeBuilder.DefineMethodOverride(addMethod, context.Event.GetAddMethod());
            context.AddMethodGenerator = addMethod.GetILGenerator();
            
            // Remove method
            MethodBuilder removeMethod = context.TypeBuilder.DefineMethod("remove_" + context.Event.Name, MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual, typeof(void), new[] { context.Event.EventHandlerType });
            context.EventBuilder.SetRemoveOnMethod(removeMethod);
            context.TypeBuilder.DefineMethodOverride(removeMethod, context.Event.GetRemoveMethod());            
            context.RemoveMethodGenerator = removeMethod.GetILGenerator();

            // Run implementor
            handler.Implement(context);

            context.AddMethodGenerator.Emit(OpCodes.Ret);
            context.RemoveMethodGenerator.Emit(OpCodes.Ret);
        }

        #endregion
    }
}
