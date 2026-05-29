using System;
using System.Runtime.InteropServices;

namespace Sharpy
{
    /// <summary>
    /// Platform identification, similar to Python's <c>platform</c> module.
    /// </summary>
    public static partial class PlatformModule
    {
        /// <summary>
        /// Return the system/OS name, e.g. "Windows", "Linux", "Darwin".
        /// </summary>
        public static string System()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "Darwin";
            }
            else
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Return the system's release, e.g. OS version string.
        /// </summary>
        public static string Release()
        {
            return RuntimeInformation.OSDescription;
        }

        /// <summary>
        /// Return the machine type, e.g. "x86_64", "arm64", "AMD64".
        /// </summary>
        public static string Machine()
        {
            var arch = RuntimeInformation.OSArchitecture;
            switch (arch)
            {
                case Architecture.X64:
                    return "x86_64";
                case Architecture.X86:
                    return "x86";
                case Architecture.Arm:
                    return "armv7l";
                case Architecture.Arm64:
                    return "arm64";
                default:
                    return arch.ToString();
            }
        }

        /// <summary>
        /// Return the computer's network name (hostname).
        /// </summary>
        public static string Node()
        {
            return Environment.MachineName;
        }

        /// <summary>
        /// Return a single string identifying the underlying platform with as much
        /// useful information as possible.
        /// </summary>
        public static string Platform()
        {
            return System() + "-" + Release() + "-" + Machine();
        }

        /// <summary>
        /// Return the Sharpy compiler version string.
        /// </summary>
        public static string SharpyVersion()
        {
            return "0.1.3";
        }

        /// <summary>
        /// Return the .NET runtime version string.
        /// </summary>
        public static string DotnetVersion()
        {
            return RuntimeInformation.FrameworkDescription;
        }

        /// <summary>
        /// Return the .NET implementation name.
        /// </summary>
        public static string DotnetImplementation()
        {
            return "CoreCLR";
        }
    }
}
