// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonStrTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_str_tests")]
        public static partial class CpythonStrTests
        {
            internal static bool _IndexRaises(string s, string sub)
            {
#line (248, 5) - (253, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                try
#line hidden
                {
#line (249, 9) - (249, 33) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return global::Sharpy.StringExtensions.Index(s, sub) < 0;
#line hidden
                }
                catch (ValueError)
                {
#line (251, 9) - (251, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return true;
#line hidden
                }
            }

            internal static bool _RindexRaises(string s, string sub)
            {
#line (263, 5) - (268, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                try
#line hidden
                {
#line (264, 9) - (264, 34) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return global::Sharpy.StringExtensions.Rindex(s, sub) < 0;
#line hidden
                }
                catch (ValueError)
                {
#line (266, 9) - (266, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return true;
#line hidden
                }
            }
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonStrTestsTests
        {
            [Xunit.FactAttribute]
            public void TestLower()
            {
#line (27, 5) - (27, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello computers", global::Sharpy.StringExtensions.Lower("HeLLo cOmpUteRs"));
#line (28, 5) - (28, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Lower("hello"));
#line (29, 5) - (29, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Lower(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestUpper()
            {
#line (33, 5) - (33, 59) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("HELLO COMPUTERS", global::Sharpy.StringExtensions.Upper("HeLLo cOmpUteRs"));
#line (34, 5) - (34, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("HELLO", global::Sharpy.StringExtensions.Upper("hello"));
#line (35, 5) - (35, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Upper(""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCasefold()
            {
#line (39, 5) - (39, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Casefold("hello"));
#line (40, 5) - (40, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Casefold("Hello"));
#line (41, 5) - (41, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Casefold("ABC"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCapitalize()
            {
#line (47, 5) - (47, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(" hello ", global::Sharpy.StringExtensions.Capitalize(" hello "));
#line (48, 5) - (48, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Hello ", global::Sharpy.StringExtensions.Capitalize("Hello "));
#line (49, 5) - (49, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Hello ", global::Sharpy.StringExtensions.Capitalize("hello "));
#line (50, 5) - (50, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Aaaa", global::Sharpy.StringExtensions.Capitalize("aaaa"));
#line (51, 5) - (51, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Aaaa", global::Sharpy.StringExtensions.Capitalize("AaAa"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestTitle()
            {
#line (55, 5) - (55, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(" Hello ", global::Sharpy.StringExtensions.Title(" hello "));
#line (56, 5) - (56, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Hello World", global::Sharpy.StringExtensions.Title("hello world"));
#line (57, 5) - (57, 83) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Format This As Title String", global::Sharpy.StringExtensions.Title("fOrMaT thIs aS titLe String"));
#line (58, 5) - (58, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Getint", global::Sharpy.StringExtensions.Title("getInt"));
#line (60, 5) - (60, 73) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("They'Re Bill'S Friends", global::Sharpy.StringExtensions.Title("they're bill's friends"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSwapcase()
            {
#line (64, 5) - (64, 62) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hEllO CoMPuTErS", global::Sharpy.StringExtensions.Swapcase("HeLLo cOmpUteRs"));
#line (65, 5) - (65, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hELLO wORLD", global::Sharpy.StringExtensions.Swapcase("Hello World"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCenter()
            {
#line (71, 5) - (71, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("   abc    ", global::Sharpy.StringExtensions.Center("abc", 10));
#line (72, 5) - (72, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(" abc  ", global::Sharpy.StringExtensions.Center("abc", 6));
#line (73, 5) - (73, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Center("abc", 3));
#line (74, 5) - (74, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Center("abc", 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestLjust()
            {
#line (78, 5) - (78, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc       ", global::Sharpy.StringExtensions.Ljust("abc", 10));
#line (79, 5) - (79, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc   ", global::Sharpy.StringExtensions.Ljust("abc", 6));
#line (80, 5) - (80, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Ljust("abc", 3));
#line (81, 5) - (81, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Ljust("abc", 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRjust()
            {
#line (85, 5) - (85, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("       abc", global::Sharpy.StringExtensions.Rjust("abc", 10));
#line (86, 5) - (86, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("   abc", global::Sharpy.StringExtensions.Rjust("abc", 6));
#line (87, 5) - (87, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Rjust("abc", 3));
#line (88, 5) - (88, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Rjust("abc", 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestZfill()
            {
#line (92, 5) - (92, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("123", global::Sharpy.StringExtensions.Zfill("123", 2));
#line (93, 5) - (93, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("123", global::Sharpy.StringExtensions.Zfill("123", 3));
#line (94, 5) - (94, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("0123", global::Sharpy.StringExtensions.Zfill("123", 4));
#line (95, 5) - (95, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("+123", global::Sharpy.StringExtensions.Zfill("+123", 3));
#line (96, 5) - (96, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("+0123", global::Sharpy.StringExtensions.Zfill("+123", 5));
#line (97, 5) - (97, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("-0123", global::Sharpy.StringExtensions.Zfill("-123", 5));
#line (98, 5) - (98, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("000", global::Sharpy.StringExtensions.Zfill("", 3));
#line (99, 5) - (99, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("34", global::Sharpy.StringExtensions.Zfill("34", 1));
#line (100, 5) - (100, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("0034", global::Sharpy.StringExtensions.Zfill("34", 4));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIslower()
            {
#line (106, 5) - (106, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Islower(""));
#line (107, 5) - (107, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Islower("a"));
#line (108, 5) - (108, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Islower("A"));
#line (109, 5) - (109, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Islower("\n"));
#line (110, 5) - (110, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Islower("abc"));
#line (111, 5) - (111, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Islower("aBc"));
#line (112, 5) - (112, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Islower("abc\n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsupper()
            {
#line (116, 5) - (116, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper(""));
#line (117, 5) - (117, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper("a"));
#line (118, 5) - (118, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isupper("A"));
#line (119, 5) - (119, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper("\n"));
#line (120, 5) - (120, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isupper("ABC"));
#line (121, 5) - (121, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper("AbC"));
#line (122, 5) - (122, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isupper("ABC\n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIstitle()
            {
#line (126, 5) - (126, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle(""));
#line (127, 5) - (127, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle("a"));
#line (128, 5) - (128, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A"));
#line (129, 5) - (129, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A Titlecased Line"));
#line (130, 5) - (130, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A\nTitlecased Line"));
#line (131, 5) - (131, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A Titlecased, Line"));
#line (132, 5) - (132, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle("Not a capitalized String"));
#line (133, 5) - (133, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle("NOT"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsspace()
            {
#line (139, 5) - (139, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isspace(""));
#line (140, 5) - (140, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace(" "));
#line (141, 5) - (141, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace("\t"));
#line (142, 5) - (142, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace("\r"));
#line (143, 5) - (143, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace("\n"));
#line (144, 5) - (144, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace(" \t\r\n"));
#line (145, 5) - (145, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isspace("a"));
#line (146, 5) - (146, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isspace(" \t\r\na"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsalpha()
            {
#line (150, 5) - (150, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isalpha(""));
#line (151, 5) - (151, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isalpha("a"));
#line (152, 5) - (152, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isalpha("A"));
#line (153, 5) - (153, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isalpha("abc"));
#line (154, 5) - (154, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isalpha("ab1c"));
#line (155, 5) - (155, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isalpha("abc\n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsalnum()
            {
#line (159, 5) - (159, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isalnum(""));
#line (160, 5) - (160, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isalnum("a"));
#line (161, 5) - (161, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isalnum("1"));
#line (162, 5) - (162, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isalnum("abc123"));
#line (163, 5) - (163, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isalnum("ab c"));
#line (164, 5) - (164, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isalnum("?"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsdigit()
            {
#line (168, 5) - (168, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit(""));
#line (169, 5) - (169, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit("0"));
#line (170, 5) - (170, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit("0123456789"));
#line (171, 5) - (171, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit("0123456789a"));
#line (172, 5) - (172, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit("abc"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsdecimal()
            {
#line (178, 5) - (178, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdecimal(""));
#line (179, 5) - (179, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdecimal("0"));
#line (180, 5) - (180, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isdecimal("0123456789"));
#line (181, 5) - (181, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdecimal("0123456789a"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsnumeric()
            {
#line (185, 5) - (185, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isnumeric(""));
#line (186, 5) - (186, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isnumeric("0"));
#line (187, 5) - (187, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isnumeric("abc"));
#line (189, 5) - (189, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isnumeric("½"));
#line (190, 5) - (190, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit("½"));
#line (191, 5) - (191, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isdecimal("½"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsidentifier()
            {
#line (197, 5) - (197, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("a"));
#line (198, 5) - (198, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("Z"));
#line (199, 5) - (199, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("_"));
#line (200, 5) - (200, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("b0"));
#line (201, 5) - (201, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("b_"));
#line (202, 5) - (202, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isidentifier("0"));
#line (203, 5) - (203, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isidentifier(""));
#line (204, 5) - (204, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isidentifier(" "));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsprintable()
            {
#line (208, 5) - (208, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isprintable(""));
#line (209, 5) - (209, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isprintable(" "));
#line (210, 5) - (210, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isprintable("abcdefg"));
#line (211, 5) - (211, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isprintable("abcdefg\n"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIsascii()
            {
#line (215, 5) - (215, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isascii(""));
#line (216, 5) - (216, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isascii("abc"));
#line (217, 5) - (217, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Isascii("\0\u007f"));
#line (218, 5) - (218, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isascii("\u0080"));
#line (219, 5) - (219, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Isascii("é"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFind()
            {
#line (225, 5) - (225, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Find("abcdefghiabc", "abc"));
#line (226, 5) - (226, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Find("abcdefghiabc", "abc", 1));
#line (227, 5) - (227, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abcdefghiabc", "def", 4));
#line (228, 5) - (228, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Find("abc", "", 0));
#line (229, 5) - (229, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Find("abc", "", 3));
#line (230, 5) - (230, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abc", "", 4));
#line (231, 5) - (231, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Find("rrarrrrrrrrra", "a"));
#line (232, 5) - (232, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Find("rrarrrrrrrrra", "a", 4));
#line (233, 5) - (233, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("rrarrrrrrrrra", "a", 4, 6));
#line (234, 5) - (234, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Find("", ""));
#line (235, 5) - (235, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("", "xx"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRfind()
            {
#line (239, 5) - (239, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", "abc"));
#line (240, 5) - (240, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", ""));
#line (241, 5) - (241, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", "abcd"));
#line (242, 5) - (242, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", "abcz"));
#line (243, 5) - (243, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Rfind("abc", "", 3));
#line (244, 5) - (244, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Rfind("rrarrrrrrrrra", "a"));
#line (245, 5) - (245, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Rfind("rrarrrrrrrrra", "a", 0, 6));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndex()
            {
#line (255, 5) - (255, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Index("abcdefghiabc", ""));
#line (256, 5) - (256, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Index("abcdefghiabc", "def"));
#line (257, 5) - (257, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Index("abcdefghiabc", "abc"));
#line (258, 5) - (258, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Index("abcdefghiabc", "abc", 1));
#line (259, 5) - (259, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(_IndexRaises("abcdefghiabc", "hib"));
#line (260, 5) - (260, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(_IndexRaises("abcdefghi", "ghix"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRindex()
            {
#line (270, 5) - (270, 44) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", ""));
#line (271, 5) - (271, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", "def"));
#line (272, 5) - (272, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", "abc"));
#line (273, 5) - (273, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", "abc", 0, -1));
#line (274, 5) - (274, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(_RindexRaises("abcdefghiabc", "hib"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplit()
            {
#line (280, 5) - (280, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c", "d" }, global::Sharpy.StringExtensions.Split("a b c d"));
#line (281, 5) - (281, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.StringExtensions.Split("a,b,c", ","));
#line (282, 5) - (282, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "", "c", "" }, global::Sharpy.StringExtensions.Split("a,b,,c,", ","));
#line (283, 5) - (283, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "endcase ", "" }, global::Sharpy.StringExtensions.Split("endcase test", "test"));
#line (287, 5) - (287, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.StringExtensions.Split("   a b   c "));
#line (288, 5) - (288, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> emptySplit = new Sharpy.List<string>()
#line hidden
                {
                };
#line (289, 5) - (289, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(emptySplit, global::Sharpy.StringExtensions.Split("   "));
#line (290, 5) - (290, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b c d" }, global::Sharpy.StringExtensions.Split("a b c d", " ", 1));
#line (291, 5) - (291, 57) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c d" }, global::Sharpy.StringExtensions.Split("a b c d", " ", 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRsplit()
            {
#line (295, 5) - (295, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c", "d" }, global::Sharpy.StringExtensions.Rsplit("a b c d"));
#line (296, 5) - (296, 55) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a b c", "d" }, global::Sharpy.StringExtensions.Rsplit("a b c d", " ", 1));
#line (297, 5) - (297, 58) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a b", "c", "d" }, global::Sharpy.StringExtensions.Rsplit("a b c d", " ", 2));
#line (298, 5) - (298, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.StringExtensions.Rsplit("a,b,c", ","));
#line (299, 5) - (299, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "", "c", "" }, global::Sharpy.StringExtensions.Rsplit("a,b,,c,", ","));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSplitlines()
            {
#line (303, 5) - (303, 72) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\n\rghi"));
#line (304, 5) - (304, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\n\r\nghi"));
#line (305, 5) - (305, 68) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\r\nghi"));
#line (306, 5) - (306, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\r\nghi\n"));
#line (307, 5) - (307, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> noLines = new Sharpy.List<string>()
#line hidden
                {
                };
#line (308, 5) - (308, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(noLines, global::Sharpy.StringExtensions.Splitlines(""));
#line (309, 5) - (309, 82) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc\n", "def\n", "\r", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\n\rghi", true));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestReplace()
            {
#line (315, 5) - (315, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one@two@three@", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@"));
#line (316, 5) - (316, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one@two!three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@", 1));
#line (317, 5) - (317, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one@two@three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@", 2));
#line (318, 5) - (318, 67) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one!two!three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "x", "@"));
#line (319, 5) - (319, 63) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("miSSiSSippi", global::Sharpy.StringExtensions.Replace("mississippi", "ss", "SS"));
#line (322, 5) - (322, 70) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one!two!three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@", 0));
#line (323, 5) - (323, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("-a-b-c-", global::Sharpy.StringExtensions.Replace("abc", "", "-"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestCount()
            {
#line (328, 5) - (328, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Count("aaa", "a"));
#line (329, 5) - (329, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Count("aaa", "b"));
#line (330, 5) - (330, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Count("aaaa", "aa"));
#line (331, 5) - (331, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.StringExtensions.Count("", ""));
#line (332, 5) - (332, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Count("mississippi", "ss"));
#line (333, 5) - (333, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.StringExtensions.Count("mississippi", "i"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStrip()
            {
#line (339, 5) - (339, 45) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Strip("   hello   "));
#line (340, 5) - (340, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello   ", global::Sharpy.StringExtensions.Lstrip("   hello   "));
#line (341, 5) - (341, 49) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("   hello", global::Sharpy.StringExtensions.Rstrip("   hello   "));
#line (342, 5) - (342, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Strip("hello"));
#line (343, 5) - (343, 54) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Strip("xyzzyhelloxyzzy", "xyz"));
#line (344, 5) - (344, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("helloxyzzy", global::Sharpy.StringExtensions.Lstrip("xyzzyhelloxyzzy", "xyz"));
#line (345, 5) - (345, 60) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("xyzzyhello", global::Sharpy.StringExtensions.Rstrip("xyzzyhelloxyzzy", "xyz"));
#line (346, 5) - (346, 53) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("mississipp", global::Sharpy.StringExtensions.Strip("mississippi", "i"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStartswith()
            {
#line (352, 5) - (352, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.StartsWith("he", "hello");
#line (353, 5) - (353, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.StartsWith("hello", "hello");
#line (354, 5) - (354, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith("hello", "hello world"));
#line (355, 5) - (355, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.StartsWith("", "hello");
#line (356, 5) - (356, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith("hello", "ello"));
#line (357, 5) - (357, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Startswith("hello", "ello", 1));
#line (358, 5) - (358, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Startswith("hello", "o", 4));
#line (359, 5) - (359, 43) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith("hello", "o", 5));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestEndswith()
            {
#line (363, 5) - (363, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("lo", "hello");
#line (364, 5) - (364, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("hello", "hello");
#line (365, 5) - (365, 48) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Endswith("hello", "world hello"));
#line (366, 5) - (366, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("", "hello");
#line (367, 5) - (367, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(global::Sharpy.StringExtensions.Endswith("hello", "hell"));
#line (368, 5) - (368, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Endswith("hello", "ell", 0, 4));
#line (369, 5) - (369, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("o", "hello");
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestContains()
            {
#line (375, 5) - (375, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "") >= 0);
#line (376, 5) - (376, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "a") >= 0);
#line (377, 5) - (377, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "c") >= 0);
#line (378, 5) - (378, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "abc") >= 0);
#line (379, 5) - (379, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abc", "d"));
#line (380, 5) - (380, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "ab") >= 0);
#line (381, 5) - (381, 35) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abc", "ac"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestConcatenation()
            {
#line (387, 5) - (387, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcdef", "abc" + "def");
#line (388, 5) - (388, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "" + "abc");
#line (389, 5) - (389, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc" + "");
#line (390, 5) - (390, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcabcabc", global::Sharpy.StringHelpers.Repeat("abc", 3));
#line (391, 5) - (391, 37) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcabcabc", global::Sharpy.StringHelpers.Repeat("abc", 3));
#line (392, 5) - (392, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringHelpers.Repeat("abc", 0));
#line (393, 5) - (393, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringHelpers.Repeat("abc", 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestComparison()
            {
#line (397, 5) - (397, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc");
#line (398, 5) - (398, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.NotEqual("abd", "abc");
#line (399, 5) - (399, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abd", System.StringComparison.Ordinal) < 0);
#line (400, 5) - (400, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abcd", System.StringComparison.Ordinal) < 0);
#line (401, 5) - (401, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abd", "abc", System.StringComparison.Ordinal) > 0);
#line (402, 5) - (402, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abc", System.StringComparison.Ordinal) <= 0);
#line (403, 5) - (403, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abc", System.StringComparison.Ordinal) >= 0);
#line (404, 5) - (404, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("Abc", "abc", System.StringComparison.Ordinal) < 0);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIndexing()
            {
#line (410, 5) - (410, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                string s = "python";
#line (411, 5) - (411, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("p", global::Sharpy.StringHelpers.GetItem(s, 0));
#line (412, 5) - (412, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("y", global::Sharpy.StringHelpers.GetItem(s, 1));
#line (413, 5) - (413, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("n", global::Sharpy.StringHelpers.GetItem(s, -1));
#line (414, 5) - (414, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("p", global::Sharpy.StringHelpers.GetItem(s, -6));
#line (415, 5) - (415, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(6, s.Length);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSlicing()
            {
#line (419, 5) - (419, 23) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                string s = "abcdef";
#line (420, 5) - (420, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("ab", global::Sharpy.Slice.GetSlice(s, 0, 2, null));
#line (421, 5) - (421, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("cdef", global::Sharpy.Slice.GetSlice(s, 2, null, null));
#line (422, 5) - (422, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.Slice.GetSlice(s, null, 3, null));
#line (423, 5) - (423, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcdef", global::Sharpy.Slice.GetSlice(s, null, null, null));
#line (424, 5) - (424, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("ef", global::Sharpy.Slice.GetSlice(s, -2, null, null));
#line (425, 5) - (425, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("fedcba", global::Sharpy.Slice.GetSlice(s, null, null, -1));
#line (426, 5) - (426, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("bd", global::Sharpy.Slice.GetSlice(s, 1, 5, 2));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIteration()
            {
#line (430, 5) - (430, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> collected = new Sharpy.List<string>()
#line hidden
                {
                };
#line (431, 5) - (433, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                foreach (var __loopVar_0 in global::Sharpy.StringHelpers.Iterate("abc"))
#line hidden
                {
                    var ch = __loopVar_0;
#line (432, 9) - (432, 29) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    collected.Append(ch);
#line hidden
                }

#line (433, 5) - (433, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, collected);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestExpandtabs()
            {
#line (439, 5) - (439, 81) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc\rab      def\ng       hi", global::Sharpy.StringExtensions.Expandtabs("abc\rab\tdef\ng\thi"));
#line (440, 5) - (440, 74) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc\rab  def\ng   hi", global::Sharpy.StringExtensions.Expandtabs("abc\rab\tdef\ng\thi", 4));
#line (441, 5) - (441, 78) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc\r\nab  def\ng   hi", global::Sharpy.StringExtensions.Expandtabs("abc\r\nab\tdef\ng\thi", 4));
#line (442, 5) - (442, 51) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("  a\n b", global::Sharpy.StringExtensions.Expandtabs(" \ta\n\tb", 1));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRemoveprefix()
            {
#line (446, 5) - (446, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("am", global::Sharpy.StringExtensions.Removeprefix("spam", "sp"));
#line (447, 5) - (447, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Removeprefix("spam", "spam"));
#line (448, 5) - (448, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removeprefix("spam", "x"));
#line (449, 5) - (449, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removeprefix("spam", ""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestRemovesuffix()
            {
#line (453, 5) - (453, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("sp", global::Sharpy.StringExtensions.Removesuffix("spam", "am"));
#line (454, 5) - (454, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Removesuffix("spam", "spam"));
#line (455, 5) - (455, 47) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removesuffix("spam", "x"));
#line (456, 5) - (456, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removesuffix("spam", ""));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestJoin()
            {
#line (460, 5) - (460, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> parts = new Sharpy.List<string>()
#line hidden
                {
                    "a",
                    "b",
                    "c"
                };
#line (461, 5) - (461, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("a,b,c", global::Sharpy.StringExtensions.Join(",", parts));
#line (462, 5) - (462, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Join("", parts));
#line (463, 5) - (463, 50) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> withEmpties = new Sharpy.List<string>()
#line hidden
                {
                    "",
                    "b",
                    "",
                    "d"
                };
#line (464, 5) - (464, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("bd", global::Sharpy.StringExtensions.Join("", withEmpties));
#line (465, 5) - (465, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> single = new Sharpy.List<string>()
#line hidden
                {
                    "x"
                };
#line (466, 5) - (466, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("x", global::Sharpy.StringExtensions.Join(",", single));
#line (467, 5) - (467, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
#line hidden
                {
                };
#line (468, 5) - (468, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Join(",", empty));
#line hidden
            }
        }
    }
}
#line default
