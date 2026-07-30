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
/// #1164: a nested generic type reference (<c>Outer.Inner[int]</c>) is resolved by the
/// GenericReferenceResolver into a <see cref="GenericReferenceKind.NestedTypeRef"/> fact, so codegen
/// lowers it from the fact instead of walking the symbol table itself at emit time. These tests read
/// <see cref="SemanticInfo.GetGenericReference"/> directly: the fact's presence, target symbol and
/// type arguments are the contract the emitter's arm consumes.
/// </summary>
public class NestedGenericReferenceFactTests
{
    private static (Module module, SemanticInfo info) Analyze(string source)
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

        return (module, semanticInfo);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    /// <summary>The single <c>qualifier.member[...]</c> index access in the analyzed source.</summary>
    private static IndexAccess MemberQualifiedIndexAccess(Module module) =>
        Descendants(module).OfType<IndexAccess>().Single(ia => ia.Object is MemberAccess);

    [Fact]
    public void NestedGenericTypeReference_RecordsNestedTypeRefFact()
    {
        var source = @"
class Registry:
    @public
    class Entry[T]:
        value: T

        def __init__(self, value: T):
            self.value = value

def use() -> None:
    e: Registry.Entry[int] = Registry.Entry[int](5)
";
        var (module, info) = Analyze(source);

        var reference = info.GetGenericReference(MemberQualifiedIndexAccess(module));

        reference.Should().NotBeNull("Outer.Inner[int] is a resolved generic reference (#1164)");
        reference!.Kind.Should().Be(GenericReferenceKind.NestedTypeRef);
        reference.TargetSymbol.Should().BeOfType<TypeSymbol>()
            .Which.Name.Should().Be("Entry", "the fact carries the nested type the emitter constructs");
        reference.TypeArgs.Should().ContainSingle().Which.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void NestedGenericTypeReference_MultipleTypeArguments_RecordsAllArgs()
    {
        var source = @"
class Registry:
    @public
    class Pair[K, V]:
        key: K
        value: V

        def __init__(self, key: K, value: V):
            self.key = key
            self.value = value

def use() -> None:
    p: Registry.Pair[int, str] = Registry.Pair[int, str](1, ""a"")
";
        var (module, info) = Analyze(source);

        var reference = info.GetGenericReference(MemberQualifiedIndexAccess(module));

        reference.Should().NotBeNull();
        reference!.Kind.Should().Be(GenericReferenceKind.NestedTypeRef);
        reference.TypeArgs.Should().HaveCount(2);
        reference.TypeArgs[0].Should().Be(SemanticType.Int);
        reference.TypeArgs[1].Should().Be(SemanticType.Str);
    }

    [Fact]
    public void TwoLevelNestedGenericTypeReference_RecordsNestedTypeRefFact()
    {
        var source = @"
class A:
    @public
    class B:
        @public
        class C[T]:
            value: T

            def __init__(self, value: T):
                self.value = value

def use() -> None:
    c: A.B.C[int] = A.B.C[int](5)
";
        var (module, info) = Analyze(source);

        // A.B.C[int]: the index access's object is the whole A.B.C member-access chain.
        var indexAccess = Descendants(module).OfType<IndexAccess>().Single(ia => ia.Object is MemberAccess);
        var reference = info.GetGenericReference(indexAccess);

        reference.Should().NotBeNull("the resolver walks the whole member-access chain");
        reference!.Kind.Should().Be(GenericReferenceKind.NestedTypeRef);
        reference.TargetSymbol.Should().BeOfType<TypeSymbol>().Which.Name.Should().Be("C");
    }

    [Fact]
    public void NonGenericNestedType_Indexed_RecordsNoFact()
    {
        // Registry.Entry is not generic, so `[0]` is ordinary value indexing, not type arguments:
        // the resolver must decline (and must not resolve the index as a type).
        var source = @"
class Registry:
    @public
    class Entry:
        value: int

        def __init__(self, value: int):
            self.value = value

def use(e: Registry.Entry) -> None:
    x = e.value
";
        var (module, info) = Analyze(source);

        foreach (var indexAccess in Descendants(module).OfType<IndexAccess>())
            info.GetGenericReference(indexAccess).Should().BeNull();
    }

    [Fact]
    public void InstanceGenericMethodReference_IsNotNestedTypeRef()
    {
        // The member-qualified arms are mutually exclusive: an instance receiver stays InstanceMethod.
        var source = @"
class Box:
    def __init__(self):
        pass

    def convert[T](self, value: T) -> str:
        return str(value)

def use() -> None:
    b: Box = Box()
    s: str = b.convert[int](5)
";
        var (module, info) = Analyze(source);

        var reference = info.GetGenericReference(MemberQualifiedIndexAccess(module));

        reference.Should().NotBeNull();
        reference!.Kind.Should().Be(GenericReferenceKind.InstanceMethod);
    }
}
