using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Holds info about an event handler
    /// </summary>
    public class EvtHandler : MemberHandler<EventInfo>
    {
        /// <summary>
        /// Mandatory implementor
        /// </summary>
        public Action<EventBuilderContext> Implement { get; internal set; }
    }

    /// <summary>
    /// Holds info about an event handler
    /// </summary>
    /// <typeparam name="TBase"></typeparam>
    public class EvtHandler<TBase> : EvtHandler
    {
    }

    /// <summary>
    /// Holds event logic
    /// </summary>
    public static class EventHandlerExtensions
    {
        /// <summary>
        /// Adds a syntax checker that checks each matched event during implementation. 
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="checker"></param>
        /// <returns></returns>
        public static THandler WithSyntaxChecker<THandler>(this THandler handler, Action<EventInfo> checker) where THandler : EvtHandler
        {
            return handler.WithSyntaxChecker<THandler, EventInfo>(checker);
        }

        /// <summary>
        /// Implements event using an Add and Remove method.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initMethod"></param>
        /// <param name="addMethod"></param>
        /// <param name="removeMethod"></param>
        /// <returns></returns>
        public static THandler UsingAdderAndRemover<THandler>(this THandler handler, MethodInfo initMethod, MethodInfo addMethod, MethodInfo removeMethod) where THandler : EvtHandler
        {
            if (initMethod == null)
                throw new ArgumentNullException("initMethod must be specified.");
            if (addMethod == null)
                throw new ArgumentNullException("addMethod must be specified.");
            if (removeMethod == null)
                throw new ArgumentNullException("removeMethod must be specified.");

            handler.Implement = context => ImplementAddRemove(context, initMethod, addMethod, removeMethod);
            return handler;
        }

        /// <summary>
        /// Implements event using an Add and Remove method.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initMethod"></param>
        /// <param name="addMethod"></param>
        /// <param name="removeMethod"></param>
        /// <returns></returns>
        public static EvtHandler<TBase> UsingAdderAndRemover<TBase, TState>(this EvtHandler<TBase> handler, Func<TBase, EventInfo, TState> initMethod, Action<TBase, TState, EventHandler> addMethod, Action<TBase, TState, EventHandler> removeMethod)
        {
            return handler.UsingAdderAndRemover(initMethod.GetMethod(), addMethod.GetMethod(), removeMethod.GetMethod());
        }

        /// <summary>
        /// Implements event using an Add and Remove method.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initMethodName"></param>
        /// <param name="addMethodName"></param>
        /// <param name="removeMethodName"></param>
        /// <returns></returns>
        public static THandler UsingAdderAndRemover<THandler>(this THandler handler, string initMethodName, string addMethodName, string removeMethodName) where THandler : EvtHandler
        {
            MethodInfo initMethod = handler.BaseClass.FindMethodOrThrow(initMethodName, null, typeof(EventInfo));
            MethodInfo addMethod = handler.BaseClass.FindMethodOrThrow(addMethodName, typeof(void), initMethod.ReturnType, null);
            MethodInfo removeMethod = handler.BaseClass.FindMethodOrThrow(removeMethodName, typeof(void), initMethod.ReturnType, null);

            handler.Implement = context => ImplementAddRemove(context, initMethod, addMethod, removeMethod);
            return handler;
        }

        private static void ImplementAddRemove(EventBuilderContext context, MethodInfo initMethod, MethodInfo addMethod, MethodInfo removeMethod)
        {
            // Define field to hold info for this context.Event
            FieldBuilder infoField = context.TypeBuilder.DefineField("__eventInfo_" + context.Event.Name, initMethod.ReturnType, FieldAttributes.Private);

            // Append to Init method code to call our initializer and store output in field
            context.InitMethodGenerator.Emit(OpCodes.Ldarg_0);
            context.InitMethodGenerator.Emit(OpCodes.Ldarg_0);
            context.InitMethodGenerator.Emit(OpCodes.Ldsfld, context.EventInfoField);
            context.InitMethodGenerator.EmitCall(OpCodes.Call, initMethod, null);
            context.InitMethodGenerator.Emit(OpCodes.Stfld, infoField);

            context.AddMethodGenerator.Emit(OpCodes.Ldarg_0); // Target for handler
            context.AddMethodGenerator.Emit(OpCodes.Ldarg_0); // Target for info field
            context.AddMethodGenerator.Emit(OpCodes.Ldfld, infoField); // Info field
            context.AddMethodGenerator.Emit(OpCodes.Ldarg_1); // Handler
            context.AddMethodGenerator.EmitCall(OpCodes.Call, addMethod.Specialize(context.BaseClass, typeof(void), infoField.FieldType, context.Event.EventHandlerType), null);

            context.RemoveMethodGenerator.Emit(OpCodes.Ldarg_0); // Target for handler
            context.RemoveMethodGenerator.Emit(OpCodes.Ldarg_0); // Target for info field
            context.RemoveMethodGenerator.Emit(OpCodes.Ldfld, infoField); // Info field
            context.RemoveMethodGenerator.Emit(OpCodes.Ldarg_1); // Handler
            context.RemoveMethodGenerator.EmitCall(OpCodes.Call, removeMethod.Specialize(context.BaseClass, typeof(void), infoField.FieldType, context.Event.EventHandlerType), null);
        }
    }
}
