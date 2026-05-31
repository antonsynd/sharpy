using System;

namespace Sharpy
{
    /// <summary>Represents a TOML decoding error.</summary>
    [SharpyModuleType("toml")]
    public class TOMLDecodeError : ValueError
    {
        /// <summary>Get the original error message.</summary>
        public string Msg { get; }

        /// <summary>Get the TOML document that failed to decode.</summary>
        public string Doc { get; }

        /// <summary>Get the zero-based error position in the document.</summary>
        public int Pos { get; }

        /// <summary>Initialize a TOML decoding error.</summary>
        public TOMLDecodeError(string msg, string doc, int pos)
            : base(FormatMessage(msg, doc, pos))
        {
            Msg = msg;
            Doc = doc;
            Pos = pos;
        }

        private static string FormatMessage(string msg, string doc, int pos)
        {
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
