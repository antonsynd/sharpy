using System.Collections.Generic;


namespace Sharpy
{
    /// <summary>
    /// Represents the result of a completed process.
    /// Equivalent to Python's <c>subprocess.CompletedProcess</c>.
    /// </summary>
    [SharpyModuleType("subprocess")]
    public class CompletedProcess
    {
        /// <summary>The arguments used to launch the process.</summary>
        public global::System.Collections.Generic.List<string> Args { get; }

        /// <summary>The exit code of the process.</summary>
        public int Returncode { get; }

        /// <summary>The captured stdout output, or null if not captured.</summary>
        public string? Stdout { get; }

        /// <summary>The captured stderr output, or null if not captured.</summary>
        public string? Stderr { get; }

        /// <summary>Create a CompletedProcess instance.</summary>
        public CompletedProcess(global::System.Collections.Generic.List<string> args, int returncode, string? stdout = null, string? stderr = null)
        {
            Args = args;
            Returncode = returncode;
            Stdout = stdout;
            Stderr = stderr;
        }

        /// <summary>
        /// Check the return code and raise CalledProcessError if non-zero.
        /// </summary>
        public void CheckReturncode()
        {
            if (Returncode != 0)
            {
                throw new CalledProcessError(Returncode, Args, Stdout, Stderr);
            }
        }
    }
}
