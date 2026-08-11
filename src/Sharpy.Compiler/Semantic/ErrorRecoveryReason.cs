namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Which of the three grounds an error-recovery mark stands on.
/// </summary>
internal enum ErrorRecoveryKind
{
    /// <summary>A diagnostic for this expression was reported earlier or elsewhere.</summary>
    AlreadyReported,

    /// <summary>
    /// The Unknown flowed in from a sub-expression or operand that carries its own reason; this
    /// expression is only inheriting it.
    /// </summary>
    Propagated,

    /// <summary>
    /// Nothing is reported and nothing should be: the reference is left on a permissive channel by
    /// design, because something downstream of the type checker resolves it.
    /// </summary>
    DeliberatelyPermissive,
}

/// <summary>
/// Why an expression is marked as error recovery. Required by
/// <c>TypeChecker.MarkExpressionAsErrorRecovery</c>, which is the whole point: the marker suppresses
/// SPY0907 for an Unknown-typed node, and "recovery claimed with nothing behind it" is how a spelling
/// reaches code generation unchecked and comes back as SPY0908 (#1344, #1389 both began that way).
/// The type makes that state unrepresentable — a mark must name its ground.
/// </summary>
/// <remarks>
/// Three grounds, not one, because they are genuinely different and a single "pass me the diagnostic"
/// signature would mislabel two of them. A caller with no diagnostic to hand would have to fabricate
/// one, which is worse than the silence it replaces:
/// <list type="bullet">
///   <item><see cref="AlreadyReported"/> — an error exists for this expression, raised elsewhere.</item>
///   <item><see cref="Propagated"/> — the operand's Unknown arrived here; the reason is the operand's.</item>
///   <item><see cref="DeliberatelyPermissive"/> — no error exists and none is wanted. This is the
///     class worth watching: it is the interop channel #1344 and #1389 each narrowed rather than
///     deleted, and the only one <c>MarkExpressionAsErrorRecovery</c> logs.</item>
/// </list>
/// A reference type, so the nullable-reference analysis refuses a caller that passes nothing —
/// a <c>default</c>-able struct would have let silence back in through the front door.
/// </remarks>
internal sealed class ErrorRecoveryReason
{
    private ErrorRecoveryReason(ErrorRecoveryKind kind, string explanation)
    {
        Kind = kind;
        Explanation = explanation;
    }

    public ErrorRecoveryKind Kind { get; }

    /// <summary>Prose for a reader of the call site and for the permissive-channel debug log.</summary>
    public string Explanation { get; }

    /// <summary>
    /// A diagnostic for this expression was already reported — by an earlier phase, by the statement
    /// that bound the name, or by the import that failed. <paramref name="reportedBy"/> says which.
    /// </summary>
    public static ErrorRecoveryReason AlreadyReported(string reportedBy) =>
        new(ErrorRecoveryKind.AlreadyReported, reportedBy);

    /// <summary>
    /// The Unknown came from <paramref name="source"/> — a sub-expression, an operand, an element
    /// type — which owns the reason. Nothing new is claimed here.
    /// </summary>
    public static ErrorRecoveryReason Propagated(string source) =>
        new(ErrorRecoveryKind.Propagated, source);

    /// <summary>
    /// No diagnostic exists and none is wanted, because <paramref name="why"/>. Every use is a
    /// standing decision to let a reference through unchecked, so every use should read like one.
    /// </summary>
    public static ErrorRecoveryReason DeliberatelyPermissive(string why) =>
        new(ErrorRecoveryKind.DeliberatelyPermissive, why);

    public override string ToString() => $"{Kind}: {Explanation}";
}
