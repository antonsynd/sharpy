using System;

namespace Sharpy
{
    /// <summary>
    /// Supplies the sentence appended to an "unknown keyword argument" diagnostic (SPY0234) when a
    /// call to the marked method passes a keyword it cannot accept, steering the user to the
    /// spelling that does accept it.
    /// </summary>
    /// <remarks>
    /// Read ONLY on the <c>str</c> methods, the <c>Sharpy.StringExtensions</c> extension methods
    /// over <c>string</c>: the compiler's <c>BuiltinRegistry.DiscoverStringExtensionMethods</c>
    /// reads the attribute, by its full type name, into <c>FunctionSymbol.KeywordSteer</c>; the
    /// checker records the resolved callee at <c>RecordResolvedCallTarget</c>, and
    /// <c>ReportUnknownKeywordArgument</c> appends that callee's steer to the SPY0234 message
    /// (R-BE, #1955). On any other method the attribute is ignored.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SharpyKeywordSteerAttribute : Attribute
    {
        /// <summary>The steer sentence appended to the diagnostic.</summary>
        public string Steer { get; }

        /// <summary>Create the attribute with the steer sentence.</summary>
        public SharpyKeywordSteerAttribute(string steer)
        {
            Steer = steer;
        }
    }
}
