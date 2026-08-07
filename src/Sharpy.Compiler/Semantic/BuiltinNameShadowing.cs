using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// What a declaration that spells a builtin name draws: nothing, a warning (SPY0483), or the
/// refusal (SPY0212).
/// </summary>
internal enum BuiltinShadowVerdict
{
    /// <summary>Not a builtin spelling, or backtick-escaped — which is the sanctioned escape.</summary>
    Allowed,

    /// <summary>
    /// A value-position binding shadows a builtin. Legal and honoured; warned because the builtin
    /// becomes unreachable by its bare spelling for the rest of the scope.
    /// </summary>
    Warned,

    /// <summary>A type declaration shadows a builtin TYPE name. Refused.</summary>
    Refused,
}

/// <summary>
/// The one place that classifies a declaration spelling a builtin name, and the one place that
/// words what it draws.
/// </summary>
/// <remarks>
/// <para><strong>The line runs between namespaces, not between "bare" and "escaped".</strong> A
/// TYPE declaration enters the type namespace, which is the namespace annotations resolve through;
/// after <c>class double:</c> two different things answer to <c>double</c> in <c>x: double</c> and
/// Sharpy — which resolves annotations statically, unlike CPython — cannot leave that unresolved.
/// That is the whole of the refusal's justification, so that is the whole of its scope (SPY0212).</para>
///
/// <para>A binding in VALUE position never enters the type namespace and therefore cannot make an
/// annotation ambiguous. <c>def double(x: int) -&gt; int</c>, <c>def __init__(self, id: int)</c>,
/// <c>for int in ...</c>, <c>len = 5</c> are all legal and honoured: the binding shadows the
/// builtin, the way any inner binding shadows an outer one. They draw SPY0483 because something
/// real does happen — the builtin stops being reachable by its bare spelling for the rest of that
/// scope — but a warning is the honest severity for it. An earlier draft of this rule refused the
/// lot; it was justified by annotation safety and applied to bindings annotations never consult.</para>
///
/// <para>Both halves fire only for a bare spelling. A backtick-escaped declaration is exactly the
/// sanctioned escape and must flow through untouched — which is why the escape had to be repaired
/// first (#1246, #1241): a rule may not demand an escape that does not work.</para>
///
/// <para>A TYPE declaration spelling a builtin FUNCTION name (<c>class len:</c>) lands in
/// <see cref="BuiltinShadowVerdict.Warned"/>. It creates no annotation ambiguity — <c>len</c> was
/// never a type — so the refusal's justification does not reach it; but the class is unconstructible
/// by its bare spelling, so it is not silent either.</para>
///
/// <para>CPython honours the rebinding in every one of these positions, including the refused one.
/// That divergence is catalogued in <c>deviations.yaml</c> as
/// <c>builtin-type-name-shadowing-refused</c> rather than left implicit.</para>
/// </remarks>
internal static class BuiltinNameShadowing
{
    /// <summary>
    /// Classifies a declaration. <paramref name="isTypeDeclaration"/> is true for the forms that
    /// enter the type namespace: class, struct, interface, enum, union, delegate.
    /// </summary>
    public static BuiltinShadowVerdict Classify(
        SymbolTable symbolTable, string name, bool isNameBacktickEscaped, bool isTypeDeclaration) =>
        Classify(symbolTable.BuiltinRegistry, name, isNameBacktickEscaped, isTypeDeclaration);

    /// <inheritdoc cref="Classify(SymbolTable, string, bool, bool)"/>
    public static BuiltinShadowVerdict Classify(
        Registry.BuiltinRegistry registry, string name, bool isNameBacktickEscaped,
        bool isTypeDeclaration)
    {
        if (isNameBacktickEscaped)
            return BuiltinShadowVerdict.Allowed;

        if (isTypeDeclaration && registry.IsReservedBuiltinTypeName(name))
            return BuiltinShadowVerdict.Refused;

        return registry.IsReservedBuiltinName(name)
            ? BuiltinShadowVerdict.Warned
            : BuiltinShadowVerdict.Allowed;
    }

    /// <summary>
    /// The refusal message (SPY0212). Names the escape explicitly: a diagnostic that refuses a
    /// spelling without showing the one-keystroke way to keep it is a worse trade than the bug it
    /// replaces.
    /// </summary>
    /// <remarks>
    /// Callers report this themselves rather than the helper doing it, because NameResolver and
    /// TypeChecker carry different phase and file-path plumbing. What must not diverge is the
    /// decision and the wording, and both live here.
    /// </remarks>
    public static string RefusalMessage(string name) =>
        $"'{name}' is a builtin type name; a type declaration with this spelling would make "
        + $"annotation position ambiguous. To declare a user type with this spelling, write it "
        + $"backtick-escaped: `{name}`";

    /// <summary>
    /// True when binding <paramref name="name"/> would displace a builtin, in either namespace.
    /// The declaration-site classifier above answers a richer question (refuse vs warn vs allow);
    /// this is the plain "does this name belong to a builtin" predicate that the import path needs,
    /// stated here so both paths read the same registry rather than each keying off its own idea of
    /// what a builtin is.
    /// </summary>
    public static bool ShadowsBuiltin(Registry.BuiltinRegistry registry, string name) =>
        registry.IsReservedBuiltinName(name);

    /// <summary>The warning message (SPY0483).</summary>
    public static string WarningMessage(string name) =>
        $"'{name}' is a builtin name; this declaration shadows it, so the builtin is no longer "
        + $"reachable by its bare spelling in this scope. Write the declaration backtick-escaped "
        + $"(`{name}`) to keep both, or rename it";

    /// <summary>The diagnostic code for the refusal.</summary>
    public const string RefusalCode = DiagnosticCodes.Semantic.BuiltinNameShadowed;

    /// <summary>The diagnostic code for the value-position warning.</summary>
    public const string WarningCode = DiagnosticCodes.Validation.BuiltinNameShadowedInValuePosition;
}
