using System.Globalization;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Classifies float literals by suffix and asks the ONE range question every float-literal
/// position shares (#2340, ruling R-HU): does the literal's text have a finite value in the
/// type it resolves to? Twin of <see cref="IntegerLiteralClassifier"/>, and the same SPY0210
/// family refuses a miss.
///
/// <para>Before this seam only one of the four float cells was checked: the unsuffixed literal
/// narrowed into a <c>float32</c>/<c>decimal</c> slot (<c>x: float32 = 1e40</c> is SPY0220,
/// <c>float_literals.md</c>). The literal's OWN type was never checked, so <c>1e400</c> and
/// <c>1e39f</c> reached Roslyn (CS0594 → SPY0599) and <c>1e29m</c> crashed the emitter's decimal
/// parse (SPY0909). Both the literal-typing arm and the slot-narrowing predicates now call
/// <see cref="Fits"/>, so a literal the store seam admits is one code generation can spell.</para>
///
/// <para>Each arm parses exactly as Roslyn's lexer judges the emitted text: <c>double</c> and
/// <c>float</c> are both correctly-rounded parses of the digits (a <c>float32</c> literal is
/// parsed as a <c>float</c> directly, never through <c>double</c> — the emitted text carries an
/// <c>f</c> suffix, so a double-rounded answer could disagree with the C# compiler at the
/// boundary), and <c>decimal</c> is <see cref="decimal.TryParse(string, NumberStyles, IFormatProvider, out decimal)"/>
/// under <see cref="NumberStyles.Float"/>, which is what admits an exponent form.</para>
/// </summary>
internal static class FloatLiteralClassifier
{
    public readonly record struct Result(SemanticType Type, bool IsError, string? ErrorMessage);

    /// <summary>The type a float literal's suffix resolves it to (<c>f</c>, <c>m</c>, else <c>float64</c>).</summary>
    public static SemanticType TypeOf(string? suffix) => suffix?.ToUpperInvariant() switch
    {
        "F" => SemanticType.Float32,
        "M" => SemanticType.Decimal,
        _ => SemanticType.Double,
    };

    /// <summary>
    /// Classifies a float literal's type from its <paramref name="suffix"/> and refuses a value
    /// outside that type's range.
    /// </summary>
    public static Result Classify(string value, string? suffix)
    {
        var type = TypeOf(suffix);
        var clrType = PrimitiveCatalog.GetPrimitiveInfo(type)!.ClrType;
        return Fits(value, clrType)
            ? new Result(type, false, null)
            : new Result(type, true, $"Float literal '{value}' is too large for '{CSharpName(clrType)}'");
    }

    /// <summary>
    /// Whether the float literal text <paramref name="value"/> has a finite value in
    /// <paramref name="clrType"/> (<see cref="double"/>, <see cref="float"/> or
    /// <see cref="decimal"/>). The one range question for every float-literal position.
    /// </summary>
    public static bool Fits(string value, Type clrType)
    {
        if (clrType == typeof(double))
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                && double.IsFinite(d);
        }

        if (clrType == typeof(float))
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
                && float.IsFinite(f);
        }

        if (clrType == typeof(decimal))
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        throw new ArgumentException(
            $"A float literal resolves to double, float or decimal, never '{clrType}'.", nameof(clrType));
    }

    private static string CSharpName(Type clrType)
        => clrType == typeof(float) ? "float" : clrType == typeof(decimal) ? "decimal" : "double";
}
