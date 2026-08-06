using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The one place that decides whether a declaration may use the bare spelling of a builtin name,
/// and the one place that words the refusal (SPY0212).
/// </summary>
/// <remarks>
/// <para>The rule: the bare spelling of a builtin name is <em>always</em> the builtin — types and
/// functions uniformly — so a user symbol wanting that spelling must be backtick-escaped. This
/// extends the rule hard keywords already follow (bare <c>class</c> is the keyword, <c>`class`</c>
/// is your symbol) into the builtin namespace, where it had never applied because builtin names are
/// ordinary identifiers resolved through a registry rather than reserved words.</para>
///
/// <para><strong>Why refuse rather than honour the binding.</strong> Axiom 3 over Axiom 2. Sharpy
/// resolves <c>x: int</c> statically, so a rebindable <c>int</c> makes annotation position
/// ambiguous in a way it is not in CPython, where annotations are not a static resolution surface.
/// CPython's late-binding behaviour becomes a catalogued deviation
/// (<c>builtin-name-shadowing-refused</c>) rather than a hidden one.</para>
///
/// <para><strong>Why the refusal sits at the binding.</strong> Reads need no diagnostic and no
/// resolution change, because once a colliding binding cannot exist bare, every bare read is
/// unambiguously the builtin. That is what makes the rule cheap: one check at declarations instead
/// of a resolution rule threaded through every use.</para>
///
/// <para>Fires only for a bare spelling. A backtick-escaped declaration is exactly the sanctioned
/// escape and must flow through untouched — which is why the escape had to be repaired first
/// (#1246, #1241): a rule may not demand an escape that does not work.</para>
/// </remarks>
internal static class BuiltinNameShadowing
{
    /// <summary>
    /// True when this declaration must be refused: a bare spelling that collides with the builtin
    /// registry. An escaped spelling never collides — that is what the escape means.
    /// </summary>
    public static bool IsRefused(SymbolTable symbolTable, string name, bool isNameBacktickEscaped) =>
        !isNameBacktickEscaped && symbolTable.BuiltinRegistry.IsReservedBuiltinName(name);

    /// <summary>
    /// The refusal message. Names the escape explicitly: a diagnostic that refuses a spelling
    /// without showing the one-keystroke way to keep it is a worse trade than the bug it replaces.
    /// </summary>
    /// <remarks>
    /// Callers report this themselves rather than the helper doing it, because NameResolver and
    /// TypeChecker carry different phase and file-path plumbing. What must not diverge is the
    /// decision and the wording, and both live here.
    /// </remarks>
    public static string Message(string name) =>
        $"'{name}' is a builtin name; the bare spelling always refers to the builtin. "
        + $"To declare a user symbol with this spelling, write it backtick-escaped: `{name}`";

    /// <summary>The diagnostic code every caller must use for this refusal.</summary>
    public const string Code = DiagnosticCodes.Semantic.BuiltinNameShadowed;
}
