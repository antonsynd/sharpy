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
        char align = '\0';
        if (spec.Length >= 2 && IsAlign(spec[1]))
        {
            align = spec[1];
            pos = 2;
        }
        else if (IsAlign(spec[0]))
        {
            align = spec[0];
            pos = 1;
        }

        // sign
        char sign = '\0';
        if (pos < spec.Length && (spec[pos] == '+' || spec[pos] == '-' || spec[pos] == ' '))
        {
            sign = spec[pos];
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
        bool altForm = false;
        if (pos < spec.Length && spec[pos] == '#')
        {
            altForm = true;
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
        char grouping = '\0';
        if (pos < spec.Length && (spec[pos] == ',' || spec[pos] == '_'))
        {
            grouping = spec[pos];
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

        // Grouping is legal only with a subset of the presentation types, and CPython checks that
        // BEFORE it checks the type against the operand — f"{1.5:,b}" is "Cannot specify ',' with
        // 'b'.", not "Unknown format code 'b' for object of type 'float'". An absent type stands for
        // the operand's default one, which is why f"{s:_}" on a str is refused by name.
        if (grouping != '\0')
        {
            char effectiveType = type != '\0' ? type : DefaultType(kind);
            if (!GroupingAllowedWith(grouping, effectiveType))
            {
                return "Cannot specify '" + grouping + "' with '" + effectiveType + "'.";
            }
        }

        return ValidateTypeCode(kind, type, hasPrecision, zCoerce, sign, altForm, align);
    }

    /// <summary>
    /// CPython's grouping/presentation-type matrix (PEP 378 + PEP 515), mirroring
    /// <c>Sharpy.PyFormat.GroupingAllowedWith</c>: both separators go with <c>d e E f F g G %</c> and
    /// the absent type; <c>_</c> additionally goes with the radix types <c>b o x X</c> (grouping
    /// every four digits) and <c>,</c> does not. Everything else, including <c>c</c>, <c>n</c>,
    /// <c>s</c> and unknown codes, is refused.
    /// </summary>
    private static bool GroupingAllowedWith(char grouping, char type) => type switch
    {
        '\0' or 'd' or 'e' or 'E' or 'f' or 'F' or 'g' or 'G' or '%' => true,
        'b' or 'o' or 'x' or 'X' => grouping == '_',
        _ => false
    };

    /// <summary>The presentation type an absent one stands for, per operand kind.</summary>
    private static char DefaultType(FormatOperandKind kind) => kind switch
    {
        FormatOperandKind.Str => 's',
        FormatOperandKind.Integral or FormatOperandKind.Bool => 'd',
        _ => '\0'
    };

    private static string? ValidateTypeCode(
        FormatOperandKind kind, char type, bool hasPrecision, bool zCoerce, char sign, bool altForm, char align)
    {
        switch (kind)
        {
            case FormatOperandKind.Str:
                // CPython refuses a numeric type code first, THEN a sign/z/'#'/'=' — in that order.
                if (type != '\0' && type != 's')
                {
                    return "Unknown format code '" + type + "' for object of type 'str'";
                }
                return StringOperandRefusal(sign, zCoerce, altForm, align);

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
                // #1944: a sign is refused with the 'c' (character) presentation type — the ONE
                // sign rule's single exception, mirroring Sharpy.PyFormat.FormatValue at runtime.
                if (type == 'c' && sign != '\0')
                {
                    return "Sign not allowed with integer format specifier 'c'";
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

    /// <summary>
    /// The ONE ordered string-operand option rule (#1945), the static twin of
    /// <c>Sharpy.PyFormat.StringOperandRefusal</c>: once a string's type code is accepted, CPython
    /// refuses a sign, a negative-zero coercion (<c>z</c>), the alternate form (<c>#</c>) and
    /// <c>=</c> alignment — in that order — with these exact messages. The two implementations carry
    /// the same wording and order by design (the M4/M5 same-logic-twice-with-cross-references
    /// pattern); a spec this returns non-null for is one Core would raise the same ValueError for.
    /// </summary>
    private static string? StringOperandRefusal(char sign, bool zCoerce, bool altForm, char align)
    {
        if (sign != '\0')
        {
            return sign == ' '
                ? "Space not allowed in string format specifier"
                : "Sign not allowed in string format specifier";
        }
        if (zCoerce)
        {
            return "Negative zero coercion (z) not allowed in string format specifier";
        }
        if (altForm)
        {
            return "Alternate form (#) not allowed in string format specifier";
        }
        if (align == '=')
        {
            return "'=' alignment not allowed in string format specifier";
        }
        return null;
    }

    private static bool IsAlign(char c) => c == '<' || c == '>' || c == '^' || c == '=';

    private static string TypeName(FormatOperandKind kind) => kind switch
    {
        FormatOperandKind.Str => "str",
        FormatOperandKind.Float => "float",
        FormatOperandKind.Bool => "bool",
        _ => "int"
    };
}
