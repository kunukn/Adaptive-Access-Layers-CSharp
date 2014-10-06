using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Represents a property handler
    /// </summary>
    public class PropertyHandler : MemberHandler<PropertyInfo>
    {
        /// <summary>
        /// Mandatory implementor
        /// </summary>
        public Action<PropertyBuilderContext> Implement { get; internal set; }
    }

    /// <summary>
    /// Like PropertyHandler but with type
    /// </summary>
    /// <typeparam name="TBase"></typeparam>
    public class PropertyHandler<TBase> : PropertyHandler
    {
    }

    /// <summary>
    /// Holds all property registration methods
    /// </summary>
    public static class PropertyHandlerExtensions
    {
        /// <summary>
        /// Adds a syntax checker that checks each matched method during implementation. 
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="checker"></param>
        /// <returns></returns>
        public static THandler WithSyntaxChecker<THandler>(this THandler handler, Action<PropertyInfo> checker) where THandler : PropertyHandler
        {
            return handler.WithSyntaxChecker<THandler, PropertyInfo>(checker);
        }

        #region Getter and Setter

        /// <summary>
        /// Implements properties using getter and setter methods
        /// </summary>
        /// <param name="context"></param>
        /// <param name="initMethod"></param>
        /// <param name="getMethod"></param>
        /// <param name="setMethod"></param>
        private static void ImplementGetterAndSetter(PropertyBuilderContext context, MethodInfo initMethod, MethodInfo getMethod, MethodInfo setMethod)
        {
            // Define field to hold info for this prop
            FieldBuilder stateField = context.TypeBuilder.DefineField("__propertyState_" + context.Property.Name, initMethod.ReturnType, FieldAttributes.Private);

            // Append to Init method code to call our initializer and store output in field
            context.InitMethodGenerator.Emit(OpCodes.Ldarg_0);
            context.InitMethodGenerator.Emit(OpCodes.Ldarg_0);
            context.InitMethodGenerator.Emit(OpCodes.Ldsfld, context.PropertyInfoField);
            context.InitMethodGenerator.EmitCall(OpCodes.Call, initMethod, null);
            context.InitMethodGenerator.Emit(OpCodes.Stfld, stateField);

            // Call getter method
            if (context.GetMethodGenerator != null)
            {
                context.GetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.GetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.GetMethodGenerator.Emit(OpCodes.Ldfld, stateField);

                getMethod = getMethod.Specialize(context.BaseClass, typeof(object), stateField.FieldType);

                context.GetMethodGenerator.EmitCall(OpCodes.Call, getMethod, null);
                if (context.Property.PropertyType.IsValueType && !getMethod.ReturnType.IsValueType)
                    context.GetMethodGenerator.Emit(OpCodes.Unbox_Any, context.Property.PropertyType);
                else if (!context.Property.PropertyType.IsValueType && getMethod.ReturnType.IsValueType)
                    context.GetMethodGenerator.Emit(OpCodes.Box);
            }

            // Call setter method
            if (context.SetMethodGenerator != null)
            {
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_0); // Instance for handler call
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_0); // Instance for field
                context.SetMethodGenerator.Emit(OpCodes.Ldfld, stateField);  // Info field   
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_1); // Value from set method

                setMethod = setMethod.Specialize(context.BaseClass, typeof(void), stateField.FieldType, context.Property.PropertyType);

                // If prop is value type but handler takes object, we must box
                if (context.Property.PropertyType.IsValueType && !setMethod.GetParameters().Last().ParameterType.IsValueType)
                    context.SetMethodGenerator.Emit(OpCodes.Box, context.Property.PropertyType);

                context.SetMethodGenerator.EmitCall(OpCodes.Call, setMethod, null);
            }
        }

        /// <summary>
        /// Implements properties using instance init, getter and setter methods on TBase class.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initMethodName"></param>
        /// <param name="getMethodName"></param>
        /// <param name="setMethodName"></param>
        /// <returns></returns>
        public static THandler UsingGetterAndSetter<THandler>(this THandler handler, string initMethodName, string getMethodName, string setMethodName) where THandler : PropertyHandler
        {
            MethodInfo initMethod = handler.BaseClass.FindMethodOrThrow(initMethodName, null, typeof(PropertyInfo));
            if (initMethod.ReturnType == typeof(void))
                throw new ArgumentException("initMethod cannot return void.");

            MethodInfo getMethod = handler.BaseClass.FindMethodOrThrow(getMethodName, typeof(object), initMethod.ReturnType); 
            MethodInfo setMethod = handler.BaseClass.FindMethodOrThrow(setMethodName, typeof(void), initMethod.ReturnType, typeof(object));

            return handler.UsingGetterAndSetter(initMethod, getMethod, setMethod);
        }

        /// <summary>
        /// Implements properties using init, getter and setter methods.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initMethod"></param>
        /// <param name="getMethod"></param>
        /// <param name="setMethod"></param>
        /// <returns></returns>
        public static THandler UsingGetterAndSetter<THandler>(this THandler handler, MethodInfo initMethod, MethodInfo getMethod, MethodInfo setMethod) where THandler : PropertyHandler
        {
            if (initMethod == null)
                throw new ArgumentNullException("initMethod");
            if (getMethod == null)
                throw new ArgumentNullException("getMethod");
            if (setMethod == null)
                throw new ArgumentNullException("setMethod");

            if (initMethod.ReturnType == typeof(void))
                throw new ArgumentException("initMethod cannot return void.");
            if (initMethod.IsStatic)
                initMethod.ValidateParameters(handler.BaseClass, typeof(PropertyInfo));
            if (!initMethod.IsStatic)
                initMethod.ValidateParameters(typeof(PropertyInfo));

            if (getMethod.IsStatic)
                getMethod.ValidateParameters(handler.BaseClass, initMethod.ReturnType);
            else
                getMethod.ValidateParameters(initMethod.ReturnType);

            if (setMethod.IsStatic)
                setMethod.ValidateParameters(handler.BaseClass, initMethod.ReturnType, typeof(object));
            else
                setMethod.ValidateParameters(initMethod.ReturnType, typeof(object));
            setMethod.ValidateVoidReturnType();

            if (handler.Implement != null)
                throw new ArgumentException("UsingGetterAndSetter must be innermost builder.");

            handler.Implement = context => ImplementGetterAndSetter(context, initMethod, getMethod, setMethod);
            return handler;
        }

        /// <summary>
        /// Implements properties using static init, getter and setter methods.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="registration"></param>
        /// <param name="initMethod"></param>
        /// <param name="getMethod"></param>
        /// <param name="setMethod"></param>
        /// <returns></returns>
        public static PropertyHandler<TBase> UsingGetterAndSetter<TBase, TState>(this PropertyHandler<TBase> registration, Func<TBase, PropertyInfo, TState> initMethod, Func<TBase, TState, object> getMethod, Action<TBase, TState, object> setMethod) where TBase : class
        {
            if (initMethod.Target != null)
                throw new ArgumentException("initMethod must be static.");
            if (getMethod.Target != null)
                throw new ArgumentException("getMethod must be static.");
            if (setMethod.Target != null)
                throw new ArgumentException("setMethod must be static.");

            return registration.UsingGetterAndSetter(initMethod.Method, getMethod.Method, setMethod.Method);
        }

        #endregion

        #region Backing Field

        /// <summary>
        /// Implements a property using a private backing field
        /// </summary>
        /// <param name="context"></param>
        private static void ImplementBackingField(PropertyBuilderContext context)
        {
            // Define field to hold info for this prop
            FieldBuilder valueField = context.TypeBuilder.DefineField("__backingField_" + context.Property.Name, context.Property.PropertyType, FieldAttributes.Private);

            if (context.GetMethodGenerator != null)
            {
                context.GetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.GetMethodGenerator.Emit(OpCodes.Ldfld, valueField);
            }

            if (context.SetMethodGenerator != null)
            {
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_1);
                context.SetMethodGenerator.Emit(OpCodes.Stfld, valueField);
            }
        }

        /// <summary>
        /// Implements all matching properties using a private backing field to hold current value.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <param name="handler"></param>
        /// <returns></returns>
        public static PropertyHandler<TBase> UsingBackingField<TBase>(this PropertyHandler<TBase> handler)
        {
            if (handler.Implement != null)
                throw new ArgumentException("UsingBackingField cannot be combined with other UsingXXX methods.");

            handler.Implement = ImplementBackingField;

            return handler;
        }

        #endregion

        #region Inspector

        private static void ImplementGetInspector(PropertyBuilderContext context, Action<PropertyBuilderContext> implement, MethodInfo getInspector)
        {
            // Run real implementor first
            implement(context);

            // Getter now has value to be returned as single element on stack
            if (context.GetMethodGenerator != null)
            {
                // Store return value in local var
                LocalBuilder returnValue = context.GetMethodGenerator.DeclareLocal(context.Property.PropertyType);
                context.GetMethodGenerator.Emit(OpCodes.Stloc, returnValue);

                // Call inspector(layer, PropertyInfo, value)
                context.GetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.GetMethodGenerator.Emit(OpCodes.Ldsfld, context.PropertyInfoField);
                context.GetMethodGenerator.Emit(OpCodes.Ldloc, returnValue);
                if (context.Property.PropertyType.IsValueType)
                    context.GetMethodGenerator.Emit(OpCodes.Box, context.Property.PropertyType);

                // Allow inspector be generic in property type
                getInspector = getInspector.Specialize(context.BaseClass, typeof(void), typeof(PropertyInfo), context.Property.PropertyType);
                
                context.GetMethodGenerator.EmitCall(OpCodes.Call, getInspector, null);

                // Load real return value to leave stack unchanged
                context.GetMethodGenerator.Emit(OpCodes.Ldloc, returnValue);
            }
        }

        private static void ImplementSetInspector(PropertyBuilderContext context, Action<PropertyBuilderContext> implement, MethodInfo preSetInspector, MethodInfo postSetInspector)
        {
            // Call pre set inspector
            if (preSetInspector != null && context.SetMethodGenerator != null)
            {
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.SetMethodGenerator.Emit(OpCodes.Ldsfld, context.PropertyInfoField);
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_1);

                preSetInspector = preSetInspector.Specialize(context.BaseClass, typeof(void), typeof(PropertyInfo), context.Property.PropertyType);

                if (context.Property.PropertyType.IsValueType && !preSetInspector.GetParameters().Last().ParameterType.IsValueType)
                    context.SetMethodGenerator.Emit(OpCodes.Box, context.Property.PropertyType);

                context.SetMethodGenerator.EmitCall(OpCodes.Call, preSetInspector, null);
            }

            // Run real implementor now
            implement(context);

            // Call post set
            if (context.SetMethodGenerator != null && postSetInspector != null)
            {
                // Call inspector(layer, PropertyInfo, value)
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.SetMethodGenerator.Emit(OpCodes.Ldsfld, context.PropertyInfoField);
                context.SetMethodGenerator.Emit(OpCodes.Ldarg_1);

                // Allow inspector be generic in property type
                postSetInspector = postSetInspector.Specialize(context.BaseClass, typeof(void), typeof(PropertyInfo), context.Property.PropertyType);
                
                if (context.Property.PropertyType.IsValueType && !postSetInspector.GetParameters().Last().ParameterType.IsValueType)
                    context.SetMethodGenerator.Emit(OpCodes.Box, context.Property.PropertyType);

                context.SetMethodGenerator.EmitCall(OpCodes.Call, postSetInspector, null);
            }
        }

        /// <summary>
        /// Injects inspector method on every property get right before value is returned.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="getInspectorMethod"></param>
        /// <returns></returns>
        public static THandler WithGetInspector<THandler>(this THandler handler, MethodInfo getInspectorMethod) where THandler : PropertyHandler
        {
            if (handler.Implement == null)
                throw new InvalidOperationException("Real builder must have been set.");

            Action<PropertyBuilderContext> realImplement = handler.Implement;
            handler.Implement = context => ImplementGetInspector(context, realImplement, getInspectorMethod);
            return handler;
        }

        /// <summary>
        /// Injects inspector method on every property set right before value is returned.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="postSetInspectorMethod"></param>
        /// <param name="preSetInspectorMethod"></param>
        /// <returns></returns>
        public static THandler WithSetInspector<THandler>(this THandler handler, MethodInfo preSetInspectorMethod, MethodInfo postSetInspectorMethod) where THandler : PropertyHandler
        {
            if (handler.Implement == null)
                throw new InvalidOperationException("Real builder must have been set.");

            Action<PropertyBuilderContext> realImplement = handler.Implement;
            handler.Implement = context => ImplementSetInspector(context, realImplement, preSetInspectorMethod, postSetInspectorMethod);
            return handler;
        }

        /// <summary>
        /// Injects inspector method on every property get right before value is returned.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <param name="handler"></param>
        /// <param name="getInspector"></param>
        /// <returns></returns>
        public static PropertyHandler<TBase> WithGetInspector<TBase>(this PropertyHandler<TBase> handler, Action<TBase, PropertyInfo, object> getInspector)
        {
            if (getInspector.Target != null)
                throw new ArgumentException("getInspector must be a static method.");

            return handler.WithGetInspector(getInspector.Method);
        }

        /// <summary>
        /// Injects inspector method on every property get right before value is returned.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="getInspectorMethodName"></param>
        /// <returns></returns>
        public static THandler WithGetInspector<THandler>(this THandler handler, string getInspectorMethodName) where THandler : PropertyHandler
        {
            MethodInfo method = handler.BaseClass.FindMethodOrThrow(getInspectorMethodName, typeof(void), typeof(PropertyInfo), typeof(object));

            return handler.WithGetInspector(method);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="preSetInspectorMethodName"></param>
        /// <param name="postSetInspectorMethodName"></param>
        /// <returns></returns>
        public static THandler WithSetInspector<THandler>(this THandler handler, string preSetInspectorMethodName, string postSetInspectorMethodName) where THandler : PropertyHandler
        {
            MethodInfo preSetMethod = null;
            if (preSetInspectorMethodName != null)
                preSetMethod = handler.BaseClass.FindMethodOrThrow(preSetInspectorMethodName, typeof(void), typeof(PropertyInfo), typeof(object));
            MethodInfo postSetMethod = null;
            if (postSetInspectorMethodName != null)
                postSetMethod = handler.BaseClass.FindMethodOrThrow(postSetInspectorMethodName, typeof(void), typeof(PropertyInfo), typeof(object));

            return handler.WithSetInspector(preSetMethod, postSetMethod);
        }

        #endregion
    }
}
