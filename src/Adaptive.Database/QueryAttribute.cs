using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Adaptive.Database
{
    /// <summary>
    /// Base class for all attributes mapping a method to a DB command.
    /// </summary>
    public abstract class CommandAttribute : Attribute
    {
        /// <summary>
        /// The query string.
        /// </summary>
        public string CommandText { get; protected set; }

        /// <summary>
        /// Type of db command. Default is Text.
        /// </summary>
        public CommandType CommandType { get; protected set; }

        /// <summary>
        /// Command timeout in seconds or 0 to use default.
        /// </summary>
        public int CommandTimeout { get; set; }

        /// <summary>
        /// Creates new
        /// </summary>
        /// <param name="commandText"></param>
        /// <param name="commandType"></param>
        public CommandAttribute(string commandText, CommandType commandType)
        {
            CommandType = commandType;
            CommandText = commandText;
        }
    }

    /// <summary>
    /// Marks method on Database Access Layer as mapped to a text query.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple=false)]
    public class QueryAttribute : CommandAttribute
    {
        /// <summary>
        /// True to read all result sets into memory, false to read lazy.
        /// </summary>
        public bool Buffered { get; set; }

        /// <summary>
        /// Marks method on Database Access Layer as mapped to a text query.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="commandType"></param>
        public QueryAttribute(string query, CommandType commandType = CommandType.Text)
            : base(query, commandType)
        {
            Buffered = true;
        }
    }

    /// <summary>
    /// Marks a method Database Access Layer as mapped to a non-query db command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class NonQueryAttribute : CommandAttribute
    {
        /// <summary>
        /// Marks a method Database Access Layer as mapped to a non-query db command.
        /// </summary>
        /// <param name="commandText"></param>
        /// <param name="commandType"></param>
        public NonQueryAttribute(string commandText, CommandType commandType = CommandType.Text)
            : base(commandText, commandType)
        {
        }
    }
}
