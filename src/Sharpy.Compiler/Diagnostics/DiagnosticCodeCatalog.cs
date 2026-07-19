using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Reflection-derived catalog of every allocated SPYxxxx diagnostic code, plus the
/// classification of which codes may be silenced with <c>@suppress</c>.
///
/// Diagnostic severity is chosen explicitly at emission (via AddError/AddWarning/AddHint)
/// and is never stored on the code constant, so suppressibility here is determined from the
/// code's numeric sub-band — the range boundaries documented in <see cref="DiagnosticCodes"/>
/// are the contract. Only Warning, Hint, and Info diagnostics are suppressible; errors never
/// are, regardless of their numeric position (e.g. the SPY0490-0499 error overflow).
/// </summary>
internal static class DiagnosticCodeCatalog
{
    private static readonly Regex CodePattern = new("^SPY[0-9]{4}$", RegexOptions.Compiled);

    /// <summary>
    /// All diagnostic-code string constants declared under the nested classes of
    /// <see cref="DiagnosticCodes"/>. Built once at type initialization and read-only after
    /// (typed as <see cref="IReadOnlySet{T}"/> so the reference cannot hand out a mutable set).
    /// </summary>
    public static readonly IReadOnlySet<string> KnownCodes = BuildKnownCodes();

    private static IReadOnlySet<string> BuildKnownCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nested in typeof(DiagnosticCodes).GetNestedTypes())
        {
            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string)
                    && field.GetRawConstantValue() is string value)
                {
                    codes.Add(value);
                }
            }
        }

        return codes;
    }

    /// <summary>True when <paramref name="code"/> has the canonical <c>SPYnnnn</c> shape.</summary>
    public static bool IsWellFormed(string? code) => code != null && CodePattern.IsMatch(code);

    /// <summary>True when <paramref name="code"/> is an allocated diagnostic code.</summary>
    public static bool IsKnown(string? code) => code != null && KnownCodes.Contains(code);

    /// <summary>
    /// True when a diagnostic with this code is emitted at Warning, Hint, or Info severity and
    /// can therefore be silenced with <c>@suppress</c>. Classification is by numeric sub-band
    /// (see <see cref="DiagnosticCodes"/>):
    /// <list type="bullet">
    ///   <item>SPY0450-SPY0489 → validation Warning/Hint (and warning overflow) — suppressible</item>
    ///   <item>SPY1000-SPY1099 → Info — suppressible</item>
    ///   <item>everything else → Error — never suppressible</item>
    /// </list>
    /// </summary>
    public static bool IsSuppressible(string? code)
    {
        if (!IsWellFormed(code))
            return false;

        // code is "SPYnnnn"; the four trailing digits parse cleanly given IsWellFormed above.
        var number = int.Parse(code!.Substring(3), CultureInfo.InvariantCulture);
        return (number >= 450 && number <= 489) || (number >= 1000 && number <= 1099);
    }
}
