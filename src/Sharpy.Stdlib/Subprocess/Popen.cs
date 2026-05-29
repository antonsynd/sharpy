using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using global::System.Collections.Generic.List<string> = System.Collections.Generic.global::System.Collections.Generic.List<string>;

namespace Sharpy
{
    /// <summary>
    /// Flexible process execution — spawn, interact with pipes, obtain status.
    /// Equivalent to Python's <c>subprocess.Popen</c>.
    /// </summary>
    [SharpyModuleType("subprocess")]
    public class Popen : IDisposable
    {
        private readonly Process _process;
        private readonly global::System.Collections.Generic.List<string> _args;
        private bool _disposed;

        /// <summary>The underlying process ID.</summary>
        public int Pid => _process.Id;

        /// <summary>The exit code of the process, or null if the process hasn't exited.</summary>
        public int? Returncode
        {
            get
            {
                if (_process.HasExited)
                {
                    return _process.ExitCode;
                }

                return null;
            }
        }

        /// <summary>The arguments used to launch the process.</summary>
        public global::System.Collections.Generic.List<string> Args => _args;

        /// <summary>The stdin stream, or null if not piped.</summary>
        public System.IO.StreamWriter? Stdin => _process.StartInfo.RedirectStandardInput ? _process.StandardInput : null;

        /// <summary>The stdout stream, or null if not piped.</summary>
        public System.IO.StreamReader? Stdout => _process.StartInfo.RedirectStandardOutput ? _process.StandardOutput : null;

        /// <summary>The stderr stream, or null if not piped.</summary>
        public System.IO.StreamReader? Stderr => _process.StartInfo.RedirectStandardError ? _process.StandardError : null;

        /// <summary>Create and start a new process.</summary>
        public Popen(
            global::System.Collections.Generic.List<string> args,
            int stdin = 0,
            int stdout = 0,
            int stderr = 0,
            bool shell = false,
            string? cwd = null,
            Dictionary<string, string>? env = null)
        {
            if (args == null || args.Count == 0)
            {
                throw new ValueError("args must be a non-empty list");
            }

            _args = new global::System.Collections.Generic.List<string>(args);

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

            // PIPE = -1, DEVNULL = -2, STDOUT = -3
            startInfo.RedirectStandardInput = (stdin == SubprocessModule.PIPE);
            startInfo.RedirectStandardOutput = (stdout == SubprocessModule.PIPE);

            if (stderr == SubprocessModule.STDOUT)
            {
                // Merge stderr into stdout
                startInfo.RedirectStandardError = true;
            }
            else
            {
                startInfo.RedirectStandardError = (stderr == SubprocessModule.PIPE);
            }

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

            _process = new Process();
            _process.StartInfo = startInfo;

            try
            {
                _process.Start();
            }
            catch (Exception ex)
            {
                throw new OSError("Failed to start process '" + args[0] + "': " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Interact with process: send data to stdin, read data from stdout and stderr.
        /// Wait for process to terminate.
        /// </summary>
        /// <param name="input">Data to send to stdin, or null.</param>
        /// <param name="timeout">Timeout in seconds, or null for no timeout.</param>
        /// <returns>A tuple of (stdout, stderr).</returns>
        public (string? stdout, string? stderr) Communicate(string? input = null, double? timeout = null)
        {
            if (input != null && _process.StartInfo.RedirectStandardInput)
            {
                _process.StandardInput.Write(input);
                _process.StandardInput.Close();
            }
            else if (_process.StartInfo.RedirectStandardInput)
            {
                _process.StandardInput.Close();
            }

            string? stdoutResult = null;
            string? stderrResult = null;

            if (_process.StartInfo.RedirectStandardOutput)
            {
                stdoutResult = _process.StandardOutput.ReadToEnd();
            }

            if (_process.StartInfo.RedirectStandardError)
            {
                string errOutput = _process.StandardError.ReadToEnd();
                if (_process.StartInfo.RedirectStandardOutput &&
                    _process.StartInfo.RedirectStandardError)
                {
                    stderrResult = errOutput;
                }
                else
                {
                    stderrResult = errOutput;
                }
            }

            if (timeout.HasValue)
            {
                int timeoutMs = (int)(timeout.Value * 1000);
                if (!_process.WaitForExit(timeoutMs))
                {
                    Kill();
                    throw new TimeoutExpired(_args, timeout.Value, stdoutResult, stderrResult);
                }
            }
            else
            {
                _process.WaitForExit();
            }

            return (stdoutResult, stderrResult);
        }

        /// <summary>
        /// Wait for child process to terminate. Returns exit code.
        /// </summary>
        /// <param name="timeout">Timeout in seconds, or null for no timeout.</param>
        /// <returns>The exit code of the process.</returns>
        public int Wait(double? timeout = null)
        {
            if (timeout.HasValue)
            {
                int timeoutMs = (int)(timeout.Value * 1000);
                if (!_process.WaitForExit(timeoutMs))
                {
                    throw new TimeoutExpired(_args, timeout.Value);
                }
            }
            else
            {
                _process.WaitForExit();
            }

            return _process.ExitCode;
        }

        /// <summary>
        /// Check if child process has terminated. Returns exit code or null.
        /// </summary>
        public int? Poll()
        {
            return Returncode;
        }

        /// <summary>Send SIGTERM (Unix) or call Process.Kill (Windows) to the process.</summary>
        public void Terminate()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
        }

        /// <summary>Kill the child process (SIGKILL on Unix, TerminateProcess on Windows).</summary>
        public void Kill()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
        }

        /// <summary>Dispose of the process resources.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _process.Dispose();
            }
        }
    }
}
