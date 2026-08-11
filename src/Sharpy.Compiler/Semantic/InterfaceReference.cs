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

    /// <summary>
    /// Concrete type arguments after type resolution. For source-declared interfaces these
    /// are resolved from <see cref="TypeArgAnnotations"/>; for CLR-discovered types they are
    /// populated directly during discovery (e.g., <c>TypeParameterType</c> instances mapping
    /// the implementing type's own type parameters to the interface's positions, so
    /// <c>list[T0]</c> implements <c>IEnumerable[T0]</c>).
    /// Empty for non-generic interfaces or when resolution has not yet run.
    /// </summary>
    public ImmutableArray<SemanticType> ResolvedTypeArguments { get; init; }
        = ImmutableArray<SemanticType>.Empty;

    /// <summary>
    /// The base-list annotation this reference was built from, when there was one. Set only on
    /// the source-declared path (<c>NameResolver.ResolveClassInheritance</c> and friends); null
    /// for references synthesized by discovery, by the module loader, or by the symbol cache.
    ///
    /// <para>Carries the identity that lets a later pass record the reference's COMPLETED
    /// arguments against the node the emitter reads (#1404): <see cref="TypeArgAnnotations"/> may
    /// have grown a PEP-696 default fill that the AST node does not spell, and
    /// <c>SemanticInfo</c> is keyed by reference, so the two must be paired by identity rather
    /// than by name or by position in the base list.</para>
    /// </summary>
    public TypeAnnotation? SourceAnnotation { get; init; }
}

/// <summary>
/// Pairs a base class definition (TypeSymbol) with its concrete type arguments from the source.
/// Mirrors <see cref="InterfaceReference"/>: two-channel — <see cref="TypeArgAnnotations"/>
/// (raw AST, source-declared) + <see cref="ResolvedTypeArguments"/> (SemanticTypes, populated
/// by discovery for CLR types). The walker reads resolved-first (#1287).
/// </summary>
public record BaseTypeReference
{
    public TypeSymbol Definition { get; init; } = null!;
    public ImmutableArray<TypeAnnotation> TypeArgAnnotations { get; init; }
        = ImmutableArray<TypeAnnotation>.Empty;

    /// <summary>
    /// Concrete type arguments after type resolution. For source-declared base classes these
    /// are resolved from <see cref="TypeArgAnnotations"/>; for CLR-discovered types they are
    /// populated directly during discovery.
    /// Empty for non-generic base classes or when resolution has not yet run.
    /// </summary>
    public ImmutableArray<SemanticType> ResolvedTypeArguments { get; init; }
        = ImmutableArray<SemanticType>.Empty;

    /// <summary>
    /// The base-list annotation this reference was built from, when there was one — the base-class
    /// twin of <see cref="InterfaceReference.SourceAnnotation"/>, with the same contract (#1404).
    /// </summary>
    public TypeAnnotation? SourceAnnotation { get; init; }
}
