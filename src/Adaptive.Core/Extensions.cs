using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Holds core extensions
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Creates a new interface type.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="fullName"></param>
        /// <param name="properties"></param>
        /// <param name="baseInterface"></param>
        /// <returns></returns>
        public static Type DefineInterface(this ModuleBuilder module, string fullName, Type baseInterface, params PropertyDefinition[] properties)
        {
            TypeBuilder builder = module.DefineType(fullName, TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.ClassSemanticsMask, baseInterface);
            System.Diagnostics.Debug.Assert(builder.FullName == fullName);

            foreach (PropertyDefinition prop in properties)
            {
                PropertyBuilder property = builder.DefineProperty(prop.Name, PropertyAttributes.None, prop.Type, Type.EmptyTypes);
                if (prop.CanRead)
                {
                    MethodBuilder getMethod = builder.DefineMethod("get_" + prop.Name, MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask, prop.Type, Type.EmptyTypes);
                    property.SetGetMethod(getMethod);
                }
                if (prop.CanWrite)
                {
                    MethodBuilder setMethod = builder.DefineMethod("set_" + prop.Name, MethodAttributes.SpecialName | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask, typeof(void), new[] { prop.Type });
                    property.SetSetMethod(setMethod);
                }
            }

            return builder.CreateType();
        }

        /// <summary>
        /// Checks that method has void return type
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static MethodInfo ValidateVoidReturnType(this MethodInfo method)
        {
            if (method.ReturnType != typeof(void))
                throw new ArgumentException(string.Format("Method {0}.{1} must return void and not {2}.", method.DeclaringType.FullName, method.Name, method.ReturnType.Name));

            return method;
        }

        /// <summary>
        /// Checks that method has given parameters
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public static MethodInfo ValidateParameters(this MethodInfo method, params Type[] parameterTypes)
        {
            if (!method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes))
                throw new ArgumentException(string.Format("Method {0}.{1} must take parameters ({2}).", method.DeclaringType.FullName, method.Name, string.Join(", ", parameterTypes.Select(p => p.Name))));
            return method;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public static MethodInfo GetMethod(this Delegate func)
        {
            if (func == null)
                return null;
            return func.Method;
        }

        /// <summary>
        /// As FindMethod but with exception when no match.
        /// </summary>
        /// <param name="targetType"></param>
        /// <param name="name"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public static MethodInfo FindMethodOrThrow(this Type targetType, string name, Type returnType, params Type[] parameterTypes)
        {
            if (name == null)
                return null;

            MethodInfo method = FindMethod(targetType, name, returnType, parameterTypes);
            if (method != null)
                return method;

            throw new ArgumentException(string.Format("No method {0} {1}.{2}({3}) found.", returnType.Name, targetType.FullName, name, string.Join(", ", parameterTypes.Select(p => p != null ? p.Name : "?" ))));
        }

        /// <summary>
        /// Finds a method that (when specialized) can take the given arguments and produce the given return type.
        /// </summary>
        /// <param name="targetType"></param>
        /// <param name="name"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public static MethodInfo FindMethod(this Type targetType, string name, Type returnType, params Type[] parameterTypes)
        {
            IEnumerable<MethodInfo> candidates = targetType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(m => m.Name == name && ((!m.IsStatic && m.GetParameters().Length == parameterTypes.Length) || (m.IsStatic && m.GetParameters().Length == parameterTypes.Length+1)));

            foreach (MethodInfo method in candidates)
            {
                // If incompatible return parameter
                if (returnType != null && !method.ReturnType.IsGenericParameter && !returnType.IsAssignableFrom(method.ReturnType))
                    continue;

                Type[] methodParameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
                List<Type> matchParameters = parameterTypes.ToList();
                if (method.IsStatic)
                    matchParameters.Insert(0, targetType);

                int i = 0;
                for (i = 0; i < matchParameters.Count; i++)
                    if (matchParameters[i] != null && !methodParameters[i].IsGenericParameter && !methodParameters[i].IsAssignableFrom(matchParameters[i]))
                        break;

                if (i == matchParameters.Count)
                    return method;
            }

            return null;
        }

        /// <summary>
        /// Specializes a generic method definition to match given return type and parameter types.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="targetType"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public static MethodInfo Specialize(this MethodInfo method, Type targetType, Type returnType, params Type[] parameterTypes)
        {
            List<Type> types = parameterTypes.ToList();
            if (method.IsStatic)
                types.Insert(0, targetType);

            if (method.IsGenericMethodDefinition)
            {
                List<Type> generics = new List<Type>();
                for (int i = 0; i < types.Count; i++)
                {
                    if (method.GetParameters()[i].ParameterType.IsGenericParameter)
                        generics.Add(types[i]);
                }

                method = method.MakeGenericMethod(generics.ToArray());
            }

            // Check return type
            if (!returnType.IsAssignableFrom(method.ReturnType))
                throw new ArgumentException(string.Format("Method {0}.{1} must return {2}.", method.DeclaringType.FullName, method.Name, returnType.Name));

            for (int i=0; i<types.Count; i++)
            {
                Type ptype = method.GetParameters()[i].ParameterType;
                if (ptype.IsSubclassOf(typeof(Delegate)))
                    continue;

                if (!ptype.IsAssignableFrom(types[i]))
                    throw new ArgumentException(string.Format("Parameter {3} of method {0}.{1} must be of type {2}.", method.DeclaringType.FullName, method.Name, types[i].Name, i));
            }

            return method;
        }
    }
}
