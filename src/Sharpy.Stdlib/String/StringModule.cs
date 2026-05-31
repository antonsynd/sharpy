// Generated from src/Sharpy.Stdlib/spy/string_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/string_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>Exposes character class constants matching Python's string module.</summary>
    public static partial class StringModule
    {
        /// <summary>The lowercase ASCII letters.</summary>
        public static string AsciiLowercase = "abcdefghijklmnopqrstuvwxyz";
        /// <summary>The uppercase ASCII letters.</summary>
        public static string AsciiUppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        /// <summary>The concatenation of the lowercase and uppercase ASCII letters.</summary>
        public static string AsciiLetters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        /// <summary>The decimal digit characters.</summary>
        public static string Digits = "0123456789";
        /// <summary>The hexadecimal digit characters.</summary>
        public static string Hexdigits = "0123456789abcdefABCDEF";
        /// <summary>The octal digit characters.</summary>
        public static string Octdigits = "01234567";
        /// <summary>The ASCII punctuation characters.</summary>
        public static string Punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
        /// <summary>The ASCII whitespace characters.</summary>
        public static string Whitespace = " \t\n\r\v\f";
        /// <summary>The ASCII characters considered printable.</summary>
        public static string Printable = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~ \t\n\r\v\f";
    }
}
