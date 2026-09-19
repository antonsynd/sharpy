using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The shared "not iterable" refusal (SPY0320), lifted out of <c>ProtocolValidator</c> so the
/// constructor ring can report the SAME wording for the SAME question (#1868, Design Decision 4):
/// "does this argument's type answer <c>__iter__</c>?" A construction site names the ARGUMENT
/// POSITION (<paramref name="siteNoun"/>, e.g. <c>"list() argument"</c>); a <c>for</c>/comprehension
/// site names nothing extra — both read the same base sentence
/// <c>ProtocolValidator.ValidateIteration</c> has always used, so no existing <c>.error</c> fixture
/// asserting that substring is disturbed.
/// </summary>
internal static class NotIterableDiagnostic
{
    public const string Code = DiagnosticCodes.Semantic.ProtocolMissingMethod;

    public static string Message(SemanticType type, string? siteNoun = null)
    {
        var noun = string.IsNullOrEmpty(siteNoun) ? "" : $" (the {siteNoun})";
        return $"Type '{type.GetDisplayName()}' is not iterable (missing '__iter__' method){noun}.";
    }
}
