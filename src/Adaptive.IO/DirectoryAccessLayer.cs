using Adaptive.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Adaptive.IO
{
    /// <summary>
    /// Access layer around static methods on System.IO.Directory class.
    /// </summary>
    public abstract class DirectoryAccessLayer
    {
        private static readonly AdaptiveFactory<DirectoryAccessLayer> s_Factory = new AdaptiveFactory<DirectoryAccessLayer>();

        static DirectoryAccessLayer()
        {
            // Implement all methods by calling mapped static method on File class.
            s_Factory.ImplementMethods().UsingTarget(null, GetDirectoryMethod);
        }

        private static MethodInfo GetDirectoryMethod(MethodInfo interfaceMethod)
        {
            DirectoryAttribute attr = interfaceMethod.GetCustomAttributes(typeof(DirectoryAttribute), false).Cast<DirectoryAttribute>().SingleOrDefault();
            string methodName = attr != null ? attr.MethodName : interfaceMethod.Name;
            return typeof(Directory).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray(), null);
        }

        /// <summary>
        /// Creates new instance
        /// </summary>
        public DirectoryAccessLayer()
        {
        }

        /// <summary>
        /// Implements type that implements given interface type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Type Implement<T>()
        {
            return s_Factory.Implement<T>();
        }

        /// <summary>
        /// Implements and creates instance of type that implements given interface.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Create<T>() where T : class
        {
            return (T)Activator.CreateInstance(Implement<T>());
        }
    }
}
