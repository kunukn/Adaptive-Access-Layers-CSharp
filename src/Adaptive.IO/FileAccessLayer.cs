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
    /// Access layer around static methods on System.IO.File class.
    /// </summary>
    public abstract class FileAccessLayer
    {
        private static readonly AdaptiveFactory<FileAccessLayer> s_Factory = new AdaptiveFactory<FileAccessLayer>();

        static FileAccessLayer()
        {
            // Implement all methods by calling mapped static method on File class.
            s_Factory.ImplementMethods().UsingTarget(null, GetFileMethod);
        }

        private static MethodInfo GetFileMethod(MethodInfo interfaceMethod)
        {
            FileAttribute attr = interfaceMethod.GetCustomAttributes(typeof(FileAttribute), false).Cast<FileAttribute>().SingleOrDefault();
            string methodName = attr != null ? attr.MethodName : interfaceMethod.Name;
            return typeof(File).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray(), null);
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public FileAccessLayer()
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
