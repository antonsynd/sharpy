using System.Globalization;
using System.Numerics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Classifies integer literals by suffix and magnitude. The semantic phase owns the width
/// decision; the emitter reads it (#1314, #1320, #1304). Spec authority:
/// <c>docs/language_specification/integer_literals.md</c> lines 17-23.
/// </summary>
internal static class IntegerLiteralClassifier
{
    public readonly record struct Result(SemanticType Type, bool IsError, string? ErrorMessage);

    /// <summary>
    /// Classifies an integer literal's type from its <paramref name="value"/> text and
    /// optional <paramref name="suffix"/>. The value may be decimal, hex (0x), octal (0o),
    /// or binary (0b), with underscores.
    /// </summary>
    public static Result Classify(string value, string? suffix)
    {
        var cleaned = value.Replace("_", "", StringComparison.Ordinal);

        BigInteger magnitude;
        try
        {
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                magnitude = BigInteger.Parse("0" + cleaned[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else if (cleaned.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                magnitude = ParseOctal(cleaned[2..]);
            else if (cleaned.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                magnitude = ParseBinary(cleaned[2..]);
            else
                magnitude = BigInteger.Parse(cleaned, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return new Result(SemanticType.Int, true, $"Invalid integer literal '{value}'");
        }

        var upper = suffix?.ToUpperInvariant();
        return upper switch
        {
            "L" => FitsLong(magnitude)
                ? new Result(SemanticType.Long, false, null)
                : new Result(SemanticType.Long, true, $"Integer literal '{value}' is too large for 'long'"),

            "U" => FitsUInt(magnitude)
                ? new Result(SemanticType.UInt, false, null)
                : new Result(SemanticType.UInt, true, $"Integer literal '{value}' is too large for 'uint'"),

            "UL" or "LU" => FitsULong(magnitude)
                ? new Result(SemanticType.ULong, false, null)
                : new Result(SemanticType.ULong, true, $"Integer literal '{value}' is too large for 'ulong'"),

            null or "" => ClassifyUnsuffixed(magnitude, value),

            _ => new Result(SemanticType.Int, true, $"Unknown integer suffix '{suffix}'")
        };
    }

    /// <summary>
    /// Classifies a negated integer literal (unary minus over integer literal).
    /// The negated magnitude determines the type: -2147483648 fits int, -9223372036854775808 fits long.
    /// </summary>
    public static Result ClassifyNegated(string value, string? suffix)
    {
        var cleaned = value.Replace("_", "", StringComparison.Ordinal);

        BigInteger magnitude;
        try
        {
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                magnitude = BigInteger.Parse("0" + cleaned[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            else if (cleaned.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
                magnitude = ParseOctal(cleaned[2..]);
            else if (cleaned.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                magnitude = ParseBinary(cleaned[2..]);
            else
                magnitude = BigInteger.Parse(cleaned, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return new Result(SemanticType.Int, true, $"Invalid integer literal '{value}'");
        }

        var negated = -magnitude;

        var upper = suffix?.ToUpperInvariant();
        if (upper is "L")
        {
            return negated >= long.MinValue
                ? new Result(SemanticType.Long, false, null)
                : new Result(SemanticType.Long, true, $"Integer literal '-{value}' is too large for 'long'");
        }

        if (upper is not null and not "")
            return Classify(value, suffix);

        if (negated >= int.MinValue && negated <= int.MaxValue)
            return new Result(SemanticType.Int, false, null);
        if (negated >= long.MinValue)
            return new Result(SemanticType.Long, false, null);

        return new Result(SemanticType.Long, true,
            $"Integer literal '-{value}' is too large for any integer type");
    }

    private static Result ClassifyUnsuffixed(BigInteger magnitude, string originalValue)
    {
        if (magnitude >= 0 && magnitude <= int.MaxValue)
            return new Result(SemanticType.Int, false, null);
        if (magnitude >= 0 && magnitude <= long.MaxValue)
            return new Result(SemanticType.Long, false, null);

        return new Result(SemanticType.Long, true,
            $"Integer literal '{originalValue}' is too large for any integer type");
    }

    private static bool FitsLong(BigInteger v) => v >= long.MinValue && v <= long.MaxValue;
    private static bool FitsUInt(BigInteger v) => v >= 0 && v <= uint.MaxValue;
    private static bool FitsULong(BigInteger v) => v >= 0 && v <= ulong.MaxValue;

    private static BigInteger ParseOctal(string digits)
    {
        BigInteger result = 0;
        foreach (char c in digits)
        {
            result = result * 8 + (c - '0');
        }
        return result;
    }

    private static BigInteger ParseBinary(string digits)
    {
        BigInteger result = 0;
        foreach (char c in digits)
        {
            result = result * 2 + (c - '0');
        }
        return result;
    }
}
