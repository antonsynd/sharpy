using System;

namespace Sharpy
{
    /// <summary>
    /// Subclass of ValueError for JSON parse errors, matching Python's json.JSONDecodeError.
    /// </summary>
    public class JsonDecodeError : ValueError
    {
        /// <summary>The unformatted error message.</summary>
        public string Msg { get; }

        /// <summary>The JSON document being parsed.</summary>
        public string Doc { get; }

        /// <summary>The position in the document where the error occurred.</summary>
        public int Pos { get; }

        public JsonDecodeError(string msg, string doc, int pos)
            : base(FormatMessage(msg, doc, pos))
        {
            Msg = msg;
            Doc = doc;
            Pos = pos;
        }

        private static string FormatMessage(string msg, string doc, int pos)
        {
            // Calculate line and column from position
            int line = 1;
            int col = 1;
            for (int i = 0; i < pos && i < doc.Length; i++)
            {
                if (doc[i] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
            return msg + ": line " + line + " column " + col + " (char " + pos + ")";
        }
    }
}
