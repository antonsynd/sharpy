using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Pairs an interface definition (TypeSymbol) with its concrete type arguments from the source.
/// For example, <c>class Foo(IEquatable[str])</c> produces an InterfaceReference with
/// Definition = IEquatable's TypeSymbol and TypeArgAnnotations = [str].
/// </summary>
public record InterfaceReference
{
    public TypeSymbol Definition { get; init; } = null!;
    public ImmutableArray<TypeAnnotation> TypeArgAnnotations { get; init; }
        = ImmutableArray<TypeAnnotation>.Empty;
}
