using System;
using System.IO;

namespace Sharpy
{
    public static partial class OsPathModule
    {
        public static string Join(string a, string b, string c)
        {
            return System.IO.Path.Combine(a, b, c);
        }

        public static string Join(string a, string b, string c, string d)
        {
            return System.IO.Path.Combine(a, b, c, d);
        }

        public static string Normpath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ".";

            char sep = System.IO.Path.DirectorySeparatorChar;
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var stack = new System.Collections.Generic.List<string>();
            bool isAbsolute = path[0] == '/' || path[0] == '\\' ||
                              (path.Length >= 2 && path[1] == ':');

            foreach (string part in parts)
            {
                if (part == ".")
                    continue;
                if (part == "..")
                {
                    if (stack.Count > 0 && stack[stack.Count - 1] != "..")
                        stack.RemoveAt(stack.Count - 1);
                    else if (!isAbsolute)
                        stack.Add("..");
                }
                else
                {
                    stack.Add(part);
                }
            }

            string result;
            if (isAbsolute)
            {
                string prefix = (path.Length >= 2 && path[1] == ':')
                    ? path.Substring(0, 2) + sep
                    : sep.ToString();
                result = prefix + string.Join(sep.ToString(), stack);
            }
            else
            {
                result = stack.Count > 0 ? string.Join(sep.ToString(), stack) : ".";
            }

            return result;
        }
    }
}
