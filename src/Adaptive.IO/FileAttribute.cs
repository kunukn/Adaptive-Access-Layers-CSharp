using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adaptive.IO
{
    /// <summary>
    /// Mandatory attribute on FileAccessLayer methods to identify underlying static method on File class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class FileAttribute : Attribute
    {
        /// <summary>
        /// Name of matching static method on System.IO.File class.
        /// </summary>
        public readonly string MethodName;

        /// <summary>
        /// Mandatory attribute on FileAccessLayer methods to identify underlying static method on File class.
        /// </summary>
        /// <param name="methodName">Name of matching static method on System.IO.File class.</param>
        public FileAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
