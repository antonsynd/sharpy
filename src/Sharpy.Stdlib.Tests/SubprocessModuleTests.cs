using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace Sharpy.Core.Tests;

public class SubprocessModuleTests
{
    // --- Constants ---

    [Fact]
    public void Constants_MatchPython()
    {
        SubprocessModule.PIPE.Should().Be(-1);
        SubprocessModule.STDOUT.Should().Be(-2);
        SubprocessModule.DEVNULL.Should().Be(-3);
    }

    // --- CompletedProcess ---

    [Fact]
    public void CompletedProcess_Properties()
    {
        var args = new Sharpy.List<string> { "echo", "hello" };
        var cp = new CompletedProcess(args, 0, "hello\n", "");
        cp.Args.Should().Equal("echo", "hello");
        cp.Returncode.Should().Be(0);
        cp.Stdout.Should().Be("hello\n");
        cp.Stderr.Should().Be("");
    }

    [Fact]
    public void CompletedProcess_CheckReturncode_Zero_NoException()
    {
        var cp = new CompletedProcess(new Sharpy.List<string> { "true" }, 0);
        var act = () => cp.CheckReturncode();
        act.Should().NotThrow();
    }

    [Fact]
    public void CompletedProcess_CheckReturncode_NonZero_Throws()
    {
        var cp = new CompletedProcess(new Sharpy.List<string> { "false" }, 1);
        var act = () => cp.CheckReturncode();
        act.Should().Throw<CalledProcessError>()
            .Where(e => e.Returncode == 1);
    }

    [Fact]
    public void CompletedProcess_ToString()
    {
        var cp = new CompletedProcess(new Sharpy.List<string> { "echo", "hi" }, 0);
        cp.ToString().Should().Contain("CompletedProcess");
        cp.ToString().Should().Contain("returncode=0");
    }

    // --- Exception hierarchy ---

    [Fact]
    public void CalledProcessError_IsSubprocessError()
    {
        var ex = new CalledProcessError(1, new Sharpy.List<string> { "false" });
        (ex is SubprocessError).Should().BeTrue();
        (ex is System.Exception).Should().BeTrue();
    }

    [Fact]
    public void CalledProcessError_MessageFormat()
    {
        var ex = new CalledProcessError(1, new Sharpy.List<string> { "my", "cmd" });
        ex.Message.Should().Contain("my cmd");
        ex.Message.Should().Contain("non-zero exit status 1");
    }

    [Fact]
    public void CalledProcessError_Properties()
    {
        var cmd = new Sharpy.List<string> { "test" };
        var ex = new CalledProcessError(42, cmd, "out", "err");
        ex.Returncode.Should().Be(42);
        ex.Cmd.Should().Equal("test");
        ex.Output.Should().Be("out");
        ex.Stderr.Should().Be("err");
    }

    [Fact]
    public void TimeoutExpired_IsSubprocessError()
    {
        var ex = new TimeoutExpired(new Sharpy.List<string> { "sleep" }, 5.0);
        (ex is SubprocessError).Should().BeTrue();
    }

    [Fact]
    public void TimeoutExpired_MessageFormat()
    {
        var ex = new TimeoutExpired(new Sharpy.List<string> { "sleep", "10" }, 5.0);
        ex.Message.Should().Contain("sleep 10");
        ex.Message.Should().Contain("timed out after 5 seconds");
    }

    // --- Run (integration tests) ---

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_BasicCommand_CaptureOutput()
    {
        var result = SubprocessModule.Run(
            new Sharpy.List<string> { "echo", "hello" },
            captureOutput: true);
        result.Returncode.Should().Be(0);
        result.Stdout.Should().Contain("hello");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_CaptureStderr()
    {
        var result = SubprocessModule.Run(
            new Sharpy.List<string> { "sh", "-c", "echo err >&2" },
            captureOutput: true);
        result.Stderr.Should().Contain("err");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_CheckMode_Success()
    {
        var act = () => SubprocessModule.Run(
            new Sharpy.List<string> { "true" },
            check: true);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_CheckMode_Failure()
    {
        var act = () => SubprocessModule.Run(
            new Sharpy.List<string> { "false" },
            check: true);
        act.Should().Throw<CalledProcessError>()
            .Where(e => e.Returncode != 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_InputPiping()
    {
        var result = SubprocessModule.Run(
            new Sharpy.List<string> { "cat" },
            input: "hello",
            captureOutput: true);
        result.Stdout.Should().Be("hello");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_Timeout_Throws()
    {
        var act = () => SubprocessModule.Run(
            new Sharpy.List<string> { "sleep", "10" },
            timeout: 0.5);
        act.Should().Throw<TimeoutExpired>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_WorkingDirectory()
    {
        var result = SubprocessModule.Run(
            new Sharpy.List<string> { "pwd" },
            captureOutput: true,
            cwd: "/tmp");
        result.Stdout!.Trim().Should().Contain("tmp");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_EnvironmentVariables()
    {
        var env = new Sharpy.Dict<string, string> { { "MY_VAR", "test_val" } };
        var result = SubprocessModule.Run(
            new Sharpy.List<string> { "sh", "-c", "echo $MY_VAR" },
            captureOutput: true,
            env: env);
        result.Stdout.Should().Contain("test_val");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_NonZeroWithoutCheck_NoException()
    {
        var result = SubprocessModule.Run(
            new Sharpy.List<string> { "false" });
        result.Returncode.Should().NotBe(0);
    }

    // --- CheckOutput ---

    [Fact]
    [Trait("Category", "Integration")]
    public void CheckOutput_BasicCommand()
    {
        var output = SubprocessModule.CheckOutput(
            new Sharpy.List<string> { "echo", "hello" });
        output.Should().Contain("hello");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CheckOutput_Failure_Throws()
    {
        var act = () => SubprocessModule.CheckOutput(
            new Sharpy.List<string> { "false" });
        act.Should().Throw<CalledProcessError>();
    }

    // --- CheckCall ---

    [Fact]
    [Trait("Category", "Integration")]
    public void CheckCall_Success()
    {
        SubprocessModule.CheckCall(
            new Sharpy.List<string> { "true" })
            .Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CheckCall_Failure_Throws()
    {
        var act = () => SubprocessModule.CheckCall(
            new Sharpy.List<string> { "false" });
        act.Should().Throw<CalledProcessError>();
    }

    // --- Popen ---

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_BasicWait()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "true" });
        var exitCode = proc.Wait();
        exitCode.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Communicate()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "cat" },
            stdin: SubprocessModule.PIPE,
            stdout: SubprocessModule.PIPE);
        var (stdout, stderr) = proc.Communicate("test input");
        stdout.Should().Be("test input");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Pid_IsPositive()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "true" });
        proc.Pid.Should().BeGreaterThan(0);
        proc.Wait();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Kill()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "sleep", "60" });
        proc.Kill();
        proc.Wait().Should().NotBe(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Poll_RunningProcess()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "sleep", "60" });
        proc.Poll().Should().BeNull();
        proc.Kill();
        proc.Wait();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Dispose_CleansUp()
    {
        var proc = new Popen(
            new Sharpy.List<string> { "sleep", "60" });
        proc.Dispose();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Terminate()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "sleep", "60" });
        proc.Terminate();
        proc.Wait().Should().NotBe(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_SendSignal_Unix()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Linux) &&
            !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX))
        {
            return;
        }

        using var proc = new Popen(
            new Sharpy.List<string> { "sleep", "60" });
        proc.SendSignal(9);
        proc.Wait().Should().NotBe(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Poll_CompletedProcess_ReturnsExitCode()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "true" });
        proc.Wait();
        proc.Poll().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Popen_Devnull_Stdout()
    {
        using var proc = new Popen(
            new Sharpy.List<string> { "echo", "discarded" },
            stdout: SubprocessModule.DEVNULL);
        var exitCode = proc.Wait();
        exitCode.Should().Be(0);
    }

    [Fact]
    public void Popen_StderrStdout_ThrowsNotImplemented()
    {
        var act = () => new Popen(
            new Sharpy.List<string> { "echo", "test" },
            stderr: SubprocessModule.STDOUT);
        act.Should().Throw<NotImplementedError>();
    }

    [Fact]
    public void Run_TextFalse_ThrowsNotImplemented()
    {
        var act = () => SubprocessModule.Run(
            new Sharpy.List<string> { "echo", "test" },
            text: false);
        act.Should().Throw<NotImplementedError>();
    }

    [Fact]
    public void CheckOutput_TextFalse_ThrowsNotImplemented()
    {
        var act = () => SubprocessModule.CheckOutput(
            new Sharpy.List<string> { "echo", "test" },
            text: false);
        act.Should().Throw<NotImplementedError>();
    }
}
