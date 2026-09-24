using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// The one Python format-spec engine. <c>str.format</c>, <c>format()</c>
    /// (<see cref="Builtins.Format(object, string)"/>) and every f-string hole route their
    /// <c>[[fill]align][sign][z][#][0][width][grouping][.precision][type]</c> specs through
    /// <see cref="Apply(object, string)"/> so there is exactly one CPython-conforming implementation.
    /// </summary>
    public static class PyFormat
    {
        /// <summary>
        /// Format <paramref name="value"/> according to the Python format specification
        /// <paramref name="spec"/>. An empty spec yields <c>str(value)</c>; otherwise the spec is
        /// validated for the value's operand kind by <see cref="PyFormatSpec.Validate"/> — the one
        /// parser and rule table, shared with the compiler's static twin (#1984, R-BX) — and the
        /// value is rendered from the spec it returns. A refused spec raises CPython's
        /// <see cref="ValueError"/> or <see cref="TypeError"/>.
        /// </summary>
        public static string Apply(object? value, string spec)
        {
            if (string.IsNullOrEmpty(spec))
            {
                // Empty spec == str(value) — literally, by calling the ONE str() authority
                // (Builtins.Str) rather than object.ToString(). #1883: .NET's ToString() spells a
                // whole double "100" where Python's str() spells it "100.0", so `format(100.0, "")`
                // and `"{}".format(100.0)` disagreed with `f"{100.0}"`, whose plain hole already
                // lowers to Builtins.Str. Builtins.Str also owns None, bool (True/False), exception
                // messages, tuples and plain CLR sequences, so every kind agrees across consumers.
                return value == null ? "None" : Builtins.Str(value);
            }

            FormatOperandKind kind = KindOf(value);
            string pyTypeName = PyTypeName(value);
            PyFormatSpec parsed;
            FormatSpecError? error = kind == FormatOperandKind.Unknown
                // A value with no python operand kind yet is parsed but no rule applies; it renders
                // its ToString() under the spec (#1988 gives it a kind).
                ? PyFormatSpec.Parse(spec, '>', pyTypeName, out parsed)
                : PyFormatSpec.Validate(spec, kind, pyTypeName, out parsed);
            if (error != null)
            {
                throw error.ToException();
            }

            if (kind == FormatOperandKind.Complex)
            {
                return RenderComplex((Complex)value!, parsed);
            }
            return Render(value!, kind, parsed);
        }

        /// <summary>
        /// The operand kind of a runtime value — which CPython <c>__format__</c> it takes.
        /// </summary>
        private static FormatOperandKind KindOf(object? value)
        {
            if (value == null)
            {
                return FormatOperandKind.NoneValue;
            }
            if (value is bool)
            {
                return FormatOperandKind.Bool;
            }
            if (value is string)
            {
                return FormatOperandKind.Str;
            }
            if (value is double || value is float || value is decimal)
            {
                return FormatOperandKind.Float;
            }
            if (value is int || value is long || value is short || value is byte
                || value is sbyte || value is uint || value is ulong || value is ushort)
            {
                return FormatOperandKind.Integral;
            }
            if (value is Complex)
            {
                return FormatOperandKind.Complex;
            }
            return FormatOperandKind.Unknown;
        }

        private static bool IsNumeric(FormatOperandKind kind) =>
            kind == FormatOperandKind.Integral || kind == FormatOperandKind.Bool || kind == FormatOperandKind.Float;

        /// <summary>
        /// Render a value from its validated spec: the presentation type, then grouping and zero
        /// fill over the digit run, then width and alignment. No rule is checked here — a refusal
        /// can only come from <see cref="PyFormatSpec.Validate"/>.
        /// </summary>
        private static string Render(object value, FormatOperandKind kind, PyFormatSpec spec)
        {
            // Format the value — sign included, grouping and zero-fill NOT (both depend on the
            // width, and CPython interleaves them: the separators go INSIDE the zero fill).
            string formatted = FormatValue(value, kind, spec);

            // Grouping + zero-fill over the digit run only, never over the sign, the 0x/0o/0b prefix
            // or the fraction/exponent tail.
            if (IsNumeric(kind))
            {
                formatted = GroupAndZeroFill(
                    formatted, spec.Type, spec.AlternateForm, spec.Grouping,
                    minWidthTotal: (spec.Align == '=' && spec.Fill == '0') ? spec.Width : 0);
            }

            // Default: numbers right-align, strings left-align
            char align = spec.Align != '\0' ? spec.Align : (IsNumeric(kind) ? '>' : '<');
            return Pad(formatted, spec.Fill, align, spec.Width, spec.Type, spec.AlternateForm);
        }

        /// <summary>
        /// Pad <paramref name="formatted"/> to <paramref name="width"/> with <paramref name="fill"/>
        /// under <paramref name="align"/>. <c>=</c> pads after the sign and the radix prefix.
        /// </summary>
        private static string Pad(string formatted, char fill, char align, int width, char type, bool altForm)
        {
            if (width > 0 && formatted.Length < width)
            {
                int padding = width - formatted.Length;
                switch (align)
                {
                    case '<':
                        formatted = formatted + new string(fill, padding);
                        break;
                    case '>':
                        formatted = new string(fill, padding) + formatted;
                        break;
                    case '^':
                        int left = padding / 2;
                        int right = padding - left;
                        formatted = new string(fill, left) + formatted + new string(fill, right);
                        break;
                    case '=':
                        // Padding between the sign/radix prefix and the digits (#1959) — the SAME
                        // predicate GroupAndZeroFill uses for the '0'-fill path, so an explicit or
                        // default fill lands where the zeros would: format(-255, '*=#10x') is
                        // '-0x*****ff'.
                        int at = Math.Min(NumericPrefixLength(formatted, type, altForm), formatted.Length);
                        formatted = formatted.Substring(0, at) + new string(fill, padding) + formatted.Substring(at);
                        break;
                }
            }

            return formatted;
        }

        /// <summary>
        /// Digits per group: four for <c>_</c> on a radix presentation type (<c>b o x X</c>), three
        /// otherwise. CPython: "for the integer presentation types 'b', 'o', 'x', and 'X',
        /// underscores are inserted every 4 digits".
        /// </summary>
        private static int GroupSizeFor(char type)
        {
            return type == 'b' || type == 'o' || type == 'x' || type == 'X' ? 4 : 3;
        }

        /// <summary>Python type name used in format-error messages.</summary>
        private static string PyTypeName(object? value)
        {
            if (value == null)
            {
                return "NoneType";
            }
            if (value is bool)
            {
                return "bool";
            }
            if (value is double || value is float || value is decimal)
            {
                return "float";
            }
            if (value is int || value is long || value is short || value is byte
                || value is sbyte || value is uint || value is ulong || value is ushort)
            {
                return "int";
            }
            if (value is string)
            {
                return "str";
            }
            if (value is Complex)
            {
                return "complex";
            }
            return value.GetType().Name;
        }

        /// <summary>
        /// CPython's <c>format_complex_internal</c> (#2018), rendering from a spec
        /// <see cref="PyFormatSpec.Validate"/> accepted for <see cref="FormatOperandKind.Complex"/>.
        /// With no type, the parts are the complex repr's own (shortest, no trailing <c>.0</c>), or
        /// <c>g</c> at the given precision, and the result is parenthesised unless the real part is
        /// +0 (then only the imaginary part is printed, as <c>str()</c> does). With a type
        /// (<c>n</c> is <c>g</c>), both parts are that float presentation. The real part takes the
        /// spec's sign; the imaginary part always carries one — unless it stands alone. Grouping and
        /// <c>z</c> apply per part; fill, alignment (default <c>&gt;</c>) and width to the whole.
        /// </summary>
        private static string RenderComplex(Complex value, PyFormatSpec spec)
        {
            double re = value.Real;
            double im = value.Imag;
            char type = spec.Type;
            bool skipRe = false;
            bool addParens = false;
            if (type == '\0')
            {
                type = spec.HasPrecision ? 'g' : 'r';
                if (re == 0.0 && !Complex.IsNegative(re))
                {
                    skipRe = true;
                }
                else
                {
                    addParens = true;
                }
            }
            if (type == 'n')
            {
                type = 'g';
            }

            string text = skipRe ? "" : ComplexPart(re, type, spec, spec.Sign);
            text += ComplexPart(im, type, spec, skipRe ? spec.Sign : '+') + "j";
            if (addParens)
            {
                text = "(" + text + ")";
            }
            return Pad(text, spec.Fill, spec.Align != '\0' ? spec.Align : '>', spec.Width, spec.Type, spec.AlternateForm);
        }

        /// <summary>One part of a complex rendering: presentation, <c>z</c>, sign, grouping.</summary>
        private static string ComplexPart(double part, char type, PyFormatSpec spec, char sign)
        {
            string result;
            if (double.IsNaN(part) || double.IsInfinity(part))
            {
                result = Builtins.Str(part);
                if (type == 'E' || type == 'F' || type == 'G')
                {
                    result = result.ToUpperInvariant();
                }
            }
            else if (type == 'r')
            {
                result = Complex.Component(part);
                if (spec.AlternateForm)
                {
                    result = ForceDecimalPoint(result);
                }
            }
            else
            {
                result = FormatFinite(part, type, spec.Precision, spec.HasPrecision, spec.AlternateForm,
                    isFloat: true, isIntegral: false);
            }

            if (spec.NegativeZeroCoercion && IsNegativeZeroText(result))
            {
                result = result.Substring(1);
            }
            if (result.Length > 0 && result[0] != '-' && (sign == '+' || sign == ' '))
            {
                result = sign + result;
            }
            if (spec.Grouping != '\0')
            {
                result = GroupAndZeroFill(result, type, false, spec.Grouping, minWidthTotal: 0);
            }
            return result;
        }

        private static string FormatValue(object value, FormatOperandKind kind, PyFormatSpec spec)
        {
            bool isFloat = kind == FormatOperandKind.Float;
            bool isIntegral = kind == FormatOperandKind.Integral || kind == FormatOperandKind.Bool;
            char type = spec.Type;
            char sign = spec.Sign;

            string result;

            // A non-finite float is spelled inf/-inf/nan under EVERY float presentation type —
            // uppercased for the uppercase codes, with '%' still appended, and with the precision
            // ignored. .NET's ToString("F6")/("E6") says "Infinity"/"NaN" instead, which is a second
            // spelling of the same value; Builtins.Str is the authority for the first one.
            if (isFloat && IsNonFinite(value))
            {
                string nonFinite = Builtins.Str(value);
                if (type == 'F' || type == 'E' || type == 'G')
                {
                    nonFinite = nonFinite.ToUpperInvariant();
                }
                result = type == '%' ? nonFinite + "%" : nonFinite;
            }
            else
            {
                result = FormatFinite(value, type, spec.Precision, spec.HasPrecision, spec.AlternateForm, isFloat, isIntegral);
            }

            // PEP 682: coerce a formatted negative zero to positive zero.
            if (spec.NegativeZeroCoercion && IsNegativeZeroText(result))
            {
                result = result.Substring(1);
            }

            // Apply sign — to every numeric presentation type including '%'. The validator refuses a
            // sign with 'c' (#1944), so a 'c' rendering never carries one.
            if (sign != '\0' && (isIntegral || isFloat) && type != 'c')
            {
                if (result.Length > 0 && result[0] != '-')
                {
                    if (sign == '+')
                    {
                        result = "+" + result;
                    }
                    else if (sign == ' ')
                    {
                        result = " " + result;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// The presentation-type dispatch for a FINITE value — every arm CPython's
        /// <c>__format__</c> reaches once the non-finite floats have been spelled by
        /// <see cref="Builtins.Str(object)"/>. Sign, negative-zero coercion, grouping and padding are
        /// the caller's job.
        /// </summary>
        private static string FormatFinite(object value, char type, int precision, bool hasPrecision,
            bool altForm, bool isFloat, bool isIntegral)
        {
            string result;

            switch (type)
            {
                case 'd':
                    result = FormatInteger(value);
                    break;
                case 'n':
                    // Locale-aware in CPython; we use the invariant locale, so 'n' == 'd'/'g'.
                    result = isFloat
                        ? FormatGeneral(value, precision, hasPrecision, false, altForm)
                        : FormatInteger(value);
                    break;
                case 'f':
                case 'F':
                    result = FormatFloat(value, hasPrecision ? precision : 6);
                    break;
                case 'e':
                    result = NormalizeScientific(ToDouble(value).ToString(
                        "e" + (hasPrecision ? precision : 6).ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture));
                    break;
                case 'E':
                    result = NormalizeScientific(ToDouble(value).ToString(
                        "E" + (hasPrecision ? precision : 6).ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture));
                    break;
                case 'g':
                    result = FormatGeneral(value, precision, hasPrecision, false, altForm);
                    break;
                case 'G':
                    result = FormatGeneral(value, precision, hasPrecision, true, altForm);
                    break;
                case 'x':
                    result = FormatRadix(value, 16, false, altForm ? "0x" : null);
                    break;
                case 'X':
                    result = FormatRadix(value, 16, true, altForm ? "0X" : null);
                    break;
                case 'o':
                    result = FormatRadix(value, 8, false, altForm ? "0o" : null);
                    break;
                case 'b':
                    result = FormatRadix(value, 2, false, altForm ? "0b" : null);
                    break;
                case 'c':
                    result = FormatCodePoint(ToLong(value));
                    break;
                case '%':
                    int pctPrec = hasPrecision ? precision : 6;
                    double scaled = ToDouble(value) * 100.0;
                    if (double.IsInfinity(scaled))
                    {
                        // A finite float whose ×100 overflows (format(1.7976931348623157e308, '%'))
                        // is spelled like any non-finite float — 'inf%', never .NET's 'Infinity%' —
                        // and, like every non-finite rendering, takes no alternate-form point.
                        return Builtins.Str(scaled) + "%";
                    }
                    result = scaled.ToString(
                        "F" + pctPrec.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";
                    break;
                case 's':
                case '\0':
                    if (type == '\0' && isFloat)
                    {
                        // float with no type: str(value), or significant-digit precision when given.
                        // #1883: str(value) is Builtins.Str — the one float authority — not .NET's
                        // ToString(), which drops the trailing ".0" and spells inf/nan its own way.
                        result = hasPrecision
                            ? FormatFloatSignificant(value, precision, altForm)
                            : Builtins.Str(value);
                    }
                    else if (type == '\0' && isIntegral)
                    {
                        result = FormatInteger(value);
                    }
                    else
                    {
                        result = value.ToString() ?? "";
                        if (hasPrecision && result.Length > precision)
                        {
                            result = result.Substring(0, precision);
                        }
                    }
                    break;
                default:
                    throw new ValueError(
                        "Unknown format code '" + type + "' for object of type '" + PyTypeName(value) + "'");
            }

            // #1958: '#' is ONE rule for the whole float presentation family — the result always
            // carries a decimal point (format(42, '#.0f') is '42.', format(3.5, '#.0') is '4.e+00').
            // The trailing-zero half of the rule lives in FormatGeneral/FormatFloatSignificant.
            // Integral presentations ('d', int 'n'/absent type, radix, 'c') are untouched, and this
            // runs BEFORE GroupAndZeroFill so grouping stops at the forced point ('1_234.').
            if (altForm && IsFloatPresentation(type, isFloat))
            {
                result = ForceDecimalPoint(result);
            }

            return result;
        }

        /// <summary>
        /// Whether <paramref name="type"/> renders a float: <c>e E f F g G %</c> for any numeric
        /// operand, and <c>n</c> or the absent type when the operand itself is a float.
        /// </summary>
        private static bool IsFloatPresentation(char type, bool isFloat)
        {
            switch (type)
            {
                case 'e':
                case 'E':
                case 'f':
                case 'F':
                case 'g':
                case 'G':
                case '%':
                    return true;
                case 'n':
                case '\0':
                    return isFloat;
                default:
                    return false;
            }
        }

        /// <summary>
        /// CPython's alternate-form decimal point (<c>Py_DTSF_ALT</c>): a finite float rendering with
        /// no <c>.</c> gets one before the exponent marker, before a trailing <c>%</c>, or at the end.
        /// </summary>
        private static string ForceDecimalPoint(string s)
        {
            if (s.IndexOf('.') >= 0)
            {
                return s;
            }
            int at = s.IndexOf('e');
            if (at < 0)
            {
                at = s.IndexOf('E');
            }
            if (at < 0 && s.EndsWith("%", StringComparison.Ordinal))
            {
                at = s.Length - 1;
            }
            return at < 0 ? s + "." : s.Insert(at, ".");
        }

        /// <summary>
        /// CPython's <c>b</c>/<c>o</c>/<c>x</c>/<c>X</c>: a SIGN-MAGNITUDE rendering, because Python
        /// integers are unbounded and have no two's complement. .NET's
        /// <c>(-255L).ToString("x")</c> is <c>ffffffffffffff01</c>, which is a different number and
        /// has no correct grouping; CPython's <c>format(-255, 'x')</c> is <c>-ff</c>.
        /// </summary>
        private static string FormatRadix(object value, int radix, bool upper, string? prefix)
        {
            long v = ToLong(value);
            bool negative = v < 0;
            // long.MinValue has no positive counterpart; render its magnitude unsigned.
            ulong magnitude = negative
                ? (v == long.MinValue ? (ulong)long.MaxValue + 1 : (ulong)(-v))
                : (ulong)v;

            string digits;
            if (radix == 16)
            {
                digits = magnitude.ToString(upper ? "X" : "x", CultureInfo.InvariantCulture);
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                if (magnitude == 0)
                {
                    sb.Append('0');
                }
                while (magnitude > 0)
                {
                    sb.Insert(0, (char)('0' + (int)(magnitude % (ulong)radix)));
                    magnitude /= (ulong)radix;
                }
                digits = sb.ToString();
            }

            return (negative ? "-" : "") + (prefix ?? "") + digits;
        }

        /// <summary>
        /// CPython's <c>c</c> presentation: the character at that code point. Out-of-range is
        /// <c>OverflowError("%c arg not in range(0x110000)")</c> — never a raw .NET
        /// <see cref="ArgumentOutOfRangeException"/>, which used to abort the process (#1883 sibling).
        /// Lone surrogates are legal in a Python <c>str</c> (<c>chr(0xD800)</c> works), so they are
        /// built directly rather than through <see cref="char.ConvertFromUtf32"/>, which refuses them.
        /// </summary>
        private static string FormatCodePoint(long codePoint)
        {
            if (codePoint < 0 || codePoint > 0x10FFFF)
            {
                throw new OverflowError("%c arg not in range(0x110000)");
            }

            if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
            {
                return ((char)codePoint).ToString();
            }

            return char.ConvertFromUtf32((int)codePoint);
        }

        /// <summary>Whether a float value is an infinity or a NaN.</summary>
        private static bool IsNonFinite(object value)
        {
            if (value is double d)
            {
                return double.IsNaN(d) || double.IsInfinity(d);
            }
            if (value is float f)
            {
                return float.IsNaN(f) || float.IsInfinity(f);
            }
            return false;
        }

        private static bool IsNegativeZeroText(string s)
        {
            if (s.Length == 0 || s[0] != '-')
            {
                return false;
            }
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '0' && c != '.' && c != ',' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// CPython's <c>g</c>/<c>G</c> presentation: <paramref name="precision"/> significant digits
        /// (default 6, minimum 1), fixed when the decimal exponent is in <c>[-4, precision)</c> and
        /// scientific otherwise, with trailing zeros stripped — unless <paramref name="altForm"/>
        /// (<c>#</c>) is set, which keeps them (<c>format(3.5, '#g')</c> is <c>3.50000</c>).
        /// </summary>
        private static string FormatGeneral(object value, int precision, bool hasPrecision, bool upper,
            bool altForm)
        {
            double d = ToDouble(value);
            int p = hasPrecision ? (precision == 0 ? 1 : precision) : 6;
            string eForm = upper ? "E" : "e";
            string precStr = (p - 1).ToString(CultureInfo.InvariantCulture);
            int exp = ParseExponent(d.ToString(eForm + precStr, CultureInfo.InvariantCulture));

            if (exp >= -4 && exp < p)
            {
                int frac = p - 1 - exp;
                if (frac < 0)
                {
                    frac = 0;
                }
                string fixedText = d.ToString(
                    "F" + frac.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                return altForm ? fixedText : StripFixedZeros(fixedText, false);
            }

            string sci = NormalizeScientific(d.ToString(eForm + precStr, CultureInfo.InvariantCulture));
            return altForm ? sci : StripSciZeros(sci);
        }

        /// <summary>
        /// CPython's float format with NO type character but a precision: like <c>g</c> but the
        /// switch to scientific happens one exponent earlier (<c>exp &gt;= precision - 1</c>) and a
        /// fixed result always keeps at least one fractional digit. <paramref name="altForm"/>
        /// (<c>#</c>) keeps every trailing zero, as it does for <c>g</c>.
        /// </summary>
        private static string FormatFloatSignificant(object value, int precision, bool altForm)
        {
            double d = ToDouble(value);
            int p = precision == 0 ? 1 : precision;
            string precStr = (p - 1).ToString(CultureInfo.InvariantCulture);
            int exp = ParseExponent(d.ToString("e" + precStr, CultureInfo.InvariantCulture));

            if (exp >= -4 && exp < p - 1)
            {
                int frac = p - 1 - exp;
                if (frac < 0)
                {
                    frac = 0;
                }
                string fixedText = d.ToString(
                    "F" + frac.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                return altForm ? fixedText : StripFixedZeros(fixedText, true);
            }

            string sci = NormalizeScientific(d.ToString("e" + precStr, CultureInfo.InvariantCulture));
            return altForm ? sci : StripSciZeros(sci);
        }

        private static int ParseExponent(string sci)
        {
            int ePos = sci.IndexOf('e');
            if (ePos < 0)
            {
                ePos = sci.IndexOf('E');
            }
            if (ePos < 0)
            {
                return 0;
            }
            return int.Parse(sci.Substring(ePos + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static string StripFixedZeros(string s, bool keepOneDecimal)
        {
            if (s.IndexOf('.') < 0)
            {
                return s;
            }
            s = s.TrimEnd('0');
            if (s.EndsWith(".", StringComparison.Ordinal))
            {
                s = keepOneDecimal ? s + "0" : s.Substring(0, s.Length - 1);
            }
            return s;
        }

        private static string StripSciZeros(string s)
        {
            int ePos = s.IndexOf('e');
            if (ePos < 0)
            {
                ePos = s.IndexOf('E');
            }
            if (ePos < 0)
            {
                return StripFixedZeros(s, false);
            }
            string mantissa = StripFixedZeros(s.Substring(0, ePos), false);
            return mantissa + s.Substring(ePos);
        }

        private static string NormalizeScientific(string s)
        {
            // .NET may produce 3-digit exponents (e+000), Python uses 2-digit minimum (e+00)
            int ePos = s.IndexOf('e');
            if (ePos < 0)
                ePos = s.IndexOf('E');
            if (ePos < 0)
                return s;

            string mantissa = s.Substring(0, ePos + 2); // includes e/E and sign
            string exponent = s.Substring(ePos + 2);
            // Remove leading zeros but keep at least 2 digits
            string trimmed = exponent.TrimStart('0');
            if (trimmed.Length < 2)
                trimmed = exponent.Length >= 2 ? exponent.Substring(exponent.Length - 2) : exponent.PadLeft(2, '0');
            return mantissa + trimmed;
        }

        private static string FormatInteger(object value)
        {
            return ToLong(value).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(object value, int precision)
        {
            return ToDouble(value).ToString(
                "F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static long ToLong(object value)
        {
            if (value is bool b)
            {
                return b ? 1L : 0L;
            }
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object value)
        {
            if (value is bool b)
            {
                return b ? 1.0 : 0.0;
            }
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The ONE numeric-prefix predicate (#1959): how many leading characters of a rendered number
        /// sit in front of its digit run — the sign (<c>+</c>, <c>-</c> or space) plus the two-character
        /// <c>0x</c>/<c>0X</c>/<c>0o</c>/<c>0b</c> prefix that <c>#</c> puts on a radix type. Both
        /// <c>=</c> paths pad at this offset: the <c>0</c>-fill path in <see cref="GroupAndZeroFill"/>
        /// and the explicit/default-fill arm of the alignment switch.
        /// </summary>
        private static int NumericPrefixLength(string formatted, char type, bool altForm)
        {
            int signLen = formatted.Length > 0
                && (formatted[0] == '-' || formatted[0] == '+' || formatted[0] == ' ') ? 1 : 0;
            int prefixLen = altForm && (type == 'x' || type == 'X' || type == 'o' || type == 'b') ? 2 : 0;
            return signLen + prefixLen;
        }

        /// <summary>
        /// Insert grouping separators into — and, when the <c>0</c> flag is in force, zero-fill —
        /// the DIGIT RUN of an already-formatted number, leaving the sign, the <c>0x</c>/<c>0o</c>/
        /// <c>0b</c> prefix and the fraction/exponent/percent tail alone.
        /// </summary>
        /// <param name="formatted">The rendered number: sign, optional prefix, digits, tail.</param>
        /// <param name="type">The presentation type, which fixes the group size and the digit run.</param>
        /// <param name="altForm">Whether <c>#</c> put a two-character radix prefix on the front.</param>
        /// <param name="grouping">The separator (<c>,</c> or <c>_</c>), or <c>\0</c> for none.</param>
        /// <param name="minWidthTotal">
        /// The spec's width when it asked for <c>0</c>-fill with <c>=</c> alignment, else 0. CPython
        /// subtracts the sign, the prefix and the tail from it and pads the digits to what is left,
        /// which is why the separators land INSIDE the zero fill
        /// (<c>format(74565, '#012_x')</c> is <c>0x0_0001_2345</c>, not <c>00x1_2345</c>).
        /// </param>
        private static string GroupAndZeroFill(
            string formatted, char type, bool altForm, char grouping, int minWidthTotal)
        {
            int digitsStart = NumericPrefixLength(formatted, type, altForm);
            if (digitsStart > formatted.Length)
            {
                return formatted;
            }

            // Where the digit run ends. For the radix types every remaining character is a digit
            // (a-f are digits in base 16); otherwise the run is the ASCII digits before the '.',
            // the exponent or the '%'.
            int digitsEnd;
            if (type == 'x' || type == 'X' || type == 'o' || type == 'b')
            {
                digitsEnd = formatted.Length;
            }
            else
            {
                digitsEnd = digitsStart;
                while (digitsEnd < formatted.Length && formatted[digitsEnd] >= '0' && formatted[digitsEnd] <= '9')
                {
                    digitsEnd++;
                }
            }

            // No leading digits at all means this is "inf"/"nan" (or a 'c' character): CPython
            // zero-fills it to the width but inserts no separators.
            char separator = grouping;
            if (digitsEnd == digitsStart)
            {
                digitsEnd = formatted.Length;
                separator = '\0';
            }

            string digits = formatted.Substring(digitsStart, digitsEnd - digitsStart);
            string tail = formatted.Substring(digitsEnd);

            if (separator == '\0' && minWidthTotal <= 0)
            {
                return formatted;
            }

            int minWidth = minWidthTotal <= 0 ? 0 : minWidthTotal - digitsStart - tail.Length;
            string grouped = InsertThousandsGrouping(digits, minWidth, GroupSizeFor(type), separator);

            return formatted.Substring(0, digitsStart) + grouped + tail;
        }

        /// <summary>
        /// A port of CPython's <c>_PyUnicode_InsertThousandsGrouping</c> for a uniform group size.
        /// Groups are emitted right to left; a group short of <paramref name="minWidth"/> is padded
        /// with zeros, so the fill and the separators interleave
        /// (<c>format(1234, '012_d')</c> is <c>0_000_001_234</c>, thirteen characters for a width of
        /// twelve — CPython never lets a separator be the leading character).
        /// </summary>
        private static string InsertThousandsGrouping(
            string digits, int minWidth, int groupSize, char separator)
        {
            if (separator == '\0')
            {
                return digits.Length >= minWidth
                    ? digits
                    : new string('0', minWidth - digits.Length) + digits;
            }

            var sb = new System.Text.StringBuilder();
            int remaining = digits.Length;
            int cursor = digits.Length;

            while (true)
            {
                int len = Math.Min(groupSize, Math.Max(Math.Max(remaining, minWidth), 1));
                int chars = Math.Max(0, Math.Min(remaining, len));
                int zeros = Math.Max(0, len - remaining);

                if (chars > 0)
                {
                    sb.Insert(0, digits.Substring(cursor - chars, chars));
                    cursor -= chars;
                    remaining -= chars;
                }
                if (zeros > 0)
                {
                    sb.Insert(0, new string('0', zeros));
                }

                minWidth -= len;
                if (remaining <= 0 && minWidth <= 0)
                {
                    break;
                }

                sb.Insert(0, separator);
                minWidth--;
            }

            return sb.ToString();
        }
    }
}
