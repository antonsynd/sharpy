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
#line (5, 5) - (5, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("abcdefghijklmnopqrstuvwxyz", @string.AsciiLowercase);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAsciiUppercaseMatchesPython()
            {
#line (9, 5) - (9, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ", @string.AsciiUppercase);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestAsciiLettersIsConcatenationOfLowercaseAndUppercase()
            {
#line (13, 5) - (13, 91) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", @string.AsciiLetters);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestDigitsMatchesPython()
            {
#line (17, 5) - (17, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("0123456789", @string.Digits);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestHexdigitsMatchesPython()
            {
#line (21, 5) - (21, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("0123456789abcdefABCDEF", @string.Hexdigits);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestOctdigitsMatchesPython()
            {
#line (25, 5) - (25, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("01234567", @string.Octdigits);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPunctuationMatchesPython()
            {
#line (29, 5) - (29, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~", @string.Punctuation);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWhitespaceMatchesPython()
            {
#line (33, 5) - (33, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(" \t\n\r\v\f", @string.Whitespace);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPrintableMatchesPython()
            {
#line (37, 5) - (37, 150) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                string expected = "0123456789" + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" + "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~" + " \t\n\r\v\f";
#line (38, 5) - (38, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(expected, @string.Printable);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPrintableHasCorrectLength()
            {
#line (42, 5) - (42, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(100, @string.Printable.Length);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestPunctuationHasCorrectLength()
            {
#line (46, 5) - (46, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(32, @string.Punctuation.Length);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestWhitespaceHasCorrectLength()
            {
#line (50, 5) - (50, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/string/string_module_tests.spy"
                Xunit.Assert.Equal(6, @string.Whitespace.Length);
#line hidden
            }
        }
    }
}
#line default
