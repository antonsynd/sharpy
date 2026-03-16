using System;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// A named logger that outputs messages at or above a configured level.
    /// Output format: LEVEL:name:message (written to stderr).
    /// </summary>
    [SharpyModuleType("logging")]
    public sealed class Logger
    {
        private readonly string _name;
        private int _level;
        private static readonly object _outputLock = new object();

        /// <summary>
        /// Create a new logger with the specified name.
        /// </summary>
        /// <param name="name">The logger name.</param>
        public Logger(string name)
        {
            _name = name;
            _level = Logging.WARNING;
        }

        /// <summary>
        /// Set the minimum logging level for this logger.
        /// </summary>
        /// <param name="level">The logging level threshold.</param>
        public void SetLevel(int level)
        {
            _level = level;
        }

        /// <summary>Log a message with DEBUG level.</summary>
        /// <param name="msg">The message to log.</param>
        public void Debug(string msg)
        {
            Log(Logging.DEBUG, "DEBUG", msg);
        }

        /// <summary>Log a message with INFO level.</summary>
        /// <param name="msg">The message to log.</param>
        public void Info(string msg)
        {
            Log(Logging.INFO, "INFO", msg);
        }

        /// <summary>Log a message with WARNING level.</summary>
        /// <param name="msg">The message to log.</param>
        public void Warning(string msg)
        {
            Log(Logging.WARNING, "WARNING", msg);
        }

        /// <summary>Log a message with ERROR level.</summary>
        /// <param name="msg">The message to log.</param>
        public void Error(string msg)
        {
            Log(Logging.ERROR, "ERROR", msg);
        }

        /// <summary>Log a message with CRITICAL level.</summary>
        /// <param name="msg">The message to log.</param>
        public void Critical(string msg)
        {
            Log(Logging.CRITICAL, "CRITICAL", msg);
        }

        private void Log(int level, string levelName, string msg)
        {
            if (level < _level)
            {
                return;
            }

            string output = levelName + ":" + _name + ":" + msg;
            lock (_outputLock)
            {
                Console.Error.WriteLine(output);
            }
        }
    }
}
