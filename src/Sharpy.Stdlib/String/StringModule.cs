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
    public static partial class StringModule
    {
        public static string AsciiLowercase = "abcdefghijklmnopqrstuvwxyz";
        public static string AsciiUppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static string AsciiLetters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static string Digits = "0123456789";
        public static string Hexdigits = "0123456789abcdefABCDEF";
        public static string Octdigits = "01234567";
        public static string Punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
        public static string Whitespace = " \t\n\r\v\f";
        public static string Printable = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~ \t\n\r\v\f";
    }
}
