extern alias SharpyRT;

using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The ONE classifier for the reservation of the four builtin tagged-union case names
/// (<c>Some</c>, <c>None</c>, <c>Ok</c>, <c>Err</c>) and the refusal of every QUALIFIED spelling of
/// a builtin case. Keyed on the builtin union's symbol IDENTITY — the registry's <c>Optional</c> /
/// <c>Result</c> symbols by their CLR type — NEVER on the receiver's spelling or NAME, so an aliased
/// (<c>O.Some</c>), indexed (<c>Optional[int].Some</c>) or module-qualified
/// (<c>sharpy.Optional.Some</c>) receiver reaches the same verdict as the bare <c>Optional.Some</c>
/// (#1856, R-S).
/// </summary>
/// <remarks>
/// A name-keyed test missed every spelling but the bare one: an alias symbol is named <c>O</c>, and a
/// constructed reference <c>Optional[int]</c> is an <c>IndexAccess</c> whose denoted type carries the
/// registry symbol, not a name the caller could switch on. The two consumers — the qualified-call /
/// value-position refusal at the member seam (SPY0608) and the union-case declaration refusal
/// (SPY0212, via <see cref="BuiltinNameShadowing"/>) — both read this classifier so the reservation
/// is decided in one place.
/// </remarks>
internal static class BuiltinUnionCaseReservation
{
    /// <summary>The four builtin tagged-union case names, reserved in every namespace.</summary>
    private static readonly HashSet<string> ReservedCaseNames =
        new(StringComparer.Ordinal) { "Some", "None", "Ok", "Err" };

    /// <summary>True when <paramref name="name"/> is one of the four reserved builtin case names.</summary>
    public static bool IsReservedCaseName(string name) => ReservedCaseNames.Contains(name);

    /// <summary>
    /// The four CLR types that ARE the builtin <c>Optional</c>/<c>Result</c> unions: the generic
    /// value types <c>Optional&lt;&gt;</c>/<c>Result&lt;,&gt;</c> a bare spelling binds, and the
    /// non-generic static factory classes <c>Optional</c>/<c>Result</c> an aliased or module-qualified
    /// import binds (<c>from sharpy import Optional</c> resolves to the factory class that carries the
    /// <c>Some</c>/<c>None</c>/<c>Ok</c>/<c>Err</c> members). All four must match, or the aliased and
    /// module-qualified spellings escape the reservation (#1856, R-S).
    /// </summary>
    private static readonly HashSet<System.Type> BuiltinUnionClrTypes = new()
    {
        typeof(SharpyRT::Sharpy.Optional<>),
        typeof(SharpyRT::Sharpy.Optional),
        typeof(SharpyRT::Sharpy.Result<,>),
        typeof(SharpyRT::Sharpy.Result),
    };

    /// <summary>
    /// True when <paramref name="sym"/> IS the builtin <c>Optional</c> or <c>Result</c> union —
    /// identity through the reflected CLR type of the registry's own builtin, NEVER the symbol's
    /// name. Every alias or module re-export of them is the same reflected type carried on a renamed
    /// <c>with</c>-copy, so it answers true here too. A user type merely spelling the name carries no
    /// <c>ClrType</c>, so it is not matched; that spelling is refused at its declaration instead
    /// (SPY0212). Keying on the CLR type rather than
    /// <see cref="BuiltinRegistry.IsBuiltinSymbol"/> is deliberate: a re-export copy is not the
    /// registry's own instance, so an identity-by-instance test would miss the aliased spellings the
    /// name-keyed check already missed (#1856, R-S).
    /// </summary>
    public static bool IsBuiltinUnionSymbol(TypeSymbol sym) =>
        sym.ClrType is { } clrType && BuiltinUnionClrTypes.Contains(clrType);
}
