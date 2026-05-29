using System;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Base exception for all yaml-related errors.
    /// Mirrors Python's <c>yaml.YAMLError</c>, which subclasses <c>Exception</c>.
    /// </summary>
    [SharpyModuleType("yaml", "YAMLError")]
    public class YAMLError : Exception
    {
        /// <summary>Create a YAMLError with no message.</summary>
        public YAMLError() : base() { }

        /// <summary>Create a YAMLError with the specified message.</summary>
        public YAMLError(string message) : base(message) { }

        /// <summary>Create a YAMLError with the specified message and inner exception.</summary>
        public YAMLError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception raised when a YAML document cannot be parsed.
    /// Mirrors Python's <c>yaml.YAMLError</c> subclasses (scanner/parser errors),
    /// carrying the problem description, surrounding context, and source location.
    /// </summary>
    [SharpyModuleType("yaml", "YAMLParseError")]
    public class YAMLParseError : YAMLError
    {
        /// <summary>The 1-based line number where parsing failed, or -1 if unknown.</summary>
        public long Line { get; }

        /// <summary>The 1-based column number where parsing failed, or -1 if unknown.</summary>
        public long Column { get; }

        /// <summary>A short description of the problem that caused the failure.</summary>
        public string? Problem { get; }

        /// <summary>Additional context describing where the problem occurred.</summary>
        public string? Context { get; }

        /// <summary>Create a YAMLParseError with the given problem, context, and location.</summary>
        public YAMLParseError(string? problem, string? context, long line, long column)
            : base(FormatMessage(problem, context, line, column))
        {
            Problem = problem;
            Context = context;
            Line = line;
            Column = column;
        }

        /// <summary>Create a YAMLParseError wrapping an underlying exception.</summary>
        public YAMLParseError(string? problem, string? context, long line, long column, Exception innerException)
            : base(FormatMessage(problem, context, line, column), innerException)
        {
            Problem = problem;
            Context = context;
            Line = line;
            Column = column;
        }

        private static string FormatMessage(string? problem, string? context, long line, long column)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(problem) ? "error parsing YAML" : problem);

            if (\!string.IsNullOrEmpty(context))
            {
                sb.Append("; ");
                sb.Append(context);
            }

            if (line >= 0)
            {
                sb.Append("\n  in \"<unicode string>\", line ");
                sb.Append(line.ToString(CultureInfo.InvariantCulture));
                sb.Append(", column ");
                sb.Append(column.ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
