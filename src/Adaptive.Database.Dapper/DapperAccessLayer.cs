using Adaptive.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using Dapper;
using System.Reflection.Emit;
using System.Collections;

namespace Adaptive.Database
{
    /// <summary>
    /// Base class for Dapper Access Layer runtime types.
    /// </summary>
    public abstract class DapperAccessLayer
    {
        #region Factory

        /// <summary>
        /// The access layer factory that creates classes derived from this class.
        /// </summary>
        private static readonly AdaptiveFactory<DapperAccessLayer> s_Factory = new AdaptiveFactory<DapperAccessLayer>();

        /// <summary>
        /// The factory that we use to create DTO types to hold method parameters for Dapper Query call.
        /// </summary>
        private static readonly AdaptiveFactory<object> s_ParameterFactory = new AdaptiveFactory<object>();

        /// <summary>
        /// Configures the factory instance
        /// </summary>
        static DapperAccessLayer()
        {
            s_Factory.ImplementAttributedMethods<QueryAttribute>()
                .UsingSharedExecuter(InitQuery, ExecQuery)
                .WithSyntaxChecker(CheckQuery);

            s_Factory.ImplementAttributedMethods<NonQueryAttribute>()
                .UsingSharedExecuter(InitNonQuery, ExecNonQuery)
                .WithSyntaxChecker(CheckNonQuery);

            s_ParameterFactory.ImplementProperties().UsingBackingField();
        }

        /// <summary>
        /// Gets/sets the module runtime types are implemented in.
        /// </summary>
        public static ModuleBuilder Module
        {
            get { return s_Factory.Module; }
            set { s_Factory.Module = s_ParameterFactory.Module = value; }
        }

        /// <summary>
        /// Implements given Dapper Access Layer interface type.
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static Type Implement(Type interfaceType)
        {
            return s_Factory.Implement(interfaceType);
        }

        /// <summary>
        /// Creates (and implements on first call for each T) instance of runtime type that implements given T interface.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection">Connection that is used for all calls.</param>
        /// <returns></returns>
        public static T Create<T>(IDbConnection connection)
        {
            return Create<T>(() => connection);
        }

        /// <summary>
        /// Connection factory that is called prior to each call. If returned connection is open, it will be left open. If it is closed,
        /// it will opened and closed again.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connectionFactory"></param>
        /// <returns></returns>
        public static T Create<T>(Func<IDbConnection> connectionFactory)
        {
            return (T)Activator.CreateInstance(Implement(typeof(T)), connectionFactory);
        }

        #endregion

        /// <summary>
        /// The factory that produces db connection for each Query call.
        /// </summary>
        private readonly Func<IDbConnection> _connectionFactory;

        /// <summary>
        /// Creates instance using given connection factory.
        /// </summary>
        /// <param name="connectionFactory"></param>
        public DapperAccessLayer(Func<IDbConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>
        /// Holds info about a reflected method
        /// </summary>
        protected class CommandInfo
        {
            public CommandAttribute Attribute;
            public Func<DapperAccessLayer, CommandInfo, object[], object> ExecQuery;
            public Type ParameterType;
            public PropertyInfo[] ParameterTypeProperties;
            public int? TimeoutSeconds;
        }

        /// <summary>
        /// We need this to easily make generic method
        /// </summary>
        private static readonly MethodInfo s_ExecCommandMethod = typeof(DapperAccessLayer).GetMethod("ExecQueryGeneric", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Returns true if .NET type corresponds to a SQL primitive (column type)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsDatabaseType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type == typeof(string)
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(byte[])
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid);
        }

        #region Command implementation

        private static void CheckCommand(MethodInfo method)
        {
            // Check either single DTO param or full list as simple types
            int simpleCount = method.GetParameters().Where(p => IsDatabaseType(p.ParameterType)).Count();
            if (simpleCount > 0 && simpleCount != method.GetParameters().Length)
                throw new ArgumentException(string.Format("Method {0}.{1} must take either a single class parameter holding all query/procedure parameters or the full set of parameters as simple types. A mix is not allowed.", method.DeclaringType.FullName, method.Name));
        }

        private static CommandInfo InitCommand(DapperAccessLayer layer, MethodInfo method)
        {
            CommandAttribute attr = method.GetCustomAttributes(typeof(CommandAttribute), false).Cast<CommandAttribute>().Single();

            CommandInfo info = new CommandInfo()
            {
                Attribute = attr,
                TimeoutSeconds = attr.CommandTimeout > 0 ? attr.CommandTimeout : (int?)null
            };

            // Find all method parameters that shall be wrapped by runtime DTO type
            PropertyDefinition[] properties = method.GetParameters()
                .Where(p => IsDatabaseType(p.ParameterType))
                .Select(p => new PropertyDefinition(p.Name, p.ParameterType))
                .ToArray();

            // If any, create runtime dto type
            if (properties.Length > 0)
            {
                // First create an interface definining the properties
                string interfaceTypeName = method.DeclaringType.FullName.Replace('+', '_') + "." + method.Name + "_" + method.GetHashCode().ToString();
                Type interfaceType = s_Factory.Module.Assembly.GetType(interfaceTypeName);
                if (interfaceType == null)
                    interfaceType = s_Factory.Module.DefineInterface(interfaceTypeName, null, properties);

                // Then implement that by runtime dto type
                info.ParameterType = s_ParameterFactory.Implement(interfaceType);
                info.ParameterTypeProperties = info.ParameterType.GetProperties();
            }

            return info;
        }

        private static object ExecCommand(DapperAccessLayer layer, CommandInfo info, object[] parameters, Func<IDbConnection, object, object> command)
        {
            // Make sure we have an open connection
            IDbConnection con = layer._connectionFactory();
            bool close = con.State != ConnectionState.Open;
            if (close)
                con.Open();

            try
            {
                // Default is to use DTO provided as single param to interface method (the Dapper style)
                object parameter = parameters.FirstOrDefault();

                // If we're using runtime dto type, create instance of it and set properties from method parameters
                if (info.ParameterType != null)
                {
                    parameter = Activator.CreateInstance(info.ParameterType);
                    for (int i = 0; i < info.ParameterTypeProperties.Length; i++)
                        info.ParameterTypeProperties[i].SetValue(parameter, parameters[i], null);
                }

                return command(con, parameter);
            }
            finally
            {
                if (close)
                    con.Close();
            }
        }

        #endregion

        #region Query implementation

        /// <summary>
        /// Checks signature of method.
        /// </summary>
        /// <param name="method"></param>
        private static void CheckQuery(MethodInfo method)
        {
            CheckCommand(method);

            // Check return type
            if (!typeof(IEnumerable).IsAssignableFrom(method.ReturnType))
                throw new ArgumentException(string.Format("Method {0}.{1} must return an IEnumerable<T> where T is the row type.", method.DeclaringType.FullName, method.Name));
        }

        /// <summary>
        /// Called once for each interface method when runtime class instance is created.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        protected static CommandInfo InitQuery(DapperAccessLayer layer, MethodInfo method)
        {
            CommandInfo info = InitCommand(layer, method);

            // Extract row type from signature and create specialized execute method
            Type rowType = method.ReturnType.GetGenericArguments().Single();
            MethodInfo execMethod = s_ExecCommandMethod.MakeGenericMethod(rowType);
            info.ExecQuery = (Func<DapperAccessLayer, CommandInfo, object[], object>)Delegate.CreateDelegate(typeof(Func<DapperAccessLayer, CommandInfo, object[], object>), execMethod);

            return info;
        }

        /// <summary>
        /// Called whenever interface method is executed.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="info"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        protected static object ExecQuery(DapperAccessLayer layer, CommandInfo info, object[] parameters)
        {
            // Execute in specialized generic method to be able to call Dapper query
            return info.ExecQuery(layer, info, parameters);
        }

        private static object ExecQueryGeneric<T>(DapperAccessLayer layer, CommandInfo info, object[] parameters)
        {
            // See if we shall read lazy or not
            bool buffered = ((QueryAttribute)info.Attribute).Buffered;

            // Define how to execute on open connection
            Func<IDbConnection, object, object> exec = (connection, parameter) =>
                {
                    // Let Dapper do the hard work
                    return connection.Query<T>(info.Attribute.CommandText, parameter, buffered: buffered, commandType: info.Attribute.CommandType, commandTimeout: info.TimeoutSeconds);
                };

            return ExecCommand(layer, info, parameters, exec);
        }

        #endregion

        #region NonQuery implementation

        private static void CheckNonQuery(MethodInfo method)
        {
            CheckCommand(method);

            // Can be int or void meaning nonquery
            if (typeof(int) != method.ReturnType && typeof(void) != method.ReturnType)
                throw new ArgumentException(string.Format("Return type must be void or int for [NonQuery] method {0}.{1}.", method.DeclaringType.FullName, method.Name));
        }

        protected static CommandInfo InitNonQuery(DapperAccessLayer layer, MethodInfo method)
        {
            return InitCommand(layer, method);
        }

        protected static object ExecNonQuery(DapperAccessLayer layer, CommandInfo info, object[] parameters)
        {
            // Define how to execute on open connection
            Func<IDbConnection, object, object> exec = (connection, parameter) =>
            {
                // Let Dapper do the hard work
                return connection.Execute(info.Attribute.CommandText, parameter, null, info.TimeoutSeconds, info.Attribute.CommandType);
            };

            return ExecCommand(layer, info, parameters, exec);
        }

        #endregion
    }
}
