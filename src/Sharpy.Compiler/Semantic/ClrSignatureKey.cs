using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The ONE CLR-mapped signature key (plan-499995 contract (ii), #1721): the identity two overloads
/// have in the emitted C#, built from resolved types. Two Sharpy signatures that are distinct at
/// the annotation level (<c>SignatureKey</c>, contract (i)) may still map to ONE C# parameter
/// list — the C# compiler then refuses the pair with CS0111 (duplicate member) behind SPY0908, or
/// every call to it with CS0121 (ambiguous). This key erases exactly what C# erases, so the
/// validator can refuse such a pair by name (SPY0701) before codegen:
/// <list type="bullet">
/// <item><c>tuple[a: int, b: str]</c> and <c>tuple[int, str]</c> — tuple element names are
/// metadata on <c>(int a, string b)</c>, not part of the signature.</item>
/// <item><c>str | None</c> and <c>str</c> — a nullable REFERENCE type is an annotation
/// (<c>string?</c>), not a distinct type; a nullable VALUE type (<c>int | None</c> →
/// <c>Nullable&lt;int&gt;</c>) stays distinct.</item>
/// <item><c>LiteralString</c> and <c>str</c> — both emit <c>string</c>.</item>
/// </list>
/// The erasure recurses through every wrapper leaf (generic arguments, Optional/Result payloads,
/// function parameter and return types, tuple elements, Task results), so
/// <c>list[str | None]</c> collides with <c>list[str]</c> the way C# says it does. Every other
/// leaf keys as its <see cref="SemanticType.CanonicalKey"/>, which is already module-qualified
/// for user types (two same-named classes from two modules are two C# types).
/// <see cref="ClrSignatureKeyTotalityTests"/> anchors the leaf roster to the literal 20 and
/// exercises every erasing arm with a pair whose canonical keys differ.
/// </summary>
internal static class ClrSignatureKey
{
    /// <summary>
    /// The C# parameter-type list of <paramref name="func"/> as a key, omitting the receiver
    /// (<c>self</c>/<c>cls</c>) the way the emitted method does.
    /// </summary>
    internal static string Of(FunctionSymbol func)
    {
        var paramTypes = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
            .Select(p => OfType(p.Type));
        return string.Join(",", paramTypes);
    }

    /// <summary>
    /// The CLR identity of one type: <see cref="SemanticType.CanonicalKey"/> with the C#-invisible
    /// distinctions erased, recursively.
    /// </summary>
    internal static string OfType(SemanticType type)
    {
        return type switch
        {
            // Element names are tuple metadata, never part of a C# signature.
            TupleType tuple =>
                $"tuple[{string.Join(",", tuple.ElementTypes.Select(OfType))}]",

            // `T | None` on a reference type is the `T?` nullable ANNOTATION — same signature as `T`.
            // On a value type it is Nullable<T>, a distinct type.
            NullableType nullable => nullable.UnderlyingType.IsValueType
                ? $"{OfType(nullable.UnderlyingType)}|None"
                : OfType(nullable.UnderlyingType),

            // LiteralString emits as string (#1731: a full type in every position, one CLR type).
            LiteralStringType => SemanticType.Str.CanonicalKey,

            OptionalType optional => $"{OfType(optional.UnderlyingType)}?",
            ResultType result => $"{OfType(result.OkType)}!{OfType(result.ErrorType)}",
            GenericType generic =>
                $"{generic.Name}[{string.Join(",", generic.TypeArguments.Select(OfType))}]",
            // A callable maps to Func<…>/Action<…>: parameter and return types only; the
            // semantic-only flags (optional count, variadic index, skip-validation) do not exist
            // on the delegate.
            FunctionType function =>
                $"({string.Join(",", function.ParameterTypes.Select(OfType))})->{OfType(function.ReturnType)}",
            TaskType task => task.ResultType != null ? $"Task[{OfType(task.ResultType)}]" : "Task",

            // Every other leaf is its canonical identity: builtins (aliases already share a key —
            // float/double are one float64), module-qualified user types, unions (one generated
            // type per union symbol), type parameters, Self, and the non-signature carriers.
            _ => type.CanonicalKey,
        };
    }
}
