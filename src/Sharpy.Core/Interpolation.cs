namespace Sharpy
{
    /// <summary>
    /// Represents an interpolated value within a template string (PEP 750).
    /// Contains the evaluated value, the original expression text, and an optional format spec.
    /// </summary>
    public class Interpolation
    {
        /// <summary>The evaluated interpolation result.</summary>
        public object Value { get; }

        /// <summary>The source text of the expression (e.g., "name", "x + 1").</summary>
        public string Expression { get; }

        /// <summary>Format specification string (e.g., ".2f"), empty string if none.</summary>
        public string FormatSpec { get; }

        /// <summary>Create an Interpolation with the given value, expression text, and format spec.</summary>
        public Interpolation(object value, string expression, string formatSpec)
        {
            Value = value;
            Expression = expression ?? string.Empty;
            FormatSpec = formatSpec ?? string.Empty;
        }

        /// <summary>
        /// Formats the value through the one Python format-spec engine (<see cref="PyFormat.Apply"/>),
        /// so a t-string renders a value identically to <c>str.format</c>, <c>format()</c> and every
        /// f-string hole instead of being a fourth authority.
        /// </summary>
        public override string ToString()
        {
            return PyFormat.Apply(Value, FormatSpec);
        }

        /// <summary>
        /// Returns a Python-style repr of this Interpolation.
        /// </summary>
        public string Repr()
        {
            var valueRepr = Value == null ? "None" : Value.ToString();
            if (string.IsNullOrEmpty(FormatSpec))
                return $"Interpolation({valueRepr}, '{Expression}')";
            return $"Interpolation({valueRepr}, '{Expression}', '{FormatSpec}')";
        }
    }
}
