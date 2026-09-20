using FluentAssertions;
using Sharpy.Compiler.Shared;
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
    public void LambdaExpression_ReadsOuterUnassigned_ProducesSPY0600()
    {
        // Positive control for LambdaExpression_SeparateScope_DoesNotFlagOuterUnassigned above: that
        // cell's lambda body (`lambda: 42`) reads nothing, so it cannot tell a working
        // deferred-read check apart from a missing one. This cell (da03, #1681) actually reads the
        // outer x, so it is the cell the LambdaExpression arm's mutation must turn red.
        var source = @"
def main() -> None:
    x: int
    f = lambda: x
    print(f())
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("the lambda reads outer x, which is never assigned");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            "a lambda's deferred read of a never-assigned outer local must flag SPY0600");
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

    // ========================================================================================= //
    // Read-site axis (#1681, P6.T): {lambda, nested def, lambda-in-nested-def,
    // nested-def-in-nested-def, comprehension, generator expression, defer (feature-flagged)} x
    // {never assigned, assigned later, assigned in one branch}. The lambda/nested-def rule (#1635,
    // extended to nested defs by #1681, landed @ 3fa30defb) judges a DEFERRED read against "assigned
    // ANYWHERE in the function" — the closure may be called after a later assignment, so flow
    // position alone cannot refuse it, but a name never assigned anywhere still must be. A
    // comprehension/generator-expression read is never routed through that rule at all —
    // CollectReadsFromExpr only recognizes LambdaExpression as deferred, so a comprehension's
    // element/iterable expressions fall through to the ordinary per-block/per-statement read scan
    // and are judged by ORDINARY FLOW POSITION at their own textual location instead: "assigned
    // later" fails exactly like "never" there, because the read point itself precedes the
    // assignment (measured @ c7ade6be2, da05/da06). `defer` is flow-positioned too, but at the
    // unconditional END of the enclosing scope's fall-through flow (variable_declaration.md:
    // "defer bodies run at scope exit, after every statement of the enclosing scope") —
    // ControlFlowGraphBuilder.BuildDefer just enqueues the statement, and InsertDeferChain splices
    // it into the CFG once, after ALL of the function's other statements, so an assignment ANYWHERE
    // on the sole path to that splice point is credited (like the deferred sites' "later" cell) but
    // an assignment confined to one if-branch is not (like the flow-positioned sites' "one branch"
    // cell) — a third, hybrid shape that needs its own cells below.
    // ========================================================================================= //

    public static IEnumerable<object[]> DeferredReadSite_NeverAssigned_Cases => new[]
    {
        new object[] { "Lambda", @"
def main() -> None:
    x: int
    f = lambda: x
    print(f())
" },
        new object[] { "NestedDef", @"
def outer() -> None:
    x: int
    def inner() -> None:
        print(x)
    inner()

def main() -> None:
    outer()
" },
        new object[] { "LambdaInNestedDef", @"
def outer() -> None:
    x: int
    def inner() -> None:
        f = lambda: x
        print(f())
    inner()

def main() -> None:
    outer()
" },
        new object[] { "NestedDefInNestedDef", @"
def outer() -> None:
    x: int
    def middle() -> None:
        def inner() -> None:
            print(x)
        inner()
    middle()

def main() -> None:
    outer()
" },
    };

    [Theory]
    [MemberData(nameof(DeferredReadSite_NeverAssigned_Cases))]
    public void DeferredReadSite_NeverAssigned_ProducesSPY0600(string kind, string source)
    {
        // Falsifier for the FunctionDef arm (Rule 12): the "NestedDef" case here is exactly the
        // shape that regresses to a silent `0` when DefiniteAssignmentAnalysis.
        // CollectNestedDefDeferredReads's `if (node is FunctionDef nestedDef)` arm is neutralized —
        // see the mutation record in this file's landing commit body.
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            $"{kind}: a deferred read of a local never assigned anywhere in the enclosing function must be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            $"{kind}: expected SPY0600 for a never-assigned outer local");
    }

    public static IEnumerable<object[]> DeferredReadSite_AssignedLater_Cases => new[]
    {
        new object[] { "Lambda", @"
def main() -> None:
    x: int
    f = lambda: x
    x = 5
    print(f())
" },
        new object[] { "NestedDef", @"
def outer() -> None:
    x: int
    def inner() -> None:
        print(x)
    x = 5
    inner()

def main() -> None:
    outer()
" },
        new object[] { "LambdaInNestedDef", @"
def outer() -> None:
    x: int
    def inner() -> None:
        f = lambda: x
        print(f())
    x = 5
    inner()

def main() -> None:
    outer()
" },
        new object[] { "NestedDefInNestedDef", @"
def outer() -> None:
    x: int
    def middle() -> None:
        def inner() -> None:
            print(x)
        inner()
    x = 5
    middle()

def main() -> None:
    outer()
" },
    };

    [Theory]
    [MemberData(nameof(DeferredReadSite_AssignedLater_Cases))]
    public void DeferredReadSite_AssignedLater_Succeeds(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            $"{kind}: the closure runs after the later assignment, so the read is definitely assigned");
        result.Success.Should().BeTrue($"{kind}: {string.Join("; ", result.CompilationErrors)}");
        result.StandardOutput.Trim().Should().Be("5");
    }

    // "assigned in one branch": #1635's rule credits a deferred read against "assigned ANYWHERE in
    // the function", so a local assigned only inside an untaken if-branch still compiles — but at
    // RUNTIME, on the branch that skipped the assignment, the bare declaration's definite
    // initializer supplies the type's default (`0` for int), so the call silently prints the
    // default instead of refusing. python3 raises UnboundLocalError here instead; this divergence
    // is the deliberate, already-shipped shape of the "assigned anywhere" heuristic (#1635), not a
    // new defect — these cells pin it for the nested-def and lambda-in-nested-def read sites too.
    public static IEnumerable<object[]> DeferredReadSite_AssignedInOneBranch_Cases => new[]
    {
        new object[] { "Lambda", @"
def helper(flag: bool) -> None:
    x: int
    f = lambda: x
    if flag:
        x = 5
    print(f())

def main() -> None:
    helper(False)
" },
        new object[] { "NestedDef", @"
def outer(flag: bool) -> None:
    x: int
    def inner() -> None:
        print(x)
    if flag:
        x = 5
    inner()

def main() -> None:
    outer(False)
" },
        new object[] { "LambdaInNestedDef", @"
def outer(flag: bool) -> None:
    x: int
    def inner() -> None:
        f = lambda: x
        print(f())
    if flag:
        x = 5
    inner()

def main() -> None:
    outer(False)
" },
        new object[] { "NestedDefInNestedDef", @"
def outer(flag: bool) -> None:
    x: int
    def middle() -> None:
        def inner() -> None:
            print(x)
        inner()
    if flag:
        x = 5
    middle()

def main() -> None:
    outer(False)
" },
    };

    [Theory]
    [MemberData(nameof(DeferredReadSite_AssignedInOneBranch_Cases))]
    public void DeferredReadSite_AssignedInOneBranch_AcceptedPrintsDefault(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            $"{kind}: assigned in even one branch satisfies the 'assigned anywhere' rule");
        result.Success.Should().BeTrue($"{kind}: {string.Join("; ", result.CompilationErrors)}");
        result.StandardOutput.Trim().Should().Be("0",
            $"{kind}: the untaken branch leaves x at its bare declaration's definite-initializer default");
    }

    public static IEnumerable<object[]> FlowPositionedReadSite_NeverAssigned_Cases => new[]
    {
        new object[] { "Comprehension", @"
def main() -> None:
    x: int
    y: list[int] = [x for i in range(3)]
    print(y)
" },
        new object[] { "GeneratorExpression", @"
def main() -> None:
    x: int
    y = list(x for i in range(3))
    print(y)
" },
    };

    [Theory]
    [MemberData(nameof(FlowPositionedReadSite_NeverAssigned_Cases))]
    public void FlowPositionedReadSite_NeverAssigned_ProducesSPY0600(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            $"{kind}: a never-assigned local read at its own flow position must be refused");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            $"{kind}: expected SPY0600");
    }

    // "assigned later" for a flow-positioned site means the assignment appears in source AFTER the
    // read (unlike the deferred sites above, where "later" means after the closure's own creation
    // but still before its eventual call) — the read still runs at its own textual position,
    // strictly before the assignment, so it is refused exactly like "never" (measured @ c7ade6be2,
    // da05/da06).
    public static IEnumerable<object[]> FlowPositionedReadSite_AssignedAfterRead_Cases => new[]
    {
        new object[] { "Comprehension", @"
def main() -> None:
    x: int
    y: list[int] = [x for i in range(3)]
    x = 5
    print(y)
" },
        new object[] { "GeneratorExpression", @"
def main() -> None:
    x: int
    y = list(x for i in range(3))
    x = 5
    print(y)
" },
    };

    [Theory]
    [MemberData(nameof(FlowPositionedReadSite_AssignedAfterRead_Cases))]
    public void FlowPositionedReadSite_AssignedAfterRead_ProducesSPY0600(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"{kind}: the read precedes the assignment in flow position");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            $"{kind}: expected SPY0600");
    }

    public static IEnumerable<object[]> FlowPositionedReadSite_AssignedInOneBranch_Cases => new[]
    {
        new object[] { "Comprehension", @"
def main(flag: bool) -> None:
    x: int
    if flag:
        x = 5
    y: list[int] = [x for i in range(3)]
    print(y)
" },
        new object[] { "GeneratorExpression", @"
def main(flag: bool) -> None:
    x: int
    if flag:
        x = 5
    y = list(x for i in range(3))
    print(y)
" },
    };

    [Theory]
    [MemberData(nameof(FlowPositionedReadSite_AssignedInOneBranch_Cases))]
    public void FlowPositionedReadSite_AssignedInOneBranch_ProducesSPY0600(string kind, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            $"{kind}: the merge after the if does not carry a one-branch assignment");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"),
            $"{kind}: expected SPY0600");
    }

    // --- defer (feature-flagged, #1023): flow-positioned at scope exit, a third hybrid shape ---

    [Fact]
    public void Defer_NeverAssigned_ProducesSPY0600()
    {
        var source = @"
def main() -> None:
    x: int
    defer print(x)
";
        var result = CompileAndExecute(source, features: FeatureFlags.None.Enable("defer"));
        result.Success.Should().BeFalse("a deferred body reading a never-assigned local must be refused");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"));
    }

    [Fact]
    public void Defer_AssignedLaterInScope_Succeeds()
    {
        // Matches variable_declaration.md's own `deferred()` example: the defer body splices into
        // the CFG AFTER `x = 5`, regardless of where `defer` appears textually.
        var source = @"
def main() -> None:
    x: int
    defer print(x)
    x = 5
";
        var result = CompileAndExecute(source, features: FeatureFlags.None.Enable("defer"));
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("5");
    }

    [Fact]
    public void Defer_AssignedInOneBranch_ProducesSPY0600()
    {
        // Unlike the deferred sites' one-branch cell, defer is NOT judged by "assigned anywhere" —
        // it is spliced in once, after the if's merge block, so a one-branch assignment does not
        // reach it.
        var source = @"
def main(flag: bool) -> None:
    x: int
    defer print(x)
    if flag:
        x = 5
    print(""done"")
";
        var result = CompileAndExecute(source, features: FeatureFlags.None.Enable("defer"));
        result.Success.Should().BeFalse(
            "the defer chain splices in AFTER the if-merge, which does not carry a one-branch assignment");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"));
    }

    // --- shadowing dimension: a deferred site's OWN binding sharing a name with an outer bare local ---

    [Fact]
    public void LambdaOwnParameter_ShadowsOuterUnassigned_Accepted()
    {
        // #1910 (drained): the deferred-read collection now judges by SCOPE, not by name. The
        // lambda's OWN parameter `y` shadows the outer never-assigned `y`, so the `y` read inside
        // the lambda body is the parameter, not the outer local — python3 prints 9 (measured).
        // Before the fix this was refused with SPY0600: the shadow set was seeded from a nested
        // def's parameters and VariableDeclarations only, and nothing bound a LAMBDA's parameters
        // (the old doc comment claimed LambdaExpression.GetChildNodes excluding them from traversal
        // made a shadow set unnecessary — that excludes them from being READ, not from BINDING).
        var source = @"
def main() -> None:
    y: int
    f = lambda y: int: y
    print(f(9))
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("9");
    }

    [Fact]
    public void NestedDef_OwnLocalDeclarationShadowsOuterUnassigned_Accepted()
    {
        // Positive control locking the P6.1 shadow-guard regression fix (3fa30defb): `inner`
        // declares its OWN `x: int = 99`, shadowing the outer never-assigned `x` for the rest of
        // inner's body. CollectDeferredReads's `shadowed` set excludes it from the enclosing
        // deferred read — before that guard existed, adding the FunctionDef arm newly, wrongly,
        // refused this program (a real regression p6-analysis caught while fixing #1681). This was
        // one of the two binding forms the original two-source seeding covered; every other form is
        // exercised by the ScopeBindingForm_* cells below (#1910).
        var source = @"
def outer() -> None:
    x: int
    def inner() -> None:
        x: int = 99
        print(x)
    inner()

def main() -> None:
    outer()
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("99");
    }

    // --- write-through dimension: an assignment inside ONE nested def, read by a SIBLING (#1681 follow-up) ---

    [Fact]
    public void NestedDefWriteThrough_SiblingNestedDefReads_Accepted()
    {
        // Regression p6-analysis caught via the whole-solution gate (BlockScopeRedeclarationMatrixTests.
        // UseBeforeAssignInSibling_SPY0600(nested-def), red @ 4a1305ea9): inner1's `x = 1` has no local
        // re-declaration of `x`, so it WRITES THROUGH to the outer bare local (C# closure semantics,
        // Axiom 1 — the owner's write-through ruling; see BlockScopeRedeclarationMatrixTests.
        // OuterDeclaredReassignInside_WritesThrough). CollectNestedDefDeferredReads made inner2's read
        // visible to DA, but assignedAnywhere was built only from assignedInBlock (main's own CFG
        // blocks) — a nested def's write-through assignment is exactly as CFG-invisible as its reads,
        // for the same reason (ControlFlowGraphBuilder never walks a FunctionDef's body). Without
        // CollectNestedDefWriteThroughs this refuses with SPY0600 instead of printing 1.
        var source = @"
def main() -> None:
    x: int
    def inner1() -> None:
        x = 1
    inner1()
    def inner2() -> None:
        print(x)
    inner2()
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            "inner1's write-through assignment satisfies the 'assigned anywhere' rule for inner2's read");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("1");
    }

    [Fact]
    public void NestedDefWriteThrough_ShadowedBySiblingsOwnDeclaration_StaysRefused()
    {
        // Negative control pairing the accepted cell above (Rule 12: non-vacuous positive control):
        // inner1 declares its OWN `x: int` before assigning it, so `x = 1` binds inner1's local, NOT
        // the outer — CollectWriteThroughs's shadow set (seeded the same way as CollectDeferredReads's)
        // must exclude it from assignedAnywhere. The outer x is still never assigned anywhere, so
        // inner2's read of it must still be genuinely refused.
        var source = @"
def main() -> None:
    x: int
    def inner1() -> None:
        x: int
        x = 1
    inner1()
    def inner2() -> None:
        print(x)
    inner2()
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "inner1's x is its OWN shadowed local — its assignment does not write through to the outer x");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'x'"));
    }

    // ========================================================================================= //
    // BINDING-FORM axis (#1910). The shadowing dimension used to have exactly two cells — a nested
    // def's PARAMETER and its own DECLARATION — because those were exactly the two forms the code
    // seeded its shadow set from. Every OTHER form a deferred body introduces was missed, so a read
    // of the body's OWN binding was matched BY NAME against an outer never-assigned bare local and
    // refused with SPY0600 on a program python3 runs.
    //
    // The axis is (binding form) × (host that admits it), where a host is a scope whose reads are
    // not flow-positioned at their own source location: a nested def, a lambda body, a top-level
    // comprehension, a top-level generator expression. Each live cell must COMPILE AND PRINT the
    // value python3 prints (verified with python3 before the fix; the `python` column of each cell
    // is that measured value). Positive controls per host follow: the outer read of the
    // never-assigned name is still SPY0600, so no cell passes by the analysis going silent.
    // ========================================================================================= //

    public static IEnumerable<object[]> ScopeBindingFormCases => new[]
    {
        // --- host: nested def ---
        new object[] { "for-target-simple", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        total: int = 0
        for k in [1, 2, 3]:
            total = total + k
        return total
    print(inner())
", "6" },
        new object[] { "for-target-tuple", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        total: int = 0
        for k, v in [(1, 10), (2, 20)]:
            total = total + k + v
        return total
    print(inner())
", "33" },
        new object[] { "for-target-starred", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        total: int = 0
        for k, *rest in [(1, 10, 100)]:
            total = total + k + len(rest)
        return total
    print(inner())
", "3" },
        new object[] { "list-comp-target", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> list[int]:
        return [k * 2 for k in [1, 2, 3]]
    print(inner())
", "[2, 4, 6]" },
        new object[] { "dict-comp-target", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> dict[int, int]:
        return {k: k * 2 for k in [1, 2]}
    print(inner())
", "{1: 2, 2: 4}" },
        new object[] { "set-comp-target", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        s: set[int] = {k * 2 for k in [1, 2]}
        return len(s)
    print(inner())
", "2" },
        new object[] { "genexp-target", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        return sum(k for k in [1, 2, 3])
    print(inner())
", "6" },
        new object[] { "match-capture", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        v: int = 7
        match v:
            case k:
                return k
    print(inner())
", "7" },
        new object[] { "class-pattern-capture", "nested-def", @"
class Point:
    x: int
    y: int
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main() -> None:
    k: int
    def inner() -> int:
        p: Point = Point(3, 4)
        match p:
            case Point(x=k):
                return k
            case _:
                return 0
    print(inner())
", "3" },
        new object[] { "lambda-parameter", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        f = lambda k: int: k + 1
        return f(8)
    print(inner())
", "9" },
        new object[] { "with-as-target", "nested-def", @"
class Tracker:
    label: str
    def __init__(self, label: str):
        self.label = label
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    k: Tracker
    def inner() -> str:
        with Tracker(""a"") as k:
            return k.label
    print(inner())
", "a" },
        new object[] { "except-as-name", "nested-def", @"
def main() -> None:
    k: ValueError
    def inner() -> str:
        try:
            raise ValueError(""boom"")
        except ValueError as k:
            return str(k)
    print(inner())
", "boom" },
        new object[] { "walrus-target", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        if (k := 5) > 0:
            return k
        return 0
    print(inner())
", "5" },
        // The two forms the pre-#1910 two-source seeding already covered, kept as controls: they
        // must stay green, so a regression that empties the shadow set fails here too.
        new object[] { "nested-def-parameter (control)", "nested-def", @"
def main() -> None:
    k: int
    def inner(k: int) -> int:
        return k + 1
    print(inner(8))
", "9" },
        new object[] { "own-declaration (control)", "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        k: int = 99
        return k
    print(inner())
", "99" },

        // --- host: lambda body (a single expression, so only the forms an expression can hold) ---
        new object[] { "list-comp-target", "lambda-body", @"
def main() -> None:
    k: int
    f = lambda: [k * 2 for k in [1, 2, 3]]
    print(f())
", "[2, 4, 6]" },
        new object[] { "dict-comp-target", "lambda-body", @"
def main() -> None:
    k: int
    f = lambda: {k: k * 2 for k in [1, 2]}
    print(f())
", "{1: 2, 2: 4}" },
        new object[] { "set-comp-target", "lambda-body", @"
def main() -> None:
    k: int
    f = lambda: len({k * 2 for k in [1, 2]})
    print(f())
", "2" },
        new object[] { "genexp-target", "lambda-body", @"
def main() -> None:
    k: int
    f = lambda: sum(k for k in [1, 2, 3])
    print(f())
", "6" },
        new object[] { "lambda-parameter", "lambda-body", @"
def main() -> None:
    k: int
    g = lambda k: int: k + 1
    f = lambda: g(8)
    print(f())
", "9" },

        // --- host: top-level comprehension / generator expression (flow-positioned reads) ---
        new object[] { "list-comp-target", "top-level-comprehension", @"
def main() -> None:
    k: int
    print([k * 2 for k in [1, 2, 3]])
", "[2, 4, 6]" },
        new object[] { "dict-comp-target", "top-level-comprehension", @"
def main() -> None:
    k: int
    print({k: k * 2 for k in [1, 2]})
", "{1: 2, 2: 4}" },
        new object[] { "set-comp-target", "top-level-comprehension", @"
def main() -> None:
    k: int
    s: set[int] = {k * 2 for k in [1, 2]}
    print(len(s))
", "2" },
        new object[] { "genexp-target", "top-level-genexp", @"
def main() -> None:
    k: int
    print(sum(k for k in [1, 2, 3]))
", "6" },
    };

    [Theory]
    [MemberData(nameof(ScopeBindingFormCases))]
    public void ScopeBindingForm_ShadowsOuterUnassigned_RunsAndPrintsPythonsValue(
        string form, string host, string source, string expected)
    {
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600",
            $"{form} in {host}: the read is the host's OWN binding, not the outer bare local");
        result.Success.Should().BeTrue(
            $"{form} in {host}: {string.Join("; ", result.CompilationErrors)}");
        result.StandardOutput.Trim().Should().Be(expected,
            $"{form} in {host}: must print what python3 prints");
    }

    /// <summary>
    /// The cell count is anchored to LITERALS written here, not to a count taken from the case
    /// source itself: every form name and every host name below is spelled out, and the product of
    /// the admitted pairs is asserted against the data. Dropping a cell from
    /// <see cref="ScopeBindingFormCases"/> fails this, so the axis cannot quietly shrink back to
    /// the two cells it had before #1910.
    /// </summary>
    [Fact]
    public void ScopeBindingFormCases_CoverEveryFormHostPairTheHostAdmits()
    {
        var expectedPairs = new HashSet<string>
        {
            "for-target-simple|nested-def",
            "for-target-tuple|nested-def",
            "for-target-starred|nested-def",
            "list-comp-target|nested-def",
            "dict-comp-target|nested-def",
            "set-comp-target|nested-def",
            "genexp-target|nested-def",
            "match-capture|nested-def",
            "class-pattern-capture|nested-def",
            "lambda-parameter|nested-def",
            "with-as-target|nested-def",
            "except-as-name|nested-def",
            "walrus-target|nested-def",
            "nested-def-parameter (control)|nested-def",
            "own-declaration (control)|nested-def",
            "list-comp-target|lambda-body",
            "dict-comp-target|lambda-body",
            "set-comp-target|lambda-body",
            "genexp-target|lambda-body",
            "lambda-parameter|lambda-body",
            "list-comp-target|top-level-comprehension",
            "dict-comp-target|top-level-comprehension",
            "set-comp-target|top-level-comprehension",
            "genexp-target|top-level-genexp",
        };

        var actualPairs = ScopeBindingFormCases
            .Select(c => $"{(string)c[0]}|{(string)c[1]}")
            .ToHashSet();

        actualPairs.Should().BeEquivalentTo(expectedPairs);
        expectedPairs.Count.Should().Be(24, "the axis is 24 live cells; shrinking it needs a reason");
    }

    public static IEnumerable<object[]> ScopeBindingFormPositiveControls => new[]
    {
        new object[] { "nested-def", @"
def main() -> None:
    k: int
    def inner() -> int:
        return k
    print(inner())
" },
        new object[] { "lambda-body", @"
def main() -> None:
    k: int
    f = lambda: k
    print(f())
" },
        new object[] { "top-level-comprehension", @"
def main() -> None:
    k: int
    print([k for i in range(3)])
" },
        // The comprehension's OUTERMOST iterable is evaluated in the enclosing scope (python3), so
        // a clause target does NOT shadow it — this stays a genuine read of the outer local even
        // though the same comprehension binds a name.
        new object[] { "nested-def-comprehension-iterator", @"
def main() -> None:
    k: list[int]
    def inner() -> list[int]:
        return [1 for i in k]
    print(inner())
" },
    };

    [Theory]
    [MemberData(nameof(ScopeBindingFormPositiveControls))]
    public void ScopeBindingForm_GenuineOuterRead_StaysRefused(string host, string source)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            $"{host}: the read is of the outer never-assigned local, which python3 raises on");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'k'"),
            $"{host}: expected SPY0600");
    }

    // --- write-through mirror: the shadow roster is shared, so these move together ---

    [Fact]
    public void NestedDefForTarget_IsNotAWriteThrough_SiblingReadStaysRefused()
    {
        // A nested def's `for k in …` declares the loop variable; it does not write through to the
        // enclosing bare local (python3 agrees: NameError in inner2). Refused at 41dd19de7 and at
        // HEAD; the enlarged shadow roster must keep it refused.
        var source = @"
def main() -> None:
    k: int
    def inner1() -> None:
        for k in [1, 2, 3]:
            pass
    inner1()
    def inner2() -> None:
        print(k)
    inner2()
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("a for target binds the loop variable, not the outer local");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'k'"));
    }

    [Fact]
    public void AssignmentInsideNestedDefForBody_IsNotAWriteThrough_SiblingReadRefused()
    {
        // Inside the loop body `k` IS the loop variable, so `k = 9` assigns it and never reaches the
        // enclosing local — python3 raises UnboundLocalError in inner2. Before the binding-form
        // roster this compiled and printed 0 (the bare declaration's default), a silent wrong
        // answer; the for target now shadows its own body, so it is refused instead.
        var source = @"
def main() -> None:
    k: int
    def inner1() -> None:
        for k in [1, 2, 3]:
            k = 9
    inner1()
    def inner2() -> None:
        print(k)
    inner2()
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            "`k = 9` inside the loop body assigns the loop variable, not the enclosing local");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0600" && d.Message.Contains("'k'"));
    }

    [Fact]
    public void AssignmentAfterNestedDefForLoop_StaysAWriteThrough_SiblingReadPrintsIt()
    {
        // The counterpart that makes the cell above non-vacuous, and the reason each binding form is
        // scoped to its own SUB-TREE rather than flat over the def body: `k = 5` is OUTSIDE the
        // loop, where the loop variable is out of scope (C# scoping, Axiom 1), so it still writes
        // through to the enclosing local. Seeding the shadow set flat over the whole body would
        // refuse this — the same class of false refusal #1910 is about. Runs and prints 5 at HEAD
        // 322fbd20e (measured); python3 diverges here by design (the write-through ruling).
        var source = @"
def main() -> None:
    k: int
    def inner1() -> None:
        for k in [1, 2, 3]:
            pass
        k = 5
    inner1()
    def inner2() -> None:
        print(k)
    inner2()
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0600");
        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("5");
    }
}
