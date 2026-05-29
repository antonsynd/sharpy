using System;
using System.Collections.Generic;
using global::System.Collections.Generic.List<string> = System.Collections.Generic.global::System.Collections.Generic.List<string>;

namespace Sharpy
{
    /// <summary>
    /// Base exception for subprocess-related errors.
    /// Equivalent to Python's <c>subprocess.SubprocessError</c>.
    /// </summary>
    [SharpyModuleType("subprocess")]
    public class SubprocessError : Exception
    {
        /// <summary>Create a SubprocessError with the specified message.</summary>
        public SubprocessError(string message) : base(message)
        {
        }

        /// <summary>Create a SubprocessError with the specified message and inner exception.</summary>
        public SubprocessError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Raised when a process run with check=True returns a non-zero exit code.
    /// Equivalent to Python's <c>subprocess.CalledProcessError</c>.
    /// </summary>
    [SharpyModuleType("subprocess")]
    public class CalledProcessError : SubprocessError
    {
        /// <summary>The exit code of the process.</summary>
        public int Returncode { get; }

        /// <summary>The command that was run.</summary>
        public global::System.Collections.Generic.List<string> Cmd { get; }

        /// <summary>The captured stdout output, or null if not captured.</summary>
        public string? Output { get; }

        /// <summary>The captured stderr output, or null if not captured.</summary>
        public string? Stderr { get; }

        /// <summary>Create a CalledProcessError.</summary>
        public CalledProcessError(int returncode, global::System.Collections.Generic.List<string> cmd, string? output = null, string? stderr = null)
            : base("Command '" + string.Join(" ", cmd) + "' returned non-zero exit status " + returncode + ".")
        {
            Returncode = returncode;
            Cmd = cmd;
            Output = output;
            Stderr = stderr;
        }
    }

    /// <summary>
    /// Raised when a process times out.
    /// Equivalent to Python's <c>subprocess.TimeoutExpired</c>.
    /// </summary>
    [SharpyModuleType("subprocess")]
    public class TimeoutExpired : SubprocessError
    {
        /// <summary>The command that was run.</summary>
        public global::System.Collections.Generic.List<string> Cmd { get; }

        /// <summary>The timeout in seconds that was exceeded.</summary>
        public double Timeout { get; }

        /// <summary>The captured stdout output, or null if not captured.</summary>
        public string? Output { get; }

        /// <summary>The captured stderr output, or null if not captured.</summary>
        public string? Stderr { get; }

        /// <summary>Create a TimeoutExpired exception.</summary>
        public TimeoutExpired(global::System.Collections.Generic.List<string> cmd, double timeout, string? output = null, string? stderr = null)
            : base("Command '" + string.Join(" ", cmd) + "' timed out after " + timeout + " seconds.")
        {
            Cmd = cmd;
            Timeout = timeout;
            Output = output;
            Stderr = stderr;
        }
    }
}
