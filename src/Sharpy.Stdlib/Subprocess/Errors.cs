using System;
using System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("subprocess", "SubprocessError")]
    public class SubprocessError : Exception
    {
        public SubprocessError(string message) : base(message)
        {
        }

        public SubprocessError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    [SharpyModuleType("subprocess", "CalledProcessError")]
    public class CalledProcessError : SubprocessError
    {
        public int Returncode { get; }
        public System.Collections.Generic.List<string> Cmd { get; }
        public string? Output { get; }
        public string? Stderr { get; }

        public CalledProcessError(int returncode, System.Collections.Generic.List<string> cmd, string? output = null, string? stderr = null)
            : base("Command '" + string.Join(" ", cmd) + "' returned non-zero exit status " + returncode + ".")
        {
            Returncode = returncode;
            Cmd = cmd;
            Output = output;
            Stderr = stderr;
        }
    }

    [SharpyModuleType("subprocess", "TimeoutExpired")]
    public class TimeoutExpired : SubprocessError
    {
        public System.Collections.Generic.List<string> Cmd { get; }
        public double Timeout { get; }
        public string? Output { get; }
        public string? Stderr { get; }

        public TimeoutExpired(System.Collections.Generic.List<string> cmd, double timeout, string? output = null, string? stderr = null)
            : base("Command '" + string.Join(" ", cmd) + "' timed out after " + timeout + " seconds.")
        {
            Cmd = cmd;
            Timeout = timeout;
            Output = output;
            Stderr = stderr;
        }
    }
}
