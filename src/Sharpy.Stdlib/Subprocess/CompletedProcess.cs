using System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("subprocess", "CompletedProcess")]
    public sealed class CompletedProcess
    {
        public System.Collections.Generic.List<string> Args { get; }
        public int Returncode { get; }
        public string? Stdout { get; }
        public string? Stderr { get; }

        public CompletedProcess(System.Collections.Generic.List<string> args, int returncode, string? stdout = null, string? stderr = null)
        {
            Args = args;
            Returncode = returncode;
            Stdout = stdout;
            Stderr = stderr;
        }

        public void CheckReturncode()
        {
            if (Returncode != 0)
            {
                throw new CalledProcessError(Returncode, Args, Stdout, Stderr);
            }
        }

        public override string ToString()
        {
            return "CompletedProcess(args=[" + string.Join(", ", Args) + "], returncode=" + Returncode + ")";
        }
    }
}
