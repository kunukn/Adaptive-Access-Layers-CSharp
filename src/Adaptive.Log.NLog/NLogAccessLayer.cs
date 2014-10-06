using Adaptive.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Adaptive.Log
{
    /// <summary>
    /// Adaptive layer in front of a NLog logger.
    /// </summary>
    public abstract class NLogAccessLayer
    {
        #region Factory

        /// <summary>
        /// The singleton factory instance
        /// </summary>
        private static readonly AdaptiveFactory<NLogAccessLayer> s_Factory = new AdaptiveFactory<NLogAccessLayer>();

        /// <summary>
        /// Configures factory
        /// </summary>
        static NLogAccessLayer()
        {
            s_Factory.ImplementMethods().UsingSharedExecuter("LogWriteInit", "LogWriteExec").WithSyntaxChecker(LogWriteCheck);
        }

        /// <summary>
        /// Creates/gets runtime type that implements given interface.
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static Type Implement(Type interfaceType)
        {
            return s_Factory.Implement(interfaceType);
        }

        /// <summary>
        /// Creates/gets runtime type that implements given interface type and returns fresh instance of it around given NLog logger instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static T Create<T>(Logger logger)
        {
            return (T)Activator.CreateInstance(s_Factory.Implement(typeof(T)), logger);
        }

        #endregion

        /// <summary>
        /// The underlying NLog logger
        /// </summary>
        private readonly Logger _logger;

        /// <summary>
        /// Creates new instance. Called by derived runtime class ctor.
        /// </summary>
        /// <param name="logger"></param>
        public NLogAccessLayer(Logger logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Translates into NLog severity.
        /// </summary>
        /// <param name="eventType"></param>
        /// <returns></returns>
        private static LogLevel TranslateLogLevel(TraceEventType eventType)
        {
            switch (eventType)
            {
                case TraceEventType.Verbose:
                    return LogLevel.Trace;

                case TraceEventType.Information:
                    return LogLevel.Info;

                case TraceEventType.Warning:
                    return LogLevel.Warn;

                case TraceEventType.Error:
                    return LogLevel.Error;

                case TraceEventType.Critical:
                    return LogLevel.Fatal;

                default:
                    return LogLevel.Debug;
            }
        }

        #region LogWrite implementation

        /// <summary>
        /// Holds info for each method.
        /// </summary>
        protected class LogWriteInfo
        {
            /// <summary>
            /// Id of event entry.
            /// </summary>
            public int Id;

            /// <summary>
            /// Message format.
            /// </summary>
            public string Message;

            /// <summary>
            /// Severity
            /// </summary>
            public LogLevel LogLevel;
        }

        /// <summary>
        /// Checks syntax of log methods.
        /// </summary>
        /// <param name="method"></param>
        protected static void LogWriteCheck(MethodInfo method)
        {
            if (method.ReturnType != typeof(void))
                throw new ArgumentException(string.Format("Method {0} must return void.", method.Name));
        }

        /// <summary>
        /// Called once per method.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        protected LogWriteInfo LogWriteInit(MethodInfo method)
        {
            // Look for optional attribute
            LogWriteAttribute attribute = method.GetCustomAttributes(typeof(LogWriteAttribute), false).OfType<LogWriteAttribute>().FirstOrDefault();

            // If found, use it
            if (attribute != null)
            {
                return new LogWriteInfo()
                {
                    Id = attribute.Id,
                    LogLevel = TranslateLogLevel(attribute.Severity),
                    Message = attribute.Message
                };
            }
            else // Run with default values
            {
                return new LogWriteInfo()
                {
                    LogLevel = LogLevel.Info,
                    Message = string.Format("{0}: {1}", method.Name, string.Join("|", method.GetParameters().Select((p, i) => string.Format("{0}={{{1}}}", p.Name, i))))
                };
            }
        }

        /// <summary>
        /// Called whenever log method is executed on interface.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        protected object LogWriteExec(LogWriteInfo info, object[] parameters)
        {
            // Find way to specify eventid via properties
            LogEventInfo entry = new LogEventInfo(info.LogLevel, _logger.Name, CultureInfo.InvariantCulture, info.Message, parameters);
            entry.Properties.Add("EventID", info.Id);

            _logger.Log(entry);
            return null;
        }

        #endregion
    }
}
