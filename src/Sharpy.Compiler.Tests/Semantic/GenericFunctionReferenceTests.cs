using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1138: an <b>uncalled</b> generic-function reference with explicit type arguments
/// (<c>f = identity[int]</c>, <c>f = b.convert[int]</c>) is an internal carrier, not a first-class
/// value. It must be called immediately; using it as a value (assign / pass / store / return) is a
/// semantic error (SPY0335) rather than a codegen internal error (CS0021 → SPY0908). The called forms
/// (<c>identity[int](5)</c>, parenthesized <c>(identity[int])(5)</c>, nested <c>outer(identity[int](5))</c>)
/// must still compile — the diagnostic keys off a call-callee marker at the CheckExpression choke point.
/// </summary>
public class GenericFunctionReferenceTests
{
    private static (Module module, SymbolTable symbols, SemanticInfo info, DiagnosticBag diagnostics) Analyze(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);

        return (module, symbolTable, semanticInfo, typeChecker.Diagnostics);
    }

    private static bool HasNotCalledError(DiagnosticBag diagnostics) =>
        diagnostics.GetErrors().Any(d => d.Code == DiagnosticCodes.Semantic.GenericFunctionReferenceNotCalled);

    // ── Arm A: bare identifier reference ──

    [Fact]
    public void BareIdentifierReference_Uncalled_Assigned_ErrorsSpy0335()
    {
        var source = @"
def identity[T](x: T) -> T:
    return x

def use() -> None:
    f = identity[int]
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeTrue(
            "an uncalled generic function reference assigned as a value is SPY0335");
    }

    // ── Arm C: instance-receiver reference ──

    [Fact]
    public void InstanceReceiverReference_Uncalled_Assigned_ErrorsSpy0335()
    {
        var source = @"
class Box:
    def __init__(self):
        pass

    def convert[T](self, value: T) -> str:
        return f""converted: {value}""

def use() -> None:
    b: Box = Box()
    f = b.convert[int]
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeTrue(
            "an uncalled generic instance-method reference assigned as a value is SPY0335");
    }

    // ── Value contexts other than assignment ──

    [Fact]
    public void Reference_InArgumentPosition_ErrorsSpy0335()
    {
        var source = @"
def identity[T](x: T) -> T:
    return x

def use() -> None:
    print(identity[int])
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeTrue(
            "a generic function reference passed as a call argument is SPY0335");
    }

    [Fact]
    public void Reference_InListLiteral_ErrorsSpy0335()
    {
        var source = @"
def identity[T](x: T) -> T:
    return x

def use() -> None:
    xs = [identity[int]]
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeTrue(
            "a generic function reference stored in a list literal is SPY0335");
    }

    [Fact]
    public void Reference_InReturnPosition_ErrorsSpy0335()
    {
        var source = @"
def identity[T](x: T) -> T:
    return x

def use():
    return identity[int]
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeTrue(
            "a generic function reference returned as a value is SPY0335");
    }

    // ── Correct-arity uncalled builtin: the slip-through the #1004 arity check does NOT catch ──

    [Fact]
    public void CorrectArityBuiltinReference_Uncalled_ErrorsSpy0335()
    {
        // map[int, int] matches the 2-type-parameter builtin overload (arity 2/3/4 exist), so the
        // #1004 wrong-count check passes and the reference resolves to a GenericFunctionType — yet
        // it is uncalled. Before #1138 this slipped past every diagnostic into codegen (SPY0908).
        var source = @"
def use() -> None:
    f = map[int, int]
";
        var (_, _, _, diagnostics) = Analyze(source);

        diagnostics.GetErrors().Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount,
            "map[int, int] is a correct-arity reference — the arity check must not fire (this is the slip-through case)");
        HasNotCalledError(diagnostics).Should().BeTrue(
            "a correct-arity uncalled builtin generic reference is SPY0335");
    }

    // ── Legal called forms must still compile ──

    [Fact]
    public void DirectCall_NoError()
    {
        var source = @"
def identity[T](x: T) -> T:
    return x

def use() -> None:
    y = identity[int](5)
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeFalse(
            "identity[int](5) is a legal immediate call, not a value use");
    }

    [Fact]
    public void ParenthesizedCall_NoError()
    {
        // (identity[int])(5): the callee is a Parenthesized wrapping the IndexAccess. The guard must
        // unwrap parentheses on BOTH the stored callee and the node under test to accept this.
        var source = @"
def identity[T](x: T) -> T:
    return x

def use() -> None:
    y = (identity[int])(5)
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeFalse(
            "(identity[int])(5) is a legal parenthesized call, not a value use");
    }

    [Fact]
    public void NestedCall_NoError()
    {
        // outer(identity[int](5)): the inner call is legal; save/restore of the callee marker must
        // let the inner IndexAccess be recognized as its own call's callee even while checking the
        // outer call's arguments.
        var source = @"
def identity[T](x: T) -> T:
    return x

def outer(x: int) -> int:
    return x

def use() -> None:
    y = outer(identity[int](5))
";
        var (_, _, _, diagnostics) = Analyze(source);

        HasNotCalledError(diagnostics).Should().BeFalse(
            "outer(identity[int](5)) nests two legal calls — neither is a value use");
    }
}
