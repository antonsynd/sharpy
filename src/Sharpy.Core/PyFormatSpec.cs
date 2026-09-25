using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// The operand kind a format spec is validated against — the value kinds CPython's
    /// <c>__format__</c> implementations distinguish. <see cref="PyFormat"/> derives it from the
    /// runtime value; the compiler's static twin projects it from the static type.
    /// </summary>
    public enum FormatOperandKind
    {
        /// <summary>Not statically known (an <c>object</c> hole, a union): never refused; the runtime decides.</summary>
        Unknown,

        /// <summary><c>None</c>: a non-empty spec is CPython's <c>object.__format__</c> TypeError.</summary>
        NoneValue,

        /// <summary><c>str</c> (<c>str.__format__</c>).</summary>
        Str,

        /// <summary>A CLR integer (<c>int.__format__</c>).</summary>
        Integral,

        /// <summary><c>bool</c> — <c>int.__format__</c> under the name <c>bool</c>.</summary>
        Bool,

        /// <summary><c>float</c> (<c>float.__format__</c>).</summary>
        Float,

        /// <summary><c>complex</c> (<c>complex.__format__</c>, #2018).</summary>
        Complex,

        /// <summary>A value whose type owns its spec (<c>System.IFormattable</c>): any spec is accepted.</summary>
        Formattable,

        /// <summary>A value with no <c>__format__</c>: a non-empty spec is a TypeError.</summary>
        NoFormat
    }

    /// <summary>
    /// A refusal from <see cref="PyFormatSpec.Validate"/>: CPython's message and the exception kind
    /// that carries it. The kind is data, never re-derived from the message text.
    /// </summary>
    public sealed class FormatSpecError
    {
        private FormatSpecError(bool isTypeError, string message)
        {
            IsTypeError = isTypeError;
            Message = message;
        }

        /// <summary>True for CPython's <c>TypeError</c> (no <c>__format__</c>), false for its <c>ValueError</c>.</summary>
        public bool IsTypeError { get; }

        /// <summary>CPython's message, verbatim.</summary>
        public string Message { get; }

        internal static FormatSpecError Type(string message) => new FormatSpecError(true, message);

        internal static FormatSpecError Value(string message) => new FormatSpecError(false, message);

        /// <summary>The exception CPython raises for this refusal.</summary>
        public Exception ToException() => IsTypeError ? (Exception)new TypeError(Message) : new ValueError(Message);
    }

    /// <summary>
    /// The ONE parser and validator of the format-spec mini-language
    /// <c>[[fill]align][sign][z][#][0][width][grouping][.precision][type]</c> (#1984, R-BX).
    /// <see cref="PyFormat.Apply(object, string)"/> renders from the spec this returns, and the
    /// compiler's static twin calls the same <see cref="Validate"/> — so a rule lives in exactly one
    /// place. The parse and the rule order are CPython 3.12's <c>Objects/stringlib/unicode_format.h</c>
    /// / <c>Python/formatter_unicode.c</c>: <c>parse_internal_render_format_spec</c>, then the
    /// per-type <c>__format__</c> switch, then <c>format_string_internal</c> /
    /// <c>format_long_internal</c>.
    /// </summary>
    public readonly struct PyFormatSpec
    {
        private PyFormatSpec(char fill, char align, char sign, bool negativeZeroCoercion, bool alternateForm,
            int width, char grouping, int precision, char type)
        {
            Fill = fill;
            Align = align;
            Sign = sign;
            NegativeZeroCoercion = negativeZeroCoercion;
            AlternateForm = alternateForm;
            Width = width;
            Grouping = grouping;
            Precision = precision;
            Type = type;
        }

        /// <summary>The fill character (<c>' '</c> unless given, or <c>'0'</c> from the <c>0</c> flag).</summary>
        public char Fill { get; }

        /// <summary><c>&lt; &gt; ^ =</c>, or <c>'\0'</c> for the operand's default; the <c>0</c> flag synthesises <c>=</c> for numbers.</summary>
        public char Align { get; }

        /// <summary><c>+ - ' '</c>, or <c>'\0'</c>.</summary>
        public char Sign { get; }

        /// <summary><c>z</c> — PEP 682 negative-zero coercion.</summary>
        public bool NegativeZeroCoercion { get; }

        /// <summary><c>#</c> — the alternate form.</summary>
        public bool AlternateForm { get; }

        /// <summary>The minimum width, 0 when absent.</summary>
        public int Width { get; }

        /// <summary><c>,</c> or <c>_</c>, or <c>'\0'</c>.</summary>
        public char Grouping { get; }

        /// <summary>The precision, -1 when absent.</summary>
        public int Precision { get; }

        /// <summary>Whether a precision was given.</summary>
        public bool HasPrecision => Precision >= 0;

        /// <summary>The presentation type, or <c>'\0'</c> when absent.</summary>
        public char Type { get; }

        /// <summary>
        /// Validate <paramref name="spec"/> for an operand of <paramref name="kind"/> whose python type
        /// name is <paramref name="pyTypeName"/> (it appears in CPython's messages). Returns null when
        /// CPython accepts the spec, with <paramref name="parsed"/> the spec to render from; otherwise
        /// the refusal CPython raises first. An empty spec is always accepted (it is <c>str(value)</c>).
        /// <see cref="FormatOperandKind.Unknown"/> and <see cref="FormatOperandKind.Formattable"/> are
        /// never refused and not parsed (<paramref name="parsed"/> is <c>default</c>): the spec is the
        /// type's own business. Pure and value-free.
        /// </summary>
        public static FormatSpecError? Validate(
            string spec, FormatOperandKind kind, string pyTypeName, out PyFormatSpec parsed)
        {
            parsed = default;
            if (string.IsNullOrEmpty(spec))
            {
                return null;
            }

            switch (kind)
            {
                case FormatOperandKind.Unknown:
                case FormatOperandKind.Formattable:
                    return null;
                case FormatOperandKind.NoneValue:
                case FormatOperandKind.NoFormat:
                    // object.__format__ refuses any non-empty spec BEFORE parsing it:
                    // format([1], 'garbage{') is the TypeError, not "Invalid format specifier".
                    return FormatSpecError.Type("unsupported format string passed to " + pyTypeName + ".__format__");
            }

            // str's defaults are type 's', align '<'; int/bool's 'd', '>'; float's and complex's none, '>'.
            char defaultType = kind == FormatOperandKind.Str ? 's'
                : kind == FormatOperandKind.Float || kind == FormatOperandKind.Complex ? '\0'
                : 'd';
            char defaultAlign = kind == FormatOperandKind.Str ? '<' : '>';

            FormatSpecError? parseError = Parse(spec, defaultAlign, pyTypeName, out parsed);
            if (parseError != null)
            {
                return parseError;
            }

            // Grouping x presentation type is checked while parsing in CPython — before the type is
            // checked against the operand: format(1.5, ',b') is "Cannot specify ',' with 'b'.". An
            // absent type stands for the operand's default one, so format('a', '_') is refused.
            if (parsed.Grouping != '\0')
            {
                char effectiveType = parsed.Type != '\0' ? parsed.Type : defaultType;
                if (!GroupingAllowedWith(parsed.Grouping, effectiveType))
                {
                    return FormatSpecError.Value(
                        "Cannot specify '" + parsed.Grouping + "' with " + QuoteCode(effectiveType) + ".");
                }
            }

            char type = parsed.Type != '\0' ? parsed.Type : defaultType;
            switch (kind)
            {
                case FormatOperandKind.Str:
                    // str.__format__'s switch, then format_string_internal: sign, z, '#', '='.
                    if (type != 's')
                    {
                        return UnknownCode(type, pyTypeName);
                    }
                    if (parsed.Sign != '\0')
                    {
                        return FormatSpecError.Value(parsed.Sign == ' '
                            ? "Space not allowed in string format specifier"
                            : "Sign not allowed in string format specifier");
                    }
                    if (parsed.NegativeZeroCoercion)
                    {
                        return FormatSpecError.Value("Negative zero coercion (z) not allowed in string format specifier");
                    }
                    if (parsed.AlternateForm)
                    {
                        return FormatSpecError.Value("Alternate form (#) not allowed in string format specifier");
                    }
                    if (parsed.Align == '=')
                    {
                        return FormatSpecError.Value("'=' alignment not allowed in string format specifier");
                    }
                    return null;

                case FormatOperandKind.Integral:
                case FormatOperandKind.Bool:
                    // int.__format__'s switch: the integer presentations reach format_long_internal
                    // (precision, then z, then 'c' with a sign, then 'c' with '#' — #1978); the float
                    // ones convert and have none.
                    switch (type)
                    {
                        case 'b':
                        case 'c':
                        case 'd':
                        case 'o':
                        case 'x':
                        case 'X':
                        case 'n':
                            if (parsed.HasPrecision)
                            {
                                return FormatSpecError.Value("Precision not allowed in integer format specifier");
                            }
                            if (parsed.NegativeZeroCoercion)
                            {
                                return FormatSpecError.Value(
                                    "Negative zero coercion (z) not allowed in integer format specifier");
                            }
                            if (type == 'c' && parsed.Sign != '\0')
                            {
                                return FormatSpecError.Value("Sign not allowed with integer format specifier 'c'");
                            }
                            if (type == 'c' && parsed.AlternateForm)
                            {
                                return FormatSpecError.Value(
                                    "Alternate form (#) not allowed with integer format specifier 'c'");
                            }
                            return null;
                        case 'e':
                        case 'E':
                        case 'f':
                        case 'F':
                        case 'g':
                        case 'G':
                        case '%':
                            return null;
                        default:
                            return UnknownCode(type, pyTypeName);
                    }

                case FormatOperandKind.Complex:
                    // complex.__format__'s switch, then format_complex_internal: no zero padding
                    // (an explicit '0' fill or the '0' flag), no '=' alignment (#2018).
                    switch (type)
                    {
                        case '\0':
                        case 'e':
                        case 'E':
                        case 'f':
                        case 'F':
                        case 'g':
                        case 'G':
                        case 'n':
                            break;
                        default:
                            return UnknownCode(type, pyTypeName);
                    }
                    if (parsed.Fill == '0')
                    {
                        return FormatSpecError.Value("Zero padding is not allowed in complex format specifier");
                    }
                    if (parsed.Align == '=')
                    {
                        return FormatSpecError.Value("'=' alignment flag is not allowed in complex format specifier");
                    }
                    return null;

                default:
                    // FormatOperandKind.Float: float.__format__'s switch is the only rule.
                    switch (type)
                    {
                        case '\0':
                        case 'e':
                        case 'E':
                        case 'f':
                        case 'F':
                        case 'g':
                        case 'G':
                        case 'n':
                        case '%':
                            return null;
                        default:
                            return UnknownCode(type, pyTypeName);
                    }
            }
        }

        /// <summary>
        /// CPython's <c>parse_internal_render_format_spec</c>, minus the grouping x type check (which
        /// needs the operand's default type and lives in <see cref="Validate"/>). Refuses only what
        /// the grammar itself refuses: a missing precision, both separators, too many digits (#2017)
        /// and more than one trailing type character. <paramref name="defaultAlign"/> is the operand's default
        /// alignment: the <c>0</c> flag synthesises <c>=</c> only when it is <c>&gt;</c> (numbers), so
        /// <c>format('ab', '05')</c> is <c>'ab000'</c>.
        /// </summary>
        private static FormatSpecError? Parse(
            string spec, char defaultAlign, string pyTypeName, out PyFormatSpec parsed)
        {
            parsed = default;
            int pos = 0;
            int end = spec.Length;
            char fill = ' ';
            char align = '\0';
            bool fillSpecified = false;
            bool alignSpecified = false;

            if (end - pos >= 2 && IsAlign(spec[pos + 1]))
            {
                fill = spec[pos];
                align = spec[pos + 1];
                fillSpecified = true;
                alignSpecified = true;
                pos += 2;
            }
            else if (end - pos >= 1 && IsAlign(spec[pos]))
            {
                align = spec[pos];
                alignSpecified = true;
                pos++;
            }

            char sign = '\0';
            if (end - pos >= 1 && (spec[pos] == '+' || spec[pos] == '-' || spec[pos] == ' '))
            {
                sign = spec[pos];
                pos++;
            }

            // 'z' (PEP 682) comes before '#': format(65, '#zc') is an invalid specifier.
            bool negativeZeroCoercion = false;
            if (end - pos >= 1 && spec[pos] == 'z')
            {
                negativeZeroCoercion = true;
                pos++;
            }

            bool alternateForm = false;
            if (end - pos >= 1 && spec[pos] == '#')
            {
                alternateForm = true;
                pos++;
            }

            // The backward-compatible '0' flag: only when no fill was given (otherwise the '0' is the
            // first width digit); '=' is synthesised only for a '>'-aligned (numeric) operand (#1945).
            if (!fillSpecified && end - pos >= 1 && spec[pos] == '0')
            {
                fill = '0';
                if (!alignSpecified && defaultAlign == '>')
                {
                    align = '=';
                }
                pos++;
            }

            if (!TryReadDecimal(spec, ref pos, out int width))
            {
                return TooManyDigits();
            }

            char grouping = '\0';
            if (end - pos >= 1 && spec[pos] == ',')
            {
                grouping = ',';
                pos++;
            }
            if (end - pos >= 1 && spec[pos] == '_')
            {
                if (grouping != '\0')
                {
                    return BothSeparators();
                }
                grouping = '_';
                pos++;
            }
            if (end - pos >= 1 && spec[pos] == ',' && grouping == '_')
            {
                return BothSeparators();
            }

            int precision = -1;
            if (end - pos >= 1 && spec[pos] == '.')
            {
                pos++;
                int precisionStart = pos;
                if (!TryReadDecimal(spec, ref pos, out precision))
                {
                    return TooManyDigits();
                }
                if (pos == precisionStart)
                {
                    return FormatSpecError.Value("Format specifier missing precision");
                }
            }

            // Exactly one trailing character is the type; more is an invalid specifier.
            if (end - pos > 1)
            {
                return FormatSpecError.Value(
                    "Invalid format specifier '" + spec + "' for object of type '" + pyTypeName + "'");
            }
            char type = end - pos == 1 ? spec[pos] : '\0';

            parsed = new PyFormatSpec(fill, align, sign, negativeZeroCoercion, alternateForm,
                width, grouping, precision, type);
            return null;
        }

        /// <summary>
        /// CPython's <c>get_integer</c>: the run of Unicode decimal digits (category <c>Nd</c>, as
        /// <c>str.isdecimal</c> — ASCII, Arabic-Indic, fullwidth, and the non-BMP digit blocks read as a
        /// surrogate pair) at <paramref name="pos"/>, advancing past it. Returns false when the value does
        /// not fit (CPython bounds it by <c>Py_ssize_t</c>; a CLR string length is an <c>int</c>, so that is
        /// the bound here) — CPython's "Too many decimal digits". An empty run is <c>0</c>, so the caller
        /// compares positions to tell absence apart. The ONE digit reader of the format mini-language:
        /// width, precision, a field index and an item key all read through it (#2017).
        /// </summary>
        public static bool TryReadDecimal(string text, ref int pos, out int value)
        {
            long accumulator = 0;
            value = 0;
            while (pos < text.Length)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(text, pos) != UnicodeCategory.DecimalDigitNumber)
                {
                    break;
                }
                accumulator = accumulator * 10 + CharUnicodeInfo.GetDecimalDigitValue(text, pos);
                if (accumulator > int.MaxValue)
                {
                    return false;
                }
                pos += char.IsSurrogatePair(text, pos) ? 2 : 1;
            }
            value = (int)accumulator;
            return true;
        }

        private static FormatSpecError TooManyDigits() =>
            FormatSpecError.Value("Too many decimal digits in format string");

        private static FormatSpecError BothSeparators() =>
            FormatSpecError.Value("Cannot specify both ',' and '_'.");

        private static FormatSpecError UnknownCode(char type, string pyTypeName) =>
            FormatSpecError.Value("Unknown format code " + QuoteCode(type) + " for object of type '" + pyTypeName + "'");

        /// <summary>
        /// A presentation type as CPython's messages spell it: <c>'x'</c> for printable ASCII,
        /// <c>'\xe9'</c> (lowercase hex, no padding) otherwise (#2017).
        /// </summary>
        private static string QuoteCode(char type) =>
            type > 32 && type < 128
                ? "'" + type + "'"
                : "'\\x" + ((int)type).ToString("x", CultureInfo.InvariantCulture) + "'";

        private static bool IsAlign(char c) => c == '<' || c == '>' || c == '^' || c == '=';

        /// <summary>
        /// CPython's grouping/presentation-type matrix (PEP 378 + PEP 515): both separators go with
        /// <c>d e E f F g G %</c> and the absent (float) type; <c>_</c> also goes with the radix types
        /// <c>b o x X</c> (grouping every four digits) and <c>,</c> does not. Everything else,
        /// including <c>c</c>, <c>n</c>, <c>s</c> and unknown codes, is refused.
        /// </summary>
        private static bool GroupingAllowedWith(char grouping, char type)
        {
            switch (type)
            {
                case '\0':
                case 'd':
                case 'e':
                case 'E':
                case 'f':
                case 'F':
                case 'g':
                case 'G':
                case '%':
                    return true;
                case 'b':
                case 'o':
                case 'x':
                case 'X':
                    return grouping == '_';
                default:
                    return false;
            }
        }
    }
}
