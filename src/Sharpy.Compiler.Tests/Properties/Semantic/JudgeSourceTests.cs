using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

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

    // The error arm's code beside a second error (SPY0200, an undefined name): two broken facts —
    // measured SPY0220 + SPY0200 with the built `sharpyc emit diagnostics` @ f10b8381f.
    private const string ErrorProgramAndUndefinedName = """
        def main():
            let n: int = "x"
            print(n)
            print(undefined_name)
        """;

    private const string CleanProgram = """
        def main():
            let n: int = 1
            print(n)
        """;

    private static readonly ErrorInjector.KnownRedRefusal Pin =
        new("probe-pin", "probe pin for the JudgeSource arms — never a real cell, so it cites no issue");

    // ---- the #2075 cascade pin (R-JE) ------------------------------------------------------------
    // The issue's own program, measured with the built sharpyc `emit diagnostics` @ a78e3aa71:
    // SPY0202 at the annotation + SPY0220 "If condition must be boolean, got 'int8'" at the condition.
    private const string AbsOfUnknownInCondition = """
        def compute(arg0: Nope_xyz) -> bool:
            if abs(arg0):
                arg0 = 15
            return False

        def main():
            print(1)
        """;

    // The second face of the same cell: abs(Unknown) stored back as int8, then met by an int in a
    // conditional expression — SPY0202 + SPY0227 (measured @ a78e3aa71; first seen at 2000 samples).
    private const string AbsOfUnknownBesideAnInt = """
        def compute(arg0: Nope_xyz) -> int:
            arg0 = abs(arg0)
            arg0 = 90 if False else arg0
            return 187

        def main():
            print(1)
        """;

    // The same cascade beside an unrelated third error (SPY0200): the pin tolerates only its own faces.
    private const string AbsOfUnknownInConditionAndUndefinedName = """
        def compute(arg0: Nope_xyz) -> bool:
            if abs(arg0):
                arg0 = 15
            return False

        def main():
            print(undefined_name)
        """;

    // The drain signal, per face: the first time a face stops cascading (the sentinel, P17 phase 1), this
    // fails and that face — or AbsOfUnknownCascade with its lane attachment — is dropped.
    [Theory]
    [InlineData(nameof(AbsOfUnknownInCondition), "SPY0220")]
    [InlineData(nameof(AbsOfUnknownBesideAnInt), "SPY0227")]
    public void CascadePin_EachFaceStillCascades(string program, string face)
    {
        var source = program == nameof(AbsOfUnknownInCondition) ? AbsOfUnknownInCondition : AbsOfUnknownBesideAnInt;
        Assert.Contains(face, ErrorInjector.AbsOfUnknownCascade.Tolerated);
        var unpinned = ErrorInjector.JudgeSource(
            source, "SPY0202", CompilerDiagnosticSeverity.Error, null, "undefined parameter type");
        Assert.True(unpinned is not null && unpinned.Contains($"drew [SPY0202, {face}]", StringComparison.Ordinal),
            $"{ErrorInjector.AbsOfUnknownCascade.Issue}'s abs(Unknown) {face} face no longer fires — drop it from "
            + $"ErrorInjector.AbsOfUnknownCascade (the pin itself when no face is left). Unpinned verdict: {unpinned ?? "accepted"}");
    }

    [Theory]
    [InlineData(nameof(AbsOfUnknownInCondition))]
    [InlineData(nameof(AbsOfUnknownBesideAnInt))]
    public void CascadePin_AcceptsTheExpectedCodePlusAToleratedFace(string program)
    {
        var source = program == nameof(AbsOfUnknownInCondition) ? AbsOfUnknownInCondition : AbsOfUnknownBesideAnInt;
        Assert.Null(ErrorInjector.JudgeSource(
            source, "SPY0202", CompilerDiagnosticSeverity.Error, null, "undefined parameter type",
            ErrorInjector.AbsOfUnknownCascade));
    }

    [Fact]
    public void CascadePin_RefusesAThirdCode()
    {
        var failure = ErrorInjector.JudgeSource(
            AbsOfUnknownInConditionAndUndefinedName, "SPY0202", CompilerDiagnosticSeverity.Error, null,
            "undefined parameter type", ErrorInjector.AbsOfUnknownCascade);
        Assert.NotNull(failure);
        Assert.Contains("drew [SPY0200, SPY0202, SPY0220]", failure);
    }

    // Positive control: the arm is keyed on the pinned code, not on "any second code".
    [Fact]
    public void CascadePin_ToleratesOnlyItsOwnCode()
    {
        var otherCode = new ErrorInjector.KnownRedCascade("probe-pin", new[] { "SPY0354" }, "probe — never a real cell");
        Assert.NotNull(ErrorInjector.JudgeSource(
            AbsOfUnknownInCondition, "SPY0202", CompilerDiagnosticSeverity.Error, null, "undefined parameter type", otherCode));
    }

    // The lane attaches the pin on the measured shape only — abs of the mutated parameter — and every
    // other sample of the lane is judged exactly.
    [Theory]
    [InlineData("def compute(arg0: int) -> bool:\n    if abs(arg0):\n        arg0 = 15\n    return False\n", true)]
    [InlineData("def compute(arg0: int) -> bool:\n    if arg0 > 0:\n        return True\n    return False\n", false)]
    [InlineData("def compute(arg0: int) -> bool:\n    if len(str(arg0)) > 0:\n        return True\n    return False\n", false)]
    public void CascadePin_IsAttachedOnlyOnTheAbsOfTheMutatedParameterShape(string baseSource, bool pinned)
    {
        var tokens = new LexerNs.Lexer(baseSource).TokenizeAll();
        var module = new ParserNs.Parser(tokens).ParseModule();
        var result = ErrorInjector.InjectUndefinedTypeAtParameter(module);
        Assert.NotNull(result);
        Assert.Equal(pinned, result!.KnownCascade is not null);
    }

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

    // (c) exactly ONE code on the error arm: the expected code beside a second error is not a catch —
    // the injection broke one fact and the program draws two.
    [Fact]
    public void ErrorArm_ExpectedCodeBesideASecondError_IsRefused()
    {
        var failure = ErrorInjector.JudgeSource(
            ErrorProgramAndUndefinedName, "SPY0220", CompilerDiagnosticSeverity.Error, null, "type mismatch");
        Assert.NotNull(failure);
        Assert.Contains("must draw exactly SPY0220, drew [SPY0200, SPY0220]", failure);
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
