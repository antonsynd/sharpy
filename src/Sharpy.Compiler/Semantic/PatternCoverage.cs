namespace Sharpy.Compiler.Semantic;

/// <summary>
/// How much of the scrutinee's static type a class-pattern head (or the <c>case None:</c> arm)
/// covers (P13 Design Decision 8). Recorded per pattern node by the type checker; absence from the
/// coverage table means "partial" (the head refutes on a value or on part of the type). The
/// distinction is null-aware: a <c>NullableType</c> scrutinee is never <see cref="Total"/> for a
/// non-nullable test type, because the <c>None</c> value escapes the type test (#1891).
/// </summary>
public enum PatternCoverage
{
    /// <summary>
    /// The scrutinee's WHOLE static type is assignable to the test type — the head matches every
    /// value. Only a total head is irrefutable (<c>ExhaustivenessHelper.IsTotal</c>).
    /// </summary>
    Total,

    /// <summary>
    /// The scrutinee is <c>T | None</c> and only the underlying payload <c>T</c> is assignable to the
    /// test type — <c>case list():</c> over <c>list[int] | None</c> covers the payload case of the
    /// finite family, leaving <c>None</c> to a later arm.
    /// </summary>
    PayloadTotal,

    /// <summary>
    /// The <c>case None:</c> arm over a <c>NullableType</c> scrutinee — it covers the <c>None</c> case
    /// of the finite family.
    /// </summary>
    NoneArm,
}
