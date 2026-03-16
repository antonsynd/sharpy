using System;
using System.Collections.Concurrent;

namespace Sharpy
{
    /// <summary>
    /// Logging facility similar to Python's logging module.
    /// Provides module-level convenience functions and named loggers.
    /// </summary>
    public static partial class LoggingModule
    {
        /// <summary>Detailed information, typically of interest only when diagnosing problems.</summary>
        public const int DEBUG = 10;

        /// <summary>Confirmation that things are working as expected.</summary>
        public const int INFO = 20;

        /// <summary>An indication that something unexpected happened.</summary>
        public const int WARNING = 30;

        /// <summary>Due to a more serious problem, the software has not been able to perform some function.</summary>
        public const int ERROR = 40;

        /// <summary>A serious error, indicating that the program itself may be unable to continue running.</summary>
        public const int CRITICAL = 50;

        private static readonly ConcurrentDictionary<string, Logger> _loggers =
            new ConcurrentDictionary<string, Logger>();

        /// <summary>
        /// Return a logger with the specified name, creating it if necessary.
        /// </summary>
        /// <param name="name">The logger name. Defaults to "root".</param>
        /// <returns>A <see cref="Logger"/> instance with the given name.</returns>
        public static Logger GetLogger(string name = "root")
        {
            return _loggers.GetOrAdd(name, n => new Logger(n));
        }

        /// <summary>
        /// Do basic configuration for the logging system by setting the root logger level.
        /// </summary>
        /// <param name="level">The logging level threshold.</param>
        public static void BasicConfig(int level = WARNING)
        {
            GetLogger("root").SetLevel(level);
        }

        /// <summary>Log a message with DEBUG level on the root logger.</summary>
        /// <param name="msg">The message to log.</param>
        public static void Debug(string msg)
        {
            GetLogger("root").Debug(msg);
        }

        /// <summary>Log a message with INFO level on the root logger.</summary>
        /// <param name="msg">The message to log.</param>
        public static void Info(string msg)
        {
            GetLogger("root").Info(msg);
        }

        /// <summary>Log a message with WARNING level on the root logger.</summary>
        /// <param name="msg">The message to log.</param>
        public static void Warning(string msg)
        {
            GetLogger("root").Warning(msg);
        }

        /// <summary>Log a message with ERROR level on the root logger.</summary>
        /// <param name="msg">The message to log.</param>
        public static void Error(string msg)
        {
            GetLogger("root").Error(msg);
        }

        /// <summary>Log a message with CRITICAL level on the root logger.</summary>
        /// <param name="msg">The message to log.</param>
        public static void Critical(string msg)
        {
            GetLogger("root").Critical(msg);
        }
    }
}
