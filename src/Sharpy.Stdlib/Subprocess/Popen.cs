using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharpy
{
    /// <summary>Starts and manages a child process like Python's subprocess.Popen.</summary>
    [SharpyModuleType("subprocess", "Popen")]
    public sealed class Popen : IDisposable
    {
        private readonly Process _process;
        private readonly List<string> _args;
        private Task? _devnullStdoutDrain;
        private Task? _devnullStderrDrain;
        private bool _disposed;

        /// <summary>Gets the operating system process identifier.</summary>
        public int Pid => _process.Id;

        /// <summary>Gets the process exit code, or null while the process is still running.</summary>
        public int? Returncode
        {
            get
            {
                try
                {
                    return _process.HasExited ? _process.ExitCode : (int?)null;
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }
        }

        /// <summary>Gets the command arguments used to start the process.</summary>
        public List<string> Args => _args;

        /// <summary>Gets the redirected standard input writer, if available.</summary>
        public System.IO.StreamWriter? Stdin =>
            _process.StartInfo.RedirectStandardInput ? _process.StandardInput : null;

        /// <summary>Gets the redirected standard output reader, if available.</summary>
        public System.IO.StreamReader? StdoutStream =>
            _process.StartInfo.RedirectStandardOutput ? _process.StandardOutput : null;

        /// <summary>Gets the redirected standard error reader, if available.</summary>
        public System.IO.StreamReader? StderrStream =>
            _process.StartInfo.RedirectStandardError ? _process.StandardError : null;

        /// <summary>Starts a new subprocess with the requested redirections and environment.</summary>
        public Popen(
            List<string> args,
            int stdin = 0,
            int stdout = 0,
            int stderr = 0,
            bool shell = false,
            string? cwd = null,
            Dict<string, string>? env = null)
        {
            if (args == null || ((ISized)args).Count == 0)
            {
                throw new ValueError("args must be a non-empty list");
            }

            if (stderr == SubprocessModule.STDOUT)
            {
                throw new NotImplementedError("stderr=STDOUT (merge stderr into stdout) is not supported by the .NET Process API");
            }

            _args = new List<string>(args);

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            ConfigureCommand(startInfo, args, shell);
            ConfigureRedirects(startInfo, stdin, stdout, stderr);

            if (cwd != null)
            {
                startInfo.WorkingDirectory = cwd;
            }

            if (env is object)
            {
                startInfo.Environment.Clear();
                foreach (var key in env)
                {
                    startInfo.Environment[key] = env[key];
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

            if (stdin == SubprocessModule.DEVNULL)
            {
                _process.StandardInput.Close();
            }

            if (stdout == SubprocessModule.DEVNULL)
            {
                _devnullStdoutDrain = Task.Run(() =>
                {
                    try
                    { _process.StandardOutput.ReadToEnd(); }
                    catch (ObjectDisposedException) { }
                });
            }

            if (stderr == SubprocessModule.DEVNULL)
            {
                _devnullStderrDrain = Task.Run(() =>
                {
                    try
                    { _process.StandardError.ReadToEnd(); }
                    catch (ObjectDisposedException) { }
                });
            }
        }

        /// <summary>Sends optional input, waits for completion, and returns captured output.</summary>
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

            Task<string>? stdoutTask = null;
            Task<string>? stderrTask = null;

            if (_process.StartInfo.RedirectStandardOutput)
            {
                stdoutTask = _process.StandardOutput.ReadToEndAsync();
            }

            if (_process.StartInfo.RedirectStandardError)
            {
                stderrTask = _process.StandardError.ReadToEndAsync();
            }

            if (timeout.HasValue)
            {
                int timeoutMs = (int)(timeout.Value * 1000);
                if (!_process.WaitForExit(timeoutMs))
                {
                    Kill();
                    string? partialOut = stdoutTask != null && stdoutTask.IsCompleted ? stdoutTask.Result : null;
                    string? partialErr = stderrTask != null && stderrTask.IsCompleted ? stderrTask.Result : null;
                    throw new TimeoutExpired(_args, timeout.Value, partialOut, partialErr);
                }
            }
            else
            {
                _process.WaitForExit();
            }

            string? stdoutResult = stdoutTask?.GetAwaiter().GetResult();
            string? stderrResult = stderrTask?.GetAwaiter().GetResult();

            return (stdoutResult, stderrResult);
        }

        /// <summary>Waits for the process to exit and returns its exit code.</summary>
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

        /// <summary>Checks whether the process has exited without blocking.</summary>
        public int? Poll()
        {
            return Returncode;
        }

        /// <summary>Forcefully terminates the child process.</summary>
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
            }
        }

        /// <summary>Terminates the child process.</summary>
        // .NET Process.Kill() sends SIGKILL on Unix — SIGTERM is not available via managed API
        public void Terminate()
        {
            Kill();
        }

        /// <summary>Sends a signal to the child process.</summary>
        public void SendSignal(int signal)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotImplementedError("SendSignal is not supported on Windows");
            }

            Kill();
        }

        /// <summary>Disposes the process and any background drain tasks.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                }

                try
                { _devnullStdoutDrain?.Wait(); }
                catch (AggregateException) { }
                try
                { _devnullStderrDrain?.Wait(); }
                catch (AggregateException) { }

                _process.Dispose();
            }
        }

        internal static void ConfigureCommand(ProcessStartInfo startInfo, List<string> args, bool shell)
        {
            if (shell)
            {
                string command = string.Join(" ", args);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    startInfo.FileName = "cmd.exe";
                    startInfo.ArgumentList.Add("/c");
                    startInfo.ArgumentList.Add(command);
                }
                else
                {
                    startInfo.FileName = "/bin/sh";
                    startInfo.ArgumentList.Add("-c");
                    startInfo.ArgumentList.Add(command);
                }
            }
            else
            {
                startInfo.FileName = args[0];
                for (int i = 1; i < ((ISized)args).Count; i++)
                {
                    startInfo.ArgumentList.Add(args[i]);
                }
            }
        }

        internal static void ConfigureRedirects(ProcessStartInfo startInfo, int stdin, int stdout, int stderr)
        {
            startInfo.RedirectStandardInput = (stdin == SubprocessModule.PIPE || stdin == SubprocessModule.DEVNULL);
            startInfo.RedirectStandardOutput = (stdout == SubprocessModule.PIPE || stdout == SubprocessModule.DEVNULL);
            startInfo.RedirectStandardError = (stderr == SubprocessModule.PIPE || stderr == SubprocessModule.DEVNULL);
        }
    }
}
