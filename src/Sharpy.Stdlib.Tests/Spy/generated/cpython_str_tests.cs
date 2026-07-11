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
#line (248, 5) - (253, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                try
                {
#line (249, 9) - (249, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return s.Index(sub) < 0;
                }
                catch (ValueError)
                {
#line (251, 9) - (251, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return true;
                }
            }

            internal static bool _RindexRaises(string s, string sub)
            {
#line (263, 5) - (268, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                try
                {
#line (264, 9) - (264, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return s.Rindex(sub) < 0;
                }
                catch (ValueError)
                {
#line (266, 9) - (266, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    return true;
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
#line (27, 5) - (27, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello computers", "HeLLo cOmpUteRs".Lower());
#line (28, 5) - (28, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", "hello".Lower());
#line (29, 5) - (29, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", "".Lower());
            }

            [Xunit.FactAttribute]
            public void TestUpper()
            {
#line (33, 5) - (33, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("HELLO COMPUTERS", "HeLLo cOmpUteRs".Upper());
#line (34, 5) - (34, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("HELLO", "hello".Upper());
#line (35, 5) - (35, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", "".Upper());
            }

            [Xunit.FactAttribute]
            public void TestCasefold()
            {
#line (39, 5) - (39, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", "hello".Casefold());
#line (40, 5) - (40, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", "Hello".Casefold());
#line (41, 5) - (41, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "ABC".Casefold());
            }

            [Xunit.FactAttribute]
            public void TestCapitalize()
            {
#line (47, 5) - (47, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(" hello ", " hello ".Capitalize());
#line (48, 5) - (48, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Hello ", "Hello ".Capitalize());
#line (49, 5) - (49, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Hello ", "hello ".Capitalize());
#line (50, 5) - (50, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Aaaa", "aaaa".Capitalize());
#line (51, 5) - (51, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Aaaa", "AaAa".Capitalize());
            }

            [Xunit.FactAttribute]
            public void TestTitle()
            {
#line (55, 5) - (55, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(" Hello ", " hello ".Title());
#line (56, 5) - (56, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Hello World", "hello world".Title());
#line (57, 5) - (57, 83) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Format This As Title String", "fOrMaT thIs aS titLe String".Title());
#line (58, 5) - (58, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("Getint", "getInt".Title());
#line (60, 5) - (60, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("They'Re Bill'S Friends", "they're bill's friends".Title());
            }

            [Xunit.FactAttribute]
            public void TestSwapcase()
            {
#line (64, 5) - (64, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hEllO CoMPuTErS", "HeLLo cOmpUteRs".Swapcase());
#line (65, 5) - (65, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hELLO wORLD", "Hello World".Swapcase());
            }

            [Xunit.FactAttribute]
            public void TestCenter()
            {
#line (71, 5) - (71, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("   abc    ", "abc".Center(10));
#line (72, 5) - (72, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(" abc  ", "abc".Center(6));
#line (73, 5) - (73, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc".Center(3));
#line (74, 5) - (74, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc".Center(2));
            }

            [Xunit.FactAttribute]
            public void TestLjust()
            {
#line (78, 5) - (78, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc       ", "abc".Ljust(10));
#line (79, 5) - (79, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc   ", "abc".Ljust(6));
#line (80, 5) - (80, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc".Ljust(3));
#line (81, 5) - (81, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc".Ljust(2));
            }

            [Xunit.FactAttribute]
            public void TestRjust()
            {
#line (85, 5) - (85, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("       abc", "abc".Rjust(10));
#line (86, 5) - (86, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("   abc", "abc".Rjust(6));
#line (87, 5) - (87, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc".Rjust(3));
#line (88, 5) - (88, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc".Rjust(2));
            }

            [Xunit.FactAttribute]
            public void TestZfill()
            {
#line (92, 5) - (92, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("123", "123".Zfill(2));
#line (93, 5) - (93, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("123", "123".Zfill(3));
#line (94, 5) - (94, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("0123", "123".Zfill(4));
#line (95, 5) - (95, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("+123", "+123".Zfill(3));
#line (96, 5) - (96, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("+0123", "+123".Zfill(5));
#line (97, 5) - (97, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("-0123", "-123".Zfill(5));
#line (98, 5) - (98, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("000", "".Zfill(3));
#line (99, 5) - (99, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("34", "34".Zfill(1));
#line (100, 5) - (100, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("0034", "34".Zfill(4));
            }

            [Xunit.FactAttribute]
            public void TestIslower()
            {
#line (106, 5) - (106, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Islower());
#line (107, 5) - (107, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("a".Islower());
#line (108, 5) - (108, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("A".Islower());
#line (109, 5) - (109, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("\n".Islower());
#line (110, 5) - (110, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Islower());
#line (111, 5) - (111, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("aBc".Islower());
#line (112, 5) - (112, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc\n".Islower());
            }

            [Xunit.FactAttribute]
            public void TestIsupper()
            {
#line (116, 5) - (116, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isupper());
#line (117, 5) - (117, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("a".Isupper());
#line (118, 5) - (118, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("A".Isupper());
#line (119, 5) - (119, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("\n".Isupper());
#line (120, 5) - (120, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("ABC".Isupper());
#line (121, 5) - (121, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("AbC".Isupper());
#line (122, 5) - (122, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("ABC\n".Isupper());
            }

            [Xunit.FactAttribute]
            public void TestIstitle()
            {
#line (126, 5) - (126, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Istitle());
#line (127, 5) - (127, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("a".Istitle());
#line (128, 5) - (128, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("A".Istitle());
#line (129, 5) - (129, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("A Titlecased Line".Istitle());
#line (130, 5) - (130, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("A\nTitlecased Line".Istitle());
#line (131, 5) - (131, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("A Titlecased, Line".Istitle());
#line (132, 5) - (132, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("Not a capitalized String".Istitle());
#line (133, 5) - (133, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("NOT".Istitle());
            }

            [Xunit.FactAttribute]
            public void TestIsspace()
            {
#line (139, 5) - (139, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isspace());
#line (140, 5) - (140, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(" ".Isspace());
#line (141, 5) - (141, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("\t".Isspace());
#line (142, 5) - (142, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("\r".Isspace());
#line (143, 5) - (143, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("\n".Isspace());
#line (144, 5) - (144, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(" \t\r\n".Isspace());
#line (145, 5) - (145, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("a".Isspace());
#line (146, 5) - (146, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(" \t\r\na".Isspace());
            }

            [Xunit.FactAttribute]
            public void TestIsalpha()
            {
#line (150, 5) - (150, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isalpha());
#line (151, 5) - (151, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("a".Isalpha());
#line (152, 5) - (152, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("A".Isalpha());
#line (153, 5) - (153, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Isalpha());
#line (154, 5) - (154, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("ab1c".Isalpha());
#line (155, 5) - (155, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("abc\n".Isalpha());
            }

            [Xunit.FactAttribute]
            public void TestIsalnum()
            {
#line (159, 5) - (159, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isalnum());
#line (160, 5) - (160, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("a".Isalnum());
#line (161, 5) - (161, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("1".Isalnum());
#line (162, 5) - (162, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc123".Isalnum());
#line (163, 5) - (163, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("ab c".Isalnum());
#line (164, 5) - (164, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("?".Isalnum());
            }

            [Xunit.FactAttribute]
            public void TestIsdigit()
            {
#line (168, 5) - (168, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isdigit());
#line (169, 5) - (169, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("0".Isdigit());
#line (170, 5) - (170, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("0123456789".Isdigit());
#line (171, 5) - (171, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("0123456789a".Isdigit());
#line (172, 5) - (172, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("abc".Isdigit());
            }

            [Xunit.FactAttribute]
            public void TestIsdecimal()
            {
#line (178, 5) - (178, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isdecimal());
#line (179, 5) - (179, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("0".Isdecimal());
#line (180, 5) - (180, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("0123456789".Isdecimal());
#line (181, 5) - (181, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("0123456789a".Isdecimal());
            }

            [Xunit.FactAttribute]
            public void TestIsnumeric()
            {
#line (185, 5) - (185, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isnumeric());
#line (186, 5) - (186, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("0".Isnumeric());
#line (187, 5) - (187, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("abc".Isnumeric());
#line (189, 5) - (189, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("½".Isnumeric());
#line (190, 5) - (190, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("½".Isdigit());
#line (191, 5) - (191, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("½".Isdecimal());
            }

            [Xunit.FactAttribute]
            public void TestIsidentifier()
            {
#line (197, 5) - (197, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("a".Isidentifier());
#line (198, 5) - (198, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("Z".Isidentifier());
#line (199, 5) - (199, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("_".Isidentifier());
#line (200, 5) - (200, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("b0".Isidentifier());
#line (201, 5) - (201, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("b_".Isidentifier());
#line (202, 5) - (202, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("0".Isidentifier());
#line (203, 5) - (203, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("".Isidentifier());
#line (204, 5) - (204, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False(" ".Isidentifier());
            }

            [Xunit.FactAttribute]
            public void TestIsprintable()
            {
#line (208, 5) - (208, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("".Isprintable());
#line (209, 5) - (209, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(" ".Isprintable());
#line (210, 5) - (210, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abcdefg".Isprintable());
#line (211, 5) - (211, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("abcdefg\n".Isprintable());
            }

            [Xunit.FactAttribute]
            public void TestIsascii()
            {
#line (215, 5) - (215, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("".Isascii());
#line (216, 5) - (216, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Isascii());
#line (217, 5) - (217, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("\0\u007f".Isascii());
#line (218, 5) - (218, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("\u0080".Isascii());
#line (219, 5) - (219, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("é".Isascii());
            }

            [Xunit.FactAttribute]
            public void TestFind()
            {
#line (225, 5) - (225, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "abcdefghiabc".Find("abc"));
#line (226, 5) - (226, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, "abcdefghiabc".Find("abc", 1));
#line (227, 5) - (227, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "abcdefghiabc".Find("def", 4));
#line (228, 5) - (228, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "abc".Find("", 0));
#line (229, 5) - (229, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, "abc".Find("", 3));
#line (230, 5) - (230, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "abc".Find("", 4));
#line (231, 5) - (231, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, "rrarrrrrrrrra".Find("a"));
#line (232, 5) - (232, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, "rrarrrrrrrrra".Find("a", 4));
#line (233, 5) - (233, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "rrarrrrrrrrra".Find("a", 4, 6));
#line (234, 5) - (234, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "".Find(""));
#line (235, 5) - (235, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "".Find("xx"));
            }

            [Xunit.FactAttribute]
            public void TestRfind()
            {
#line (239, 5) - (239, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, "abcdefghiabc".Rfind("abc"));
#line (240, 5) - (240, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, "abcdefghiabc".Rfind(""));
#line (241, 5) - (241, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "abcdefghiabc".Rfind("abcd"));
#line (242, 5) - (242, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "abcdefghiabc".Rfind("abcz"));
#line (243, 5) - (243, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, "abc".Rfind("", 3));
#line (244, 5) - (244, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, "rrarrrrrrrrra".Rfind("a"));
#line (245, 5) - (245, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, "rrarrrrrrrrra".Rfind("a", 0, 6));
            }

            [Xunit.FactAttribute]
            public void TestIndex()
            {
#line (255, 5) - (255, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "abcdefghiabc".Index(""));
#line (256, 5) - (256, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, "abcdefghiabc".Index("def"));
#line (257, 5) - (257, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "abcdefghiabc".Index("abc"));
#line (258, 5) - (258, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, "abcdefghiabc".Index("abc", 1));
#line (259, 5) - (259, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(_IndexRaises("abcdefghiabc", "hib"));
#line (260, 5) - (260, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(_IndexRaises("abcdefghi", "ghix"));
            }

            [Xunit.FactAttribute]
            public void TestRindex()
            {
#line (270, 5) - (270, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(12, "abcdefghiabc".Rindex(""));
#line (271, 5) - (271, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, "abcdefghiabc".Rindex("def"));
#line (272, 5) - (272, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(9, "abcdefghiabc".Rindex("abc"));
#line (273, 5) - (273, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "abcdefghiabc".Rindex("abc", 0, -1));
#line (274, 5) - (274, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(_RindexRaises("abcdefghiabc", "hib"));
            }

            [Xunit.FactAttribute]
            public void TestSplit()
            {
#line (280, 5) - (280, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c", "d" }, "a b c d".Split());
#line (281, 5) - (281, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, "a,b,c".Split(","));
#line (282, 5) - (282, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "", "c", "" }, "a,b,,c,".Split(","));
#line (283, 5) - (283, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "endcase ", "" }, "endcase test".Split("test"));
            }

            [Xunit.FactAttribute]
            public void TestRsplit()
            {
#line (291, 5) - (291, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c", "d" }, "a b c d".Rsplit());
#line (292, 5) - (292, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a b c", "d" }, "a b c d".Rsplit(" ", 1));
#line (293, 5) - (293, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a b", "c", "d" }, "a b c d".Rsplit(" ", 2));
#line (294, 5) - (294, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, "a,b,c".Rsplit(","));
#line (295, 5) - (295, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "", "c", "" }, "a,b,,c,".Rsplit(","));
            }

            [Xunit.FactAttribute]
            public void TestSplitlines()
            {
#line (299, 5) - (299, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "", "ghi" }, "abc\ndef\n\rghi".Splitlines());
#line (300, 5) - (300, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "", "ghi" }, "abc\ndef\n\r\nghi".Splitlines());
#line (301, 5) - (301, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "ghi" }, "abc\ndef\r\nghi".Splitlines());
#line (302, 5) - (302, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc", "def", "ghi" }, "abc\ndef\r\nghi\n".Splitlines());
#line (303, 5) - (303, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> noLines = new Sharpy.List<string>()
                {
                };
#line (304, 5) - (304, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(noLines, "".Splitlines());
#line (305, 5) - (305, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "abc\n", "def\n", "\r", "ghi" }, "abc\ndef\n\rghi".Splitlines(true));
            }

            [Xunit.FactAttribute]
            public void TestReplace()
            {
#line (311, 5) - (311, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one@two@three@", "one!two!three!".Replace("!", "@"));
#line (312, 5) - (312, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one@two!three!", "one!two!three!".Replace("!", "@", 1));
#line (313, 5) - (313, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one@two@three!", "one!two!three!".Replace("!", "@", 2));
#line (314, 5) - (314, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("one!two!three!", "one!two!three!".Replace("x", "@"));
#line (315, 5) - (315, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("miSSiSSippi", "mississippi".Replace("ss", "SS"));
            }

            [Xunit.FactAttribute]
            public void TestCount()
            {
#line (323, 5) - (323, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(3, "aaa".Count("a"));
#line (324, 5) - (324, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(0, "aaa".Count("b"));
#line (325, 5) - (325, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, "aaaa".Count("aa"));
#line (326, 5) - (326, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(1, "".Count(""));
#line (327, 5) - (327, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(2, "mississippi".Count("ss"));
#line (328, 5) - (328, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(4, "mississippi".Count("i"));
            }

            [Xunit.FactAttribute]
            public void TestStrip()
            {
#line (334, 5) - (334, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", "   hello   ".Strip());
#line (335, 5) - (335, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello   ", "   hello   ".Lstrip());
#line (336, 5) - (336, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("   hello", "   hello   ".Rstrip());
#line (337, 5) - (337, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", "hello".Strip());
#line (338, 5) - (338, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("hello", "xyzzyhelloxyzzy".Strip("xyz"));
#line (339, 5) - (339, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("helloxyzzy", "xyzzyhelloxyzzy".Lstrip("xyz"));
#line (340, 5) - (340, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("xyzzyhello", "xyzzyhelloxyzzy".Rstrip("xyz"));
#line (341, 5) - (341, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("mississipp", "mississippi".Strip("i"));
            }

            [Xunit.FactAttribute]
            public void TestStartswith()
            {
#line (347, 5) - (347, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.StartsWith("he", "hello");
#line (348, 5) - (348, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.StartsWith("hello", "hello");
#line (349, 5) - (349, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("hello".Startswith("hello world"));
#line (350, 5) - (350, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.StartsWith("", "hello");
#line (351, 5) - (351, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("hello".Startswith("ello"));
#line (352, 5) - (352, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("hello".Startswith("ello", 1));
#line (353, 5) - (353, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("hello".Startswith("o", 4));
#line (354, 5) - (354, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("hello".Startswith("o", 5));
            }

            [Xunit.FactAttribute]
            public void TestEndswith()
            {
#line (358, 5) - (358, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("lo", "hello");
#line (359, 5) - (359, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("hello", "hello");
#line (360, 5) - (360, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("hello".Endswith("world hello"));
#line (361, 5) - (361, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("", "hello");
#line (362, 5) - (362, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.False("hello".Endswith("hell"));
#line (363, 5) - (363, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("hello".Endswith("ell", 0, 4));
#line (364, 5) - (364, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.EndsWith("o", "hello");
            }

            [Xunit.FactAttribute]
            public void TestContains()
            {
#line (370, 5) - (370, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Find("") >= 0);
#line (371, 5) - (371, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Find("a") >= 0);
#line (372, 5) - (372, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Find("c") >= 0);
#line (373, 5) - (373, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Find("abc") >= 0);
#line (374, 5) - (374, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "abc".Find("d"));
#line (375, 5) - (375, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True("abc".Find("ab") >= 0);
#line (376, 5) - (376, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(-1, "abc".Find("ac"));
            }

            [Xunit.FactAttribute]
            public void TestConcatenation()
            {
#line (382, 5) - (382, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcdef", "abc" + "def");
#line (383, 5) - (383, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "" + "abc");
#line (384, 5) - (384, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc" + "");
#line (385, 5) - (385, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcabcabc", global::Sharpy.StringHelpers.Repeat("abc", 3));
#line (386, 5) - (386, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcabcabc", global::Sharpy.StringHelpers.Repeat("abc", 3));
#line (387, 5) - (387, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", global::Sharpy.StringHelpers.Repeat("abc", 0));
#line (388, 5) - (388, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.StringHelpers.Repeat("abc", 1));
            }

            [Xunit.FactAttribute]
            public void TestComparison()
            {
#line (392, 5) - (392, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "abc");
#line (393, 5) - (393, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.NotEqual("abd", "abc");
#line (394, 5) - (394, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abd", System.StringComparison.Ordinal) < 0);
#line (395, 5) - (395, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abcd", System.StringComparison.Ordinal) < 0);
#line (396, 5) - (396, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abd", "abc", System.StringComparison.Ordinal) > 0);
#line (397, 5) - (397, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abc", System.StringComparison.Ordinal) <= 0);
#line (398, 5) - (398, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("abc", "abc", System.StringComparison.Ordinal) >= 0);
#line (399, 5) - (399, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.True(string.Compare("Abc", "abc", System.StringComparison.Ordinal) < 0);
            }

            [Xunit.FactAttribute]
            public void TestIndexing()
            {
#line (405, 5) - (405, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                string s = "python";
#line (406, 5) - (406, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("p", global::Sharpy.StringHelpers.GetItem(s, 0));
#line (407, 5) - (407, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("y", global::Sharpy.StringHelpers.GetItem(s, 1));
#line (408, 5) - (408, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("n", global::Sharpy.StringHelpers.GetItem(s, -1));
#line (409, 5) - (409, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("p", global::Sharpy.StringHelpers.GetItem(s, -6));
#line (410, 5) - (410, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(6, s.Length);
            }

            [Xunit.FactAttribute]
            public void TestSlicing()
            {
#line (414, 5) - (414, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                string s = "abcdef";
#line (415, 5) - (415, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("ab", global::Sharpy.Slice.GetSlice(s, 0, 2, null));
#line (416, 5) - (416, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("cdef", global::Sharpy.Slice.GetSlice(s, 2, null, null));
#line (417, 5) - (417, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", global::Sharpy.Slice.GetSlice(s, null, 3, null));
#line (418, 5) - (418, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abcdef", global::Sharpy.Slice.GetSlice(s, null, null, null));
#line (419, 5) - (419, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("ef", global::Sharpy.Slice.GetSlice(s, -2, null, null));
#line (420, 5) - (420, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("fedcba", global::Sharpy.Slice.GetSlice(s, null, null, -1));
#line (421, 5) - (421, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("bd", global::Sharpy.Slice.GetSlice(s, 1, 5, 2));
            }

            [Xunit.FactAttribute]
            public void TestIteration()
            {
#line (425, 5) - (425, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> collected = new Sharpy.List<string>()
                {
                };
#line (426, 5) - (428, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                foreach (var __loopVar_0 in global::Sharpy.StringHelpers.Iterate("abc"))
                {
                    var ch = __loopVar_0;
#line (427, 9) - (427, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                    collected.Append(ch);
                }

#line (428, 5) - (428, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal(new Sharpy.List<string>() { "a", "b", "c" }, collected);
            }

            [Xunit.FactAttribute]
            public void TestExpandtabs()
            {
#line (434, 5) - (434, 81) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc\rab      def\ng       hi", "abc\rab\tdef\ng\thi".Expandtabs());
#line (435, 5) - (435, 74) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc\rab  def\ng   hi", "abc\rab\tdef\ng\thi".Expandtabs(4));
#line (436, 5) - (436, 78) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc\r\nab  def\ng   hi", "abc\r\nab\tdef\ng\thi".Expandtabs(4));
#line (437, 5) - (437, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("  a\n b", " \ta\n\tb".Expandtabs(1));
            }

            [Xunit.FactAttribute]
            public void TestRemoveprefix()
            {
#line (441, 5) - (441, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("am", "spam".Removeprefix("sp"));
#line (442, 5) - (442, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", "spam".Removeprefix("spam"));
#line (443, 5) - (443, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", "spam".Removeprefix("x"));
#line (444, 5) - (444, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", "spam".Removeprefix(""));
            }

            [Xunit.FactAttribute]
            public void TestRemovesuffix()
            {
#line (448, 5) - (448, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("sp", "spam".Removesuffix("am"));
#line (449, 5) - (449, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", "spam".Removesuffix("spam"));
#line (450, 5) - (450, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", "spam".Removesuffix("x"));
#line (451, 5) - (451, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("spam", "spam".Removesuffix(""));
            }

            [Xunit.FactAttribute]
            public void TestJoin()
            {
#line (455, 5) - (455, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> parts = new Sharpy.List<string>()
                {
                    "a",
                    "b",
                    "c"
                };
#line (456, 5) - (456, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("a,b,c", ",".Join(parts));
#line (457, 5) - (457, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("abc", "".Join(parts));
#line (458, 5) - (458, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> withEmpties = new Sharpy.List<string>()
                {
                    "",
                    "b",
                    "",
                    "d"
                };
#line (459, 5) - (459, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("bd", "".Join(withEmpties));
#line (460, 5) - (460, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> single = new Sharpy.List<string>()
                {
                    "x"
                };
#line (461, 5) - (461, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("x", ",".Join(single));
#line (462, 5) - (462, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Sharpy.List<string> empty = new Sharpy.List<string>()
                {
                };
#line (463, 5) - (463, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_str_tests.spy"
                Xunit.Assert.Equal("", ",".Join(empty));
            }
        }
    }
}
