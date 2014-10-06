using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    internal static class GeneratorExtensions
    {
        public static void EmitLoadPropertyInfo(this ILGenerator generator, PropertyInfo propertyInfo)
        {
            int index = Array.IndexOf(propertyInfo.DeclaringType.GetProperties(), propertyInfo);
            
            generator.Emit(OpCodes.Ldtoken, propertyInfo.DeclaringType);
            generator.EmitCall(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"), null);
            generator.EmitCall(OpCodes.Call, typeof(Type).GetMethod("GetProperties", Type.EmptyTypes), null); // Get all interface props on top of stack
            generator.Emit(OpCodes.Ldc_I4, index);
            generator.Emit(OpCodes.Ldelem, typeof(PropertyInfo));
        }

        public static void EmitLoadEventInfo(this ILGenerator generator, EventInfo eventInfo)
        {
            generator.Emit(OpCodes.Ldtoken, eventInfo.DeclaringType);
            generator.EmitCall(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"), null);
            generator.EmitCall(OpCodes.Call, typeof(Type).GetMethod("GetEvents", Type.EmptyTypes), null); // Get all interface props on top of stack
            generator.Emit(OpCodes.Ldc_I4, Array.IndexOf(eventInfo.DeclaringType.GetEvents(), eventInfo)); // Load index into properties
            generator.Emit(OpCodes.Ldelem, typeof(EventInfo)); // Get MethodInfo for current method
        }

        public static LocalBuilder EmitParametersArray(this ILGenerator execGenerator, MethodInfo method)
        {
            execGenerator.Emit(OpCodes.Ldc_I4, method.GetParameters().Length);
            LocalBuilder objectRef = execGenerator.DeclareLocal(typeof(object[]));
            execGenerator.Emit(OpCodes.Newarr, typeof(object));
            execGenerator.Emit(OpCodes.Stloc, objectRef);

            // Fill it with parameters
            for (int i = 0; i < method.GetParameters().Length; i++)
            {
                // Load array on stack
                execGenerator.Emit(OpCodes.Ldloc_0);

                // Load index on stack
                execGenerator.Emit(OpCodes.Ldc_I4, i);

                // Load argument N on stack
                execGenerator.Emit(OpCodes.Ldarg, i + 1);

                // Optionally box it
                if (method.GetParameters()[i].ParameterType.IsValueType)
                    execGenerator.Emit(OpCodes.Box, method.GetParameters()[i].ParameterType);

                // Store in object[]
                execGenerator.Emit(OpCodes.Stelem, typeof(object));
            }

            return objectRef;
        }

        public static void EmitLoadMethodInfo(this ILGenerator generator, MethodInfo method)
        {
            MethodInfo getMethodFromHandle = typeof(MethodInfo).GetMethod("GetMethodFromHandle", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy, null, new[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }, null);
            generator.Emit(OpCodes.Ldtoken, method);
            generator.Emit(OpCodes.Ldtoken, method.DeclaringType);
            generator.EmitCall(OpCodes.Call, getMethodFromHandle, null);
        }
    }
}
