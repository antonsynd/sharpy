namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The operand kind a static format spec is validated against — the compile-time projection of the
/// value kinds <c>Sharpy.PyFormat</c> distinguishes at runtime. <see cref="Unknown"/> means the
/// operand's kind is not statically known (an <c>object</c> hole, a union, an inferred variable), so
/// the spec is not refused early and Core validates it at runtime instead.
/// </summary>
internal enum FormatOperandKind
{
    Unknown,
    Str,
    Integral,
    Bool,
    Float,
    NoneLiteral
}

/// <summary>
/// Parses and validates a STATIC f-string format spec (one with no nested replacement fields) at
/// compile time, by name, mirroring the CPython grammar and refusals that
/// <c>Sharpy.PyFormat.Apply</c> enforces at runtime (#1815, #1814). Static and dynamic paths agree
/// because the wording is copied from the one Core engine: a spec this class accepts, Core renders;
/// a spec this class rejects, Core would have raised the same <c>ValueError</c>/<c>TypeError</c> for.
/// </summary>
internal static class FormatSpecGrammar
{
    /// <summary>
    /// Returns <c>null</c> when <paramref name="spec"/> is a valid format spec for an operand of
    /// <paramref name="kind"/>, or the CPython error message (the text of the <c>ValueError</c> /
    /// <c>TypeError</c> the runtime would raise) when it is not. An empty spec is always valid
    /// (it is <c>str(value)</c>). <see cref="FormatOperandKind.Unknown"/> is never refused here.
    /// </summary>
    public static string? Validate(string spec, FormatOperandKind kind)
    {
        if (string.IsNullOrEmpty(spec))
        {
            // Empty spec == str(value): valid for every kind, including None.
            return null;
        }

        if (kind == FormatOperandKind.Unknown)
        {
            // The operand kind is not statically known; Core validates the spec at runtime.
            return null;
        }

        if (kind == FormatOperandKind.NoneLiteral)
        {
            // A non-empty spec on the None literal is a TypeError in CPython.
            return "unsupported format string passed to NoneType.__format__";
        }

        // Parse: [[fill]align][sign][z][#][0][width][grouping][.precision][type] — same walk as
        // Sharpy.PyFormat.ApplyFormatSpec.
        int pos = 0;

        // fill + align, or a lone align.
        if (spec.Length >= 2 && IsAlign(spec[1]))
        {
            pos = 2;
        }
        else if (IsAlign(spec[0]))
        {
            pos = 1;
        }

        // sign
        if (pos < spec.Length && (spec[pos] == '+' || spec[pos] == '-' || spec[pos] == ' '))
        {
            pos++;
        }

        // z (PEP 682 negative-zero coercion)
        bool zCoerce = false;
        if (pos < spec.Length && spec[pos] == 'z')
        {
            zCoerce = true;
            pos++;
        }

        // # (alternate form)
        if (pos < spec.Length && spec[pos] == '#')
        {
            pos++;
        }

        // 0 (zero padding)
        if (pos < spec.Length && spec[pos] == '0')
        {
            pos++;
        }

        // width
        while (pos < spec.Length && spec[pos] >= '0' && spec[pos] <= '9')
        {
            pos++;
        }

        // grouping
        if (pos < spec.Length && (spec[pos] == ',' || spec[pos] == '_'))
        {
            pos++;
        }

        // precision
        bool hasPrecision = false;
        if (pos < spec.Length && spec[pos] == '.')
        {
            pos++;
            while (pos < spec.Length && spec[pos] >= '0' && spec[pos] <= '9')
            {
                pos++;
            }
            hasPrecision = true;
        }

        // type — exactly one trailing character; anything left over is invalid.
        char type = '\0';
        if (pos < spec.Length)
        {
            type = spec[pos];
            pos++;
        }
        if (pos != spec.Length)
        {
            return "Invalid format specifier '" + spec + "' for object of type '" + TypeName(kind) + "'";
        }

        return ValidateTypeCode(kind, type, hasPrecision, zCoerce);
    }

    private static string? ValidateTypeCode(FormatOperandKind kind, char type, bool hasPrecision, bool zCoerce)
    {
        switch (kind)
        {
            case FormatOperandKind.Str:
                if (type != '\0' && type != 's')
                {
                    return "Unknown format code '" + type + "' for object of type 'str'";
                }
                return null;

            case FormatOperandKind.Float:
                if (type == 'b' || type == 'c' || type == 'd' || type == 'o'
                    || type == 'x' || type == 'X' || type == 's')
                {
                    return "Unknown format code '" + type + "' for object of type 'float'";
                }
                if (!IsKnownFloatType(type))
                {
                    return "Unknown format code '" + type + "' for object of type 'float'";
                }
                return null;

            case FormatOperandKind.Integral:
            case FormatOperandKind.Bool:
                if (type == 's')
                {
                    return "Unknown format code 's' for object of type '" + TypeName(kind) + "'";
                }
                if (!IsKnownIntegralType(type))
                {
                    return "Unknown format code '" + type + "' for object of type '" + TypeName(kind) + "'";
                }
                bool integerPresentation = type == '\0' || type == 'b' || type == 'c' || type == 'd'
                    || type == 'n' || type == 'o' || type == 'x' || type == 'X';
                if (hasPrecision && integerPresentation)
                {
                    return "Precision not allowed in integer format specifier";
                }
                if (zCoerce && integerPresentation)
                {
                    return "Negative zero coercion (z) not allowed in integer format specifier";
                }
                return null;

            default:
                return null;
        }
    }

    private static bool IsKnownFloatType(char type) =>
        type == '\0' || type == 'e' || type == 'E' || type == 'f' || type == 'F'
        || type == 'g' || type == 'G' || type == 'n' || type == '%';

    private static bool IsKnownIntegralType(char type) =>
        type == '\0' || type == 'b' || type == 'c' || type == 'd' || type == 'e' || type == 'E'
        || type == 'f' || type == 'F' || type == 'g' || type == 'G' || type == 'n' || type == 'o'
        || type == 'x' || type == 'X' || type == '%';

    private static bool IsAlign(char c) => c == '<' || c == '>' || c == '^' || c == '=';

    private static string TypeName(FormatOperandKind kind) => kind switch
    {
        FormatOperandKind.Str => "str",
        FormatOperandKind.Float => "float",
        FormatOperandKind.Bool => "bool",
        _ => "int"
    };
}
