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
    public void TryLocate_FindsAPinnedInterpreter_AndRefusesAnOlderMinimum()
    {
        var python = PythonInterpreter.Locate(PythonInterpreter.DefaultCandidates, PythonInterpreter.Required);
        if (python is null)
        {
            _output.WriteLine("SKIP: no Python 3.12+ interpreter (python3/python) on PATH; set SHARPY_REQUIRE_PYTHON_ORACLE=1 to make this a failure.");
            return;
        }

        _output.WriteLine($"python oracle: {python.Executable} → {python.Version}");
        Assert.Matches(@"^Python \d+\.\d+", python.Version);
        Assert.True(python.Major > 3 || (python.Major == 3 && python.Minor >= 12), python.Version);

        // The version gate is load-bearing: the same interpreter fails a minimum it does not meet.
        Assert.Null(PythonInterpreter.TryLocate(new[] { python.Executable }, minMinor: python.Minor + 1000));
        Assert.NotNull(PythonInterpreter.TryLocate(new[] { python.Executable }, minMinor: python.Minor));
    }

    [Fact]
    public void RunBatch_EchoesStdinLines_AndReportsStderrOnFailure()
    {
        var python = PythonInterpreter.Locate(PythonInterpreter.DefaultCandidates, PythonInterpreter.Required);
        if (python is null)
        {
            _output.WriteLine("SKIP: no Python 3.12+ interpreter (python3/python) on PATH.");
            return;
        }

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
