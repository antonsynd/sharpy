using System.Text;

namespace Sharpy
{
    public static partial class ShlexModule
    {
        public static List<string> Split(string s, bool comments = false, bool posix = true)
        {
            if (s == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (!posix)
            {
                throw new ValueError("non-POSIX mode is not supported");
            }

            var tokens = new List<string>();
            var token = new StringBuilder();
            bool inToken = false;
            int i = 0;

            while (i < s.Length)
            {
                char c = s[i];

                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length)
                    {
                        throw new ValueError("No escaped character");
                    }

                    inToken = true;
                    token.Append(s[i]);
                    i++;
                }
                else if (c == '\'')
                {
                    inToken = true;
                    i++;
                    while (i < s.Length && s[i] != '\'')
                    {
                        token.Append(s[i]);
                        i++;
                    }

                    if (i >= s.Length)
                    {
                        throw new ValueError("No closing quotation");
                    }

                    i++;
                }
                else if (c == '"')
                {
                    inToken = true;
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == '\\')
                        {
                            i++;
                            if (i >= s.Length)
                            {
                                throw new ValueError("No closing quotation");
                            }

                            char next = s[i];
                            if (next == '\\' || next == '"' || next == '$' || next == '`' || next == '\n')
                            {
                                token.Append(next);
                            }
                            else
                            {
                                token.Append('\\');
                                token.Append(next);
                            }

                            i++;
                        }
                        else
                        {
                            token.Append(s[i]);
                            i++;
                        }
                    }

                    if (i >= s.Length)
                    {
                        throw new ValueError("No closing quotation");
                    }

                    i++;
                }
                else if (c == '#' && comments && !inToken)
                {
                    break;
                }
                else if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    if (inToken)
                    {
                        tokens.Add(token.ToString());
                        token.Clear();
                        inToken = false;
                    }

                    i++;
                }
                else
                {
                    inToken = true;
                    token.Append(c);
                    i++;
                }
            }

            if (inToken)
            {
                tokens.Add(token.ToString());
            }

            return tokens;
        }

        public static string Quote(string s)
        {
            if (s == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (s.Length == 0)
            {
                return "''";
            }

            bool safe = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (!IsShellSafe(s[i]))
                {
                    safe = false;
                    break;
                }
            }

            if (safe)
            {
                return s;
            }

            var sb = new StringBuilder(s.Length + 2);
            sb.Append('\'');
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\'')
                {
                    sb.Append("'\"'\"'");
                }
                else
                {
                    sb.Append(s[i]);
                }
            }

            sb.Append('\'');
            return sb.ToString();
        }

        public static string Join(List<string> splitCommand)
        {
            if (splitCommand == null)
            {
                throw new TypeError("argument must be list, not NoneType");
            }

            var sb = new StringBuilder();
            int count = ((System.Collections.Generic.ICollection<string>)splitCommand).Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(Quote(splitCommand[i]));
            }

            return sb.ToString();
        }

        private static bool IsShellSafe(char c)
        {
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;
            return c == '@' || c == '%' || c == '+' || c == '=' ||
                   c == ':' || c == ',' || c == '.' || c == '/' ||
                   c == '-' || c == '_';
        }
    }
}
