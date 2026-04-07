using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Split, join, partition methods for Str.
    /// </summary>
    public readonly partial struct Str
    {
        /// <summary>
        /// Return a string which is the concatenation of the strings in
        /// <paramref name="iterable"/>. The separator between elements is this
        /// string.
        /// Python: <c>str.join(iterable)</c> — called as <c>separator.join(list)</c>.
        /// </summary>
        public Str Join(IEnumerable<Str> iterable)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var item in iterable)
            {
                parts.Add(item.Value);
            }
            return new Str(string.Join(Value, parts));
        }

        /// <summary>
        /// Overload accepting <c>IEnumerable&lt;string&gt;</c> for .NET interop.
        /// </summary>
        public Str Join(IEnumerable<string> iterable)
        {
            return new Str(string.Join(Value, iterable));
        }

        /// <summary>
        /// Split on whitespace. Consecutive whitespace is collapsed,
        /// leading/trailing whitespace is stripped.
        /// Python: <c>str.split()</c>
        /// </summary>
        public List<Str> Split()
        {
            var result = new List<Str>();
            int i = 0;
            while (i < Value.Length)
            {
                while (i < Value.Length && char.IsWhiteSpace(Value[i]))
                {
                    i++;
                }
                if (i >= Value.Length)
                {
                    break;
                }
                int start = i;
                while (i < Value.Length && !char.IsWhiteSpace(Value[i]))
                {
                    i++;
                }
                result.Add(new Str(Value.Substring(start, i - start)));
            }
            return result;
        }

        /// <summary>
        /// Split on a separator string.
        /// Python: <c>str.split(sep)</c>
        /// </summary>
        public List<Str> Split(Str sep)
        {
            return Split(sep, -1);
        }

        /// <summary>
        /// Split on a separator string, performing at most
        /// <paramref name="maxsplit"/> splits (from the left).
        /// Python: <c>str.split(sep, maxsplit)</c>
        /// </summary>
        public List<Str> Split(Str sep, int maxsplit)
        {
            string sepStr = (string)sep;
            if (sepStr == null)
            {
                throw TypeError.ArgNone("split", "sep");
            }
            if (sepStr.Length == 0)
            {
                throw new ValueError("empty separator");
            }

            var result = new List<Str>();
            int start = 0;
            int splits = 0;

            while (start <= Value.Length)
            {
                if (maxsplit >= 0 && splits >= maxsplit)
                {
                    break;
                }
                int index = Value.IndexOf(sepStr, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
                result.Add(new Str(Value.Substring(start, index - start)));
                start = index + sepStr.Length;
                splits++;
            }
            result.Add(new Str(Value.Substring(start)));
            return result;
        }

        /// <summary>
        /// Split on whitespace from the right.
        /// Python: <c>str.rsplit()</c>
        /// </summary>
        public List<Str> Rsplit()
        {
            return Split();
        }

        /// <summary>
        /// Split on a separator string from the right.
        /// Python: <c>str.rsplit(sep)</c>
        /// </summary>
        public List<Str> Rsplit(Str sep)
        {
            return Rsplit(sep, -1);
        }

        /// <summary>
        /// Split on a separator string from the right, performing at most
        /// <paramref name="maxsplit"/> splits.
        /// Python: <c>str.rsplit(sep, maxsplit)</c>
        /// </summary>
        public List<Str> Rsplit(Str sep, int maxsplit)
        {
            string sepStr = (string)sep;
            if (sepStr == null)
            {
                throw TypeError.ArgNone("rsplit", "sep");
            }
            if (sepStr.Length == 0)
            {
                throw new ValueError("empty separator");
            }

            if (maxsplit < 0)
            {
                return Split(sep, -1);
            }

            var parts = new System.Collections.Generic.List<Str>();
            int end = Value.Length;
            int splits = 0;

            while (end > 0 && splits < maxsplit)
            {
                int index = Value.LastIndexOf(sepStr, end - 1, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
                parts.Add(new Str(Value.Substring(index + sepStr.Length, end - index - sepStr.Length)));
                end = index;
                splits++;
            }
            parts.Add(new Str(Value.Substring(0, end)));
            parts.Reverse();
            return new List<Str>(parts);
        }

        /// <summary>
        /// Return a list of the lines in the string, breaking at line boundaries.
        /// Python: <c>str.splitlines()</c>
        /// </summary>
        public List<Str> Splitlines()
        {
            return Splitlines(false);
        }

        /// <summary>
        /// Return a list of lines, optionally keeping line break characters.
        /// Python: <c>str.splitlines(keepends)</c>
        /// </summary>
        public List<Str> Splitlines(bool keepends)
        {
            var result = new List<Str>();
            if (string.IsNullOrEmpty(Value))
            {
                return result;
            }

            var currentLine = new StringBuilder();
            for (int i = 0; i < Value.Length; i++)
            {
                char c = Value[i];

                if (c == '\r')
                {
                    if (i + 1 < Value.Length && Value[i + 1] == '\n')
                    {
                        if (keepends)
                        {
                            currentLine.Append("\r\n");
                        }
                        i++;
                    }
                    else
                    {
                        if (keepends)
                        {
                            currentLine.Append(c);
                        }
                    }
                    result.Add(new Str(currentLine.ToString()));
                    currentLine.Clear();
                }
                else if (c == '\n' || c == '\x0B' || c == '\x0C'
                    || c == '\x1C' || c == '\x1D' || c == '\x1E'
                    || c == '\x85' || c == '\u2028' || c == '\u2029')
                {
                    if (keepends)
                    {
                        currentLine.Append(c);
                    }
                    result.Add(new Str(currentLine.ToString()));
                    currentLine.Clear();
                }
                else
                {
                    currentLine.Append(c);
                }
            }

            if (currentLine.Length > 0)
            {
                result.Add(new Str(currentLine.ToString()));
            }

            return result;
        }

        /// <summary>
        /// Split at the first occurrence of <paramref name="sep"/>, returning a
        /// 3-tuple.
        /// Python: <c>str.partition(sep)</c>
        /// </summary>
        public (Str, Str, Str) Partition(Str sep)
        {
            string sepStr = (string)sep;
            if (sepStr == null)
            {
                throw TypeError.ArgNone("partition", "sep");
            }
            if (sepStr.Length == 0)
            {
                throw new ValueError("empty separator");
            }
            int index = Value.IndexOf(sepStr, StringComparison.Ordinal);
            if (index < 0)
            {
                return (this, new Str(""), new Str(""));
            }
            return (
                new Str(Value.Substring(0, index)),
                sep,
                new Str(Value.Substring(index + sepStr.Length))
            );
        }

        /// <summary>
        /// Split at the last occurrence of <paramref name="sep"/>, returning a
        /// 3-tuple.
        /// Python: <c>str.rpartition(sep)</c>
        /// </summary>
        public (Str, Str, Str) Rpartition(Str sep)
        {
            string sepStr = (string)sep;
            if (sepStr == null)
            {
                throw TypeError.ArgNone("rpartition", "sep");
            }
            if (sepStr.Length == 0)
            {
                throw new ValueError("empty separator");
            }
            int index = Value.LastIndexOf(sepStr, StringComparison.Ordinal);
            if (index < 0)
            {
                return (new Str(""), new Str(""), this);
            }
            return (
                new Str(Value.Substring(0, index)),
                sep,
                new Str(Value.Substring(index + sepStr.Length))
            );
        }
    }
}
