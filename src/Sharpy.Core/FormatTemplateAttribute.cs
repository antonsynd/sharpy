using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a parameter as a PEP 3101 format template (<c>"{0:>5}"</c>) whose replacement fields
    /// are filled from the method's positional arguments, so the literal specs inside a literal
    /// template get the same compile-time check an f-string hole's spec gets.
    /// </summary>
    /// <remarks>
    /// The checker's <c>ReportClrCallDecision</c> (Selected arm) reads the attribute, by its full
    /// type name, from the selected method's first parameter; when the receiver is a string literal
    /// it splits the template with <c>FormatTemplateGrammar</c>, pairs each static hole with its
    /// positional operand, validates the spec through <c>FormatSpecGrammar</c> and reports SPY0609
    /// (#1956). Holes whose operand or spec is not known statically keep the runtime
    /// <c>ValueError</c>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class FormatTemplateAttribute : Attribute
    {
    }
}
