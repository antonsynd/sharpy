namespace Sharpy
{
    /// <summary>
    /// The CLR spelling of <c>__repr__</c> for a Core/Stdlib type whose python <c>repr</c> differs from
    /// its <c>str</c> (<see cref="object.ToString"/> is both <c>__str__</c> and, by default,
    /// <c>__repr__</c>). <see cref="Builtins.Repr"/> asks it before the <c>ToString</c> fallback, so
    /// <c>repr()</c>, <c>!r</c>, <c>ascii()</c> and every container element agree: <c>Optional</c>
    /// prints <c>Some(1)</c>/<c>None()</c> while <c>str</c> stays transparent, a <c>Template</c>'s
    /// repr is its PEP 750 spelling while <c>str</c> renders it, a <c>datetime.date</c> prints
    /// <c>datetime.date(2020, 1, 2)</c> while <c>str</c> is ISO text.
    /// </summary>
    public interface IRepr
    {
        /// <summary>Python's <c>repr()</c> of this value.</summary>
        string Repr();
    }
}
