using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sharpy
{
    [SharpyModuleType("subprocess", "Popen")]
    public sealed class Popen : IDisposable
    {
        private readonly Process _process;
        private readonly System.Collections.Generic.List<string> _args;
        private bool _disposed;

        public int Pid => _process.Id;

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

        public System.Collections.Generic.List<string> Args => _args;

        public System.IO.StreamWriter? Stdin =>
            _process.StartInfo.RedirectStandardInput ? _process.StandardInput : null;

        public System.IO.StreamReader? StdoutStream =>
            _process.StartInfo.RedirectStandardOutput ? _process.StandardOutput : null;

        public System.IO.StreamReader? StderrStream =>
            _process.StartInfo.RedirectStandardError ? _process.StandardError : null;

        public Popen(
            System.Collections.Generic.List<string> args,
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

            _args = new System.Collections.Generic.List<string>(args);

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            ConfigureCommand(startInfo, args, shell);
            ConfigureRedirects(startInfo, stdin, stdout, stderr);

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

            // Read stdout and stderr concurrently to avoid deadlocks
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

        public int? Poll()
        {
            return Returncode;
        }

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

        // .NET Process.Kill() sends SIGKILL on Unix — SIGTERM is not available via managed API
        public void Terminate()
        {
            Kill();
        }

        public void SendSignal(int signal)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotImplementedError("SendSignal is not supported on Windows");
            }

            Kill();
        }

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

                _process.Dispose();
            }
        }

        internal static void ConfigureCommand(ProcessStartInfo startInfo, System.Collections.Generic.List<string> args, bool shell)
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
                for (int i = 1; i < args.Count; i++)
                {
                    startInfo.ArgumentList.Add(args[i]);
                }
            }
        }

        internal static void ConfigureRedirects(ProcessStartInfo startInfo, int stdin, int stdout, int stderr)
        {
            startInfo.RedirectStandardInput = (stdin == SubprocessModule.PIPE);
            startInfo.RedirectStandardOutput = (stdout == SubprocessModule.PIPE);

            if (stderr == SubprocessModule.STDOUT)
            {
                startInfo.RedirectStandardError = true;
            }
            else
            {
                startInfo.RedirectStandardError = (stderr == SubprocessModule.PIPE);
            }

            if (stdin == SubprocessModule.DEVNULL)
            {
                startInfo.RedirectStandardInput = true;
            }

            if (stdout == SubprocessModule.DEVNULL)
            {
                startInfo.RedirectStandardOutput = true;
            }

            if (stderr == SubprocessModule.DEVNULL)
            {
                startInfo.RedirectStandardError = true;
            }
        }
    }
}
