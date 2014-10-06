using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Holds all info needed by an implementor
    /// </summary>
    public class TypeBuilderContext
    {
        /// <summary>
        /// The base class of the current dynamic type.
        /// </summary>
        public Type BaseClass { get; internal set; }

        /// <summary>
        /// The type builder
        /// </summary>
        public TypeBuilder TypeBuilder { get; internal set; }

        /// <summary>
        /// The IL of static ctor
        /// </summary>
        public ILGenerator StaticCtorGenerator { get; internal set; }

        /// <summary>
        /// The IL of instance init method called by all instance ctors.
        /// </summary>
        public ILGenerator InitMethodGenerator { get; internal set; }
    }

    /// <summary>
    /// Holds all info needed by a method implementor
    /// </summary>
    public class MethodBuilderContext : TypeBuilderContext
    {
        /// <summary>
        /// The static field holding method info
        /// </summary>
        public FieldBuilder MethodInfoField { get; internal set; }

        /// <summary>
        /// The method being implemented
        /// </summary>
        public MethodInfo Method { get; internal set; }

        /// <summary>
        /// The method builder
        /// </summary>
        public MethodBuilder MethodBuilder { get; internal set; }

        /// <summary>
        /// The IL of the execute method
        /// </summary>
        public ILGenerator ExecMethodGenerator { get; internal set; }
    }

    /// <summary>
    /// Holds all info needed by a property implementor
    /// </summary>
    public class PropertyBuilderContext : TypeBuilderContext
    {
        /// <summary>
        /// The static field holding PropertyInfo
        /// </summary>
        public FieldBuilder PropertyInfoField { get; internal set; }

        /// <summary>
        /// The property being implemented
        /// </summary>
        public PropertyInfo Property { get; internal set; }

        /// <summary>
        /// The property builder
        /// </summary>
        public PropertyBuilder PropertyBuilder { get; internal set; }

        /// <summary>
        /// The IL of set method
        /// </summary>
        public ILGenerator SetMethodGenerator { get; internal set; }

        /// <summary>
        /// The IL of get method
        /// </summary>
        public ILGenerator GetMethodGenerator { get; internal set; }
    }

    /// <summary>
    /// Holds all info needed by an event implementor
    /// </summary>
    public class EventBuilderContext : TypeBuilderContext
    {
        /// <summary>
        /// The event being implemented
        /// </summary>
        public EventInfo Event { get; internal set; }

        /// <summary>
        /// The event builder
        /// </summary>
        public EventBuilder EventBuilder { get; internal set; }

        /// <summary>
        /// The static field holding EventInfo
        /// </summary>
        public FieldBuilder EventInfoField { get; internal set; }

        /// <summary>
        /// The IL for add method
        /// </summary>
        public ILGenerator AddMethodGenerator { get; internal set; }

        /// <summary>
        /// The IL for remove method
        /// </summary>
        public ILGenerator RemoveMethodGenerator { get; internal set; }
    }
}
