using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// End-to-end tests for <c>TransitionWarningValidator</c> — the pipeline
/// validator that emits hint-severity SPY0470+ diagnostics about Sharpy's
/// behavioral differences from Python and C# (UTF-16 string length,
/// struct value-semantics, etc.).
///
/// Hints are folded into <see cref="ExecutionResult.CompilationWarnings"/>
/// alongside warnings (see <see cref="IntegrationTestBase"/>) so these
/// tests assert on hint content via the same channel.
/// </summary>
public class TransitionWarningValidatorIntegrationTests : IntegrationTestBase
{
    public TransitionWarningValidatorIntegrationTests(ITestOutputHelper output) : base(output) { }

    // ──────────────────────────────────────────────────────────────────────
    // SPY0470 — UTF-16 len() hint
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Len_OnBmpOnlyStringLiteral_DoesNotEmitUtf16Hint()
    {
        // ASCII string: every char fits in a single UTF-16 code unit, so
        // len() returns the same value Python would. No SPY0470 expected.
        const string source = """
            def main():
                n: int = len("hello")
                print(n)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("UTF-16 code-unit count"));
    }

    [Fact]
    public void Len_OnLatin1AndCommonBmpStringLiteral_DoesNotEmitUtf16Hint()
    {
        // Common European/CJK BMP characters: still single code units in UTF-16.
        const string source = """
            def main():
                n: int = len("café漢字")
                print(n)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("UTF-16 code-unit count"));
    }

    [Fact]
    public void Len_OnStringVariable_DoesNotEmitUtf16Hint()
    {
        // The validator only inspects string literals at the call site —
        // it can't know whether a variable holds non-BMP characters at
        // compile time, so it must stay silent.
        const string source = """
            def main():
                s: str = "😀"
                n: int = len(s)
                print(n)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("UTF-16 code-unit count"));
    }

    [Fact]
    public void Len_OnEmptyStringLiteral_DoesNotEmitUtf16Hint()
    {
        const string source = """
            def main():
                n: int = len("")
                print(n)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.Equal("0\n", result.StandardOutput);
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("UTF-16 code-unit count"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // SPY0471 — Struct value-semantics hint
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void StructReassignment_BetweenDistinctVariables_EmitsValueSemanticsHint()
    {
        // p2 = p1 is a plain Assignment to an Identifier whose RHS resolves
        // to a struct value — exactly the pattern SPY0471 targets.
        const string source = """
            struct Point:
                x: int
                y: int

            def main():
                p1: Point = Point(1, 2)
                p2: Point = Point(0, 0)
                p2 = p1
                print(p2.x)
                print(p2.y)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.Equal("1\n2\n", result.StandardOutput);
        Assert.Contains(result.CompilationWarnings,
            w => w.Contains("struct type 'Point'") && w.Contains("creates a copy"));
    }

    [Fact]
    public void StructFreshDeclaration_DoesNotEmitValueSemanticsHint()
    {
        // `p2: Point = p1` is a VariableDeclaration, not an Assignment.
        // The validator only inspects Assignment nodes, so this should
        // be silent — fresh bindings are not "assigned to another variable".
        const string source = """
            struct Point:
                x: int
                y: int

            def main():
                p1: Point = Point(1, 2)
                p2: Point = p1
                print(p2.x)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.Equal("1\n", result.StandardOutput);
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("creates a copy"));
    }

    [Fact]
    public void NonStructReassignment_DoesNotEmitValueSemanticsHint()
    {
        // Reassigning a primitive or class-typed variable is not a struct copy,
        // so SPY0471 must stay silent. (Also covers the broad "any reassignment"
        // false-positive risk.)
        const string source = """
            def main():
                x: int = 1
                y: int = 2
                x = y
                s: str = "a"
                t: str = "b"
                s = t
                print(x)
                print(s)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("creates a copy"));
    }

    [Fact]
    public void StructCompoundAssignment_DoesNotEmitValueSemanticsHint()
    {
        // The validator early-returns when node.Operator \!= AssignmentOperator.Assign.
        // Compound operators rebind in place rather than producing a copy,
        // so the value-copy framing doesn't apply.
        const string source = """
            struct Counter:
                value: int

                def __add__(self, other: Counter) -> Counter:
                    return Counter(self.value + other.value)

            def main():
                c: Counter = Counter(1)
                d: Counter = Counter(2)
                c += d
                print(c.value)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("creates a copy"));
    }

    [Fact]
    public void ClassReassignment_DoesNotEmitValueSemanticsHint()
    {
        // Classes are reference types; SPY0471 (struct value-copy) must
        // not fire for class assignments.
        const string source = """
            class Box:
                value: int

                def __init__(self, value: int):
                    self.value = value

            def main():
                a: Box = Box(1)
                b: Box = Box(2)
                a = b
                print(a.value)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.Equal("2\n", result.StandardOutput);
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("creates a copy"));
    }

    [Fact]
    public void StructAssignmentToFieldTarget_DoesNotEmitValueSemanticsHint()
    {
        // The validator only flags Identifier targets ("assigned to another
        // variable"); member targets (obj.field = ...) are excluded.
        const string source = """
            struct Point:
                x: int
                y: int

            class Container:
                origin: Point

                def __init__(self):
                    self.origin = Point(0, 0)

                def update(self, p: Point):
                    self.origin = p

            def main():
                c: Container = Container()
                p: Point = Point(3, 4)
                c.update(p)
                print(c.origin.x)
            """;

        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"Compilation failed: {string.Join("\n", result.CompilationErrors)}");
        Assert.DoesNotContain(result.CompilationWarnings,
            w => w.Contains("creates a copy"));
    }
}
