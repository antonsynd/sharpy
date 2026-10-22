using System.Text;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// The shared python-oracle locator the lexer's numeric twin uses (#2300, P68 Phase 1). It sits
/// in the Lexer namespace so the Stryker filter that scores the lexer runs it with the twin; it
/// mutates nothing in the lexer.
/// </summary>
/// <remarks>
/// The required-mode arm is exercised through the explicit <c>required</c> overload with a
/// candidate list that cannot resolve — never by setting <c>SHARPY_REQUIRE_PYTHON_ORACLE</c>
/// inside this testhost, which every other oracle consumer shares.
/// </remarks>
public class PythonInterpreterTests
{
    private static readonly string[] NoSuchInterpreter = { "sharpy-p68-no-such-python-interpreter" };

    private readonly ITestOutputHelper _output;

    public PythonInterpreterTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Locate_RequiredAndMissing_Fails()
    {
        var ex = Assert.Throws<PythonOracleMissingException>(
            () => PythonInterpreter.Locate(NoSuchInterpreter, required: true));
        Assert.Contains(PythonInterpreter.RequireEnvironmentVariable, ex.Message);
        Assert.Contains(NoSuchInterpreter[0], ex.Message);
    }

    [Fact]
    public void Locate_NotRequiredAndMissing_ReturnsNull()
    {
        // The skip arm: the same unresolvable list without the required flag is a loud skip
        // for the caller (null), not a throw — the control for the failing arm above.
        Assert.Null(PythonInterpreter.Locate(NoSuchInterpreter, required: false));
    }

    [Fact]
    public void RequireOrSkip_RequiredAndMissing_Fails_NamingTheOracle()
    {
        var ex = Assert.Throws<PythonOracleMissingException>(
            () => PythonInterpreter.RequireOrSkip(NoSuchInterpreter, required: true));
        Assert.Contains(PythonInterpreter.RequireEnvironmentVariable, ex.Message);
        Assert.Contains(NoSuchInterpreter[0], ex.Message);
    }

    [Fact]
    public void RequireOrSkip_NotRequiredAndMissing_Skips()
    {
        // The skip verdict is a SkipException, which [SkippableFact] reports as Skipped — never a
        // silent return that xunit counts Passed (#2342).
        var ex = Assert.Throws<SkipException>(
            () => PythonInterpreter.RequireOrSkip(NoSuchInterpreter, required: false));
        Assert.Contains(NoSuchInterpreter[0], ex.Message);
        Assert.Contains(PythonInterpreter.RequireEnvironmentVariable, ex.Message);
    }

    [SkippableFact]
    public void TryLocate_FindsAPinnedInterpreter_AndRefusesAnOlderMinimum()
    {
        var python = PythonInterpreter.RequireOrSkip(minMinor: 12);

        // The located verdict: the same interpreter, with the required flag either way, is returned.
        Assert.Equal(python.Executable, PythonInterpreter.RequireOrSkip(new[] { python.Executable }, required: true).Executable);
        Assert.Equal(python.Executable, PythonInterpreter.RequireOrSkip(new[] { python.Executable }, required: false).Executable);

        _output.WriteLine($"python oracle: {python.Executable} → {python.Version}");
        Assert.Matches(@"^Python \d+\.\d+", python.Version);
        Assert.True(python.Major > 3 || (python.Major == 3 && python.Minor >= 12), python.Version);

        // The version gate is load-bearing: the same interpreter fails a minimum it does not meet.
        Assert.Null(PythonInterpreter.TryLocate(new[] { python.Executable }, minMinor: python.Minor + 1000));
        Assert.NotNull(PythonInterpreter.TryLocate(new[] { python.Executable }, minMinor: python.Minor));
    }

    [SkippableFact]
    public void RunBatch_EchoesStdinLines_AndReportsStderrOnFailure()
    {
        var python = PythonInterpreter.RequireOrSkip(minMinor: 12);

        string dir = Path.Combine(Path.GetTempPath(), $"sharpy-p68-pyinterp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            string echo = Path.Combine(dir, "echo.py");
            File.WriteAllText(echo,
                "import sys\nfor line in sys.stdin:\n    line = line.strip()\n    if line:\n        print(line[::-1])\n",
                new UTF8Encoding(false));
            var lines = python.RunBatch(echo, new[] { "{\"id\": 1}", "", "abc" });
            Assert.Equal(new[] { "}1 :\"di\"{", "cba" }, lines);

            string fail = Path.Combine(dir, "fail.py");
            File.WriteAllText(fail, "import sys\nsys.stderr.write('p68-oracle-broke')\nsys.exit(3)\n", new UTF8Encoding(false));
            var ex = Assert.Throws<InvalidOperationException>(() => python.RunBatch(fail, new[] { "x" }));
            Assert.Contains("exited 3", ex.Message);
            Assert.Contains("p68-oracle-broke", ex.Message);

            // A script that exits without reading stdin, sent a batch larger than any pipe buffer: the write breaks the
            // pipe, and the failure must still be the script's exit code and stderr, never a bare IOException.
            var large = Enumerable.Repeat(new string('x', 1000), 2000);
            var broken = Assert.Throws<InvalidOperationException>(() => python.RunBatch(fail, large));
            Assert.Contains("exited 3", broken.Message);
            Assert.Contains("p68-oracle-broke", broken.Message);
        }
        finally
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
        }
    }

    [Fact]
    public void FindRepoFile_ResolvesAFileUnderTheRepositoryRoot_AndNullForAMissingOne()
    {
        Assert.NotNull(PythonInterpreter.FindRepoFile("sharpy.sln"));
        Assert.Null(PythonInterpreter.FindRepoFile("build_tools", "sharpy-p68-no-such-file.py"));
    }
}
