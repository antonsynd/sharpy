namespace Sharpy;

using System.Text;
using Collections.Interfaces;

public static partial class Exports
{
    public static Str Str(object x)
    {
        return new Str(x);
    }
}

/// <remarks>
/// Unlike Python strings, Sharpy strings use C# strings as storage, so
/// they are always UTF-16.
/// </remarks>
public readonly partial struct Str
{
    private readonly string _s;

    public Str()
    {
        _s = "";
    }

    public Str(string x)
    {
        _s = x;
    }

    /// <remarks>
    /// <see cref="Object.ToString"/> calls <see cref="Object.__Str__"/>
    /// so this implementation covers all native C# objects and Sharpy
    /// objects.
    /// </remarks>
    public Str(object x)
    {
        _s = x.ToString() ?? "";
    }

    public Str(Bytes bytes, string encoding = "utf-8", string errors = "strict")
    {
        _s = "";
    }

    /// <remarks>
    /// Implicit conversion from string.
    /// </remarks>
    public static implicit operator Str(string value)
    {
        return new Str(value);
    }

    /// <remarks>
    /// Implicit conversion to string.
    /// </remarks>
    public static implicit operator string(Str value)
    {
        return value._s;
    }

    public static bool operator ==(Str lhs, string rhs)
    {
        return lhs._s == rhs;
    }

    public static bool operator !=(Str lhs, string rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(string lhs, Str rhs)
    {
        return lhs == rhs._s;
    }

    public static bool operator !=(string lhs, Str rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(Str lhs, Str rhs)
    {
        return lhs._s == rhs._s;
    }

    public static bool operator !=(Str lhs, Str rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Str str)
        {
            return _s == str._s;
        }

        if (obj is string s)
        {
            return _s == s;
        }

        return false;
    }

    /// <remarks>
    /// Same hash code as C# strings. Both are immutable and readonly with
    /// the same logical semantics.
    /// </remarks>
    public override int GetHashCode()
    {
        return _s.GetHashCode();
    }

    public override string ToString()
    {
        return _s;
    }

    public Str Capitalize()
    {
        if (string.IsNullOrEmpty(_s))
        {
            return _s;
        }

        if (_s.Length == 1)
        {
            return _s.ToUpper();
        }

        return char.ToUpper(_s[0]) + _s[1..].ToLower();
    }

    public string CaseFold()
    {
        return _s;
    }

    public string Center(uint width, string fillchar = " ")
    {
        return _s;
    }

    public string Count(string sub, int start, int end)
    {
        return _s;
    }

    public string Encode(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    public string EndsWith(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    public string ExpandTabs(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    public string Find(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    public Str Format(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    public Str FormatMap(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    public Str Index(string encoding = "utf-8", string errors = "strict")
    {
        return _s;
    }

    /// <summary>
    /// Return True if all characters in the string are alphanumeric and there is at least one character, False otherwise.
    /// </summary>
    public bool IsAlnum()
    {
        if (string.IsNullOrEmpty(_s))
        {
            return false;
        }

        return _s.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Return True if all characters in the string are alphabetic and there is at least one character, False otherwise.
    /// </summary>
    public bool IsAlpha()
    {
        if (string.IsNullOrEmpty(_s))
        {
            return false;
        }

        return _s.All(char.IsLetter);
    }

    public bool IsAscii()
    {
        return false;
    }

    /// <summary>
    /// Return True if all characters in the string are digits and there is at least one character, False otherwise.
    /// </summary>
    public bool IsDigit()
    {
        if (string.IsNullOrEmpty(_s))
        {
            return false;
        }

        return _s.All(char.IsDigit);
    }

    public bool IsDecimal()
    {
        return false;
    }

    public bool IsIdentifier()
    {
        return false;
    }

    public bool IsLower()
    {
        return false;
    }

    public bool IsNumeric()
    {
        return false;
    }

    public bool IsPrintable()
    {
        return false;
    }

    /// <summary>
    /// Return True if all characters in the string are whitespace and there is at least one character, False otherwise.
    /// </summary>
    public bool IsSpace()
    {
        if (string.IsNullOrEmpty(_s))
        {
            return false;
        }

        return _s.All(char.IsWhiteSpace);
    }

    public bool IsTitle()
    {
        return false;
    }

    public bool IsUpper()
    {
        return false;
    }

    /// <summary>
    /// Return a string which is the concatenation of the strings in iterable.
    /// A TypeError will be raised if there are any non-string values in iterable.
    /// The separator between elements is the string providing this method.
    /// </summary>
    public Str Join(IEnumerable<Str> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        return new Str(string.Join(_s, iterable.Select(item => (string)item)));
    }

    public void LJust(uint width, string fillchar)
    {
    }

    /// <summary>
    /// Return a copy of the string converted to lowercase.
    /// </summary>
    public Str Lower()
    {
        return new Str(_s.ToLower());
    }

    /// <summary>
    /// Return a copy of the string with leading characters removed.
    /// The chars argument is a string specifying the set of characters to be removed.
    /// If omitted or None, the chars argument defaults to removing whitespace.
    /// </summary>
    public Str LStrip(Str? chars = null)
    {
        if (chars is null)
        {
            return new Str(_s.TrimStart());
        }

        return new Str(_s.TrimStart(((string)chars).ToCharArray()));
    }

    public void MakeTrans(IMapping<string, string?> mapping)
    {
    }

    public void MakeTrans(IMapping<uint, string?> mapping)
    {
    }

    public void MakeTrans(string fromChars, string toChars)
    {
    }

    public void MakeTrans(string fromChars, string toChars, string ignoreChars)
    {
    }

    public void Partition(string sep)
    {
    }

    public void RemovePrefix(string prefix)
    {
    }

    public void RemoveSuffix(string suffix)
    {
    }

    /// <summary>
    /// Return a copy of the string with all occurrences of substring old replaced by new.
    /// If the optional argument count is given, only the first count occurrences are replaced.
    /// </summary>
    public Str Replace(Str old, Str @new, int count = -1)
    {
        if (count == 0)
        {
            return this;
        }

        if (count < 0)
        {
            return new Str(_s.Replace((string)old, (string)@new));
        }

        // Replace only the first 'count' occurrences
        var oldStr = (string)old;
        var newStr = (string)@new;
        var builder = new StringBuilder(_s);
        
        int replacements = 0;
        int searchIndex = 0;
        
        while (replacements < count && searchIndex < builder.Length)
        {
            var index = builder.ToString().IndexOf(oldStr, searchIndex);
            if (index < 0)
            {
                break;
            }

            builder.Remove(index, oldStr.Length);
            builder.Insert(index, newStr);
            searchIndex = index + newStr.Length;
            replacements++;
        }

        return new Str(builder.ToString());
    }

    public void RFind(string sub, int start = 0, int end = -1)
    {
    }

    public void RIndex(string sub, int start = 0, int end = -1)
    {
    }

    public void RJust(uint width, string fillchar)
    {
    }

    public void RPartition(string sep)
    {
    }

    public void RSplit(string sep, uint maxsplit)
    {
    }

    /// <summary>
    /// Return a copy of the string with trailing characters removed.
    /// The chars argument is a string specifying the set of characters to be removed.
    /// If omitted or None, the chars argument defaults to removing whitespace.
    /// </summary>
    public Str RStrip(Str? chars = null)
    {
        if (chars is null)
        {
            return new Str(_s.TrimEnd());
        }

        return new Str(_s.TrimEnd(((string)chars).ToCharArray()));
    }

    /// <summary>
    /// Return a list of the words in the string, using sep as the delimiter string.
    /// If maxsplit is given, at most maxsplit splits are done (thus, the list will
    /// have at most maxsplit+1 elements). If maxsplit is not specified or -1,
    /// then there is no limit on the number of splits (all possible splits are made).
    /// </summary>
    public List<Str> Split(Str? sep = null, int maxsplit = -1)
    {
        var result = new List<Str>();

        if (sep is null)
        {
            // Split on whitespace
            var parts = maxsplit < 0
                ? _s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                : _s.Split((char[]?)null, maxsplit + 1, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                result.Add(new Str(part));
            }
        }
        else
        {
            var parts = maxsplit < 0
                ? _s.Split(new[] { (string)sep }, StringSplitOptions.None)
                : _s.Split(new[] { (string)sep }, maxsplit + 1, StringSplitOptions.None);

            foreach (var part in parts)
            {
                result.Add(new Str(part));
            }
        }

        return result;
    }

    public void SplitLines(bool keepends = false)
    {
    }

    public void StartsWith(string prefix, int start = 0, int end = -1)
    {
    }

    /// <summary>
    /// Return a copy of the string with leading and trailing characters removed.
    /// The chars argument is a string specifying the set of characters to be removed.
    /// If omitted or None, the chars argument defaults to removing whitespace.
    /// </summary>
    public Str Strip(Str? chars = null)
    {
        if (chars is null)
        {
            return new Str(_s.Trim());
        }

        return new Str(_s.Trim(((string)chars).ToCharArray()));
    }

    public void SwapCase()
    {
    }

    public void Title()
    {
    }

    public void Translate(uint table)
    {
    }

    /// <summary>
    /// Return a copy of the string converted to uppercase.
    /// </summary>
    public Str Upper()
    {
        return new Str(_s.ToUpper());
    }

    public void ZFill(uint width)
    {
    }
}
