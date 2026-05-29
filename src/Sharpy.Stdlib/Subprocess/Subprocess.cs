using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using global::System.Collections.Generic.List<string> = System.Collections.Generic.global::System.Collections.Generic.List<string>;

namespace Sharpy
{
    public static partial class SubprocessModule
    {
        /// <summary>Special value to indicate that a pipe should be created.</summary>
        public const int PIPE = -1;

        /// <summary>Special value to indicate that output should be discarded.</summary>
        public const int DEVNULL = -2;

        /// <summary>Special value to indicate stderr should be merged into stdout.</summary>
        public const int STDOUT = -3;

        /// <summary>
        /// Run command with arguments and return a CompletedProcess instance.
        /// This is the recommended approach to invoking subprocesses.
        /// </summary>
        /// <param name="args">Command and arguments as a list of strings.</param>
        /// <param name="captureOutput">If true, capture stdout and stderr.</param>
        /// <param name="text">If true, decode output as UTF-8 text (always true in Sharpy).</param>
        /// <param name="check">If true, raise CalledProcessError on non-zero exit.</param>
        /// <param name="timeout">Timeout in seconds, or null for no timeout.</param>
        /// <param name="input">Input to send to stdin, or null.</param>
        /// <param name="cwd">Working directory for the process, or null for current directory.</param>
        /// <param name="env">Environment variables for the process, or null to inherit.</param>
        /// <param name="shell">If true, execute command through the system shell.</param>
        /// <returns>A CompletedProcess instance.</returns>
        public static CompletedProcess Run(
            global::System.Collections.Generic.List<string> args,
            bool captureOutput = false,
            bool text = false,
            bool check = false,
            double? timeout = null,
            string? input = null,
            string? cwd = null,
            Dictionary<string, string>? env = null,
            bool shell = false)
        {
            if (args == null || args.Count == 0)
            {
                throw new ValueError("args must be a non-empty list");
            }

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            if (shell)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/c " + string.Join(" ", args);
                }
                else
                {
                    startInfo.FileName = "/bin/sh";
                    startInfo.Arguments = "-c \"" + string.Join(" ", args).Replace("\"", "\\\"") + "\"";
                }
            }
            else
            {
                startInfo.FileName = args[0];
                if (args.Count > 1)
                {
                    var sb = new StringBuilder();
                    for (int i = 1; i < args.Count; i++)
                    {
                        if (i > 1) sb.Append(' ');
                        string arg = args[i];
                        if (arg.Contains(' ') || arg.Contains('"'))
                        {
                            sb.Append('"');
                            sb.Append(arg.Replace("\"", "\\\""));
                            sb.Append('"');
                        }
                        else
                        {
                            sb.Append(arg);
                        }
                    }

                    startInfo.Arguments = sb.ToString();
                }
            }

            startInfo.RedirectStandardInput = (input != null);
            startInfo.RedirectStandardOutput = captureOutput;
            startInfo.RedirectStandardError = captureOutput;

            if (cwd != null)
            {
                startInfo.WorkingDirectory = cwd;
            }

            if (env != null)
            {
                startInfo.Environment.Clear();
                foreach (var kvp in env)
                {
                    startInfo.Environment[kvp.Key] = kvp.Value;
                }
            }

            Process process;
            try
            {
                process = new Process();
                process.StartInfo = startInfo;
                process.Start();
            }
            catch (Exception ex)
            {
                throw new OSError("Failed to start process '" + args[0] + "': " + ex.Message, ex);
            }

            try
            {
                if (input != null)
                {
                    process.StandardInput.Write(input);
                    process.StandardInput.Close();
                }

                string? stdoutResult = null;
                string? stderrResult = null;

                if (captureOutput)
                {
                    stdoutResult = process.StandardOutput.ReadToEnd();
                    stderrResult = process.StandardError.ReadToEnd();
                }

                if (timeout.HasValue)
                {
                    int timeoutMs = (int)(timeout.Value * 1000);
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                            // Process already exited
                        }

                        throw new TimeoutExpired(new global::System.Collections.Generic.List<string>(args), timeout.Value, stdoutResult, stderrResult);
                    }
                }
                else
                {
                    process.WaitForExit();
                }

                int exitCode = process.ExitCode;
                var result = new CompletedProcess(new global::System.Collections.Generic.List<string>(args), exitCode, stdoutResult, stderrResult);

                if (check && exitCode != 0)
                {
                    throw new CalledProcessError(exitCode, new global::System.Collections.Generic.List<string>(args), stdoutResult, stderrResult);
                }

                return result;
            }
            finally
            {
                process.Dispose();
            }
        }

        /// <summary>
        /// Run command with arguments and return its output.
        /// Raises CalledProcessError on non-zero exit.
        /// </summary>
        /// <param name="args">Command and arguments.</param>
        /// <param name="text">If true, decode output as UTF-8 text.</param>
        /// <param name="timeout">Timeout in seconds, or null.</param>
        /// <param name="input">Input to send to stdin, or null.</param>
        /// <param name="cwd">Working directory, or null.</param>
        /// <param name="env">Environment variables, or null.</param>
        /// <param name="shell">If true, execute through the system shell.</param>
        /// <returns>The stdout output of the command.</returns>
        public static string CheckOutput(
            global::System.Collections.Generic.List<string> args,
            bool text = false,
            double? timeout = null,
            string? input = null,
            string? cwd = null,
            Dictionary<string, string>? env = null,
            bool shell = false)
        {
            var result = Run(args, captureOutput: true, check: true, timeout: timeout,
                input: input, cwd: cwd, env: env, shell: shell);
            return result.Stdout ?? "";
        }

        /// <summary>
        /// Run command with arguments and check the return code.
        /// Raises CalledProcessError on non-zero exit.
        /// </summary>
        /// <param name="args">Command and arguments.</param>
        /// <param name="timeout">Timeout in seconds, or null.</param>
        /// <param name="cwd">Working directory, or null.</param>
        /// <param name="env">Environment variables, or null.</param>
        /// <param name="shell">If true, execute through the system shell.</param>
        /// <returns>The exit code (always 0 if no exception).</returns>
        public static int CheckCall(
            global::System.Collections.Generic.List<string> args,
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
