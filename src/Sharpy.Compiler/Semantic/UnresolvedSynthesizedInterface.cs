using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// A dunder-synthesized interface row as it travels through the symbol cache (#1746): the CLR
/// interface's Sharpy name, the written type-argument annotations, and the dunder that produced
/// it. It becomes an <see cref="InterfaceReference"/> with <see cref="InterfaceReference.SynthesizedVia"/>
/// once <see cref="InheritanceResolver"/> re-resolves the definition on a warm build through
/// <see cref="SynthesisAnalyzer.ResolveInterfaceDefinition"/> — the rule the cold hoist used.
/// </summary>
public sealed record UnresolvedSynthesizedInterface(
    string InterfaceName,
    ImmutableArray<TypeAnnotation> TypeArgAnnotations,
    string SynthesizedVia);
