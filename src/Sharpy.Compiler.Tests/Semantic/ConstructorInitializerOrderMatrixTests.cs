using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
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
/// CPython's.</para>
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

    // CPython 3.15.2, the same program with the statement AFTER the call (the chained constructor
    // prints its own line first, then these): `python3.15` over the source BuildSource renders with
    // `struct` read as `class` and the two-argument overload renamed to a helper the chain calls,
    // since Python has neither structs nor overloads.
    private static readonly Dictionary<string, string> PythonTailAfterCall = new()
    {
        ["print"] = "pre\n28\n",
        ["local"] = "28\n",
        ["store"] = "6\n",
        ["augstore"] = "82\n",
        ["call"] = "note 1\n28\n",
        ["block"] = "10\n",
        ["raise"] = "caught\n",
        ["pass"] = "28\n",
    };

    private static readonly (string Call, string Host)[] Hosts =
    {
        ("super", "class"),
        ("self", "class"),
        ("self", "struct"),
    };

    /// <summary>The program, and the 1-based line of the chaining call.</summary>
    private static (string Source, int CallLine) BuildSource(
        string call, string host, string[] statement, bool callFirst, bool dependentArgument)
    {
        var lines = new List<string> { "def note(v: int) -> None:", "    print(\"note\", v)", "" };
        if (call == "super")
        {
            lines.AddRange(new[]
            {
                "class B:", "    x: int = 20", "", "    def __init__(self, a: int):",
                "        print(\"base\", a)", "        self.x += a", "", $"{host} C(B):",
            });
        }
        else
        {
            lines.AddRange(new[]
            {
                $"{host} C:", "    x: int = 20", "", "    def __init__(self, a: int, b: int):",
                "        print(\"chained\", a)", "        self.x += a", "",
            });
        }

        lines.Add("    def __init__(self, a: int):");
        var body = new List<string>();
        if (dependentArgument)
            body.Add("t: int = a + 1");
        var argument = dependentArgument ? "t" : "7";
        var callText = call == "super" ? $"super().__init__({argument})" : $"self.__init__({argument}, 0)";
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

    public static IEnumerable<object[]> RunCells()
    {
        foreach (var (call, host) in Hosts)
            foreach (var (name, _) in Statements)
                yield return new object[] { call, host, name };
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

    [Theory]
    [MemberData(nameof(RunCells))]
    public void StatementAfterChainingCall_RunsAsCPython(string call, string host, string statement)
    {
        var (source, _) = BuildSource(call, host, StatementLines(statement), callFirst: true, dependentArgument: false);

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(result.FailureSummary() + "\n" + source);
        var head = call == "super" ? "base 7\n" : "chained 7\n";
        result.StandardOutput.ReplaceLineEndings("\n").Should().Be(head + PythonTailAfterCall[statement], source);
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

    [Theory]
    [InlineData("if a > 0:\n            self.__init__(a, 0)", 7, "not inside control flow")]
    [InlineData("self.__init__(a, 0)\n        self.__init__(a, 1)", 7, "only be called once")]
    public void SelfInitNotHoisted_IsRefused(string body, int line, string message)
    {
        // Only the first top-level self.__init__() becomes `: this(...)`; one under control flow or a
        // second one had no constructor to call (was SPY0908, CS1061 'Constructor').
        var source = $$"""
            class C:
                x: int = 20
                def __init__(self, a: int, b: int):
                    self.x += a
                def __init__(self, a: int):
                    {{body}}
                    self.x += 1

            def main():
                print(C(4).x)
            """;

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        var refusal = result.RawDiagnostics.Should()
            .ContainSingle(d => d.Code == DiagnosticCodes.Semantic.ConstructorInitializerNotFirst, source).Which;
        refusal.Line.Should().Be(line, source);
        refusal.Message.Should().Contain(message);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, source);
    }

    [Fact]
    public void SuperInitInsideControlFlow_KeepsItsExistingRefusal()
    {
        // Positive control: the existing control-flow arm still owns this cell (SPY0287), and the new
        // position rule does not add a second diagnostic for it.
        const string source = """
            class B:
                def __init__(self):
                    pass

            class C(B):
                def __init__(self, a: int):
                    if a > 0:
                        super().__init__()

            def main():
                C(1)
            """;

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.InvalidSuperUsage);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.ConstructorInitializerNotFirst);
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
