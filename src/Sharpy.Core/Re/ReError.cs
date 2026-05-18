using System;

namespace Sharpy
{
    /// <summary>
    /// Exception raised when a regex pattern is invalid.
    /// Equivalent to Python's <c>re.error</c>.
    /// </summary>
    [SharpyModuleType("re", "error")]
    public class ReError : Exception
    {
        /// <summary>The unformatted error message.</summary>
        public string Msg { get; }

        /// <summary>The regex pattern that caused the error, if available.</summary>
        public string? PatternStr { get; }

        /// <summary>The position in the pattern where the error occurred, if available.</summary>
        public int? Pos { get; }

        /// <summary>The line number of the error position, if available.</summary>
        public int? Lineno { get; }

        /// <summary>The column number of the error position, if available.</summary>
        public int? Colno { get; }

        /// <summary>Create a ReError with the specified message and optional pattern/position info.</summary>
        public ReError(string msg, string? pattern = null, int? pos = null)
            : base(msg)
        {
            Msg = msg;
            PatternStr = pattern;
            Pos = pos;
            if (pos != null && pattern != null)
            {
                int line = 1;
                int col = pos.Value + 1;
                for (int i = 0; i < pos.Value && i < pattern.Length; i++)
                {
                    if (pattern[i] == '\n')
                    {
                        line++;
                        col = pos.Value - i;
                    }
                }

                Lineno = line;
                Colno = col;
            }
        }
    }
}
