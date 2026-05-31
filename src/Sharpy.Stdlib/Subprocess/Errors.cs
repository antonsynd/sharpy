using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Base exception for subprocess-related failures.</summary>
    [SharpyModuleType("subprocess", "SubprocessError")]
    public class SubprocessError : Exception
    {
        /// <summary>Initializes the exception with an error message.</summary>
        public SubprocessError(string message) : base(message)
        {
        }

        /// <summary>Initializes the exception with an error message and inner exception.</summary>
        public SubprocessError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>Raised when a process exits with a non-zero status.</summary>
    [SharpyModuleType("subprocess", "CalledProcessError")]
    public class CalledProcessError : SubprocessError
    {
        /// <summary>Gets the process exit status.</summary>
        public int Returncode { get; }

        /// <summary>Gets the command that was run.</summary>
        public List<string> Cmd { get; }

        /// <summary>Gets the captured standard output, if any.</summary>
        public string? Output { get; }

        /// <summary>Gets the captured standard error, if any.</summary>
        public string? Stderr { get; }

        /// <summary>Initializes the exception for a failed process invocation.</summary>
        public CalledProcessError(int returncode, List<string> cmd, string? output = null, string? stderr = null)
            : base("Command '" + string.Join(" ", cmd) + "' returned non-zero exit status " + returncode + ".")
        {
            Returncode = returncode;
            Cmd = cmd;
            Output = output;
            Stderr = stderr;
        }
    }

    /// <summary>Raised when a process exceeds the allowed timeout.</summary>
    [SharpyModuleType("subprocess", "TimeoutExpired")]
    public class TimeoutExpired : SubprocessError
    {
        /// <summary>Gets the command that timed out.</summary>
        public List<string> Cmd { get; }

        /// <summary>Gets the timeout value in seconds.</summary>
        public double Timeout { get; }

        /// <summary>Gets the captured standard output, if any.</summary>
        public string? Output { get; }

        /// <summary>Gets the captured standard error, if any.</summary>
        public string? Stderr { get; }

        /// <summary>Initializes the exception for a timed out process invocation.</summary>
        public TimeoutExpired(List<string> cmd, double timeout, string? output = null, string? stderr = null)
            : base("Command '" + string.Join(" ", cmd) + "' timed out after " + timeout + " seconds.")
        {
            Cmd = cmd;
            Timeout = timeout;
            Output = output;
            Stderr = stderr;
        }
    }
}
