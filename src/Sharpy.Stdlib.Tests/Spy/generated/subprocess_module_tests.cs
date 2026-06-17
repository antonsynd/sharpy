// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using @operator = global::Sharpy.Operator;
using subprocess = global::Sharpy.SubprocessModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Subprocess.SubprocessModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Subprocess
    {
        [global::Sharpy.SharpyModule("subprocess.subprocess_module_tests")]
        public static partial class SubprocessModuleTests
        {
        }
    }

    public static partial class Subprocess
    {
        public partial class SubprocessModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestConstantsMatchPython()
            {
#line (36, 5) - (36, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(-1, subprocess.PIPE);
#line (37, 5) - (37, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(-2, subprocess.STDOUT);
#line (38, 5) - (38, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(-3, subprocess.DEVNULL);
            }

            [Xunit.FactAttribute]
            public void TestCompletedProcessProperties()
            {
#line (45, 5) - (45, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Sharpy.List<string> args = new Sharpy.List<string>()
                {
                    "echo",
                    "hello"
                };
#line (46, 5) - (46, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var cp = new global::Sharpy.CompletedProcess(args, 0, "hello\n", "");
#line (47, 5) - (47, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "echo", "hello" }, cp.Args);
#line (48, 5) - (48, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(0, cp.Returncode);
#line (49, 5) - (49, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal("hello\n", cp.Stdout);
#line (50, 5) - (50, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal("", cp.Stderr);
            }

            [Xunit.FactAttribute]
            public void TestCompletedProcessCheckReturncodeZeroNoException()
            {
#line (55, 5) - (55, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var cp = new global::Sharpy.CompletedProcess(new Sharpy.List<string>() { "true" }, 0);
#line (56, 5) - (56, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                cp.CheckReturncode();
            }

            [Xunit.FactAttribute]
            public void TestCompletedProcessCheckReturncodeNonZeroThrows()
            {
#line (61, 5) - (61, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var cp = new global::Sharpy.CompletedProcess(new Sharpy.List<string>() { "false" }, 1);
#line (62, 5) - (64, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.CalledProcessError>((global::System.Action)(() =>
                {
#line (63, 9) - (63, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    cp.CheckReturncode();
                }));
#line (64, 5) - (64, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(1, ex.Returncode);
            }

            [Xunit.FactAttribute]
            public void TestCompletedProcessToString()
            {
#line (69, 5) - (69, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var cp = new global::Sharpy.CompletedProcess(new Sharpy.List<string>() { "echo", "hi" }, 0);
#line (70, 5) - (70, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                string text = global::Sharpy.Builtins.Str(cp);
#line (71, 5) - (71, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("CompletedProcess", text);
#line (72, 5) - (72, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("returncode=0", text);
            }

            [Xunit.FactAttribute]
            public void TestCalledProcessErrorIsSubprocessError()
            {
#line (79, 5) - (79, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = new global::Sharpy.CalledProcessError(1, new Sharpy.List<string>() { "false" });
#line (80, 5) - (80, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.True(ex is global::Sharpy.SubprocessError);
#line (81, 5) - (81, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.IsAssignableFrom<Exception>(ex);
            }

            [Xunit.FactAttribute]
            public void TestCalledProcessErrorMessageFormat()
            {
#line (86, 5) - (86, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = new global::Sharpy.CalledProcessError(1, new Sharpy.List<string>() { "my", "cmd" });
#line (87, 5) - (87, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                string msg = global::Sharpy.Builtins.Str(ex);
#line (88, 5) - (88, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("my cmd", msg);
#line (89, 5) - (89, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("non-zero exit status 1", msg);
            }

            [Xunit.FactAttribute]
            public void TestCalledProcessErrorProperties()
            {
#line (94, 5) - (94, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Sharpy.List<string> cmd = new Sharpy.List<string>()
                {
                    "test"
                };
#line (95, 5) - (95, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = new global::Sharpy.CalledProcessError(42, cmd, "out", "err");
#line (96, 5) - (96, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(42, ex.Returncode);
#line (97, 5) - (97, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "test" }, ex.Cmd);
#line (98, 5) - (98, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal("out", ex.Output);
#line (99, 5) - (99, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal("err", ex.Stderr);
            }

            [Xunit.FactAttribute]
            public void TestTimeoutExpiredIsSubprocessError()
            {
#line (104, 5) - (104, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = new global::Sharpy.TimeoutExpired(new Sharpy.List<string>() { "sleep" }, 5.0d);
#line (105, 5) - (105, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.True(ex is global::Sharpy.SubprocessError);
            }

            [Xunit.FactAttribute]
            public void TestTimeoutExpiredMessageFormat()
            {
#line (110, 5) - (110, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = new global::Sharpy.TimeoutExpired(new Sharpy.List<string>() { "sleep", "10" }, 5.0d);
#line (111, 5) - (111, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                string msg = global::Sharpy.Builtins.Str(ex);
#line (112, 5) - (112, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("sleep 10", msg);
#line (113, 5) - (113, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("timed out after 5 seconds", msg);
            }

            [Xunit.FactAttribute]
            public void TestRunBasicCommandCaptureOutput()
            {
#line (120, 5) - (120, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var result = subprocess.Run(new Sharpy.List<string>() { "echo", "hello" }, captureOutput: true);
#line (121, 5) - (121, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(0, result.Returncode);
#line (122, 5) - (122, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("hello", result.Stdout);
            }

            [Xunit.FactAttribute]
            public void TestRunCaptureStderr()
            {
#line (127, 5) - (127, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var result = subprocess.Run(new Sharpy.List<string>() { "sh", "-c", "echo err >&2" }, captureOutput: true);
#line (128, 5) - (128, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("err", result.Stderr);
            }

            [Xunit.FactAttribute]
            public void TestRunCheckModeSuccess()
            {
#line (133, 5) - (133, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                subprocess.Run(new Sharpy.List<string>() { "true" }, check: true);
            }

            [Xunit.FactAttribute]
            public void TestRunCheckModeFailure()
            {
#line (138, 5) - (140, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var ex = Xunit.Assert.Throws<global::Sharpy.CalledProcessError>((global::System.Action)(() =>
                {
#line (139, 9) - (139, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    subprocess.Run(new Sharpy.List<string>() { "false" }, check: true);
                }));
#line (140, 5) - (140, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.NotEqual(0, ex.Returncode);
            }

            [Xunit.FactAttribute]
            public void TestRunInputPiping()
            {
#line (145, 5) - (145, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var result = subprocess.Run(new Sharpy.List<string>() { "cat" }, input: "hello", captureOutput: true);
#line (146, 5) - (146, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal("hello", result.Stdout);
            }

            [Xunit.FactAttribute]
            public void TestRunTimeoutThrows()
            {
#line (151, 5) - (155, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.TimeoutExpired>((global::System.Action)(() =>
                {
#line (152, 9) - (152, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    subprocess.Run(new Sharpy.List<string>() { "sleep", "10" }, timeout: 0.5d);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRunWorkingDirectory()
            {
#line (157, 5) - (157, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var result = subprocess.Run(new Sharpy.List<string>() { "pwd" }, captureOutput: true, cwd: "/tmp");
#line (158, 5) - (158, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("tmp", result.Stdout.Strip());
            }

            [Xunit.FactAttribute]
            public void TestRunEnvironmentVariables()
            {
#line (163, 5) - (163, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Sharpy.Dict<string, string> env = new Sharpy.Dict<string, string>()
                {
                    {
                        "MY_VAR",
                        "test_val"
                    }
                };
#line (164, 5) - (164, 87) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var result = subprocess.Run(new Sharpy.List<string>() { "sh", "-c", "echo $MY_VAR" }, captureOutput: true, env: env);
#line (165, 5) - (165, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("test_val", result.Stdout);
            }

            [Xunit.FactAttribute]
            public void TestRunNonZeroWithoutCheckNoException()
            {
#line (170, 5) - (170, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                var result = subprocess.Run(new Sharpy.List<string>() { "false" });
#line (171, 5) - (171, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.NotEqual(0, result.Returncode);
            }

            [Xunit.FactAttribute]
            public void TestCheckOutputBasicCommand()
            {
#line (178, 5) - (178, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                string output = subprocess.CheckOutput(new Sharpy.List<string>() { "echo", "hello" });
#line (179, 5) - (179, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Contains("hello", output);
            }

            [Xunit.FactAttribute]
            public void TestCheckOutputFailureThrows()
            {
#line (184, 5) - (190, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.CalledProcessError>((global::System.Action)(() =>
                {
#line (185, 9) - (185, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    subprocess.CheckOutput(new Sharpy.List<string>() { "false" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestCheckCallSuccess()
            {
#line (192, 5) - (192, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Equal(0, subprocess.CheckCall(new Sharpy.List<string>() { "true" }));
            }

            [Xunit.FactAttribute]
            public void TestCheckCallFailureThrows()
            {
#line (197, 5) - (203, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Throws<global::Sharpy.CalledProcessError>((global::System.Action)(() =>
                {
#line (198, 9) - (198, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    subprocess.CheckCall(new Sharpy.List<string>() { "false" });
                }));
            }

            [Xunit.FactAttribute]
            public void TestPopenBasicWait()
            {
#line (205, 5) - (210, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "true" }))
                {
#line (206, 9) - (206, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    int exitCode = proc.Wait();
#line (207, 9) - (207, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.Equal(0, exitCode);
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenCommunicate()
            {
#line (212, 5) - (217, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "cat" }, stdin: subprocess.PIPE, stdout: subprocess.PIPE))
                {
#line (213, 9) - (213, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    var (stdout, stderr) = proc.Communicate("test input");
#line (214, 9) - (214, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.Equal("test input", stdout);
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenPidIsPositive()
            {
#line (219, 5) - (224, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "true" }))
                {
#line (220, 9) - (220, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.True(proc.Pid > 0);
#line (221, 9) - (221, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.Wait();
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenKill()
            {
#line (226, 5) - (231, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "sleep", "60" }))
                {
#line (227, 9) - (227, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.Kill();
#line (228, 9) - (228, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.NotEqual(0, proc.Wait());
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenPollRunningProcess()
            {
#line (233, 5) - (239, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "sleep", "60" }))
                {
#line (234, 9) - (234, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.Null(proc.Poll());
#line (235, 9) - (235, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.Kill();
#line (236, 9) - (236, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.Wait();
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenDisposeCleansUp()
            {
#line (244, 5) - (248, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "sleep", "60" }))
                {
#line (245, 9) - (245, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    ;
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenTerminate()
            {
#line (250, 5) - (255, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "sleep", "60" }))
                {
#line (251, 9) - (251, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.Terminate();
#line (252, 9) - (252, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.NotEqual(0, proc.Wait());
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenSendSignalUnix()
            {
#line (257, 5) - (262, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "sleep", "60" }))
                {
#line (258, 9) - (258, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.SendSignal(9);
#line (259, 9) - (259, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.NotEqual(0, proc.Wait());
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenPollCompletedProcessReturnsExitCode()
            {
#line (264, 5) - (269, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "true" }))
                {
#line (265, 9) - (265, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    proc.Wait();
#line (266, 9) - (266, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.True(@operator.Eq(proc.Poll(), 0));
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenDevnullStdout()
            {
#line (271, 5) - (276, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                using (var proc = new global::Sharpy.Popen(new Sharpy.List<string>() { "echo", "discarded" }, stdout: subprocess.DEVNULL))
                {
#line (272, 9) - (272, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    int exitCode = proc.Wait();
#line (273, 9) - (273, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    Xunit.Assert.Equal(0, exitCode);
                }
            }

            [Xunit.FactAttribute]
            public void TestPopenStderrStdoutThrowsNotImplemented()
            {
#line (278, 5) - (282, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Throws<NotImplementedError>((global::System.Action)(() =>
                {
#line (279, 9) - (279, 69) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    new global::Sharpy.Popen(new Sharpy.List<string>() { "echo", "test" }, stderr: subprocess.STDOUT);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRunTextFalseThrowsNotImplemented()
            {
#line (284, 5) - (288, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Throws<NotImplementedError>((global::System.Action)(() =>
                {
#line (285, 9) - (285, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    subprocess.Run(new Sharpy.List<string>() { "echo", "test" }, text: false);
                }));
            }

            [Xunit.FactAttribute]
            public void TestCheckOutputTextFalseThrowsNotImplemented()
            {
#line (290, 5) - (292, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                Xunit.Assert.Throws<NotImplementedError>((global::System.Action)(() =>
                {
#line (291, 9) - (291, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/subprocess/subprocess_module_tests.spy"
                    subprocess.CheckOutput(new Sharpy.List<string>() { "echo", "test" }, text: false);
                }));
            }
        }
    }
}
