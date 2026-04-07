using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Case and trim methods for Str — ported from StringExtensions.
    /// </summary>
    public readonly partial struct Str
    {
        /// <summary>
        /// Return a copy of the string converted to uppercase.
        /// Python: <c>str.upper()</c>
        /// </summary>
        public Str Upper() => new Str(Value.ToUpperInvariant());

        /// <summary>
        /// Return a copy of the string converted to lowercase.
        /// Python: <c>str.lower()</c>
        /// </summary>
        public Str Lower() => new Str(Value.ToLowerInvariant());

        /// <summary>
        /// Return a copy of the string with its first character capitalized
        /// and the rest lowercased.
        /// Python: <c>str.capitalize()</c>
        /// </summary>
        public Str Capitalize()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return this;
            }

            if (Value.Length == 1)
            {
                return new Str(Value.ToUpperInvariant());
            }

            return new Str(char.ToUpperInvariant(Value[0]) + Value.Substring(1).ToLowerInvariant());
        }

        /// <summary>
        /// Return a titlecased version of the string.
        /// Python: <c>str.title()</c>
        /// </summary>
        public Str Title()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return this;
            }

            var result = new StringBuilder(Value.Length);
            bool previousWasLetter = false;

            foreach (char c in Value)
            {
                if (char.IsLetter(c))
                {
                    if (!previousWasLetter)
                    {
                        result.Append(char.ToUpperInvariant(c));
                    }
                    else
                    {
                        result.Append(char.ToLowerInvariant(c));
                    }
                    previousWasLetter = true;
                }
                else
                {
                    result.Append(c);
                    previousWasLetter = false;
                }
            }

            return new Str(result.ToString());
        }

        /// <summary>
        /// Return a copy of the string with uppercase characters converted to
        /// lowercase and vice versa.
        /// Python: <c>str.swapcase()</c>
        /// </summary>
        public Str Swapcase()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return this;
            }

            var result = new StringBuilder(Value.Length);
            foreach (char c in Value)
            {
                if (char.IsUpper(c))
                {
                    result.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsLower(c))
                {
                    result.Append(char.ToUpperInvariant(c));
                }
                else
                {
                    result.Append(c);
                }
            }

            return new Str(result.ToString());
        }

        /// <summary>
        /// Return a casefolded copy of the string.
        /// Python: <c>str.casefold()</c>
        /// </summary>
        public Str Casefold()
        {
            return new Str(StringExtensions.Casefold(Value));
        }

        /// <summary>
        /// Return a copy with leading and trailing whitespace removed.
        /// Python: <c>str.strip()</c>
        /// </summary>
        public Str Strip() => new Str(Value.Trim());

        /// <summary>
        /// Return a copy with leading and trailing characters in
        /// <paramref name="chars"/> removed.
        /// Python: <c>str.strip(chars)</c>
        /// </summary>
        public Str Strip(Str chars) => new Str(Value.Trim(((string)chars).ToCharArray()));

        /// <summary>
        /// Return a copy with leading whitespace removed.
        /// Python: <c>str.lstrip()</c>
        /// </summary>
        public Str Lstrip() => new Str(Value.TrimStart());

        /// <summary>
        /// Return a copy with leading characters in <paramref name="chars"/> removed.
        /// Python: <c>str.lstrip(chars)</c>
        /// </summary>
        public Str Lstrip(Str chars) => new Str(Value.TrimStart(((string)chars).ToCharArray()));

        /// <summary>
        /// Return a copy with trailing whitespace removed.
        /// Python: <c>str.rstrip()</c>
        /// </summary>
        public Str Rstrip() => new Str(Value.TrimEnd());

        /// <summary>
        /// Return a copy with trailing characters in <paramref name="chars"/> removed.
        /// Python: <c>str.rstrip(chars)</c>
        /// </summary>
        public Str Rstrip(Str chars) => new Str(Value.TrimEnd(((string)chars).ToCharArray()));

        /// <summary>
        /// Return centered in a string of length <paramref name="width"/>.
        /// Python: <c>str.center(width, fillchar)</c>
        /// </summary>
        public Str Center(int width, char fillchar = ' ')
        {
            if (Value.Length >= width)
            {
                return this;
            }

            int totalPadding = width - Value.Length;
            int leftPadding = totalPadding / 2;
            int rightPadding = totalPadding - leftPadding;

            return new Str(new string(fillchar, leftPadding) + Value + new string(fillchar, rightPadding));
        }

        /// <summary>
        /// Return left-justified in a string of length <paramref name="width"/>.
        /// Python: <c>str.ljust(width, fillchar)</c>
        /// </summary>
        public Str Ljust(int width, char fillchar = ' ')
        {
            if (Value.Length >= width)
            {
                return this;
            }

            return new Str(Value.PadRight(width, fillchar));
        }

        /// <summary>
        /// Return right-justified in a string of length <paramref name="width"/>.
        /// Python: <c>str.rjust(width, fillchar)</c>
        /// </summary>
        public Str Rjust(int width, char fillchar = ' ')
        {
            if (Value.Length >= width)
            {
                return this;
            }

            return new Str(Value.PadLeft(width, fillchar));
        }

        /// <summary>
        /// Return left filled with ASCII '0' digits to make a string of length
        /// <paramref name="width"/>. A leading sign prefix is handled.
        /// Python: <c>str.zfill(width)</c>
        /// </summary>
        public Str Zfill(int width)
        {
            if (Value.Length >= width)
            {
                return this;
            }

            int fillCount = width - Value.Length;

            if (Value.Length > 0 && (Value[0] == '+' || Value[0] == '-'))
            {
                return new Str(Value[0] + new string('0', fillCount) + Value.Substring(1));
            }

            return new Str(new string('0', fillCount) + Value);
        }

        /// <summary>
        /// If the string starts with the <paramref name="prefix"/>, return the
        /// string with the prefix removed. Otherwise, return a copy.
        /// Python: <c>str.removeprefix(prefix)</c>
        /// </summary>
        public Str Removeprefix(Str prefix)
        {
            if (Value.StartsWith((string)prefix, StringComparison.Ordinal))
            {
                return new Str(Value.Substring(((string)prefix).Length));
            }

            return this;
        }

        /// <summary>
        /// If the string ends with the <paramref name="suffix"/>, return the
        /// string with the suffix removed. Otherwise, return a copy.
        /// Python: <c>str.removesuffix(suffix)</c>
        /// </summary>
        public Str Removesuffix(Str suffix)
        {
            if (Value.EndsWith((string)suffix, StringComparison.Ordinal))
            {
                return new Str(Value.Substring(0, Value.Length - ((string)suffix).Length));
            }

            return this;
        }

        /// <summary>
        /// Return a copy where all tab characters are expanded using spaces.
        /// Python: <c>str.expandtabs(tabsize=8)</c>
        /// </summary>
        public Str Expandtabs(int tabsize = 8)
        {
            return new Str(StringExtensions.Expandtabs(Value, tabsize));
        }

        /// <summary>
        /// Return a copy with all occurrences of <paramref name="old"/> replaced
        /// by <paramref name="new_"/>.
        /// Python: <c>str.replace(old, new)</c>
        /// </summary>
        public Str Replace(Str old, Str new_)
        {
            return new Str(StringExtensions.Replace(Value, (string)old, (string)new_));
        }

        /// <summary>
        /// Return a copy with the first <paramref name="count"/> occurrences of
        /// <paramref name="old"/> replaced by <paramref name="new_"/>.
        /// Python: <c>str.replace(old, new, count)</c>
        /// </summary>
        public Str Replace(Str old, Str new_, int count)
        {
            return new Str(StringExtensions.Replace(Value, (string)old, (string)new_, count));
        }
    }
}
