using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Sharpy
{
    public static partial class SubprocessModule
    {
        public const int PIPE = -1;
        public const int STDOUT = -2;
        public const int DEVNULL = -3;

        public static CompletedProcess Run(
            System.Collections.Generic.List<string> args,
            bool captureOutput = false,
            bool text = true,
            bool check = false,
            double? timeout = null,
            string? input = null,
            string? cwd = null,
            Dictionary<string, string>? env = null,
            bool shell = false,
            int stdin = 0,
            int stdout = 0,
            int stderr = 0)
        {
            if (args == null || args.Count == 0)
            {
                throw new ValueError("args must be a non-empty list");
            }

            if (captureOutput)
            {
                stdout = PIPE;
                stderr = PIPE;
            }

            if (input != null)
            {
                stdin = PIPE;
            }

            using (var proc = new Popen(args, stdin: stdin, stdout: stdout, stderr: stderr,
                       shell: shell, cwd: cwd, env: env))
            {
                var (stdoutStr, stderrStr) = proc.Communicate(input, timeout);

                int exitCode = proc.Returncode ?? 0;
                var result = new CompletedProcess(
                    new System.Collections.Generic.List<string>(args),
                    exitCode, stdoutStr, stderrStr);

                if (check && exitCode != 0)
                {
                    throw new CalledProcessError(
                        exitCode,
                        new System.Collections.Generic.List<string>(args),
                        stdoutStr, stderrStr);
                }

                return result;
            }
        }

        public static string CheckOutput(
            System.Collections.Generic.List<string> args,
            bool text = true,
            double? timeout = null,
            string? input = null,
            string? cwd = null,
            Dictionary<string, string>? env = null,
            bool shell = false,
            int stderr = 0)
        {
            var result = Run(args, captureOutput: true, check: true, text: text,
                timeout: timeout, input: input, cwd: cwd, env: env, shell: shell,
                stderr: stderr);
            return result.Stdout ?? "";
        }

        public static int CheckCall(
            System.Collections.Generic.List<string> args,
            double? timeout = null,
            string? cwd = null,
            Dictionary<string, string>? env = null,
            bool shell = false)
        {
            var result = Run(args, check: true, timeout: timeout, cwd: cwd, env: env, shell: shell);
            return result.Returncode;
        }
    }
}
