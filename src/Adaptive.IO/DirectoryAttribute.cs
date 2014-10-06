using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adaptive.IO
{
    /// <summary>
    /// Marks a method as mapped to static method on System.IO.Directory class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DirectoryAttribute : Attribute
    {
        /// <summary>
        /// Name of matching static method on System.IO.File class.
        /// </summary>
        public readonly string MethodName;

        /// <summary>
        /// Mandatory attribute on FileAccessLayer methods to identify underlying static method on File class.
        /// </summary>
        /// <param name="methodName">Name of matching static method on System.IO.File class.</param>
        public DirectoryAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
