using System;

namespace Sharpy
{
    /// <summary>
    /// Subclass of ValueError raised when a JSON document cannot be decoded.
    /// Mirrors Python's json.JSONDecodeError.
    /// </summary>
    [SharpyModuleType("json")]
    public class JSONDecodeError : ValueError
    {
        /// <summary>The unformatted error message.</summary>
        public string Msg { get; }

        /// <summary>The JSON document being parsed.</summary>
        public string Doc { get; }

        /// <summary>The index in doc where parsing failed.</summary>
        public int Pos { get; }

        /// <summary>Create a JSONDecodeError with the specified message, document, and position.</summary>
        public JSONDecodeError(string msg, string doc, int pos)
            : base(FormatMessage(msg, doc, pos))
        {
            Msg = msg;
            Doc = doc;
            Pos = pos;
        }

        private static string FormatMessage(string msg, string doc, int pos)
        {
            // Compute line and column from position
            int line = 1;
            int col = 0;
            int limit = System.Math.Min(pos, doc.Length);
            for (int i = 0; i < limit; i++)
            {
                if (doc[i] == '\n')
                {
                    line++;
                    col = 0;
                }
                else
                {
                    col++;
                }
            }

            return msg + ": line " + line.ToString(System.Globalization.CultureInfo.InvariantCulture) + " column " + (col + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " (char " + pos.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }
    }
}
