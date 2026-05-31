using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>Represents the result of a finished subprocess.</summary>
    [SharpyModuleType("subprocess", "CompletedProcess")]
    public sealed class CompletedProcess
    {
        /// <summary>Gets the command arguments used to launch the process.</summary>
        public List<string> Args { get; }

        /// <summary>Gets the process exit status.</summary>
        public int Returncode { get; }

        /// <summary>Gets the captured standard output, if any.</summary>
        public string? Stdout { get; }

        /// <summary>Gets the captured standard error, if any.</summary>
        public string? Stderr { get; }

        /// <summary>Initializes a completed subprocess result.</summary>
        public CompletedProcess(List<string> args, int returncode, string? stdout = null, string? stderr = null)
        {
            Args = args;
            Returncode = returncode;
            Stdout = stdout;
            Stderr = stderr;
        }

        /// <summary>Raises CalledProcessError if the process exited with a non-zero status.</summary>
        public void CheckReturncode()
        {
            if (Returncode != 0)
            {
                throw new CalledProcessError(Returncode, Args, Stdout, Stderr);
            }
        }

        /// <summary>Returns the Python-style string representation of the completed process.</summary>
        public override string ToString()
        {
            return "CompletedProcess(args=[" + string.Join(", ", Args) + "], returncode=" + Returncode + ")";
        }
    }
}
