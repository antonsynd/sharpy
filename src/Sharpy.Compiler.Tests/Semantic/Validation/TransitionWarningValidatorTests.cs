using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Unit coverage for <see cref="TransitionWarningValidator"/>'s SPY0479 <c>to</c>→<c>as?</c>/<c>as!</c>
/// migration hint. After <c>failable_cast</c> graduated (#1096) the hint is default-on: it fires on
/// every legacy <c>to</c>/<c>to?</c> cast even with no experimental features enabled. Before
/// graduation it was gated on the flag; these tests are the regression guard for that flip.
/// </summary>
public class TransitionWarningValidatorTests
{
    private static (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // SemanticContext.Features defaults to FeatureFlags.None — i.e. failable_cast NOT enabled.
        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void ToCast_WithNoFeaturesEnabled_EmitsSpy0479AtHintSeverity()
    {
        // #1096: SPY0479 is default-on after graduation. A plain `to` cast emits the hint with no
        // experimental features enabled — the pre-graduation feature guard is gone.
        var code = @"
def main() -> None:
    x: object = 42
    y = x to int
    print(y)
";
        var (module, context) = Parse(code);
        Assert.False(context.Features.IsEnabled("failable_cast"));

        new TransitionWarningValidator().Validate(module, context);

        var hint = Assert.Single(
            context.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.ToCastTransitionHint);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hint.Severity);
        Assert.Contains("as! int", hint.Message);
    }

    [Fact]
    public void ToNullableCast_WithNoFeaturesEnabled_SuggestsQuestionForm()
    {
        // The null-failure `to T?` form maps to the `as? T` suggestion.
        var code = @"
def main() -> None:
    x: object = 42
    y = x to int?
    print(y)
";
        var (module, context) = Parse(code);

        new TransitionWarningValidator().Validate(module, context);

        var hint = Assert.Single(
            context.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.ToCastTransitionHint);
        Assert.Equal(CompilerDiagnosticSeverity.Hint, hint.Severity);
        Assert.Contains("as? int", hint.Message);
    }

    [Fact]
    public void AsCast_EmitsNoSpy0479()
    {
        // The `as` forms are already the primary syntax — no migration hint.
        var code = @"
def main() -> None:
    x: object = 42
    y = x as! int
    print(y)
";
        var (module, context) = Parse(code);

        new TransitionWarningValidator().Validate(module, context);

        Assert.DoesNotContain(
            context.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.ToCastTransitionHint);
    }
}
