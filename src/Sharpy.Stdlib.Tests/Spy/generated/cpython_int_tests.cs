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
using static Sharpy.Stdlib.Tests.Spy.Cpython.CpythonIntTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Cpython
    {
        [global::Sharpy.SharpyModule("cpython.cpython_int_tests")]
        public static partial class CpythonIntTests
        {
            internal static bool _IntRaises(string s)
            {
#line (60, 5) - (65, 1) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                try
#line hidden
                {
#line (61, 9) - (61, 38) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                    return global::Sharpy.Builtins.Int(s) == 0 && false;
#line hidden
                }
                catch (global::Sharpy.ValueError)
                {
#line (63, 9) - (63, 21) 20 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                    return true;
#line hidden
                }
            }
        }
    }

    public static partial class Cpython
    {
        public partial class CpythonIntTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFromString()
            {
#line (28, 5) - (28, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Int("0"));
#line (29, 5) - (29, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(10, global::Sharpy.Builtins.Int("10"));
#line (30, 5) - (30, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(314, global::Sharpy.Builtins.Int("314"));
#line (31, 5) - (31, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(314, global::Sharpy.Builtins.Int(" 314"));
#line (32, 5) - (32, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(314, global::Sharpy.Builtins.Int("314 "));
#line (33, 5) - (33, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(314, global::Sharpy.Builtins.Int("  \t\t  314  \t\t  "));
#line (34, 5) - (34, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(-3, global::Sharpy.Builtins.Int("-3"));
#line (35, 5) - (35, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(-3, global::Sharpy.Builtins.Int(" -3 "));
#line (36, 5) - (36, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(10, global::Sharpy.Builtins.Int("+10"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFromFloatTruncates()
            {
#line (42, 5) - (42, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Int(3.14d));
#line (43, 5) - (43, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(-3, global::Sharpy.Builtins.Int(-3.14d));
#line (44, 5) - (44, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Int(3.9d));
#line (45, 5) - (45, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(-3, global::Sharpy.Builtins.Int(-3.9d));
#line (46, 5) - (46, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Int(3.5d));
#line (47, 5) - (47, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(-3, global::Sharpy.Builtins.Int(-3.5d));
#line (48, 5) - (48, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Int(0.0d));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestFromBool()
            {
#line (54, 5) - (54, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Int(true));
#line (55, 5) - (55, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Int(false));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInvalidSigns()
            {
#line (67, 5) - (67, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("+"));
#line (68, 5) - (68, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("-"));
#line (69, 5) - (69, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("- 1"));
#line (70, 5) - (70, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("+ 1"));
#line (71, 5) - (71, 33) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises(" + 1 "));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestInvalidStrings()
            {
#line (77, 5) - (77, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises(""));
#line (78, 5) - (78, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises(" "));
#line (79, 5) - (79, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("  \t\t  "));
#line (80, 5) - (80, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("1x"));
#line (81, 5) - (81, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("  1x"));
#line (82, 5) - (82, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("abc"));
#line (83, 5) - (83, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("12.3"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestStringFloat()
            {
#line (89, 5) - (89, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("1.5"));
#line (90, 5) - (90, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises("1e3"));
#line (91, 5) - (91, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(_IntRaises(".5"));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestIntToString()
            {
#line (97, 5) - (97, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal("0", global::Sharpy.Builtins.Str(0));
#line (98, 5) - (98, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal("7", global::Sharpy.Builtins.Str(7));
#line (99, 5) - (99, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal("-5", global::Sharpy.Builtins.Str(-5));
#line (100, 5) - (100, 34) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal("12345", global::Sharpy.Builtins.Str(12345));
#line (101, 5) - (101, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal("-12345", global::Sharpy.Builtins.Str(-12345));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestArithmetic()
            {
#line (107, 5) - (107, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(3, (5 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(17) / 5))));
#line (108, 5) - (108, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.FloorMod(17, 5));
#line (109, 5) - (109, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(5, (4 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)(20) / 4))));
#line (110, 5) - (110, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.FloorMod(20, 4));
#line (111, 5) - (111, 28) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(1024, 1024);
#line (112, 5) - (112, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(81, 81);
#line (113, 5) - (113, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(9, global::Sharpy.Builtins.Abs(-9));
#line (114, 5) - (114, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(9, global::Sharpy.Builtins.Abs(9));
#line (115, 5) - (115, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal((3, 2), global::Sharpy.Builtins.Divmod(17, 5));
#line (116, 5) - (116, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal((5, 0), global::Sharpy.Builtins.Divmod(20, 4));
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestComparisons()
            {
#line (122, 5) - (122, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(1 < 2);
#line (123, 5) - (123, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(2 <= 2);
#line (124, 5) - (124, 18) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(3 > 2);
#line (125, 5) - (125, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(3 >= 3);
#line (126, 5) - (126, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(5, 5);
#line (127, 5) - (127, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.NotEqual(6, 5);
#line (128, 5) - (128, 19) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.True(-1 < 0);
#line (129, 5) - (129, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Min(3, 1, 2));
#line (130, 5) - (130, 30) 16 "src/Sharpy.Stdlib.Tests/Spy/cpython/cpython_int_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Max(3, 1, 2));
#line hidden
            }
        }
    }
}
#line default
