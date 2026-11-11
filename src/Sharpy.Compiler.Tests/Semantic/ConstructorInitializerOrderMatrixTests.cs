using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A constructor's chaining call (<c>super().__init__(…)</c> / <c>self.__init__(…)</c>) is hoisted
/// into the C# constructor initializer (<c>: base(…)</c> / <c>: this(…)</c>), which runs before the
/// body. A statement written ahead of the call would run after it, against source order, so it is
/// refused with SPY0292 at the call (#2507). Generated matrix:
/// call {super, self} × host {class, struct (self only)} × statement {print, local, <c>self.x =</c>,
/// <c>self.x op=</c>, side-effecting call, a block, a raise in a block, <c>pass</c>} × the call's
/// argument {independent, a local computed ahead of it}.
/// <para>Refusal cells put the statement ahead of the call and assert exactly one SPY0292 at the
/// call's position. Run cells put the same statement after the call and compare stdout with
/// CPython's, run at test time through <see cref="PythonBatchOracle"/> (skipped, never passed,
/// without a python).</para>
/// </summary>
public class ConstructorInitializerOrderMatrixTests : IntegrationTestBase
{
    public ConstructorInitializerOrderMatrixTests(ITestOutputHelper output) : base(output) { }

    private static readonly (string Name, string[] Lines)[] Statements =
    {
        ("print", new[] { "print(\"pre\")" }),
        ("local", new[] { "k: int = 3" }),
        ("store", new[] { "self.x = 5" }),
        ("augstore", new[] { "self.x *= 3" }),
        ("call", new[] { "note(1)" }),
        ("block", new[] { "if a > 0:", "    self.x = 9" }),
        ("raise", new[] { "if a > 0:", "    raise ValueError(\"boom\")" }),
        ("pass", new[] { "pass" }),
    };

    private static readonly (string Call, string Host)[] Hosts =
    {
        ("super", "class"),
        ("self", "class"),
        ("self", "struct"),
    };

    /// <summary>
    /// The program, and the 1-based line of the chaining call. With <paramref name="python"/> it is the
    /// CPython twin of the same program: Python has no structs and no overloads, so a struct host is a
    /// class and the two-argument constructor overload is a helper the chain calls; it also calls main.
    /// </summary>
    private static (string Source, int CallLine) BuildSource(
        string call, string host, string[] statement, bool callFirst, bool dependentArgument, bool python = false)
    {
        var lines = new List<string> { "def note(v: int) -> None:", "    print(\"note\", v)", "" };
        var keyword = python ? "class" : host;
        if (call == "super")
        {
            lines.AddRange(new[]
            {
                "class B:", "    x: int = 20", "", "    def __init__(self, a: int):",
                "        print(\"base\", a)", "        self.x += a", "", $"{keyword} C(B):",
            });
        }
        else
        {
            lines.AddRange(new[]
            {
                $"{keyword} C:", "    x: int = 20", "",
                python ? "    def _init2(self, a: int, b: int):" : "    def __init__(self, a: int, b: int):",
                "        print(\"chained\", a)", "        self.x += a", "",
            });
        }

        lines.Add("    def __init__(self, a: int):");
        var body = new List<string>();
        if (dependentArgument)
            body.Add("t: int = a + 1");
        var argument = dependentArgument ? "t" : "7";
        var callText = call == "super"
            ? $"super().__init__({argument})"
            : python ? $"self._init2({argument}, 0)" : $"self.__init__({argument}, 0)";
        if (!callFirst)
            body.AddRange(statement);
        var callIndex = lines.Count + body.Count;
        body.Add(callText);
        if (callFirst)
            body.AddRange(statement);
        body.Add("self.x += 1");
        lines.AddRange(body.Select(b => "        " + b));
        lines.AddRange(new[]
        {
            "", "def main():", "    try:", "        c = C(4)", "        print(c.x)",
            "    except ValueError:", "        print(\"caught\")", "",
        });
        if (python)
            lines.Add("main()");
        return (string.Join("\n", lines), callIndex + 1);
    }

    public static IEnumerable<object[]> RefusalCells()
    {
        foreach (var (call, host) in Hosts)
        {
            foreach (var (name, _) in Statements)
            {
                yield return new object[] { call, host, name, false };
                yield return new object[] { call, host, name, true };
            }
            // Only the argument's local ahead of the call: the call reads a body local the hoisted
            // initializer cannot see (was SPY0908, CS0103).
            yield return new object[] { call, host, "none", true };
        }
    }

    private static IEnumerable<(string Call, string Host, string Statement)> RunCells()
    {
        foreach (var (call, host) in Hosts)
            foreach (var (name, _) in Statements)
                yield return (call, host, name);
    }

    private static string[] StatementLines(string name) =>
        name == "none" ? Array.Empty<string>() : Statements.Single(s => s.Name == name).Lines;

    [Theory]
    [MemberData(nameof(RefusalCells))]
    public void StatementAheadOfChainingCall_IsRefusedAtTheCall(
        string call, string host, string statement, bool dependentArgument)
    {
        var (source, callLine) = BuildSource(call, host, StatementLines(statement), callFirst: false, dependentArgument);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        var refusals = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Semantic.ConstructorInitializerNotFirst)
            .ToList();
        refusals.Should().ContainSingle(source);
        refusals[0].Line.Should().Be(callLine, source);
        refusals[0].Column.Should().Be(9, source);
        refusals[0].Message.Should().Contain(call == "super" ? ": base(...)" : ": this(...)");
        refusals[0].Message.Should().Contain("move the call first");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, source);
    }

    [SkippableFact]
    public void StatementAfterChainingCall_RunsAsCPython()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));
        var cells = RunCells().ToList();
        Assert.Equal(Hosts.Length * Statements.Length, cells.Count);

        var python = oracle.RunBatch(cells
            .Select((c, i) => new PyRequest(i, BuildSource(c.Call, c.Host, StatementLines(c.Statement),
                callFirst: true, dependentArgument: false, python: true).Source))
            .ToList());

        var divergences = new List<string>();
        for (int i = 0; i < cells.Count; i++)
        {
            var (call, host, statement) = cells[i];
            var label = $"{call}/{host}/{statement}";
            Assert.True(python.TryGetValue(i, out var py) && py.Ok,
                $"python3 did not run {label}: {py?.Stderr ?? "no verdict"}");
            var (source, _) = BuildSource(call, host, StatementLines(statement), callFirst: true, dependentArgument: false);
            var result = CompileAndExecute(source);
            var actual = result.Success ? result.StandardOutput.ReplaceLineEndings("\n") : "<" + result.FailureSummary() + ">";
            var expected = py!.Stdout.ReplaceLineEndings("\n");
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                divergences.Add($"  {label}: python {expected.Replace("\n", "|")}, sharpy {actual.Replace("\n", "|")}");
        }

        Assert.True(divergences.Count == 0,
            $"{divergences.Count} of {cells.Count} run cells diverge from CPython:\n" + string.Join("\n", divergences));
    }

    [Fact]
    public void ArgumentLocalAfterCall_IsNotNeeded_ChainFirstStillRuns()
    {
        // Control for the dependent-argument axis: the call reads only the parameter, so it can be
        // first, and the local computed after it runs in source order (CPython 3.15.2: base 5, 26).
        const string source = """
            class B:
                x: int = 20

                def __init__(self, a: int):
                    print("base", a)
                    self.x += a

            class C(B):
                def __init__(self, a: int):
                    super().__init__(a + 1)
                    t: int = a + 1
                    self.x += t - 4

            def main():
                print(C(4).x)
            """;

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary());
        result.StandardOutput.ReplaceLineEndings("\n").Should().Be("base 5\n26\n");
    }

    [Fact]
    public void DocstringAheadOfChainingCall_IsNotAStatement()
    {
        // CPython 3.15.2: base 4, 25 — the docstring is lifted out of the body.
        const string source = """"
            class B:
                x: int = 20

                def __init__(self, a: int):
                    print("base", a)
                    self.x += a

            class C(B):
                def __init__(self, a: int):
                    """Docstring first."""
                    super().__init__(a)
                    self.x += 1

            def main():
                print(C(4).x)
            """";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary());
        result.StandardOutput.ReplaceLineEndings("\n").Should().Be("base 4\n25\n");
    }

    [Fact]
    public void ParenthesizedChainingCall_IsTheSameCall_AndIsRefusedWhenNotFirst()
    {
        // The emitter hoists `(super().__init__(a))` too (#1197), so the position rule sees it.
        const string source = """
            class B:
                x: int = 20

                def __init__(self, a: int):
                    self.x += a

            class C(B):
                def __init__(self, a: int):
                    self.x = 5
                    (super().__init__(a))
                    self.x += 1

            def main():
                print(C(4).x)
            """;

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.Semantic.ConstructorInitializerNotFirst)
            .Which.Line.Should().Be(10);
    }

    private const string SuperHost =
        "class B:\n    def __init__(self):\n        print(\"base\")\n\nclass C(B):\n    def __init__(self, a: int):\n";

    private const string SelfHost =
        "class C:\n    x: int = 0\n    def __init__(self, a: int, b: int):\n        self.x = a\n    def __init__(self, a: int):\n";

    // A chaining call that is not the hoisted top-level statement has no constructor initializer to
    // become: under control flow, repeated, or inside an expression. One rule, one code (SPY0292),
    // for super().__init__() and self.__init__() alike. The self forms were SPY0908 (CS1061
    // 'Constructor'), the super expression forms SPY0501, the super control-flow and repeat forms SPY0287.
    [Theory]
    [InlineData("super", "if a > 0:\n            CALL", 8, "not inside control flow")]
    [InlineData("super", "CALL\n        CALL", 8, "only be called once")]
    [InlineData("super", "f = lambda: CALL\n        f()", 7, "part of an expression")]
    [InlineData("super", "y = CALL", 7, "part of an expression")]
    [InlineData("super", "CALL if a > 0 else print(\"no\")", 7, "part of an expression")]
    [InlineData("self", "if a > 0:\n            CALL", 7, "not inside control flow")]
    [InlineData("self", "CALL\n        CALL", 7, "only be called once")]
    [InlineData("self", "f = lambda: CALL\n        f()", 6, "part of an expression")]
    [InlineData("self", "y = CALL", 6, "part of an expression")]
    [InlineData("self", "CALL if a > 0 else print(\"no\")", 6, "part of an expression")]
    public void ChainingCallThatIsNotHoisted_IsRefusedWithOneCode(string call, string body, int line, string message)
    {
        var callText = call == "super" ? "super().__init__()" : "self.__init__(a, 0)";
        var source = (call == "super" ? SuperHost : SelfHost)
            + "        " + body.Replace("CALL", callText, StringComparison.Ordinal) + "\n\n"
            + "def main():\n    C(4)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        var refusal = result.RawDiagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.Semantic.ConstructorInitializerNotFirst, source).Which;
        refusal.Line.Should().Be(line, source);
        refusal.Message.Should().Contain(message);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.InvalidSuperUsage, source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.CodeGen.UnsupportedFeature, source);
    }

    [Fact]
    public void DecoratedChainingCall_IsHoistedLikeAnUndecoratedOne()
    {
        // `@suppress` is compile-time only: the call under it is the constructor's chaining call
        // (it was SPY0501). CPython has no statement decorators; the undecorated twin prints base, C.
        var source = SuperHost + "        @suppress(\"SPY0451\")\n        super().__init__()\n        print(\"C\")\n\n"
            + "def main():\n    C(4)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary() + "\n" + source);
        result.StandardOutput.ReplaceLineEndings("\n").Should().Be("base\nC\n");
    }

    [Fact]
    public void DecoratedChainingCall_IsPositionChecked()
    {
        var source = SuperHost + "        print(\"pre\")\n        @suppress(\"SPY0451\")\n        super().__init__()\n\n"
            + "def main():\n    C(4)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.Semantic.ConstructorInitializerNotFirst, source)
            .Which.Line.Should().Be(9);
    }

    [Fact]
    public void SuperInitOnInterfaceOnlyBase_KeepsTheNoParentRefusal()
    {
        // Positive control for #2508: an interface is not a base class, so there is no constructor to
        // chain to (SPY0285), and the implicit-constructor resolution does not apply.
        const string source = """
            interface I:
                def f(self) -> int: ...

            class C(I):
                def __init__(self):
                    super().__init__()
                def f(self) -> int:
                    return 1

            def main():
                print(C().f())
            """;

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().ContainSingle(d => d.IsError)
            .Which.Code.Should().Be(DiagnosticCodes.Semantic.SuperNoParent);
    }

    // A dataclass base carries its synthesized __init__, so super().__init__(...) binds it and its
    // arguments are validated against the dataclass's fields. CPython 3.15.2: `F 1`, `F 5`, `F 6`;
    // with a required field and no argument it raises TypeError (missing 'a'), refused here at compile
    // time (SPY0224); a str for an int field is SPY0220.
    [Theory]
    [InlineData("a: int = 1", "", "F 1\n")]
    [InlineData("a: int = 1", "5", "F 5\n")]
    [InlineData("a: int = 1", "a=6", "F 6\n")]
    [InlineData("a: int", "3", "F 3\n")]
    public void SuperInitOnDataclassBase_BindsTheSynthesizedConstructor(string field, string args, string expected)
    {
        var source = $"@dataclass\nclass DC:\n    {field}\n\nclass F(DC):\n    def __init__(self):\n"
            + $"        super().__init__({args})\n        print(\"F\", self.a)\n\ndef main():\n    F()\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary() + "\n" + source);
        result.StandardOutput.ReplaceLineEndings("\n").Should().Be(expected);
    }

    [Theory]
    [InlineData("a: int", "", "SPY0224")]
    [InlineData("a: int = 1", "\"x\"", "SPY0220")]
    public void SuperInitOnDataclassBase_ValidatesArguments(string field, string args, string code)
    {
        var source = $"@dataclass\nclass DC:\n    {field}\n\nclass F(DC):\n    def __init__(self):\n"
            + $"        super().__init__({args})\n        print(\"F\", self.a)\n\ndef main():\n    F()\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().Contain(d => d.Code == code && d.Line == 7, result.FailureSummary());
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    // #2508: a base with no declared __init__ has the implicit parameterless constructor (Python's
    // object.__init__), which the emitted `: base()` chains to. CPython 3.15.2 prints 22 at both
    // depths, and refuses an argument (TypeError: object.__init__() takes exactly one argument).
    [Theory]
    [InlineData("class B:\n    x: int = 20\n\nclass D(B):")]
    [InlineData("class A:\n    x: int = 20\n\nclass B(A):\n    pass\n\nclass D(B):")]
    public void SuperInitOnImplicitBaseConstructor_Runs(string hierarchy)
    {
        var source = hierarchy + "\n    def __init__(self) -> None:\n        super().__init__()\n        self.x += 2\n\n"
            + "def main():\n    print(D().x)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary() + "\n" + source);
        result.StandardOutput.ReplaceLineEndings("\n").Should().Be("22\n");
    }

    [Fact]
    public void SuperInitWithArgumentOnImplicitBaseConstructor_IsRefused()
    {
        const string source = """
            class B:
                x: int = 20

            class D(B):
                def __init__(self) -> None:
                    super().__init__(5)

            def main():
                print(D().x)
            """;

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.IsError && d.Line == 6, result.FailureSummary());
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }
}
