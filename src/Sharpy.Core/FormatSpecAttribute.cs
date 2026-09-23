using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a parameter as a PEP 3101 format specification applied to another parameter of the
    /// same method (the value it formats), so a literal spec gets the same compile-time check an
    /// f-string hole's spec gets.
    /// </summary>
    /// <remarks>
    /// Discovery (<c>OverloadIndexBuilder.CreateParameterSignature</c>) records the attribute, read
    /// by its full type name, as <c>ParameterSignature.FormatSpecOf</c>; the checker's
    /// <c>CheckStaticFormatSpecArguments</c> (run from <c>RecordResolvedCallTarget</c>) validates a
    /// string-literal argument bound to the marked parameter against the value argument's operand
    /// kind through <c>FormatSpecGrammar</c> and reports SPY0609 (#1956). A dynamic spec keeps the
    /// runtime <c>ValueError</c> from <see cref="PyFormat"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class FormatSpecAttribute : Attribute
    {
        /// <summary>The name of the parameter whose value the spec formats.</summary>
        public string ValueParameter { get; }

        /// <summary>Create the attribute naming the formatted value's parameter.</summary>
        public FormatSpecAttribute(string valueParameter)
        {
            ValueParameter = valueParameter;
        }
    }
}
