// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using math = global::Sharpy.MathModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Fractions.FractionsTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Fractions
    {
        [global::Sharpy.SharpyModule("fractions.fractions_tests")]
        public static partial class FractionsTests
        {
        }
    }

    public static partial class Fractions
    {
        public partial class FractionsTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFractionFromIntsReducesToLowestTerms()
            {
#line (10, 5) - (10, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(2, 4);
#line (11, 5) - (11, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Numerator);
#line (12, 5) - (12, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromIntHasDenominatorOne()
            {
#line (16, 5) - (16, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(5);
#line (17, 5) - (17, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(5, f.Numerator);
#line (18, 5) - (18, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionNegativeDenominatorNormalizesSign()
            {
#line (22, 5) - (22, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(1, -3);
#line (23, 5) - (23, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(-1, f.Numerator);
#line (24, 5) - (24, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionBothNegativeBecomesPositive()
            {
#line (28, 5) - (28, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(-2, -6);
#line (29, 5) - (29, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Numerator);
#line (30, 5) - (30, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionZeroDenominatorThrows()
            {
#line (34, 5) - (37, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ZeroDivisionError>((global::System.Action)(() =>
                {
#line (35, 9) - (35, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    new global::Sharpy.Fraction(1, 0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionFromDoubleExactRepresentationHalf()
            {
#line (39, 5) - (39, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(0.5d);
#line (40, 5) - (40, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Numerator);
#line (41, 5) - (41, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromDoubleExactRepresentationQuarter()
            {
#line (45, 5) - (45, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(0.25d);
#line (46, 5) - (46, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Numerator);
#line (47, 5) - (47, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(4, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromDouble01ExactBinaryRepresentation()
            {
#line (52, 5) - (52, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(0.1d);
#line (53, 5) - (53, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3602879701896397L, f.Numerator);
#line (54, 5) - (54, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(36028797018963968L, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromNanThrows()
            {
#line (58, 5) - (61, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (59, 9) - (59, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    new global::Sharpy.Fraction(math.Nan);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionFromInfinityThrows()
            {
#line (63, 5) - (66, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (64, 9) - (64, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    new global::Sharpy.Fraction(math.Inf);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionCopyConstructor()
            {
#line (68, 5) - (68, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var original = new global::Sharpy.Fraction(3, 7);
#line (69, 5) - (69, 40) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var copy = new global::Sharpy.Fraction(original);
#line (70, 5) - (70, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, copy.Numerator);
#line (71, 5) - (71, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(7, copy.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringSlashFormat()
            {
#line (77, 5) - (77, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("3/7");
#line (78, 5) - (78, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, f.Numerator);
#line (79, 5) - (79, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(7, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringDecimalFormat()
            {
#line (83, 5) - (83, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("3.14");
#line (84, 5) - (84, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(157, f.Numerator);
#line (85, 5) - (85, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(50, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringIntegerFormat()
            {
#line (89, 5) - (89, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("42");
#line (90, 5) - (90, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(42, f.Numerator);
#line (91, 5) - (91, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringNegative()
            {
#line (95, 5) - (95, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("-1/3");
#line (96, 5) - (96, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(-1, f.Numerator);
#line (97, 5) - (97, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringNegativeDecimal()
            {
#line (101, 5) - (101, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("-0.5");
#line (102, 5) - (102, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(-1, f.Numerator);
#line (103, 5) - (103, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringEmptyThrows()
            {
#line (107, 5) - (110, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (108, 9) - (108, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    new global::Sharpy.Fraction("");
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringScientificNotationNegativeExponent()
            {
#line (112, 5) - (112, 35) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("1e-2");
#line (113, 5) - (113, 29) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Numerator);
#line (114, 5) - (114, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(100, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionFromStringScientificNotationPositiveExponent()
            {
#line (118, 5) - (118, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction("1.5e2");
#line (119, 5) - (119, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(150, f.Numerator);
#line (120, 5) - (120, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionAddition()
            {
#line (126, 5) - (126, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(1, 3) + new global::Sharpy.Fraction(1, 6);
#line (127, 5) - (127, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, result.Numerator);
#line (128, 5) - (128, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionSubtraction()
            {
#line (132, 5) - (132, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(1, 2) - new global::Sharpy.Fraction(1, 3);
#line (133, 5) - (133, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, result.Numerator);
#line (134, 5) - (134, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(6, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionMultiplication()
            {
#line (138, 5) - (138, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(2, 3) * new global::Sharpy.Fraction(3, 4);
#line (139, 5) - (139, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, result.Numerator);
#line (140, 5) - (140, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionDivision()
            {
#line (144, 5) - (144, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(1, 2) / new global::Sharpy.Fraction(1, 4);
#line (145, 5) - (145, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, result.Numerator);
#line (146, 5) - (146, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionDivisionByZeroThrows()
            {
#line (150, 5) - (153, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ZeroDivisionError>((global::System.Action)(() =>
                {
#line (151, 9) - (151, 57) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    _ = new global::Sharpy.Fraction(1, 2) / new global::Sharpy.Fraction(0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionNegation()
            {
#line (155, 5) - (155, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = -new global::Sharpy.Fraction(3, 4);
#line (156, 5) - (156, 30) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(-3, f.Numerator);
#line (157, 5) - (157, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(4, f.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionModuloOperator()
            {
#line (161, 5) - (161, 65) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(7, 2) % new global::Sharpy.Fraction(3, 2);
#line (162, 5) - (162, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(1, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionFloorDivReturnsLong()
            {
#line (169, 5) - (169, 81) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                long result = new global::Sharpy.Fraction(7, 2).FloorDiv(new global::Sharpy.Fraction(3, 2));
#line (170, 5) - (170, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, result);
            }

            [Xunit.FactAttribute]
            public void TestFractionFloorDivNegativeReturnsLong()
            {
#line (174, 5) - (174, 82) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                long result = new global::Sharpy.Fraction(-7, 2).FloorDiv(new global::Sharpy.Fraction(3, 2));
#line (175, 5) - (175, 25) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(-3, result);
            }

            [Xunit.FactAttribute]
            public void TestFractionFloorDivExact()
            {
#line (179, 5) - (179, 81) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                long result = new global::Sharpy.Fraction(6, 1).FloorDiv(new global::Sharpy.Fraction(3, 1));
#line (180, 5) - (180, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, result);
            }

            [Xunit.FactAttribute]
            public void TestFractionFloorDivByZeroThrows()
            {
#line (184, 5) - (189, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ZeroDivisionError>((global::System.Action)(() =>
                {
#line (185, 9) - (185, 66) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    new global::Sharpy.Fraction(1, 2).FloorDiv(new global::Sharpy.Fraction(0));
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionMod()
            {
#line (191, 5) - (191, 88) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = global::Sharpy.Fraction.Mod(new global::Sharpy.Fraction(7, 2), new global::Sharpy.Fraction(3, 2));
#line (192, 5) - (192, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(1, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionModByZeroThrows()
            {
#line (196, 5) - (201, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ZeroDivisionError>((global::System.Action)(() =>
                {
#line (197, 9) - (197, 77) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    global::Sharpy.Fraction.Mod(new global::Sharpy.Fraction(1), new global::Sharpy.Fraction(0));
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionPowPositive()
            {
#line (203, 5) - (203, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(2, 3).Pow(3);
#line (204, 5) - (204, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(8, result.Numerator);
#line (205, 5) - (205, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(27, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionPowNegative()
            {
#line (209, 5) - (209, 46) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(2, 3).Pow(-2);
#line (210, 5) - (210, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(9, result.Numerator);
#line (211, 5) - (211, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(4, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionPowZero()
            {
#line (215, 5) - (215, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(2, 3).Pow(0);
#line (216, 5) - (216, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(1), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionAbsNegative()
            {
#line (222, 5) - (222, 45) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(-3, 4).Abs();
#line (223, 5) - (223, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, result.Numerator);
#line (224, 5) - (224, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(4, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionAbsPositive()
            {
#line (228, 5) - (228, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(3, 4).Abs();
#line (229, 5) - (229, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, result.Numerator);
#line (230, 5) - (230, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(4, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionAbsZero()
            {
#line (234, 5) - (234, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(0).Abs();
#line (235, 5) - (235, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(0), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionToLongPositive()
            {
#line (242, 5) - (242, 52) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(3, new global::Sharpy.Fraction(7, 2).ToLong());
            }

            [Xunit.FactAttribute]
            public void TestFractionToLongNegative()
            {
#line (247, 5) - (247, 54) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(-3, new global::Sharpy.Fraction(-7, 2).ToLong());
            }

            [Xunit.FactAttribute]
            public void TestFractionToLongExact()
            {
#line (251, 5) - (251, 52) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(2, new global::Sharpy.Fraction(6, 3).ToLong());
            }

            [Xunit.FactAttribute]
            public void TestFractionAddInt()
            {
#line (257, 5) - (257, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(1, 2) + 3;
#line (258, 5) - (258, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(7, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestIntAddFraction()
            {
#line (262, 5) - (262, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = 3 + new global::Sharpy.Fraction(1, 2);
#line (263, 5) - (263, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(7, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionSubtractInt()
            {
#line (267, 5) - (267, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(7, 2) - 1;
#line (268, 5) - (268, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(5, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestIntSubtractFraction()
            {
#line (272, 5) - (272, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = 3 - new global::Sharpy.Fraction(1, 2);
#line (273, 5) - (273, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(5, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionMultiplyInt()
            {
#line (277, 5) - (277, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(1, 3) * 6;
#line (278, 5) - (278, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(2), result);
            }

            [Xunit.FactAttribute]
            public void TestIntMultiplyFraction()
            {
#line (282, 5) - (282, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = 6 * new global::Sharpy.Fraction(1, 3);
#line (283, 5) - (283, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(2), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionDivideInt()
            {
#line (287, 5) - (287, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(3, 2) / 3;
#line (288, 5) - (288, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(1, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestIntDivideFraction()
            {
#line (292, 5) - (292, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = 3 / new global::Sharpy.Fraction(2, 1);
#line (293, 5) - (293, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(3, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionModInt()
            {
#line (297, 5) - (297, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = new global::Sharpy.Fraction(7, 2) % 2;
#line (298, 5) - (298, 47) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(3, 2), result);
            }

            [Xunit.FactAttribute]
            public void TestIntModFraction()
            {
#line (303, 5) - (303, 42) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = 7 % new global::Sharpy.Fraction(3, 2);
#line (304, 5) - (304, 44) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(1), result);
            }

            [Xunit.FactAttribute]
            public void TestFractionEquality()
            {
#line (310, 5) - (310, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var a = new global::Sharpy.Fraction(2, 4);
#line (311, 5) - (311, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var b = new global::Sharpy.Fraction(1, 2);
#line (312, 5) - (312, 19) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(b, a);
            }

            [Xunit.FactAttribute]
            public void TestFractionInequality()
            {
#line (316, 5) - (316, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var a = new global::Sharpy.Fraction(1, 3);
#line (317, 5) - (317, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var b = new global::Sharpy.Fraction(1, 2);
#line (318, 5) - (318, 19) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.NotEqual(b, a);
            }

            [Xunit.FactAttribute]
            public void TestFractionLessThan()
            {
#line (322, 5) - (322, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Fraction(1, 3) < new global::Sharpy.Fraction(1, 2));
            }

            [Xunit.FactAttribute]
            public void TestFractionGreaterThan()
            {
#line (326, 5) - (326, 64) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.True(new global::Sharpy.Fraction(2, 3) > new global::Sharpy.Fraction(1, 2));
            }

            [Xunit.FactAttribute]
            public void TestFractionCompareWithInt()
            {
#line (330, 5) - (330, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(3, 1);
#line (331, 5) - (331, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(new global::Sharpy.Fraction(3), f);
            }

            [Xunit.FactAttribute]
            public void TestFractionLimitDenominatorPi()
            {
#line (337, 5) - (337, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var pi = new global::Sharpy.Fraction("3.141592653589793");
#line (338, 5) - (338, 43) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var approxPi = pi.LimitDenominator(1000);
#line (339, 5) - (339, 39) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(355, approxPi.Numerator);
#line (340, 5) - (340, 41) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(113, approxPi.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionLimitDenominatorAlreadyBelow()
            {
#line (344, 5) - (344, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(1, 3);
#line (345, 5) - (345, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = f.LimitDenominator(10);
#line (346, 5) - (346, 24) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(f, result);
            }

            [Xunit.FactAttribute]
            public void TestFractionLimitDenominatorInvalidMaxThrows()
            {
#line (350, 5) - (350, 33) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(1, 3);
#line (351, 5) - (354, 1) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (352, 9) - (352, 31) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                    f.LimitDenominator(0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestFractionLimitDenominator355113MaxDenom100()
            {
#line (357, 5) - (357, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(355, 113);
#line (358, 5) - (358, 38) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = f.LimitDenominator(100);
#line (359, 5) - (359, 36) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(311, result.Numerator);
#line (360, 5) - (360, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(99, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionLimitDenominator01MaxDenom10()
            {
#line (365, 5) - (365, 32) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var f = new global::Sharpy.Fraction(0.1d);
#line (366, 5) - (366, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                var result = f.LimitDenominator(10);
#line (367, 5) - (367, 34) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1, result.Numerator);
#line (368, 5) - (368, 37) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(10, result.Denominator);
            }

            [Xunit.FactAttribute]
            public void TestFractionToStringFraction()
            {
#line (374, 5) - (374, 51) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal("1/3", global::Sharpy.Builtins.Str(new global::Sharpy.Fraction(1, 3)));
            }

            [Xunit.FactAttribute]
            public void TestFractionToStringInteger()
            {
#line (378, 5) - (378, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal("2", global::Sharpy.Builtins.Str(new global::Sharpy.Fraction(4, 2)));
            }

            [Xunit.FactAttribute]
            public void TestFractionToStringZero()
            {
#line (382, 5) - (382, 49) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal("0", global::Sharpy.Builtins.Str(new global::Sharpy.Fraction(0, 5)));
            }

            [Xunit.FactAttribute]
            public void TestFractionToDouble()
            {
#line (393, 5) - (393, 81) 1 "/Users/anton/Documents/github/sharpy/src/Sharpy.Stdlib.Tests/Spy/fractions/fractions_tests.spy"
                Xunit.Assert.Equal(1.0d / 3.0d, new global::Sharpy.Fraction(1, 3).ToDouble(), 1e-15d);
            }
        }
    }
}
