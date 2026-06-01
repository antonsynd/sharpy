using System;
using System.Xml;

namespace Sharpy
{
    /// <summary>
    /// Exception raised when XML parsing fails.
    /// Mirrors Python's xml.etree.ElementTree.ParseError.
    /// </summary>
    [SharpyModuleType("xml")]
    public class ParseError : Exception
    {
        /// <summary>The character position where parsing failed.</summary>
        public int Position { get; }

        /// <summary>The 1-based line number where the error occurred.</summary>
        public int Line { get; }

        /// <summary>The 1-based column number where the error occurred.</summary>
        public int Column { get; }

        /// <summary>Create a ParseError with optional position information.</summary>
        public ParseError(string message, int position = 0, int line = 0, int column = 0)
            : base(FormatMessage(message, line, column))
        {
            Position = position;
            Line = line;
            Column = column;
        }

        /// <summary>Create a ParseError from a .NET XmlException.</summary>
        internal static ParseError FromXmlException(XmlException ex)
        {
            return new ParseError(
                ex.Message,
                position: 0,
                line: ex.LineNumber,
                column: ex.LinePosition);
        }

        private static string FormatMessage(string message, int line, int column)
        {
            if (line > 0)
            {
                return message + " (line " + line.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", column " + column.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            }

            return message;
        }
    }
}
