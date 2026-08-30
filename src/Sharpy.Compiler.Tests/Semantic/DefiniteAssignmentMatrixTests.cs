using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// DA position x node-kind matrix (#1635): for each representative expression kind, an
/// unassigned read must produce SPY0600 and an assigned read must succeed.
/// </summary>
[Collection("HeavyCompilation")]
public class DefiniteAssignmentMatrixTests : IntegrationTestBase
{
    public DefiniteAssignmentMatrixTests(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> UnassignedReadCases => new[]
    {
        new object[] { "Identifier", "def main() -> None:\n    x: int\n    print(x)" },
        new object[] { "BinaryOp", "def main() -> None:\n    x: int\n    y: int = x + 1" },
        new object[] { "UnaryOp", "def main() -> None:\n    x: int\n    y: int = -x" },
        new object[] { "FunctionCall", "def main() -> None:\n    x: int\n    print(x)" },
        new object[] { "IndexAccess", "def main() -> None:\n    x: int\n    items: list[int] = [10, 20, 30]\n    y: int = items[x]" },
        new object[] { "MemberAccess", "def main() -> None:\n    x: str\n    y: str = x.upper()" },
        new object[] { "ConditionalExpression", "def main() -> None:\n    x: int\n    y: int = x if True else 0" },
        new object[] { "ListLiteral", "def main() -> None:\n    x: int\n    y: list[int] = [x]" },
        new object[] { "TupleLiteral", "def main() -> None:\n    x: int\n    y: tuple[int, int] = (x, 1)" },
        new object[] { "ComparisonChain", "def main() -> None:\n    x: int\n    y: bool = 0 < x < 10" },
        new object[] { "TypeCheck", "def main() -> None:\n    x: object\n    y: bool = isinstance(x, int)" },
        new object[] { "ExceptHandler", "def main() -> None:\n    x: int\n    try:\n        x = 1\n    except Exception:\n        print(x)" },
    };

    [Theory]
    [MemberData(nameof(UnassignedReadCases))]
    public void UnassignedRead_ProducesSPY0600(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"{kind}: unassigned read must produce SPY0600");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0600",
            $"{kind}: expected SPY0600 for unassigned variable");
    }

    public static IEnumerable<object[]> AssignedReadCases => new[]
    {
        new object[] { "Identifier", "def main() -> None:\n    x: int\n    x = 42\n    print(x)" },
        new object[] { "BinaryOp", "def main() -> None:\n    x: int\n    x = 1\n    y: int = x + 1" },
        new object[] { "UnaryOp", "def main() -> None:\n    x: int\n    x = 1\n    y: int = -x" },
        new object[] { "FunctionCall", "def main() -> None:\n    x: int\n    x = 42\n    print(x)" },
        new object[] { "IndexAccess", "def main() -> None:\n    x: int\n    x = 0\n    items: list[int] = [10, 20, 30]\n    y: int = items[x]" },
        new object[] { "MemberAccess", "def main() -> None:\n    x: str\n    x = \"hello\"\n    y: str = x.upper()" },
        new object[] { "ConditionalExpression", "def main() -> None:\n    x: int\n    x = 1\n    y: int = x if True else 0" },
        new object[] { "ListLiteral", "def main() -> None:\n    x: int\n    x = 1\n    y: list[int] = [x]" },
        new object[] { "TupleLiteral", "def main() -> None:\n    x: int\n    x = 1\n    y: tuple[int, int] = (x, 1)" },
        new object[] { "ComparisonChain", "def main() -> None:\n    x: int\n    x = 5\n    y: bool = 0 < x < 10" },
        new object[] { "TypeCheck", "def main() -> None:\n    x: object\n    x = 42\n    y: bool = isinstance(x, int)" },
    };

    [Theory]
    [MemberData(nameof(AssignedReadCases))]
    public void AssignedRead_Succeeds(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == "SPY0600",
            $"{kind}: assigned read must not produce SPY0600");
        result.Success.Should().BeTrue($"{kind}: assigned read must compile successfully. Errors: {string.Join("; ", result.CompilationErrors)}");
    }

    [Fact]
    public void LambdaExpression_SeparateScope_DoesNotFlagOuterUnassigned()
    {
        var source = @"
def main() -> None:
    x: int
    f = lambda: 42
    x = 1
    print(f())
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            "lambda has its own scope — outer x not read inside lambda");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void ExceptHandler_AssignedInsideTry_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: int
    try:
        x = 1
    except Exception:
        print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "x assigned inside try body is not definitely assigned in except handler");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0600",
            "except handler sees x as possibly unassigned");
    }

    [Fact]
    public void ExceptHandler_AssignedBeforeTry_Succeeds()
    {
        var source = @"
def main() -> None:
    x: int
    x = 1
    try:
        pass
    except Exception:
        print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == "SPY0600",
            "x assigned before try is definitely assigned in except handler");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // ========================================================================================= //
    // Block-kind × write-kind axis (#1668, #1672 DA)
    // ========================================================================================= //

    // --- except-as write kind ---

    [Fact]
    public void ExceptAs_HandlerName_IsAssigned()
    {
        var source = @"
def main() -> None:
    try:
        raise ValueError(""test"")
    except ValueError as e:
        print(e)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "except-as binding e is assigned at handler entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void ExceptAs_ReadAfterTry_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    e: str
    try:
        raise ValueError(""test"")
    except ValueError as e:
        pass
    print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "e assigned only inside except handler is not definitely assigned after try");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading e after try/except must flag SPY0600");
    }

    [Fact]
    public void ExceptAs_ReadAfterTry_JoinPath_ProducesSPY0600()
    {
        // Sibling of ExceptAs_ReadAfterTry_ProducesSPY0600 with a LIVE normal exit from the try:
        // the merge block joins the handler with the no-exception path, so the intersection alone
        // already drops e. Pairs with the dead-normal-exit cell above, which only the handler's
        // scope exit can catch (#1672 DA).
        // python3: UnboundLocalError: cannot access local variable 'e' ... (the handler deletes e).
        var source = @"
def main(flag: bool) -> None:
    e: str
    try:
        if flag:
            raise ValueError(""test"")
    except ValueError as e:
        pass
    print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "e bound only by the handler is not definitely assigned on the no-exception path");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading e after try/except must flag SPY0600 on the join path too");
    }

    [Fact]
    public void ExceptAs_NoPriorDeclaration_IsSPY0200()
    {
        // Positive control for the SPY0600 cells: without an outer declaration the read is not a
        // definite-assignment question at all — name resolution refuses it, because the except-as
        // binder is block-scoped (#1647). Proves the SPY0600 cells are exercising DA and not
        // merely inheriting a resolution error.
        // python3: UnboundLocalError: cannot access local variable 'e' ... .
        var source = @"
def main() -> None:
    try:
        raise ValueError(""test"")
    except ValueError as e:
        pass
    print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("e is not in scope after the handler");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0200",
            "an undeclared name read after the handler is a resolution error, not SPY0600");
    }

    [Fact]
    public void ExceptAs_WithFinally_ReadInFinally_ProducesSPY0600()
    {
        // Handler scope ends BEFORE finally runs, so the finally block is where the handler's
        // names go out of scope when the statement has one (#1672 DA).
        // python3: UnboundLocalError: cannot access local variable 'e' ... .
        var source = @"
def main() -> None:
    e: str
    try:
        raise ValueError(""test"")
    except ValueError as e:
        pass
    finally:
        print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "e bound only by the handler is not definitely assigned in finally");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading e in finally must flag SPY0600");
    }

    [Fact]
    public void ExceptAs_TwoHandlers_DistinctNames_ReadAfterTry_ProducesSPY0600()
    {
        // Two binders end at the same merge block. `a` is bound by the first handler only, so the
        // predecessor intersection already drops it; the cell pins that adding the second handler
        // does not resurrect it.
        // python3: UnboundLocalError: cannot access local variable 'a' ... .
        var source = @"
def main() -> None:
    a: str
    try:
        raise ValueError(""test"")
    except ValueError as a:
        pass
    except TypeError as b:
        pass
    print(a)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("a is bound only inside the first handler");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading a after two handlers must flag SPY0600");
    }

    [Fact]
    public void ExceptAs_TwoHandlers_SameName_ReadAfterTry_ProducesSPY0600()
    {
        // Both handlers bind `e`, so BOTH merge predecessors carry it and the intersection keeps
        // it — only the scope exit of every handler removes it. This is the cell that needs
        // RebindScopeEntries to hold more than one binder (#1672 DA).
        // python3: UnboundLocalError: cannot access local variable 'e' ... .
        var source = @"
def main() -> None:
    e: str
    try:
        raise ValueError(""test"")
    except ValueError as e:
        pass
    except TypeError as e:
        pass
    print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "e is bound only inside the handlers, on every incoming edge");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading e after two same-named handlers must flag SPY0600");
    }

    [Fact]
    public void ExceptAs_SecondHandlerIsTheOnlyLivePath_ProducesSPY0600()
    {
        // The first handler returns, so the merge block's only predecessor is the SECOND handler
        // and the intersection cannot drop `b`. Falsifies "every handler with a name is
        // registered": registering only the first leaves this cell compiling (#1672 DA).
        // python3 (raising TypeError so the second handler runs):
        //   UnboundLocalError: cannot access local variable 'b' ... .
        var source = @"
def main() -> None:
    b: str
    try:
        raise ValueError(""test"")
    except ValueError as a:
        print(a)
        return
    except TypeError as b:
        pass
    print(b)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("b is bound only inside the second handler");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "reading b after the handlers must flag SPY0600");
    }

    [Fact]
    public void ExceptAs_OuterAssignedBeforeTry_KeepsOuterValue()
    {
        // The other arm of the scope-exit rule: the handler's `e` shadows a DIFFERENT, outer
        // variable, so the outer binding is restored — assigned, with its own value — after the
        // handler. Guards against over-correcting the leak into an unconditional unbind.
        // python3 prints nothing here: UnboundLocalError, because Python has one function-level
        // `e` that the handler deletes. Sharpy's except-as binder is block-scoped (#1647), so the
        // outer local is untouched; Axiom 1 (.NET scoping) governs (variable_declaration.md).
        var source = @"
def main() -> None:
    e: str
    e = ""outer""
    try:
        raise ValueError(""test"")
    except ValueError as e:
        pass
    print(e)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "the outer e was assigned before the try, so it stays assigned after it");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("outer");
    }

    // --- match capture write kind ---

    [Fact]
    public void MatchCapture_BindingPattern_IsAssigned()
    {
        var source = @"
def main() -> None:
    x: object = 42
    match x:
        case int(n):
            print(n)
        case _:
            pass
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "match capture n is assigned at case block entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void MatchCapture_AsPattern_IsAssigned()
    {
        var source = @"
def main() -> None:
    x: object = ""hello""
    match x:
        case str() as s:
            print(s)
        case _:
            pass
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "as-pattern capture s is assigned at case block entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- for-else / while-else block kinds (#1668) ---

    // variable_declaration.md:94-100 — a local is definitely assigned after a loop when the `else`
    // body assigns it, or when every path through the body AND the `else` assigns it. Assignment in
    // the body alone is not enough: the loop may run zero times, and the compiler does not prove
    // an iterable non-empty. python3 has no static check and prints the body's last value (2) for
    // the two refusal cells below — the divergence is the point of SPY0600.

    [Fact]
    public void ForElse_VariableAssignedInBody_NotDefiniteAfterElse()
    {
        // python3: prints 2 (range(3) happens to be non-empty at runtime).
        var source = @"
def main() -> None:
    x: int
    for i in range(3):
        x = i
    else:
        pass
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "the for body may run zero times, and the else body assigns nothing");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "x assigned only in the loop body is not definitely assigned after the else");
    }

    [Fact]
    public void ForElse_VariableAssignedInElseBody_DefiniteAfter()
    {
        // python3: prints -1.
        var source = @"
def main() -> None:
    x: int
    for i in range(3):
        pass
    else:
        x = -1
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "the else body runs on every no-break exit, so x is definitely assigned");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("-1");
    }

    [Fact]
    public void ForElse_VariableAssignedInBodyAndElse_DefiniteAfter()
    {
        // python3: prints -1 (the else runs after the body's last iteration).
        var source = @"
def main() -> None:
    x: int
    for i in range(3):
        x = i
    else:
        x = -1
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "every path through the body and the else assigns x");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("-1");
    }

    [Fact]
    public void ForElse_VariableAssignedInBodyWithBreakAndElse_DefiniteAfter()
    {
        // The break path leaves the loop from the body (which assigned x) and the no-break path
        // runs the else (which assigns x): both exits are covered.
        // python3: prints 0.
        var source = @"
def main() -> None:
    x: int
    for i in range(3):
        x = i
        break
    else:
        x = -1
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x is assigned on the break path and on the else path");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("0");
    }

    [Fact]
    public void WhileElse_VariableAssignedInBody_NotDefiniteAfterElse()
    {
        // python3: prints 2 (the condition happens to be true at runtime).
        var source = @"
def main() -> None:
    x: int
    i: int = 0
    while i < 3:
        x = i
        i += 1
    else:
        pass
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "the while body may run zero times, and the else body assigns nothing");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600",
            "x assigned only in the loop body is not definitely assigned after the else");
    }

    [Fact]
    public void WhileElse_VariableAssignedInElseBody_DefiniteAfter()
    {
        // python3: prints -1.
        var source = @"
def main() -> None:
    x: int
    i: int = 0
    while i < 3:
        i += 1
    else:
        x = -1
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "the else body runs on every no-break exit, so x is definitely assigned");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("-1");
    }

    [Fact]
    public void WhileElse_VariableAssignedInBodyAndElse_DefiniteAfter()
    {
        // python3: prints -1.
        var source = @"
def main() -> None:
    x: int
    i: int = 0
    while i < 3:
        x = i
        i += 1
    else:
        x = -1
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "every path through the body and the else assigns x");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("-1");
    }

    [Fact]
    public void WhileElse_VariableAssignedInBodyWithBreakAndElse_DefiniteAfter()
    {
        // python3: prints 0.
        var source = @"
def main() -> None:
    x: int
    i: int = 0
    while i < 3:
        x = i
        break
    else:
        x = -1
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x is assigned on the break path and on the else path");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("0");
    }

    [Fact]
    public void ForTarget_IsAssigned()
    {
        var source = @"
def main() -> None:
    for x in [1, 2, 3]:
        print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "for-target x is assigned at loop body entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- with-as write kind ---

    [Fact]
    public void WithAs_BindingIsAssigned()
    {
        var source = @"
class Tracker:
    label: str
    def __init__(self, label: str):
        self.label = label
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    with Tracker(""a"") as t:
        print(t.label)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "with-as binding t is assigned at body entry");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- walrus write kind ---

    [Fact]
    public void Walrus_AssignsVariable()
    {
        var source = @"
def main() -> None:
    xs: list[int] = [1, 2, 3]
    if (n := len(xs)) > 0:
        print(n)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "walrus operator assigns n before the if body");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    // --- plain assignment in various block kinds ---

    [Fact]
    public void IfBlock_AssignedInsideOnly_NotDefiniteAfter()
    {
        var source = @"
def main() -> None:
    x: int
    if True:
        x = 1
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("x assigned only in if-body is not definite after");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600");
    }

    [Fact]
    public void IfElse_AssignedInBoth_DefiniteAfter()
    {
        var source = @"
def main() -> None:
    x: int
    if True:
        x = 1
    else:
        x = 2
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in both if and else branches is definite after");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void TryElse_AssignedInTryBody_NotDefiniteInElse()
    {
        var source = @"
def main() -> None:
    x: int
    try:
        x = 1
    except Exception:
        pass
    else:
        print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in try body is definite in else (else only runs if try succeeded)");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void Finally_AssignedInsideTry_NotDefiniteInFinally()
    {
        var source = @"
def main() -> None:
    x: int
    try:
        x = 1
    finally:
        pass
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in try body with no except is definite after finally");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }

    [Fact]
    public void MatchCase_WildcardDefault_AssignedInAllArms_DefiniteAfter()
    {
        var source = @"
def main() -> None:
    x: int
    y: object = 42
    match y:
        case int():
            x = 1
        case _:
            x = 2
    print(x)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "x assigned in all match arms (with wildcard default) is definite after");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
    }
}
