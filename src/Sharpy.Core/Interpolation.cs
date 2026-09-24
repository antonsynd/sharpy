namespace Sharpy
{
    /// <summary>
    /// Represents an interpolated value within a template string (PEP 750).
    /// Contains the evaluated value, the original expression text, the optional conversion
    /// (<c>!r</c>/<c>!s</c>/<c>!a</c>) and an optional format spec.
    /// </summary>
    public class Interpolation
    {
        /// <summary>The evaluated interpolation result.</summary>
        public object Value { get; }

        /// <summary>The source text of the expression (e.g., "name", "x + 1").</summary>
        public string Expression { get; }

        /// <summary>
        /// The conversion (PEP 750's <c>conversion</c>): <c>"r"</c>, <c>"s"</c>, <c>"a"</c>, or
        /// <c>null</c> when the field had none. A self-documenting <c>{x=}</c> with no format spec
        /// carries <c>"r"</c>, as in PEP 750.
        /// </summary>
        public string? Conversion { get; }

        /// <summary>Format specification string (e.g., ".2f"), empty string if none.</summary>
        public string FormatSpec { get; }

        /// <summary>
        /// Create an Interpolation with the given value, expression text, format spec and optional
        /// conversion (<c>"r"</c>, <c>"s"</c>, <c>"a"</c> or <c>null</c>).
        /// </summary>
        public Interpolation(object value, string expression, string formatSpec, string? conversion = null)
        {
            if (conversion != null && conversion != "r" && conversion != "s" && conversion != "a")
            {
                throw new ValueError("Interpolation() argument 'conversion' must be one of 's', 'a' or 'r'");
            }

            Value = value;
            Expression = expression ?? string.Empty;
            FormatSpec = formatSpec ?? string.Empty;
            Conversion = conversion;
        }

        /// <summary>
        /// Applies the conversion, then formats the result through the one Python format-spec engine
        /// (<see cref="PyFormat.Apply"/>) — the same <c>Builtins.Repr/Str/Ascii</c> then
        /// <c>PyFormat.Apply</c> sequence an f-string hole lowers to, so a t-string renders a value
        /// identically to <c>str.format</c>, <c>format()</c> and every f-string hole instead of being
        /// a fourth authority.
        /// </summary>
        public override string ToString()
        {
            return PyFormat.Apply(ConvertedValue(), FormatSpec);
        }

        /// <summary>The value after the conversion: <c>repr</c>/<c>str</c>/<c>ascii</c> of it, or itself.</summary>
        private object ConvertedValue()
        {
            switch (Conversion)
            {
                case "r":
                    return Builtins.Repr(Value);
                case "s":
                    return Builtins.Str(Value);
                case "a":
                    return Builtins.Ascii(Value);
                default:
                    return Value;
            }
        }

        /// <summary>
        /// Python's <c>repr</c> of this Interpolation, in PEP 750's positional order — always all four
        /// positions: <c>Interpolation(value, expression, conversion, format_spec)</c>, each spelled
        /// with <see cref="Builtins.Repr"/> and a missing conversion as <c>None</c>
        /// (python3.14: <c>Interpolation('ab', 's', None, '&gt;6')</c>). <see cref="ToString"/> stays
        /// the renderer; <c>repr()</c>, <c>!r</c> and <c>ascii()</c> reach this through
        /// <see cref="Builtins.Repr"/>.
        /// </summary>
        public string Repr()
        {
            return "Interpolation(" + Builtins.Repr(Value) + ", " + Builtins.Repr(Expression) + ", "
                + (Conversion == null ? "None" : Builtins.Repr(Conversion)) + ", "
                + Builtins.Repr(FormatSpec) + ")";
        }
    }
}
