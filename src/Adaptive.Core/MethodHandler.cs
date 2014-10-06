using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Holds all info about a method handler
    /// </summary>
    public abstract class MethodHandler : MemberHandler<MethodInfo>
    {
        /// <summary>
        /// Performs the actual implementation
        /// </summary>
        public Action<MethodBuilderContext> Implement { get; internal set; }
    }

    /// <summary>
    /// Like method handler but with base class
    /// </summary>
    /// <typeparam name="TBase"></typeparam>
    public class MethodHandler<TBase> : MethodHandler
    {
    }

    /// <summary>
    /// Holds all the method registration extension methods
    /// </summary>
    public static class MethodHandlerExtensions
    {
        /// <summary>
        /// Adds a syntax checker that checks each matched method during implementation. 
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="checker"></param>
        /// <returns></returns>
        public static THandler WithSyntaxChecker<THandler>(this THandler handler, Action<MethodInfo> checker) where THandler : MethodHandler
        {
            return handler.WithSyntaxChecker<THandler, MethodInfo>(checker);
        }

        #region UsingSharedExecuter

        /// <summary>
        /// Handles method calls using general execute method.
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initializer"></param>
        /// <param name="executer"></param>
        /// <returns></returns>
        public static MethodHandler<TBase> UsingSharedExecuter<TBase, TState>(this MethodHandler<TBase> handler, Func<TBase, MethodInfo, TState> initializer, Func<TBase, TState, object[], object> executer)
        {
            if (handler.Implement != null)
                throw new ArgumentException("UsingGeneralExecuter must be inner-most implementor.");

            handler.Implement = context => ImplementSharedExecuter(context, initializer.Method, executer.Method);
            return handler;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="initMethodName"></param>
        /// <param name="execMethodName"></param>
        /// <returns></returns>
        public static THandler UsingSharedExecuter<THandler>(this THandler handler, string initMethodName, string execMethodName) where THandler : MethodHandler
        {
            if (handler.Implement != null)
                throw new ArgumentException("UsingGeneralExecuter must be inner-most implementor.");

            MethodInfo initMethod = handler.BaseClass.FindMethodOrThrow(initMethodName, null, typeof(MethodInfo));
            MethodInfo execMethod = handler.BaseClass.FindMethodOrThrow(execMethodName, typeof(object), initMethod.ReturnType, typeof(object[]));
            handler.Implement = context => ImplementSharedExecuter(context, initMethod, execMethod);
            return handler;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TBase"></typeparam>
        /// <param name="handler"></param>
        /// <param name="executer"></param>
        /// <returns></returns>
        public static MethodHandler<TBase> UsingSharedExecuter<TBase>(this MethodHandler<TBase> handler, Func<TBase, MethodInfo, object[], object> executer)
        {
            if (handler.Implement != null)
                throw new ArgumentException("UsingGeneralExecuter must be inner-most implementor.");

            handler.Implement = context => ImplementSharedExecuter(context, null, executer.Method);
            return handler;
        }

        private static void ImplementSharedExecuter(MethodBuilderContext context, MethodInfo initMethod, MethodInfo execMethod)
        {
            FieldBuilder field = null;
            if (initMethod != null)
            {
                field = context.TypeBuilder.DefineField("__methodField_" + context.Method.Name + context.Method.GetHashCode().ToString(), initMethod.ReturnType, FieldAttributes.Private);

                // Load instance for initializer
                context.InitMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.InitMethodGenerator.Emit(OpCodes.Ldarg_0);
                context.InitMethodGenerator.Emit(OpCodes.Ldsfld, context.MethodInfoField);
                context.InitMethodGenerator.EmitCall(OpCodes.Call, initMethod, null); // Call init method
                context.InitMethodGenerator.Emit(OpCodes.Stfld, field); // Store result in field
            }

            // Create object array to hold all parameters and load on stack
            var parametersArray = context.ExecMethodGenerator.EmitParametersArray(context.Method);

            // Call executer(layer, state, params)
            context.ExecMethodGenerator.Emit(OpCodes.Ldarg_0); // Load instance as first param to execute

            if (field != null)
            {
                context.ExecMethodGenerator.Emit(OpCodes.Ldarg_0); // Load instance for load field
                context.ExecMethodGenerator.Emit(OpCodes.Ldfld, field); // Load infos array
            }
            else
            {
                context.ExecMethodGenerator.Emit(OpCodes.Ldsfld, context.MethodInfoField);
            }

            context.ExecMethodGenerator.Emit(OpCodes.Ldloc, parametersArray); // Load params as third param for execute
            context.ExecMethodGenerator.EmitCall(OpCodes.Call, execMethod, null); // Call execute method

            // Handle return value
            if (context.Method.ReturnType == typeof(void))
                context.ExecMethodGenerator.Emit(OpCodes.Pop);
            else if (context.Method.ReturnType.IsValueType)
                context.ExecMethodGenerator.Emit(OpCodes.Unbox_Any, context.Method.ReturnType);
        }

        #endregion

        #region UsingTarget

        private static void ImplementTarget(MethodBuilderContext context, string targetMemberName, Func<MethodInfo, MethodInfo> targetMethodSelector)
        {
            // Find target method
            MethodInfo targetMethod = targetMethodSelector(context.Method);
            if (targetMethod == null)
                throw new ArgumentException("Target method for interface method {0} not found.", context.Method.Name);

            // If method is instance, we need to find target instance and load it on stack
            if (!targetMethod.IsStatic)
            {
                MemberInfo getTargetMember = context.BaseClass.GetMember(targetMemberName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Single();

                context.ExecMethodGenerator.Emit(OpCodes.Ldarg_0);
                if (getTargetMember.MemberType == MemberTypes.Field)
                    context.ExecMethodGenerator.Emit(OpCodes.Ldfld, (FieldInfo)getTargetMember);
                else if (getTargetMember.MemberType == MemberTypes.Property)
                    context.ExecMethodGenerator.EmitCall(OpCodes.Call, ((PropertyInfo)getTargetMember).GetGetMethod(), null);
                else
                    context.ExecMethodGenerator.EmitCall(OpCodes.Call, (MethodInfo)getTargetMember, null);
            }

            // Load all parameters
            for (int i = 1; i <= context.Method.GetParameters().Length; i++)
                context.ExecMethodGenerator.Emit(OpCodes.Ldarg, i);

            // Call method and return
            context.ExecMethodGenerator.EmitCall(OpCodes.Call, targetMethod, null);
            context.ExecMethodGenerator.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Implements methods by calling same method on inner target instance.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="getTargetMember"></param>
        /// <returns></returns>
        public static THandler UsingTarget<THandler>(this THandler handler, string getTargetMember) where THandler : MethodHandler
        {
            return handler.UsingTarget(getTargetMember, m => m);
        }

        /// <summary>
        /// Implements methods by calling selected method (with same signature) on inner target instance.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handler"></param>
        /// <param name="getTargetMember"></param>
        /// <param name="targetMemberSelector"></param>
        /// <returns></returns>
        public static THandler UsingTarget<THandler>(this THandler handler, string getTargetMember, Func<MethodInfo, MethodInfo> targetMemberSelector) where THandler : MethodHandler
        {
            handler.Implement = context =>
                ImplementTarget(context, getTargetMember, targetMemberSelector);
            return handler;
        }

        #endregion
    }
}
