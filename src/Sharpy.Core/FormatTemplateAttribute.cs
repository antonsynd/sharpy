using System;

namespace Sharpy
{
    /// <summary>
    /// Marks a parameter as a PEP 3101 format template (<c>"{0:>5}"</c>) whose replacement fields
    /// are filled from the method's positional arguments, so the literal specs inside a literal
    /// template get the same compile-time check an f-string hole's spec gets.
    /// </summary>
    /// <remarks>
    /// Read ONLY on the <c>this string</c> receiver parameter of a <c>str</c> method, a
    /// <c>Sharpy.StringExtensions</c> extension method over <c>string</c>: the compiler's
    /// <c>BuiltinRegistry.DiscoverStringExtensionMethods</c> reads the attribute, by its full type
    /// name, into <c>FunctionSymbol.IsFormatTemplateReceiver</c>. The checker's
    /// <c>RecordResolvedCallTarget</c> runs <c>CheckStaticFormatTemplateArguments</c>, which, when
    /// the receiver is a string literal, splits the template with <c>FormatTemplateGrammar</c>,
    /// pairs each static hole with its positional operand, validates the spec through
    /// <c>FormatSpecGrammar</c> and reports SPY0609 (#1956). Holes whose operand or spec is not
    /// known statically keep the runtime <c>ValueError</c>. On any other parameter the attribute is
    /// ignored.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class FormatTemplateAttribute : Attribute
    {
    }
}
