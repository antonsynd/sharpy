using System;

namespace Sharpy
{
    /// <summary>
    /// Supplies the sentence appended to an "unknown keyword argument" diagnostic (SPY0234) when a
    /// call to the marked method passes a keyword it cannot accept, steering the user to the
    /// spelling that does accept it.
    /// </summary>
    /// <remarks>
    /// The checker's <c>ReportUnknownClrKeyword</c> reads the attribute, by its full type name,
    /// from every candidate method of the refused call; when all candidates carry the same steer it
    /// is appended to the message (R-BE, #1955).
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
