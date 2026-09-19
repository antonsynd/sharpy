using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The ONE decider for "what is a producer's element type" (#1832, Design Decision 3): a producer
/// dunder — <c>__reversed__</c>, a non-generator <c>__iter__</c> — is written as either a GENERATOR
/// (the annotation IS the element, per <c>generators.md</c>) or a regular method returning a
/// PRODUCER TYPE (<c>Iterator[T]</c>, <c>IEnumerator[T]</c>, <c>IEnumerable[T]</c>) that must be
/// unpeeled to its element. Before this, each of synthesis and each route asked this question with
/// its own hand-rolled, narrower check — some special-cased only <c>Iterator[T]</c>, none unpeeled at
/// all for a non-generator <c>__iter__</c> — so a receiver's declared element silently became the
/// wrong thing (c02: <c>Iterator[int32]</c> instead of <c>int</c>) or was never recognized as
/// iterable at all (c03: a non-generator <c>__iter__</c> synthesized nothing).
///
/// <para>Two entry points exist — <see cref="UnpeelProducerAnnotation"/> over the AST,
/// <see cref="UnpeelProducerType"/> over the resolved <see cref="SemanticType"/> — because
/// synthesis runs before type checking (<see cref="SynthesisAnalyzer"/> reads
/// <see cref="TypeAnnotation"/>s during inheritance resolution) while the route consumers
/// (<see cref="TypeInferenceService.InferReversedElementType"/>,
/// <see cref="TypeInferenceService.InferIterableElementType"/>) ask about an already-resolved
/// <see cref="FunctionSymbol.ReturnType"/>. The roster is written once so the two entry points can
/// never drift apart (<see cref="IterableElementDeciderConformanceTests"/> asserts they agree).</para>
/// </summary>
internal static class IterableElementDecider
{
    /// <summary>
    /// Producer type names whose sole type argument IS the element, for a NON-generator body.
    /// <c>Iterator[T]</c> is Sharpy's own producer name; <c>IEnumerator[T]</c>/<c>IEnumerable[T]</c>
    /// are the CLR names a non-generator <c>__iter__</c>/<c>__reversed__</c> may also be spelled with.
    /// </summary>
    public static readonly string[] ProducerElementRoster =
    {
        BuiltinNames.Iterator, BuiltinNames.IEnumerator, BuiltinNames.IEnumerable,
    };

    /// <summary>
    /// The element a producer dunder's WRITTEN annotation denotes. A generator's annotation IS the
    /// element (spec — <c>generators.md</c> §Return Type Annotation): <c>-&gt; str: yield "a"</c>
    /// yields <c>str</c> directly, never <c>Iterator[str]</c>. A non-generator body instead RETURNS a
    /// producer object, so its annotation names the producer and must be unpeeled: an annotation
    /// whose name is on the roster with EXACTLY one type argument yields that argument; anything
    /// else (an unrecognized name, zero or more than one argument) is returned unchanged — the
    /// caller's existing "not the shape I expected" handling decides what that means.
    /// </summary>
    public static TypeAnnotation UnpeelProducerAnnotation(TypeAnnotation annotation, bool isGenerator)
    {
        if (isGenerator)
            return annotation;

        if (annotation.TypeArguments.Length == 1
            && Array.IndexOf(ProducerElementRoster, annotation.Name) >= 0)
        {
            return annotation.TypeArguments[0];
        }

        return annotation;
    }

    /// <summary>The resolved-type twin of <see cref="UnpeelProducerAnnotation"/>, over a
    /// <see cref="GenericType"/> instead of a <see cref="TypeAnnotation"/> — same rule, same
    /// roster, asked once the annotation has been resolved to a <see cref="SemanticType"/>.</summary>
    public static SemanticType UnpeelProducerType(SemanticType type, bool isGenerator)
    {
        if (isGenerator)
        {
            // An UNANNOTATED generator's resolved ReturnType is Void (TypeChecker.ResolveReturnType
            // defaults every unannotated function to Void — Unknown never survives that call), not
            // "object". SynthesisAnalyzer's own default for the SAME unannotated case is applied at
            // the AST level, before type checking (`?? new TypeAnnotation { Name = "object" }`), so
            // it never sees Void at all. Left unhandled here, a route consumer (list()/reversed())
            // would refuse a receiver whose interface synthesis already put
            // IEnumerable[object]/IReverseEnumerable[object] on it — the two entry points agreeing is
            // this decider's whole point, so Void (and Unknown, defensively) gets the same "object"
            // default synthesis already committed to.
            return type == SemanticType.Void || type == SemanticType.Unknown ? SemanticType.Object : type;
        }

        if (type is GenericType { TypeArguments.Count: 1 } generic
            && Array.IndexOf(ProducerElementRoster, generic.Name) >= 0)
        {
            return generic.TypeArguments[0];
        }

        return type;
    }

    /// <summary>
    /// The gate a caller checks BEFORE trusting a NON-generator body's <see cref="UnpeelProducerType"/>
    /// answer: whether the resolved return type actually NAMES a producer on
    /// <see cref="ProducerElementRoster"/>. Needed because <see cref="UnpeelProducerType"/>'s own
    /// "not recognized" case returns the type UNCHANGED — indistinguishable, by return value alone,
    /// from "this value already IS its element". Without the gate, a `__next__`-based enumerator's
    /// self-referential, non-generator `__iter__` (<c>-&gt; Counter: return self</c>, paired with
    /// `__next__`) was wrongly treated as producing a `Counter` element — that combo's element comes
    /// from `__next__`, not `__iter__`, and is not this decider's question at all.
    /// </summary>
    public static bool IsRecognizedProducerType(SemanticType type)
        => type is GenericType { TypeArguments.Count: 1 } generic
           && Array.IndexOf(ProducerElementRoster, generic.Name) >= 0;
}
