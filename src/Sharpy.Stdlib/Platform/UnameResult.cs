using System;

namespace Sharpy
{
    /// <summary>
    /// Represents the result of a platform.uname() call.
    /// Mirrors Python's platform.uname_result named tuple.
    /// </summary>
    [SharpyModuleType("platform", "uname_result")]
    public sealed class UnameResult
    {
        /// <summary>The operating system name (e.g., "Windows", "Linux", "Darwin").</summary>
        public string System { get; }

        /// <summary>The network name of the machine (hostname).</summary>
        public string Node { get; }

        /// <summary>The operating system release version.</summary>
        public string Release { get; }

        /// <summary>The operating system version description.</summary>
        public string Version { get; }

        /// <summary>The hardware machine identifier (e.g., "x86_64", "arm64").</summary>
        public string Machine { get; }

        /// <summary>Create a new UnameResult with the specified system information.</summary>
        public UnameResult(string system, string node, string release, string version, string machine)
        {
            System = system ?? throw new ArgumentNullException(nameof(system));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Release = release ?? throw new ArgumentNullException(nameof(release));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Machine = machine ?? throw new ArgumentNullException(nameof(machine));
        }

        /// <summary>Returns a string representation matching Python's uname_result format.</summary>
        public override string ToString()
        {
            return "uname_result(system='" + System + "', node='" + Node +
                   "', release='" + Release + "', version='" + Version +
                   "', machine='" + Machine + "')";
        }
    }
}
