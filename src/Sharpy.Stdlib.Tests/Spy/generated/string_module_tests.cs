// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using @string = global::Sharpy.StringModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.String.StringModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class String
    {
        [global::Sharpy.SharpyModule("string.string_module_tests")]
        public static partial class StringModuleTests
        {
        }
    }

    public static partial class String
    {
        public partial class StringModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestAsciiLowercaseMatchesPython()
            {
#line (5, 5) - (5, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("abcdefghijklmnopqrstuvwxyz", @string.AsciiLowercase);
            }

            [Xunit.FactAttribute]
            public void TestAsciiUppercaseMatchesPython()
            {
#line (9, 5) - (9, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ", @string.AsciiUppercase);
            }

            [Xunit.FactAttribute]
            public void TestAsciiLettersIsConcatenationOfLowercaseAndUppercase()
            {
#line (13, 5) - (13, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", @string.AsciiLetters);
            }

            [Xunit.FactAttribute]
            public void TestDigitsMatchesPython()
            {
#line (17, 5) - (17, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("0123456789", @string.Digits);
            }

            [Xunit.FactAttribute]
            public void TestHexdigitsMatchesPython()
            {
#line (21, 5) - (21, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("0123456789abcdefABCDEF", @string.Hexdigits);
            }

            [Xunit.FactAttribute]
            public void TestOctdigitsMatchesPython()
            {
#line (25, 5) - (25, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("01234567", @string.Octdigits);
            }

            [Xunit.FactAttribute]
            public void TestPunctuationMatchesPython()
            {
#line (29, 5) - (29, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~", @string.Punctuation);
            }

            [Xunit.FactAttribute]
            public void TestWhitespaceMatchesPython()
            {
#line (33, 5) - (33, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(" \t\n\r\v\f", @string.Whitespace);
            }

            [Xunit.FactAttribute]
            public void TestPrintableMatchesPython()
            {
#line (37, 5) - (37, 150) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                string expected = "0123456789" + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" + "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~" + " \t\n\r\v\f";
#line (38, 5) - (38, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(expected, @string.Printable);
            }

            [Xunit.FactAttribute]
            public void TestPrintableHasCorrectLength()
            {
#line (42, 5) - (42, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(100, @string.Printable.Length);
            }

            [Xunit.FactAttribute]
            public void TestPunctuationHasCorrectLength()
            {
#line (46, 5) - (46, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(32, @string.Punctuation.Length);
            }

            [Xunit.FactAttribute]
            public void TestWhitespaceHasCorrectLength()
            {
#line (50, 5) - (50, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(6, @string.Whitespace.Length);
            }
        }
    }
}
