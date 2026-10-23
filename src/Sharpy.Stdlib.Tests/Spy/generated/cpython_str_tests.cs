// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Cpython.CpythonStrTests
{
    [global::Sharpy.SharpyModule("cpython.cpython_str_tests")]
    public static partial class CpythonStrTestsModule
    {
        internal static bool _IndexRaises(string s, string sub)
        {
#line (249, 5) - (252, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            try
#line hidden
            {
#line (250, 9) - (250, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                return global::Sharpy.StringExtensions.Index(s, sub) < 0;
#line hidden
            }
            catch (global::Sharpy.ValueError)
            {
#line (252, 9) - (252, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                return true;
#line hidden
            }
        }

        internal static bool _RindexRaises(string s, string sub)
        {
#line (264, 5) - (267, 21) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            try
#line hidden
            {
#line (265, 9) - (265, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                return global::Sharpy.StringExtensions.Rindex(s, sub) < 0;
#line hidden
            }
            catch (global::Sharpy.ValueError)
            {
#line (267, 9) - (267, 21) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                return true;
#line hidden
            }
        }
    }

    public partial class CpythonStrTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestLower()
        {
#line (25, 5) - (25, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello computers", global::Sharpy.StringExtensions.Lower("HeLLo cOmpUteRs"));
#line (26, 5) - (26, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Lower("hello"));
#line (27, 5) - (27, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Lower(""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestUpper()
        {
#line (31, 5) - (31, 59) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("HELLO COMPUTERS", global::Sharpy.StringExtensions.Upper("HeLLo cOmpUteRs"));
#line (32, 5) - (32, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("HELLO", global::Sharpy.StringExtensions.Upper("hello"));
#line (33, 5) - (33, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Upper(""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCasefold()
        {
#line (37, 5) - (37, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Casefold("hello"));
#line (38, 5) - (38, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Casefold("Hello"));
#line (39, 5) - (39, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Casefold("ABC"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCapitalize()
        {
#line (45, 5) - (45, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(" hello ", global::Sharpy.StringExtensions.Capitalize(" hello "));
#line (46, 5) - (46, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Hello ", global::Sharpy.StringExtensions.Capitalize("Hello "));
#line (47, 5) - (47, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Hello ", global::Sharpy.StringExtensions.Capitalize("hello "));
#line (48, 5) - (48, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Aaaa", global::Sharpy.StringExtensions.Capitalize("aaaa"));
#line (49, 5) - (49, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Aaaa", global::Sharpy.StringExtensions.Capitalize("AaAa"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestTitle()
        {
#line (53, 5) - (53, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(" Hello ", global::Sharpy.StringExtensions.Title(" hello "));
#line (54, 5) - (54, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Hello World", global::Sharpy.StringExtensions.Title("hello world"));
#line (55, 5) - (55, 83) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Format This As Title String", global::Sharpy.StringExtensions.Title("fOrMaT thIs aS titLe String"));
#line (56, 5) - (56, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("Getint", global::Sharpy.StringExtensions.Title("getInt"));
#line (58, 5) - (58, 73) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("They'Re Bill'S Friends", global::Sharpy.StringExtensions.Title("they're bill's friends"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSwapcase()
        {
#line (62, 5) - (62, 62) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hEllO CoMPuTErS", global::Sharpy.StringExtensions.Swapcase("HeLLo cOmpUteRs"));
#line (63, 5) - (63, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hELLO wORLD", global::Sharpy.StringExtensions.Swapcase("Hello World"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCenter()
        {
#line (69, 5) - (69, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("   abc    ", global::Sharpy.StringExtensions.Center("abc", 10));
#line (70, 5) - (70, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(" abc  ", global::Sharpy.StringExtensions.Center("abc", 6));
#line (71, 5) - (71, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Center("abc", 3));
#line (72, 5) - (72, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Center("abc", 2));
#line (73, 5) - (73, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("***abc****", global::Sharpy.StringExtensions.Center("abc", 10, "*"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLjust()
        {
#line (77, 5) - (77, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc       ", global::Sharpy.StringExtensions.Ljust("abc", 10));
#line (78, 5) - (78, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc   ", global::Sharpy.StringExtensions.Ljust("abc", 6));
#line (79, 5) - (79, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Ljust("abc", 3));
#line (80, 5) - (80, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Ljust("abc", 2));
#line (81, 5) - (81, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc*******", global::Sharpy.StringExtensions.Ljust("abc", 10, "*"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRjust()
        {
#line (85, 5) - (85, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("       abc", global::Sharpy.StringExtensions.Rjust("abc", 10));
#line (86, 5) - (86, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("   abc", global::Sharpy.StringExtensions.Rjust("abc", 6));
#line (87, 5) - (87, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Rjust("abc", 3));
#line (88, 5) - (88, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Rjust("abc", 2));
#line (89, 5) - (89, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("*******abc", global::Sharpy.StringExtensions.Rjust("abc", 10, "*"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestZfill()
        {
#line (93, 5) - (93, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("123", global::Sharpy.StringExtensions.Zfill("123", 2));
#line (94, 5) - (94, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("123", global::Sharpy.StringExtensions.Zfill("123", 3));
#line (95, 5) - (95, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("0123", global::Sharpy.StringExtensions.Zfill("123", 4));
#line (96, 5) - (96, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("+123", global::Sharpy.StringExtensions.Zfill("+123", 3));
#line (97, 5) - (97, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("+0123", global::Sharpy.StringExtensions.Zfill("+123", 5));
#line (98, 5) - (98, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("-0123", global::Sharpy.StringExtensions.Zfill("-123", 5));
#line (99, 5) - (99, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("000", global::Sharpy.StringExtensions.Zfill("", 3));
#line (100, 5) - (100, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("34", global::Sharpy.StringExtensions.Zfill("34", 1));
#line (101, 5) - (101, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("0034", global::Sharpy.StringExtensions.Zfill("34", 4));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIslower()
        {
#line (107, 5) - (107, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Islower(""));
#line (108, 5) - (108, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Islower("a"));
#line (109, 5) - (109, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Islower("A"));
#line (110, 5) - (110, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Islower("\n"));
#line (111, 5) - (111, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Islower("abc"));
#line (112, 5) - (112, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Islower("aBc"));
#line (113, 5) - (113, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Islower("abc\n"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsupper()
        {
#line (117, 5) - (117, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper(""));
#line (118, 5) - (118, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper("a"));
#line (119, 5) - (119, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isupper("A"));
#line (120, 5) - (120, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper("\n"));
#line (121, 5) - (121, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isupper("ABC"));
#line (122, 5) - (122, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isupper("AbC"));
#line (123, 5) - (123, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isupper("ABC\n"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIstitle()
        {
#line (127, 5) - (127, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle(""));
#line (128, 5) - (128, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle("a"));
#line (129, 5) - (129, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A"));
#line (130, 5) - (130, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A Titlecased Line"));
#line (131, 5) - (131, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A\nTitlecased Line"));
#line (132, 5) - (132, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Istitle("A Titlecased, Line"));
#line (133, 5) - (133, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle("Not a capitalized String"));
#line (134, 5) - (134, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Istitle("NOT"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsspace()
        {
#line (140, 5) - (140, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isspace(""));
#line (141, 5) - (141, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace(" "));
#line (142, 5) - (142, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace("\t"));
#line (143, 5) - (143, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace("\r"));
#line (144, 5) - (144, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace("\n"));
#line (145, 5) - (145, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isspace(" \t\r\n"));
#line (146, 5) - (146, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isspace("a"));
#line (147, 5) - (147, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isspace(" \t\r\na"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsalpha()
        {
#line (151, 5) - (151, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isalpha(""));
#line (152, 5) - (152, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isalpha("a"));
#line (153, 5) - (153, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isalpha("A"));
#line (154, 5) - (154, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isalpha("abc"));
#line (155, 5) - (155, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isalpha("ab1c"));
#line (156, 5) - (156, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isalpha("abc\n"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsalnum()
        {
#line (160, 5) - (160, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isalnum(""));
#line (161, 5) - (161, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isalnum("a"));
#line (162, 5) - (162, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isalnum("1"));
#line (163, 5) - (163, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isalnum("abc123"));
#line (164, 5) - (164, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isalnum("ab c"));
#line (165, 5) - (165, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isalnum("?"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsdigit()
        {
#line (169, 5) - (169, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit(""));
#line (170, 5) - (170, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit("0"));
#line (171, 5) - (171, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isdigit("0123456789"));
#line (172, 5) - (172, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit("0123456789a"));
#line (173, 5) - (173, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit("abc"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsdecimal()
        {
#line (179, 5) - (179, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdecimal(""));
#line (180, 5) - (180, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isdecimal("0"));
#line (181, 5) - (181, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isdecimal("0123456789"));
#line (182, 5) - (182, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdecimal("0123456789a"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsnumeric()
        {
#line (186, 5) - (186, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isnumeric(""));
#line (187, 5) - (187, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isnumeric("0"));
#line (188, 5) - (188, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isnumeric("abc"));
#line (190, 5) - (190, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isnumeric("½"));
#line (191, 5) - (191, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdigit("½"));
#line (192, 5) - (192, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isdecimal("½"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsidentifier()
        {
#line (198, 5) - (198, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("a"));
#line (199, 5) - (199, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("Z"));
#line (200, 5) - (200, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("_"));
#line (201, 5) - (201, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("b0"));
#line (202, 5) - (202, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isidentifier("b_"));
#line (203, 5) - (203, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isidentifier("0"));
#line (204, 5) - (204, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isidentifier(""));
#line (205, 5) - (205, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isidentifier(" "));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsprintable()
        {
#line (209, 5) - (209, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isprintable(""));
#line (210, 5) - (210, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isprintable(" "));
#line (211, 5) - (211, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isprintable("abcdefg"));
#line (212, 5) - (212, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isprintable("abcdefg\n"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIsascii()
        {
#line (216, 5) - (216, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isascii(""));
#line (217, 5) - (217, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isascii("abc"));
#line (218, 5) - (218, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Isascii("\0\u007f"));
#line (219, 5) - (219, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isascii("\u0080"));
#line (220, 5) - (220, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Isascii("é"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestFind()
        {
#line (226, 5) - (226, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Find("abcdefghiabc", "abc"));
#line (227, 5) - (227, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Find("abcdefghiabc", "abc", 1));
#line (228, 5) - (228, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abcdefghiabc", "def", 4));
#line (229, 5) - (229, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Find("abc", "", 0));
#line (230, 5) - (230, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Find("abc", "", 3));
#line (231, 5) - (231, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abc", "", 4));
#line (232, 5) - (232, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Find("rrarrrrrrrrra", "a"));
#line (233, 5) - (233, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Find("rrarrrrrrrrra", "a", 4));
#line (234, 5) - (234, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("rrarrrrrrrrra", "a", 4, 6));
#line (235, 5) - (235, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Find("", ""));
#line (236, 5) - (236, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("", "xx"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRfind()
        {
#line (240, 5) - (240, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", "abc"));
#line (241, 5) - (241, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", ""));
#line (242, 5) - (242, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", "abcd"));
#line (243, 5) - (243, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Rfind("abcdefghiabc", "abcz"));
#line (244, 5) - (244, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Rfind("abc", "", 3));
#line (245, 5) - (245, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Rfind("rrarrrrrrrrra", "a"));
#line (246, 5) - (246, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Rfind("rrarrrrrrrrra", "a", 0, 6));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIndex()
        {
#line (256, 5) - (256, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Index("abcdefghiabc", ""));
#line (257, 5) - (257, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Index("abcdefghiabc", "def"));
#line (258, 5) - (258, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Index("abcdefghiabc", "abc"));
#line (259, 5) - (259, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Index("abcdefghiabc", "abc", 1));
#line (260, 5) - (260, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Cpython.CpythonStrTests.CpythonStrTestsModule._IndexRaises("abcdefghiabc", "hib"));
#line (261, 5) - (261, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Cpython.CpythonStrTests.CpythonStrTestsModule._IndexRaises("abcdefghi", "ghix"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRindex()
        {
#line (271, 5) - (271, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(12, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", ""));
#line (272, 5) - (272, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", "def"));
#line (273, 5) - (273, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(9, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", "abc"));
#line (274, 5) - (274, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Rindex("abcdefghiabc", "abc", 0, -1));
#line (275, 5) - (275, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.Stdlib.Tests.Spy.Cpython.CpythonStrTests.CpythonStrTestsModule._RindexRaises("abcdefghiabc", "hib"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplit()
        {
#line (281, 5) - (281, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c", "d" }, global::Sharpy.StringExtensions.Split("a b c d"));
#line (282, 5) - (282, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.StringExtensions.Split("a,b,c", ","));
#line (283, 5) - (283, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "", "c", "" }, global::Sharpy.StringExtensions.Split("a,b,,c,", ","));
#line (284, 5) - (284, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "endcase ", "" }, global::Sharpy.StringExtensions.Split("endcase test", "test"));
#line (288, 5) - (288, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.StringExtensions.Split("   a b   c "));
#line (289, 5) - (289, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> emptySplit = new Sharpy.List<string>()
#line hidden
            {
            };
#line (290, 5) - (290, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(emptySplit, global::Sharpy.StringExtensions.Split("   "));
#line (291, 5) - (291, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b c d" }, global::Sharpy.StringExtensions.Split("a b c d", " ", 1));
#line (292, 5) - (292, 57) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c d" }, global::Sharpy.StringExtensions.Split("a b c d", " ", 2));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRsplit()
        {
#line (296, 5) - (296, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c", "d" }, global::Sharpy.StringExtensions.Rsplit("a b c d"));
#line (297, 5) - (297, 55) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a b c", "d" }, global::Sharpy.StringExtensions.Rsplit("a b c d", " ", 1));
#line (298, 5) - (298, 58) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a b", "c", "d" }, global::Sharpy.StringExtensions.Rsplit("a b c d", " ", 2));
#line (299, 5) - (299, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, global::Sharpy.StringExtensions.Rsplit("a,b,c", ","));
#line (300, 5) - (300, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "", "c", "" }, global::Sharpy.StringExtensions.Rsplit("a,b,,c,", ","));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSplitlines()
        {
#line (304, 5) - (304, 72) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\n\rghi"));
#line (305, 5) - (305, 74) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\n\r\nghi"));
#line (306, 5) - (306, 68) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\r\nghi"));
#line (307, 5) - (307, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\r\nghi\n"));
#line (308, 5) - (308, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> noLines = new Sharpy.List<string>()
#line hidden
            {
            };
#line (309, 5) - (309, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(noLines, global::Sharpy.StringExtensions.Splitlines(""));
#line (310, 5) - (310, 82) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "abc\n", "def\n", "\r", "ghi" }, global::Sharpy.StringExtensions.Splitlines("abc\ndef\n\rghi", true));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestReplace()
        {
#line (316, 5) - (316, 67) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("one@two@three@", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@"));
#line (317, 5) - (317, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("one@two!three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@", 1));
#line (318, 5) - (318, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("one@two@three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@", 2));
#line (319, 5) - (319, 67) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("one!two!three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "x", "@"));
#line (320, 5) - (320, 63) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("miSSiSSippi", global::Sharpy.StringExtensions.Replace("mississippi", "ss", "SS"));
#line (323, 5) - (323, 70) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("one!two!three!", global::Sharpy.StringExtensions.Replace("one!two!three!", "!", "@", 0));
#line (324, 5) - (324, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("-a-b-c-", global::Sharpy.StringExtensions.Replace("abc", "", "-"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestCount()
        {
#line (329, 5) - (329, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.StringExtensions.Count("aaa", "a"));
#line (330, 5) - (330, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(0, global::Sharpy.StringExtensions.Count("aaa", "b"));
#line (331, 5) - (331, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Count("aaaa", "aa"));
#line (332, 5) - (332, 30) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(1, global::Sharpy.StringExtensions.Count("", ""));
#line (333, 5) - (333, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.StringExtensions.Count("mississippi", "ss"));
#line (334, 5) - (334, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(4, global::Sharpy.StringExtensions.Count("mississippi", "i"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStrip()
        {
#line (340, 5) - (340, 45) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Strip("   hello   "));
#line (341, 5) - (341, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello   ", global::Sharpy.StringExtensions.Lstrip("   hello   "));
#line (342, 5) - (342, 49) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("   hello", global::Sharpy.StringExtensions.Rstrip("   hello   "));
#line (343, 5) - (343, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Strip("hello"));
#line (344, 5) - (344, 54) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("hello", global::Sharpy.StringExtensions.Strip("xyzzyhelloxyzzy", "xyz"));
#line (345, 5) - (345, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("helloxyzzy", global::Sharpy.StringExtensions.Lstrip("xyzzyhelloxyzzy", "xyz"));
#line (346, 5) - (346, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("xyzzyhello", global::Sharpy.StringExtensions.Rstrip("xyzzyhelloxyzzy", "xyz"));
#line (347, 5) - (347, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("mississipp", global::Sharpy.StringExtensions.Strip("mississippi", "i"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestStartswith()
        {
#line (353, 5) - (353, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.StartsWith("he", "hello");
#line (354, 5) - (354, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.StartsWith("hello", "hello");
#line (355, 5) - (355, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith("hello", "hello world"));
#line (356, 5) - (356, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.StartsWith("", "hello");
#line (357, 5) - (357, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith("hello", "ello"));
#line (358, 5) - (358, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Startswith("hello", "ello", 1));
#line (359, 5) - (359, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Startswith("hello", "o", 4));
#line (360, 5) - (360, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Startswith("hello", "o", 5));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestEndswith()
        {
#line (364, 5) - (364, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.EndsWith("lo", "hello");
#line (365, 5) - (365, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.EndsWith("hello", "hello");
#line (366, 5) - (366, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Endswith("hello", "world hello"));
#line (367, 5) - (367, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.EndsWith("", "hello");
#line (368, 5) - (368, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.False(global::Sharpy.StringExtensions.Endswith("hello", "hell"));
#line (369, 5) - (369, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Endswith("hello", "ell", 0, 4));
#line (370, 5) - (370, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.EndsWith("o", "hello");
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestContains()
        {
#line (376, 5) - (376, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "") >= 0);
#line (377, 5) - (377, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "a") >= 0);
#line (378, 5) - (378, 33) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "c") >= 0);
#line (379, 5) - (379, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "abc") >= 0);
#line (380, 5) - (380, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abc", "d"));
#line (381, 5) - (381, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(global::Sharpy.StringExtensions.Find("abc", "ab") >= 0);
#line (382, 5) - (382, 35) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(-1, global::Sharpy.StringExtensions.Find("abc", "ac"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestConcatenation()
        {
#line (388, 5) - (388, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abcdef", "abc" + "def");
#line (389, 5) - (389, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", "" + "abc");
#line (390, 5) - (390, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", "abc" + "");
#line (391, 5) - (391, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abcabcabc", global::Sharpy.StringHelpers.Repeat("abc", 3));
#line (392, 5) - (392, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abcabcabc", global::Sharpy.StringHelpers.Repeat("abc", 3));
#line (393, 5) - (393, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.StringHelpers.Repeat("abc", 0));
#line (394, 5) - (394, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringHelpers.Repeat("abc", 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestComparison()
        {
#line (398, 5) - (398, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", "abc");
#line (399, 5) - (399, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.NotEqual("abd", "abc");
#line (400, 5) - (400, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(string.Compare("abc", "abd", System.StringComparison.Ordinal) < 0);
#line (401, 5) - (401, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(string.Compare("abc", "abcd", System.StringComparison.Ordinal) < 0);
#line (402, 5) - (402, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(string.Compare("abd", "abc", System.StringComparison.Ordinal) > 0);
#line (403, 5) - (403, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(string.Compare("abc", "abc", System.StringComparison.Ordinal) <= 0);
#line (404, 5) - (404, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(string.Compare("abc", "abc", System.StringComparison.Ordinal) >= 0);
#line (405, 5) - (405, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.True(string.Compare("Abc", "abc", System.StringComparison.Ordinal) < 0);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIndexing()
        {
#line (411, 5) - (411, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            string s = "python";
#line (412, 5) - (412, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("p", global::Sharpy.StringHelpers.GetItem(s, 0));
#line (413, 5) - (413, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("y", global::Sharpy.StringHelpers.GetItem(s, 1));
#line (414, 5) - (414, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("n", global::Sharpy.StringHelpers.GetItem(s, -1));
#line (415, 5) - (415, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("p", global::Sharpy.StringHelpers.GetItem(s, -6));
#line (416, 5) - (416, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(6, s.Length);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSlicing()
        {
#line (420, 5) - (420, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            string s = "abcdef";
#line (421, 5) - (421, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("ab", global::Sharpy.Slice.GetSlice(s, 0, 2, null));
#line (422, 5) - (422, 28) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("cdef", global::Sharpy.Slice.GetSlice(s, 2, null, null));
#line (423, 5) - (423, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.Slice.GetSlice(s, null, 3, null));
#line (424, 5) - (424, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abcdef", global::Sharpy.Slice.GetSlice(s, null, null, null));
#line (425, 5) - (425, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("ef", global::Sharpy.Slice.GetSlice(s, -2, null, null));
#line (426, 5) - (426, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("fedcba", global::Sharpy.Slice.GetSlice(s, null, null, -1));
#line (427, 5) - (427, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("bd", global::Sharpy.Slice.GetSlice(s, 1, 5, 2));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestIteration()
        {
#line (431, 5) - (431, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> collected = new Sharpy.List<string>()
#line hidden
            {
            };
#line (432, 5) - (433, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            foreach (var __loopVar_0 in global::Sharpy.StringHelpers.Iterate("abc"))
#line hidden
            {
                var ch = __loopVar_0;
#line (433, 9) - (433, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                collected.Append(ch);
#line hidden
            }

#line (434, 5) - (434, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, collected);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestExpandtabs()
        {
#line (440, 5) - (440, 81) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc\rab      def\ng       hi", global::Sharpy.StringExtensions.Expandtabs("abc\rab\tdef\ng\thi"));
#line (441, 5) - (441, 74) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc\rab  def\ng   hi", global::Sharpy.StringExtensions.Expandtabs("abc\rab\tdef\ng\thi", 4));
#line (442, 5) - (442, 78) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc\r\nab  def\ng   hi", global::Sharpy.StringExtensions.Expandtabs("abc\r\nab\tdef\ng\thi", 4));
#line (443, 5) - (443, 51) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("  a\n b", global::Sharpy.StringExtensions.Expandtabs(" \ta\n\tb", 1));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRemoveprefix()
        {
#line (447, 5) - (447, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("am", global::Sharpy.StringExtensions.Removeprefix("spam", "sp"));
#line (448, 5) - (448, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Removeprefix("spam", "spam"));
#line (449, 5) - (449, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removeprefix("spam", "x"));
#line (450, 5) - (450, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removeprefix("spam", ""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestRemovesuffix()
        {
#line (454, 5) - (454, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("sp", global::Sharpy.StringExtensions.Removesuffix("spam", "am"));
#line (455, 5) - (455, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Removesuffix("spam", "spam"));
#line (456, 5) - (456, 47) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removesuffix("spam", "x"));
#line (457, 5) - (457, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("spam", global::Sharpy.StringExtensions.Removesuffix("spam", ""));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestJoin()
        {
#line (461, 5) - (461, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> parts = new Sharpy.List<string>()
#line hidden
            {
                "a",
                "b",
                "c"
            };
#line (462, 5) - (462, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("a,b,c", global::Sharpy.StringExtensions.Join(",", parts));
#line (463, 5) - (463, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("abc", global::Sharpy.StringExtensions.Join("", parts));
#line (464, 5) - (464, 50) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> withEmpties = new Sharpy.List<string>()
#line hidden
            {
                "",
                "b",
                "",
                "d"
            };
#line (465, 5) - (465, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("bd", global::Sharpy.StringExtensions.Join("", withEmpties));
#line (466, 5) - (466, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> single = new Sharpy.List<string>()
#line hidden
            {
                "x"
            };
#line (467, 5) - (467, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("x", global::Sharpy.StringExtensions.Join(",", single));
#line (468, 5) - (468, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Sharpy.List<string> empty = new Sharpy.List<string>()
#line hidden
            {
            };
#line (469, 5) - (469, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
            Xunit.Assert.Equal("", global::Sharpy.StringExtensions.Join(",", empty));
#line hidden
        }
    }
}
#line default
