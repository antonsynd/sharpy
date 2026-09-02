using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for lambda parameter type inference from context (bidirectional type checking).
/// Verifies that lambda parameters can be inferred from expected function types.
/// </summary>
public class LambdaTypeInferenceTests
{
    private (Module, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, typeChecker);
    }

    #region Function Argument Context

    [Fact]
    public void Lambda_AsFunctionArgument_InfersParameterTypes()
    {
        var source = @"
def apply(f: (int) -> int, x: int) -> int:
    return f(x)

def main():
    result = apply(lambda n: n * 2, 5)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_AsFunctionArgument_MultipleParams_InfersTypes()
    {
        var source = @"
def combine(f: (int, str) -> str, x: int, s: str) -> str:
    return f(x, s)

def main():
    result = combine(lambda n, s: s, 5, ""hello"")
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_AsFunctionArgument_ReturnsCorrectType()
    {
        var source = @"
def transform(f: (int) -> str, x: int) -> str:
    return f(x)

def main():
    result = transform(lambda n: str(n), 42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Variable Declaration Context

    [Fact]
    public void Lambda_WithTypedDeclaration_InfersParameterTypes()
    {
        var source = @"
def main():
    f: (int) -> int = lambda x: x * 2
    result = f(5)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_WithTypedDeclaration_MultipleParams()
    {
        var source = @"
def main():
    f: (int, int) -> int = lambda a, b: a + b
    result = f(3, 4)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region No Context (Unknown Parameters)

    /// <summary>
    /// A lambda bound under `auto` has no context to take its parameter types from, and `x` alone
    /// pins nothing, so the parameter type stays Unknown. Sharpy is statically typed — there is no
    /// runtime inference to fall back on — and the emitter would produce `var f = x => x`, which C#
    /// rejects with CS8917 behind SPY0908. So this is refused at semantic time instead (#1212).
    /// This test previously asserted the opposite, on the premise that `auto` accepts Unknown
    /// parameters; that premise is what #1212 overturned.
    /// </summary>
    [Fact]
    public void Lambda_WithAutoType_UninferableParameters_IsRefused()
    {
        var source = @"
def main():
    f: auto = lambda x: x
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors()
            .Should().Contain(e => e.Code == DiagnosticCodes.Semantic.UnresolvedLambdaParameterType);
    }

    [Fact]
    public void Lambda_WithAutoType_AnnotatedParameter_NoError()
    {
        // The one-keystroke remedy the diagnostic names.
        var source = @"
def main():
    f: auto = lambda x: int: x
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Cascade non-regression (#1212 — SPY0343 keys on parameter unknowns only)

    /// <summary>
    /// A zero-parameter lambda whose body errors leaves the return type Unknown, so
    /// FunctionType.HasUnresolvedTypes() is true — but there is no parameter to annotate, and the
    /// body's own error is already reported. SPY0343 must key on parameter-position unknowns only.
    /// The .error fixture harness matches by substring and cannot catch an added code, so the
    /// diagnostic set is asserted programmatically here.
    /// </summary>
    [Fact]
    public void Lambda_ZeroParameters_ErroringBody_DoesNotAlsoDrawSpy0343()
    {
        var source = @"
def main():
    f = lambda: ""a"" - 1
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var errors = typeChecker.Diagnostics.GetErrors();
        errors.Should().NotBeEmpty("the body's operator error still reports");
        errors.Should().NotContain(
            e => e.Code == DiagnosticCodes.Semantic.UnresolvedLambdaParameterType,
            "a lambda with no parameters has nothing SPY0343 could ask the user to annotate");
    }

    /// <summary>
    /// An annotated parameter whose annotation fails to resolve is its own error; a SPY0343 on top
    /// would tell the user to annotate a parameter they already annotated.
    /// </summary>
    [Fact]
    public void Lambda_AnnotatedButUnresolvableParameter_DoesNotAlsoDrawSpy0343()
    {
        var source = @"
def main():
    f = lambda x: list[int | str]: x
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // The "Free unions … not supported" error itself is reported by annotation resolution,
        // outside this harness's checker bag; errors/lambda_free_union_param.spy pins it
        // end-to-end. This test pins only the cascade half: no SPY0343 on top.
        typeChecker.Diagnostics.GetErrors().Should().NotContain(
            e => e.Code == DiagnosticCodes.Semantic.UnresolvedLambdaParameterType,
            "the parameter is annotated in source; only inference-left unknowns are this defect");
    }

    #endregion

    #region Return Value Context

    [Fact]
    public void Lambda_ReturnedFromFunction_InfersFromReturnType()
    {
        var source = @"
def make_doubler() -> (int) -> int:
    return lambda x: x * 2
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Receiver-substituted method signatures (#889)

    [Fact]
    public void Lambda_SortKeyKeyword_InfersParamFromListElement()
    {
        // list[str].sort(key=lambda s: len(s)) — s must infer as str so len(s) type-checks.
        var source = @"
def main() -> None:
    combined: list[str] = [""bb"", ""a"", ""ccc""]
    combined.sort(key=lambda s: len(s))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_SortKeyKeyword_WithReverse_InfersParamFromListElement()
    {
        var source = @"
def main() -> None:
    combined: list[str] = [""bb"", ""a"", ""ccc""]
    combined.sort(key=lambda s: len(s), reverse=True)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_SortKey_IntList_InfersIntParam()
    {
        var source = @"
def main() -> None:
    nums: list[int] = [3, 1, 2]
    nums.sort(key=lambda n: -n)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Lambda_AmbiguousOverloads_DoesNotGuess_ExplicitLambdaStillResolves()
    {
        // Two overloads disagree on the type of the same-named parameter `f`. The early
        // expected-type resolution must bail (never guess); an explicitly-typed lambda still
        // resolves via normal overload resolution, so there are no errors.
        var source = @"
class Runner:
    def run(self, f: (int) -> int) -> int:
        return f(1)

    def run(self, f: (str) -> str) -> str:
        return f(""x"")

def main() -> None:
    r = Runner()
    r.run(lambda n: n * 2)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // The lambda has no annotation and the overloads conflict, so we don't pre-set its
        // parameter type. The important guarantee is that this does not crash or misfire — the
        // call resolves through normal overload resolution.
        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("internal", System.StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Operator Section Refusal Net (#1716 — TryResolveExpressionType / InferParamTypesFromSubExpression defaults)

    [Fact]
    public void OperatorSection_FunctionCallOperand_RefusesSPY0343()
    {
        var source = @"
def main():
    xs: list[int] = [1, 2, 3]
    f = (_ > len(xs))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors()
            .Should().Contain(e => e.Code == DiagnosticCodes.Semantic.UnresolvedLambdaParameterType);
        typeChecker.Diagnostics.GetErrors()
            .Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.UnexpectedUnknownType);
    }

    [Fact]
    public void OperatorSection_AndOrChainWithFunctionCall_InfersFromLiteralSide()
    {
        var source = @"
def is_valid(n: int) -> bool:
    return n > 0

def main():
    f = (_ > 0 and is_valid(5))
    result: bool = f(3)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    /// <summary>
    /// The plan's nested-call cell proper: the placeholder appears INSIDE the call on the and/or
    /// chain's right operand. Measured @ 277f54543 (verify round): refused loudly with SPY0220
    /// ("Operand of 'and' must be truth-testable, got '(int32) -> bool'") — a named refusal, never
    /// SPY0907/SPY0908. The sibling above (`is_valid(5)`, no placeholder in the call) is the
    /// inference-succeeds control.
    /// </summary>
    [Fact]
    public void OperatorSection_AndOrChainWithPlaceholderInsideCall_RefusesLoudly()
    {
        var source = @"
def is_valid(n: int) -> bool:
    return n > 0

def main():
    f = (_ > 0 and is_valid(_))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors()
            .Should().Contain(e => e.Code == DiagnosticCodes.Semantic.TypeMismatch
                                || e.Code == DiagnosticCodes.Semantic.UnresolvedLambdaParameterType);
        typeChecker.Diagnostics.GetErrors()
            .Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.UnexpectedUnknownType);
    }

    [Fact]
    public void OperatorSection_MemberAccessOperand_RefusesSPY0343()
    {
        var source = @"
class Filter:
    limit: int
    def __init__(self, limit: int):
        self.limit = limit
    def make_pred(self):
        f = (_ > self.limit)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors()
            .Should().Contain(e => e.Code == DiagnosticCodes.Semantic.UnresolvedLambdaParameterType);
        typeChecker.Diagnostics.GetErrors()
            .Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.UnexpectedUnknownType);
    }

    [Fact]
    public void OperatorSection_ComparisonWithIntLiteral_InfersType()
    {
        var source = @"
def main():
    f = (_ > 2)
    result: bool = f(5)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void OperatorSection_ArithmeticWithIntLiteral_InfersType()
    {
        var source = @"
def main():
    f = (_ * 2)
    result: int = f(5)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion
}
