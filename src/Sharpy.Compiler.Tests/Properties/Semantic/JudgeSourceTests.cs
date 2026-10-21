using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// <see cref="ErrorInjector.JudgeSource"/> is the one refusal verdict of the typed-generator lanes
/// (#2288); these facts pin each arm on literal programs whose diagnostics were measured with the
/// built <c>sharpyc emit diagnostics</c> / <c>run</c> @ 8e0791f85: <see cref="NonExhaustiveMatch"/> draws the
/// SPY0463 warning alone and runs, <see cref="NonExhaustiveMatchAndError"/> SPY0220 + SPY0463,
/// <see cref="ErrorProgram"/> SPY0220 alone, <see cref="CleanProgram"/> nothing. Expected codes are
/// literal strings — a renumbering of <c>DiagnosticCodes</c> must redden them.
/// </summary>
public class JudgeSourceTests
{
    // Match STATEMENT on bool missing `case False` in a `-> None` body: warning SPY0463, no error.
    private const string NonExhaustiveMatch = """
        def f(x: bool) -> None:
            match x:
                case True:
                    print("yes")

        def main():
            f(True)
        """;

    // The same warning beside an error: the warning arm must refuse to call this a SPY0463 catch.
    private const string NonExhaustiveMatchAndError = """
        def f(x: bool) -> None:
            match x:
                case True:
                    print("yes")

        def main():
            let n: int = "x"
            print(n)
            f(True)
        """;

    // Two match statements on bool, each missing a case: two SPY0463 warnings, no error.
    private const string TwoNonExhaustiveMatches = """
        def f(x: bool) -> None:
            match x:
                case True:
                    print("yes")

        def g(x: bool) -> None:
            match x:
                case False:
                    print("no")

        def main():
            f(True)
            g(False)
        """;

    // The warning beside an unrelated one (SPY0450, a statement after `return`): no error.
    private const string NonExhaustiveMatchAndUnreachable = """
        def f(x: bool) -> None:
            match x:
                case True:
                    print("yes")

        def main():
            f(True)
            return
            print("never")
        """;

    private const string ErrorProgram = """
        def main():
            let n: int = "x"
            print(n)
        """;

    private const string CleanProgram = """
        def main():
            let n: int = 1
            print(n)
        """;

    private static readonly ErrorInjector.KnownRedRefusal Pin =
        new("probe-pin", "probe pin for the JudgeSource arms — never a real cell, so it cites no issue");

    // (a) the warning arm accepts exactly one warning of the code with no error.
    [Fact]
    public void WarningArm_SoleExpectedWarning_IsAccepted()
    {
        Assert.Null(ErrorInjector.JudgeSource(
            NonExhaustiveMatch, "SPY0463", CompilerDiagnosticSeverity.Warning, null, "match statement"));
    }

    // (a) zero errors is part of the warning arm: the same warning beside SPY0220 is not a catch.
    [Fact]
    public void WarningArm_ExpectedWarningBesideAnError_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            NonExhaustiveMatchAndError, "SPY0463", CompilerDiagnosticSeverity.Warning, null, "match statement");
        Assert.NotNull(failure);
        Assert.Contains("errors [SPY0220], warnings [SPY0463]", failure);
    }

    // (a) exactly ONE warning diagnostic (P63 Design Decision 6): two non-exhaustive matches are two
    // SPY0463 warnings — one distinct code, but two broken facts, not the one the injection made.
    [Fact]
    public void WarningArm_TwoOfTheExpectedWarning_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            TwoNonExhaustiveMatches, "SPY0463", CompilerDiagnosticSeverity.Warning, null, "match statement");
        Assert.NotNull(failure);
        Assert.Contains("errors [nothing], warnings [SPY0463] (2 warning diagnostic(s))", failure);
    }

    // (a) an unrelated warning beside the expected one is not a catch either.
    [Fact]
    public void WarningArm_ExpectedWarningBesideAnUnrelatedWarning_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            NonExhaustiveMatchAndUnreachable, "SPY0463", CompilerDiagnosticSeverity.Warning, null, "match statement");
        Assert.NotNull(failure);
        Assert.Contains("errors [nothing], warnings [SPY0450, SPY0463] (2 warning diagnostic(s))", failure);
    }

    // (b) the error arm never reads warnings: the warning program drew no error.
    [Fact]
    public void ErrorArm_WarningOnlyProgram_DrewNothing()
    {
        var failure = ErrorInjector.JudgeSource(
            NonExhaustiveMatch, "SPY0463", CompilerDiagnosticSeverity.Error, null, "match statement");
        Assert.NotNull(failure);
        Assert.Contains("must draw exactly SPY0463, drew [nothing]", failure);
    }

    // (c) the error arm: the exact code passes, any other code fails.
    [Fact]
    public void ErrorArm_ExactCode_IsAccepted()
    {
        Assert.Null(ErrorInjector.JudgeSource(
            ErrorProgram, "SPY0220", CompilerDiagnosticSeverity.Error, null, "type mismatch"));
    }

    [Fact]
    public void ErrorArm_OtherCode_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            ErrorProgram, "SPY0222", CompilerDiagnosticSeverity.Error, null, "type mismatch");
        Assert.NotNull(failure);
        Assert.Contains("must draw exactly SPY0222, drew [SPY0220]", failure);
    }

    // (c) an error is not a warning: the warning arm does not read the error list as its own.
    [Fact]
    public void WarningArm_ErrorProgram_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            ErrorProgram, "SPY0220", CompilerDiagnosticSeverity.Warning, null, "type mismatch");
        Assert.NotNull(failure);
        Assert.Contains("errors [SPY0220], warnings [nothing]", failure);
    }

    // (d) a pin requires the program to still analyse clean — a refusal fails the pin.
    [Fact]
    public void ErrorPin_CleanProgram_IsAccepted()
    {
        Assert.Null(ErrorInjector.JudgeSource(
            CleanProgram, "SPY0220", CompilerDiagnosticSeverity.Error, Pin, "pinned"));
    }

    [Fact]
    public void ErrorPin_RefusedProgram_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            ErrorProgram, "SPY0220", CompilerDiagnosticSeverity.Error, Pin, "pinned");
        Assert.NotNull(failure);
        Assert.Contains("is now refused [SPY0220]", failure);
    }

    // (d) a pinned warning lane: no error and no warning of the code.
    [Fact]
    public void WarningPin_CleanProgram_IsAccepted()
    {
        Assert.Null(ErrorInjector.JudgeSource(
            CleanProgram, "SPY0463", CompilerDiagnosticSeverity.Warning, Pin, "pinned warning"));
    }

    [Fact]
    public void WarningPin_ProgramDrawingTheWarning_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            NonExhaustiveMatch, "SPY0463", CompilerDiagnosticSeverity.Warning, Pin, "pinned warning");
        Assert.NotNull(failure);
        Assert.Contains("is now refused errors [nothing], warnings [SPY0463]", failure);
    }

    [Fact]
    public void WarningPin_ProgramDrawingAnError_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            ErrorProgram, "SPY0463", CompilerDiagnosticSeverity.Warning, Pin, "pinned warning");
        Assert.NotNull(failure);
        Assert.Contains("is now refused errors [SPY0220]", failure);
    }

    // A refusal lane is judged at Error or Warning only.
    [Fact]
    public void InfoSeverity_IsNotARefusalLane()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorInjector.JudgeSource(
            CleanProgram, "SPY1000", CompilerDiagnosticSeverity.Info, null, "info"));
    }

    // (e) Judge(InjectionResult) unparses and delegates — with the result's severity.
    [Fact]
    public void Judge_DelegatesAtErrorSeverityByDefault()
    {
        var injection = new ErrorInjector.InjectionResult(ParseModule(ErrorProgram), "SPY0220");
        Assert.Null(ErrorInjector.Judge(injection, "type mismatch"));
        Assert.Contains("drew [SPY0220]",
            ErrorInjector.Judge(injection with { ExpectedDiagnosticCode = "SPY0222" }, "type mismatch"));
    }

    [Fact]
    public void Judge_DelegatesWithTheInjectionsSeverity()
    {
        var injection = new ErrorInjector.InjectionResult(
            ParseModule(NonExhaustiveMatch), "SPY0463", Severity: CompilerDiagnosticSeverity.Warning);
        Assert.Null(ErrorInjector.Judge(injection, "match statement"));
        Assert.Contains("drew [nothing]",
            ErrorInjector.Judge(injection with { Severity = CompilerDiagnosticSeverity.Error }, "match statement"));
    }

    private static Sharpy.Compiler.Parser.Ast.Module ParseModule(string source)
    {
        var tokens = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
        return new Sharpy.Compiler.Parser.Parser(tokens).ParseModule();
    }
}
