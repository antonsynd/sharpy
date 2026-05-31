using System;
using System.Runtime.InteropServices;

namespace Sharpy
{
    /// <summary>
    /// Access to underlying platform identifying data, equivalent to Python's platform module.
    /// </summary>
    public static partial class PlatformModule
    {
        /// <summary>
        /// Returns the system/OS name, e.g., "Windows", "Linux", "Darwin".
        /// </summary>
        public static string System()
        {
#if NET10_0_OR_GREATER
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }

            if (OperatingSystem.IsLinux())
            {
                return "Linux";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "Darwin";
            }

            return "Unknown";
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "Darwin";
            }

            return "Unknown";
#endif
        }

        /// <summary>
        /// Returns the system's release version, e.g., "10.0.19041".
        /// </summary>
        public static string Release()
        {
            return Environment.OSVersion.Version.ToString();
        }

        /// <summary>
        /// Returns the system's release version description.
        /// </summary>
        public static string Version()
        {
            return RuntimeInformation.OSDescription;
        }

        /// <summary>
        /// Returns the machine type, e.g., "x86_64", "AMD64", "arm64".
        /// Follows Python's platform.machine() conventions per OS.
        /// </summary>
        public static string Machine()
        {
            var arch = RuntimeInformation.OSArchitecture;
            string system = System();

            switch (arch)
            {
                case global::System.Runtime.InteropServices.Architecture.X64:
                    return system == "Windows" ? "AMD64" : "x86_64";
                case global::System.Runtime.InteropServices.Architecture.X86:
                    return "x86";
                case global::System.Runtime.InteropServices.Architecture.Arm64:
                    return "arm64";
                case global::System.Runtime.InteropServices.Architecture.Arm:
                    return "arm";
                default:
                    return arch.ToString().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Returns the computer's network name (hostname).
        /// </summary>
        public static string Node()
        {
            return Environment.MachineName;
        }

        /// <summary>
        /// Returns the (real) processor name or architecture string.
        /// </summary>
        public static string Processor()
        {
            return RuntimeInformation.OSArchitecture.ToString();
        }

        /// <summary>
        /// Returns a single string identifying the underlying platform
        /// with as much useful information as possible.
        /// </summary>
        /// <param name="aliased">If true, use aliased platform names (currently unused).</param>
        /// <param name="terse">If true, return a minimal platform string without version.</param>
        public static string Platform(bool aliased = false, bool terse = false)
        {
            string system = System();
            string machine = Machine();

            if (terse)
            {
                return system + "-" + machine;
            }

            string version = Release();
            return system + "-" + version + "-" + machine;
        }

        /// <summary>
        /// Returns the Sharpy runtime version string.
        /// </summary>
        public static string SharpyVersion()
        {
            var attr = (System.Reflection.AssemblyInformationalVersionAttribute?)
                Attribute.GetCustomAttribute(
                    typeof(PlatformModule).Assembly,
                    typeof(System.Reflection.AssemblyInformationalVersionAttribute));

            if (attr != null)
            {
                return attr.InformationalVersion;
            }

            var assemblyVersion = typeof(PlatformModule).Assembly.GetName().Version;
            return assemblyVersion != null ? assemblyVersion.ToString() : "0.0.0";
        }

        /// <summary>
        /// Returns the .NET runtime version string.
        /// </summary>
        public static string DotnetVersion()
        {
            return Environment.Version.ToString();
        }

        /// <summary>
        /// Returns the name of the .NET implementation (always "CoreCLR" on .NET 5+).
        /// </summary>
        public static string DotnetImplementation()
        {
            return "CoreCLR";
        }

        /// <summary>
        /// Returns the .NET framework description string.
        /// </summary>
        public static string DotnetCompiler()
        {
            return RuntimeInformation.FrameworkDescription;
        }

        /// <summary>
        /// Returns a tuple (bits, linkage) identifying the architecture.
        /// bits is "64bit" or "32bit", linkage is always empty.
        /// </summary>
        public static (string, string) Architecture()
        {
            var arch = RuntimeInformation.OSArchitecture;
            switch (arch)
            {
                case global::System.Runtime.InteropServices.Architecture.X64:
                    return ("64bit", "");
                case global::System.Runtime.InteropServices.Architecture.Arm64:
                    return ("64bit", "");
                case global::System.Runtime.InteropServices.Architecture.Arm:
                    return ("32bit", "");
                case global::System.Runtime.InteropServices.Architecture.X86:
                    return ("32bit", "");
                default:
                    return ("64bit", "");
            }
        }

        /// <summary>
        /// Returns a <see cref="UnameResult"/> containing system identification information.
        /// </summary>
        public static UnameResult Uname()
        {
            return new UnameResult(
                System(),
                Node(),
                Release(),
                Version(),
                Machine());
        }
    }
}
