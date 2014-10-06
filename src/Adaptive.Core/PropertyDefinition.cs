using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adaptive.Core
{
    /// <summary>
    /// Represents the definition of an interface property.
    /// </summary>
    public class PropertyDefinition
    {
        /// <summary>
        /// Property name
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Property type
        /// </summary>
        public readonly Type Type;

        /// <summary>
        /// True if property has a getter.
        /// </summary>
        public readonly bool CanRead;

        /// <summary>
        /// True if property has a setter
        /// </summary>
        public readonly bool CanWrite;

        /// <summary>
        /// Creates new property defintion.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="canRead"></param>
        /// <param name="canWrite"></param>
        public PropertyDefinition(string name, Type type, bool canRead = true, bool canWrite = true)
        {
            Name = name;
            Type = type;
            CanRead = canRead;
            CanWrite = canWrite;
        }
    }
}
