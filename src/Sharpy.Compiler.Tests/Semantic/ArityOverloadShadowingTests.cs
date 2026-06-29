using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
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
    private static (Module module, SymbolTable symbols, SemanticInfo info) Analyze(string source)
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

        return (module, symbolTable, semanticInfo);
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
        var (module, symbols, info) = Analyze(source);

        var userMap = symbols.Lookup("map");
        userMap.Should().BeOfType<FunctionSymbol>();

        var genericRef = FindGenericRef(module, "map");
        genericRef.Should().NotBeNull();

        var refType = info.GetExpressionType(genericRef!);
        refType.Should().BeOfType<GenericFunctionType>();
        ((GenericFunctionType)refType!).FunctionSymbol.Should().BeSameAs(userMap,
            "a user-defined generic that shadows a builtin must keep its own symbol (#1002)");
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
        var (module, _, info) = Analyze(source);

        var genericRef = FindGenericRef(module, "map");
        genericRef.Should().NotBeNull();

        var refType = info.GetExpressionType(genericRef!);
        refType.Should().BeOfType<GenericFunctionType>();
        ((GenericFunctionType)refType!).FunctionSymbol.TypeParameters.Count.Should().Be(3,
            "explicit-generic builtin map must select the arity-matching (3 type-parameter) overload (#999)");
    }
}
