// Generated from src/Sharpy.Stdlib/spy/fnmatch_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/fnmatch_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    public static partial class FnmatchModule
    {
        public static bool Fnmatch(string name, string pat)
        {
            if (name == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            if (pat == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            if (global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows))
            {
                name = name.ToLowerInvariant();
                pat = pat.ToLowerInvariant();
            }

            return Fnmatchcase(name, pat);
        }

        public static bool Fnmatchcase(string name, string pat)
        {
            if (name == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            if (pat == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            string regexPattern = Translate(pat);
            return global::System.Text.RegularExpressions.Regex.IsMatch(name, regexPattern);
        }

        public static Sharpy.List<string> Filter(Sharpy.List<string> names, string pat)
        {
            if (names == null)
            {
                throw new global::Sharpy.TypeError("argument must be list, not NoneType");
            }

            if (pat == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            string matchPat = pat;
            bool caseInsensitive = global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows);
            if (caseInsensitive)
            {
                matchPat = matchPat.ToLowerInvariant();
            }

            string regexPattern = Translate(matchPat);
            global::System.Text.RegularExpressions.Regex regex = new global::System.Text.RegularExpressions.Regex(regexPattern);
            Sharpy.List<string> result = new Sharpy.List<string>()
            {
            };
            foreach (var __loopVar_0 in names)
            {
                var name = __loopVar_0;
                string testName = caseInsensitive ? name.ToLowerInvariant() : name;
                if (regex.IsMatch(testName))
                {
                    result.Append(name);
                }
            }

            return result;
        }

        public static string Translate(string pat)
        {
            if (pat == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
            sb.Append("\\A(?s:");
            int i = 0;
            while (i < pat.Length)
            {
                string c = global::Sharpy.StringHelpers.GetItem(pat, i);
                i = i + 1;
                if (c == "*")
                {
                    sb.Append(".*");
                }
                else if (c == "?")
                {
                    sb.Append(".");
                }
                else if (c == "[")
                {
                    int j = i;
                    if (j < pat.Length && global::Sharpy.StringHelpers.GetItem(pat, j) == "!")
                    {
                        j = j + 1;
                    }

                    if (j < pat.Length && global::Sharpy.StringHelpers.GetItem(pat, j) == "]")
                    {
                        j = j + 1;
                    }

                    while (j < pat.Length && global::Sharpy.StringHelpers.GetItem(pat, j) != "]")
                    {
                        j = j + 1;
                    }

                    if (j >= pat.Length)
                    {
                        sb.Append("\\[");
                    }
                    else
                    {
                        string stuff = pat.Substring(i, j - i);
                        i = j + 1;
                        stuff = stuff.Replace("\\", "\\\\");
                        if (global::Sharpy.Builtins.Len(stuff) > 0 && stuff.StartsWith("!"))
                        {
                            stuff = "^" + stuff.Substring(1);
                        }

                        sb.Append("[");
                        sb.Append(stuff);
                        sb.Append("]");
                    }
                }
                else
                {
                    sb.Append(global::System.Text.RegularExpressions.Regex.Escape(c));
                }
            }

            sb.Append(")\\Z");
            return sb.ToString();
        }
    }
}
