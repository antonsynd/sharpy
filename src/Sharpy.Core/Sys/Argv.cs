using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Sharpy
{
    /// <summary>Provides access to system-specific parameters and functions, similar to Python's sys module.</summary>
    public sealed partial class Sys
    {
        private static readonly string[] _argv = Environment.GetCommandLineArgs();
        private static readonly string _platform = GetPlatform();
        private static readonly string[] _path = new[] { Environment.CurrentDirectory };

        /// <summary>
        /// The list of command line arguments passed to the program.
        /// argv[0] is the program name (or empty string).
        /// </summary>
        public static string[] Argv => (string[])_argv.Clone();

        /// <summary>
        /// Exit the program with the given status code.
        /// </summary>
        /// <param name="code">The exit code (default is 0).</param>
        /// <example>
        /// <code>
        /// sys.exit()     # exit with code 0
        /// sys.exit(1)    # exit with code 1
        /// </code>
        /// </example>
        public static void Exit(int code = 0)
        {
            Environment.Exit(code);
        }

        /// <summary>
        /// A string containing the version number of the Python interpreter.
        /// This is a simplified version for Sharpy.
        /// </summary>
        public static string Version => "Sharpy 0.1.0 (Python-like for .NET)";

        /// <summary>
        /// This string contains a platform identifier.
        /// </summary>
        public static string Platform => _platform;

        private static string GetPlatform()
        {
            // Polyfill for OperatingSystem.IsWindows/IsLinux/IsMacOS (not available in netstandard)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win32";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "darwin";
            }
            else
            {
                return "unknown";
            }
        }

        /// <summary>
        /// The standard input stream.
        /// </summary>
        public static TextReader Stdin => Console.In;

        /// <summary>
        /// The absolute path of the executable binary for the Python interpreter.
        /// In Sharpy, this returns the path to the current executable.
        /// </summary>
        public static string Executable => GetExecutablePath();

        private static string GetExecutablePath()
        {
            // Polyfill for Environment.ProcessPath (not available in netstandard)
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                return args[0];
            }
            return "";
        }

        /// <summary>
        /// A list of strings that specifies the search path for modules.
        /// In Sharpy, this is simplified to just return the current directory.
        /// </summary>
        public static string[] Path => (string[])_path.Clone();

        /// <summary>
        /// An integer giving the maximum value a variable of type int can take.
        /// Equivalent to Python's sys.maxsize.
        /// </summary>
        public static int Maxsize => int.MaxValue;

        /// <summary>
        /// Return the size of an object in bytes. Best-effort estimate.
        /// Returns -1 if the size cannot be determined.
        /// </summary>
        public static int Getsizeof(object? obj)
        {
            if (obj == null)
            {
                return 0;
            }

            var type = obj.GetType();

            if (type.IsValueType)
            {
                try
                {
                    return System.Runtime.InteropServices.Marshal.SizeOf(type);
                }
                catch
                {
                    return -1;
                }
            }

            return -1;
        }
    }
}
