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
/// #1002: <c>SelectArityMatchingOverload</c> (the arity-search added for #999's explicit-generic
/// multi-arity <c>map</c>/<c>zip</c>) must only swap among genuine builtin overloads. A user-defined
/// generic that shadows a builtin name must keep its own symbol, never be silently replaced by a
/// same-name builtin overload of a different arity.
/// </summary>
public class ArityOverloadShadowingTests
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

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var d in Descendants(child))
                yield return d;
        }
    }

    private static IndexAccess? FindGenericRef(Module module, string name) =>
        Descendants(module).OfType<IndexAccess>()
            .FirstOrDefault(ia => ia.Object is Identifier id && id.Name == name);

    [Fact]
    public void UserShadowedGeneric_KeepsUserSymbol_OverSameNameBuiltin()
    {
        // `def map[T]` shadows the builtin map. An explicit-generic reference using the builtin's
        // type-arg arity (3) must still bind to the USER function (1 type parameter), not a builtin
        // overload of matching arity. Before #1002 the arity search returned the builtin overload.
        var source = @"
def map[T](x: T) -> T:
    return x

def use() -> None:
    f = map[int, int, int]
";
        var (_, symbols, _, diagnostics) = Analyze(source);

        var userMap = symbols.Lookup("map");
        userMap.Should().BeOfType<FunctionSymbol>();

        // #1004: the wrong type-arg count (3 against the user map's 1 type parameter) is now
        // diagnosed at the reference site. The message "expects 1 ..." also proves #1002's
        // invariant: the arity search kept the USER symbol (1 type parameter) rather than
        // swapping for a 3-parameter builtin overload (which would have said "expects 3").
        diagnostics.GetErrors().Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount
                 && d.Message.Contains("expects 1 type argument(s) but got 3"),
            "a user-shadowed generic keeps its own arity (#1002) and a wrong type-arg count is diagnosed (#1004)");
    }

    [Fact]
    public void BuiltinGeneric_StillSelectsArityMatchingOverload_NoRegression()
    {
        // No user shadow: map[int, int, int] must still resolve to the builtin overload whose
        // type-parameter count matches the supplied type args (the #999 behaviour this guard preserves).
        var source = @"
def use() -> None:
    f = map[int, int, int]
";
        var (module, _, info, diagnostics) = Analyze(source);

        var genericRef = FindGenericRef(module, "map");
        genericRef.Should().NotBeNull();

        var refType = info.GetExpressionType(genericRef!);
        refType.Should().BeOfType<GenericFunctionType>();
        ((GenericFunctionType)refType!).FunctionSymbol.TypeParameters.Count.Should().Be(3,
            "explicit-generic builtin map must select the arity-matching (3 type-parameter) overload (#999)");
        diagnostics.GetErrors().Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount,
            "a genuine arity-matching builtin reference must not be diagnosed (#1004 must not regress #999)");
    }

    // ── #1004: wrong explicit type-argument count on a generic function reference ──

    [Fact]
    public void BuiltinGeneric_ExcessTypeArgs_NoMatchingOverload_Diagnosed()
    {
        // map has overloads up to 4 type parameters; 5 type args match no overload. After the
        // arity search returns the fallback, the count mismatch must be diagnosed (#1004).
        var source = @"
def use() -> None:
    f = map[int, int, int, int, int]
";
        var (_, _, _, diagnostics) = Analyze(source);

        diagnostics.GetErrors().Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount
                 && d.Message.Contains("got 5"),
            "a builtin generic reference with a type-arg count matching no overload is diagnosed (#1004)");
    }

    [Fact]
    public void UserGeneric_TooFewTypeArgs_Diagnosed()
    {
        // The reference contract is exact: a user generic of arity 2 referenced with 1 type arg
        // is diagnosed (no partial-explicit-type-arg feature exists in the language).
        var source = @"
def pair[T, U](a: T, b: U) -> T:
    return a

def use() -> None:
    f = pair[int]
";
        var (_, _, _, diagnostics) = Analyze(source);

        diagnostics.GetErrors().Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount
                 && d.Message.Contains("expects 2 type argument(s) but got 1"),
            "too-few explicit type args on a generic reference is diagnosed (#1004)");
    }

    [Fact]
    public void UserGeneric_CorrectTypeArgs_NotDiagnosed()
    {
        // Exact arity binds cleanly with no diagnostic.
        var source = @"
def identity[T](x: T) -> T:
    return x

def use() -> None:
    f = identity[int]
";
        var (module, _, info, diagnostics) = Analyze(source);

        diagnostics.GetErrors().Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount,
            "a correct type-arg count must not be diagnosed (#1004)");

        var genericRef = FindGenericRef(module, "identity");
        info.GetExpressionType(genericRef!).Should().BeOfType<GenericFunctionType>(
            "a correct generic reference still records its GenericFunctionType");
    }
}
