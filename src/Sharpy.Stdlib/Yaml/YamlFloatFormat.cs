using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// The one place a float becomes YAML text (#1229).
    /// </summary>
    /// <remarks>
    /// <para>
    /// PyYAML does NOT spell floats the way <c>repr</c> does, so this is not simply
    /// <see cref="Builtins.FormatFloat(double)"/>. The rule, MEASURED against <b>PyYAML 6.0.3</b>
    /// under CPython 3.12.13 rather than derived from <c>json.dumps</c>:
    /// </para>
    ///
    /// <para>
    /// <b>PyYAML = repr, except that in exponent form the mantissa is forced to contain a decimal
    /// point.</b> Where the mantissa already has one, the two agree; positional forms always agree.
    /// </para>
    ///
    /// <code>
    ///   value      repr / json.dumps     PyYAML          agree?
    ///   1e20       1e+20                 1.0e+20         no
    ///   1e-5       1e-05                 1.0e-05         no
    ///   -1e20      -1e+20                -1.0e+20        no
    ///   5e-324     5e-324                5.0e-324        no
    ///   2.5e-10    2.5e-10               2.5e-10         yes  (mantissa already has '.')
    ///   1.5e+20    1.5e+20               1.5e+20         yes
    ///   1e15       1000000000000000.0    1000000000000000.0  yes  (positional)
    ///   0.1 1.0 -0.0 3.14159             identical       yes
    /// </code>
    ///
    /// <para>
    /// The table is here so a future reader does not "correct" this toward <c>repr</c> — which is
    /// what the original plan for #1229 assumed, before the spellings were actually measured.
    /// </para>
    ///
    /// <para>
    /// Both YAML surfaces read this: the round-trip emitter (<c>YamlRoundtrip</c>) and the
    /// <c>safe_dump</c> serializer (<c>Yaml.DumpSingle</c>, via <c>YamlFloatEventEmitter</c>). They
    /// had drifted — <c>safe_dump</c> emitted <c>1</c> for <c>1.0</c>, which reloads as an
    /// <b>int</b> — so one helper rather than two implementations is the point, not a convenience.
    /// </para>
    /// </remarks>
    internal static class YamlFloatFormat
    {
        /// <summary>YAML text for a double, including the special values.</summary>
        internal static string Format(double value)
        {
            if (double.IsNaN(value))
                return ".nan";
            if (double.IsPositiveInfinity(value))
                return ".inf";
            if (double.IsNegativeInfinity(value))
                return "-.inf";

            return ApplyMantissaRule(Builtins.FormatFloat(value));
        }

        /// <summary>
        /// YAML text for a single-precision float, formatted at its OWN precision — widening to
        /// double first would change the shortest-round-trip digits (the #1204 defect).
        /// </summary>
        internal static string Format(float value)
        {
            if (float.IsNaN(value))
                return ".nan";
            if (float.IsPositiveInfinity(value))
                return ".inf";
            if (float.IsNegativeInfinity(value))
                return "-.inf";

            return ApplyMantissaRule(Builtins.FormatFloat(value));
        }

        /// <summary>
        /// Forces a decimal point into an exponent-form mantissa, which is the single respect in
        /// which PyYAML differs from <c>repr</c>. A positional form is returned unchanged, so a whole
        /// value keeps the <c>.0</c> the authority already gave it.
        /// </summary>
        private static string ApplyMantissaRule(string repr)
        {
            int e = repr.IndexOf('e');
            if (e < 0)
            {
                return repr;
            }

            string mantissa = repr.Substring(0, e);
            if (mantissa.IndexOf('.') >= 0)
            {
                return repr;
            }

            return string.Concat(mantissa, ".0", repr.Substring(e));
        }
    }
}
