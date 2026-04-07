using System;

namespace Sharpy
{
    /// <summary>
    /// Search and predicate methods for Str — ported from StringExtensions.Search.
    /// </summary>
    public readonly partial struct Str
    {
        // ----------------------------------------------------------------
        // Find / Rfind
        // ----------------------------------------------------------------

        /// <summary>
        /// Return the lowest index where substring <paramref name="sub"/> is found.
        /// Return -1 if not found.
        /// Python: <c>str.find(sub)</c>
        /// </summary>
        public int Find(Str sub)
        {
            return Value.IndexOf((string)sub, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the lowest index where substring <paramref name="sub"/> is found,
        /// starting the search at <paramref name="start"/>.
        /// Python: <c>str.find(sub, start)</c>
        /// </summary>
        public int Find(Str sub, int start)
        {
            return StringExtensions.Find(Value, (string)sub, start);
        }

        /// <summary>
        /// Return the lowest index where substring <paramref name="sub"/> is found
        /// within <c>s[start:end]</c>.
        /// Python: <c>str.find(sub, start, end)</c>
        /// </summary>
        public int Find(Str sub, int start, int end)
        {
            return StringExtensions.Find(Value, (string)sub, start, end);
        }

        /// <summary>
        /// Return the highest index where substring <paramref name="sub"/> is found.
        /// Return -1 if not found.
        /// Python: <c>str.rfind(sub)</c>
        /// </summary>
        public int Rfind(Str sub)
        {
            return Value.LastIndexOf((string)sub, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return the highest index where substring <paramref name="sub"/> is found,
        /// searching within <c>s[start:]</c>.
        /// Python: <c>str.rfind(sub, start)</c>
        /// </summary>
        public int Rfind(Str sub, int start)
        {
            return StringExtensions.Rfind(Value, (string)sub, start);
        }

        /// <summary>
        /// Return the highest index where substring <paramref name="sub"/> is found
        /// within <c>s[start:end]</c>.
        /// Python: <c>str.rfind(sub, start, end)</c>
        /// </summary>
        public int Rfind(Str sub, int start, int end)
        {
            return StringExtensions.Rfind(Value, (string)sub, start, end);
        }

        // ----------------------------------------------------------------
        // Index / Rindex
        // ----------------------------------------------------------------

        /// <summary>
        /// Like <see cref="Find(Str)"/> but raises <see cref="ValueError"/>
        /// when the substring is not found.
        /// Python: <c>str.index(sub)</c>
        /// </summary>
        public int Index(Str sub)
        {
            int result = Find(sub);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Find(Str, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.index(sub, start)</c>
        /// </summary>
        public int Index(Str sub, int start)
        {
            int result = Find(sub, start);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Find(Str, int, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.index(sub, start, end)</c>
        /// </summary>
        public int Index(Str sub, int start, int end)
        {
            int result = Find(sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(Str)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.rindex(sub)</c>
        /// </summary>
        public int Rindex(Str sub)
        {
            int result = Rfind(sub);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(Str, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.rindex(sub, start)</c>
        /// </summary>
        public int Rindex(Str sub, int start)
        {
            int result = Rfind(sub, start);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        /// <summary>
        /// Like <see cref="Rfind(Str, int, int)"/> but raises <see cref="ValueError"/>.
        /// Python: <c>str.rindex(sub, start, end)</c>
        /// </summary>
        public int Rindex(Str sub, int start, int end)
        {
            int result = Rfind(sub, start, end);
            if (result < 0)
            {
                throw new ValueError("substring not found");
            }
            return result;
        }

        // ----------------------------------------------------------------
        // Count
        // ----------------------------------------------------------------

        /// <summary>
        /// Return the number of non-overlapping occurrences of substring
        /// <paramref name="sub"/>.
        /// Python: <c>str.count(sub)</c>
        /// </summary>
        public int Count(Str sub)
        {
            return StringExtensions.Count(Value, (string)sub);
        }

        // ----------------------------------------------------------------
        // Startswith / Endswith
        // ----------------------------------------------------------------

        /// <summary>
        /// Return true if the string starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix)</c>
        /// </summary>
        public bool Startswith(Str prefix)
        {
            return Value.StartsWith((string)prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return true if <c>s[start:]</c> starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix, start)</c>
        /// </summary>
        public bool Startswith(Str prefix, int start)
        {
            return StringExtensions.Startswith(Value, (string)prefix, start);
        }

        /// <summary>
        /// Return true if <c>s[start:end]</c> starts with the <paramref name="prefix"/>.
        /// Python: <c>str.startswith(prefix, start, end)</c>
        /// </summary>
        public bool Startswith(Str prefix, int start, int end)
        {
            return StringExtensions.Startswith(Value, (string)prefix, start, end);
        }

        /// <summary>
        /// Return true if the string ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix)</c>
        /// </summary>
        public bool Endswith(Str suffix)
        {
            return Value.EndsWith((string)suffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Return true if <c>s[start:]</c> ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix, start)</c>
        /// </summary>
        public bool Endswith(Str suffix, int start)
        {
            return StringExtensions.Endswith(Value, (string)suffix, start);
        }

        /// <summary>
        /// Return true if <c>s[start:end]</c> ends with the <paramref name="suffix"/>.
        /// Python: <c>str.endswith(suffix, start, end)</c>
        /// </summary>
        public bool Endswith(Str suffix, int start, int end)
        {
            return StringExtensions.Endswith(Value, (string)suffix, start, end);
        }

        // ----------------------------------------------------------------
        // Character-class predicates
        // ----------------------------------------------------------------

        /// <summary>
        /// Return true if all characters are digits and there is at least one character.
        /// Python: <c>str.isdigit()</c>
        /// </summary>
        public bool Isdigit() => StringExtensions.Isdigit(Value);

        /// <summary>
        /// Return true if all characters are alphabetic and there is at least one character.
        /// Python: <c>str.isalpha()</c>
        /// </summary>
        public bool Isalpha() => StringExtensions.Isalpha(Value);

        /// <summary>
        /// Return true if all characters are alphanumeric and there is at least one character.
        /// Python: <c>str.isalnum()</c>
        /// </summary>
        public bool Isalnum() => StringExtensions.Isalnum(Value);

        /// <summary>
        /// Return true if all characters are whitespace and there is at least one character.
        /// Python: <c>str.isspace()</c>
        /// </summary>
        public bool Isspace() => StringExtensions.Isspace(Value);

        /// <summary>
        /// Return true if all cased characters are uppercase and there is at least
        /// one cased character.
        /// Python: <c>str.isupper()</c>
        /// </summary>
        public bool Isupper() => StringExtensions.Isupper(Value);

        /// <summary>
        /// Return true if all cased characters are lowercase and there is at least
        /// one cased character.
        /// Python: <c>str.islower()</c>
        /// </summary>
        public bool Islower() => StringExtensions.Islower(Value);

        /// <summary>
        /// Return true if the string is titlecased and there is at least one character.
        /// Python: <c>str.istitle()</c>
        /// </summary>
        public bool Istitle() => StringExtensions.Istitle(Value);

        /// <summary>
        /// Return true if all characters are numeric and there is at least one character.
        /// Python: <c>str.isnumeric()</c>
        /// </summary>
        public bool Isnumeric()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return false;
            }

            foreach (char c in Value)
            {
                // Python's isnumeric() includes digits, numeric chars (e.g., fractions, superscripts)
                if (!char.IsDigit(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.OtherNumber)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return true if all characters are decimal characters and there is at least one character.
        /// Python: <c>str.isdecimal()</c>
        /// </summary>
        public bool Isdecimal()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return false;
            }

            foreach (char c in Value)
            {
                // Python's isdecimal() is stricter than isdigit() — only Nd category
                if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.DecimalDigitNumber)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return true if the string is a valid Python identifier.
        /// Python: <c>str.isidentifier()</c>
        /// </summary>
        public bool Isidentifier()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return false;
            }

            char first = Value[0];
            if (!char.IsLetter(first) && first != '_')
            {
                return false;
            }

            for (int i = 1; i < Value.Length; i++)
            {
                char c = Value[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return true if all characters are printable or the string is empty.
        /// Python: <c>str.isprintable()</c>
        /// </summary>
        public bool Isprintable()
        {
            // Python: empty string is printable
            if (Value.Length == 0)
            {
                return true;
            }

            foreach (char c in Value)
            {
                var category = char.GetUnicodeCategory(c);
                if (category == System.Globalization.UnicodeCategory.Control
                    || category == System.Globalization.UnicodeCategory.Format
                    || category == System.Globalization.UnicodeCategory.Surrogate
                    || category == System.Globalization.UnicodeCategory.PrivateUse
                    || category == System.Globalization.UnicodeCategory.OtherNotAssigned
                    || category == System.Globalization.UnicodeCategory.LineSeparator
                    || category == System.Globalization.UnicodeCategory.ParagraphSeparator)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return true if all characters are ASCII (U+0000 to U+007F) or the string is empty.
        /// Python: <c>str.isascii()</c>
        /// </summary>
        public bool Isascii()
        {
            foreach (char c in Value)
            {
                if (c > '\u007F')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
