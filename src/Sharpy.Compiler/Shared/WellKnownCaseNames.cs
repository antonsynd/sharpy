namespace Sharpy.Compiler.Shared;

/// <summary>
/// Constants for well-known case names used in finite type exhaustiveness checks.
/// Centralizes magic strings to prevent drift across the three sites that check
/// exhaustiveness (ExhaustivenessValidator, ControlFlowGraphBuilder, RoslynEmitter.Patterns).
/// </summary>
internal static class WellKnownCaseNames
{
    // ---- Optional ----
    public const string Some = "Some";
    public const string None = "None";

    // ---- Result ----
    public const string Ok = "Ok";
    public const string Err = "Err";

    // ---- Bool pattern names ----
    public const string True = "True";
    public const string False = "False";

    // ---- T | None finite family (P13 DD10) ----
    // The payload case of a `T | None` scrutinee whose payload is a CONCRETE (non-union) type: any
    // PayloadTotal/Total head over the nullable covers it. A sentinel, not a type name, so it cannot
    // collide with a real case name; the None case reuses <see cref="None"/>.
    public const string NullablePayload = "<payload>";
}
