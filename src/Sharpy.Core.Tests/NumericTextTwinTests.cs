using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// #2357: <c>int()</c>, every sized integer conversion, <c>int.parse()</c>, <c>float()</c> and
/// <c>float.parse()</c> read ONE numeric-text grammar (<c>NumericText</c>) and agree with python3:
/// whitespace, sign, base prefixes, PEP 515 underscores, Unicode decimal digits. Well-formed text
/// that does not fit the target type is <see cref="OverflowError"/> (R-IS; the 32-bit <c>int</c>
/// is the ledger row <c>int-text-out-of-range</c>), malformed text is <see cref="ValueError"/>.
/// </summary>
public class NumericTextTwinTests
{
    // python3 (3.12): int(text, base) for every text × base; "ValueError" when python refuses.
    private static readonly (string Text, int Base, string Python)[] IntTable =
    {
        ("0", 2, "0"),
        ("0", 8, "0"),
        ("0", 10, "0"),
        ("0", 16, "0"),
        ("-0", 2, "0"),
        ("-0", 8, "0"),
        ("-0", 10, "0"),
        ("-0", 16, "0"),
        ("+0", 2, "0"),
        ("+0", 8, "0"),
        ("+0", 10, "0"),
        ("+0", 16, "0"),
        ("127", 2, "ValueError"),
        ("127", 8, "87"),
        ("127", 10, "127"),
        ("127", 16, "295"),
        ("128", 2, "ValueError"),
        ("128", 8, "ValueError"),
        ("128", 10, "128"),
        ("128", 16, "296"),
        ("-128", 2, "ValueError"),
        ("-128", 8, "ValueError"),
        ("-128", 10, "-128"),
        ("-128", 16, "-296"),
        ("-129", 2, "ValueError"),
        ("-129", 8, "ValueError"),
        ("-129", 10, "-129"),
        ("-129", 16, "-297"),
        ("255", 2, "ValueError"),
        ("255", 8, "173"),
        ("255", 10, "255"),
        ("255", 16, "597"),
        ("256", 2, "ValueError"),
        ("256", 8, "174"),
        ("256", 10, "256"),
        ("256", 16, "598"),
        ("32767", 2, "ValueError"),
        ("32767", 8, "13815"),
        ("32767", 10, "32767"),
        ("32767", 16, "206695"),
        ("32768", 2, "ValueError"),
        ("32768", 8, "ValueError"),
        ("32768", 10, "32768"),
        ("32768", 16, "206696"),
        ("-32768", 2, "ValueError"),
        ("-32768", 8, "ValueError"),
        ("-32768", 10, "-32768"),
        ("-32768", 16, "-206696"),
        ("-32769", 2, "ValueError"),
        ("-32769", 8, "ValueError"),
        ("-32769", 10, "-32769"),
        ("-32769", 16, "-206697"),
        ("65535", 2, "ValueError"),
        ("65535", 8, "27485"),
        ("65535", 10, "65535"),
        ("65535", 16, "415029"),
        ("65536", 2, "ValueError"),
        ("65536", 8, "27486"),
        ("65536", 10, "65536"),
        ("65536", 16, "415030"),
        ("2147483647", 2, "ValueError"),
        ("2147483647", 8, "ValueError"),
        ("2147483647", 10, "2147483647"),
        ("2147483647", 16, "142929835591"),
        ("2147483648", 2, "ValueError"),
        ("2147483648", 8, "ValueError"),
        ("2147483648", 10, "2147483648"),
        ("2147483648", 16, "142929835592"),
        ("-2147483648", 2, "ValueError"),
        ("-2147483648", 8, "ValueError"),
        ("-2147483648", 10, "-2147483648"),
        ("-2147483648", 16, "-142929835592"),
        ("-2147483649", 2, "ValueError"),
        ("-2147483649", 8, "ValueError"),
        ("-2147483649", 10, "-2147483649"),
        ("-2147483649", 16, "-142929835593"),
        ("4294967295", 2, "ValueError"),
        ("4294967295", 8, "ValueError"),
        ("4294967295", 10, "4294967295"),
        ("4294967295", 16, "285960729237"),
        ("4294967296", 2, "ValueError"),
        ("4294967296", 8, "ValueError"),
        ("4294967296", 10, "4294967296"),
        ("4294967296", 16, "285960729238"),
        ("9223372036854775807", 2, "ValueError"),
        ("9223372036854775807", 8, "ValueError"),
        ("9223372036854775807", 10, "9223372036854775807"),
        ("9223372036854775807", 16, "43132194673522839607303"),
        ("9223372036854775808", 2, "ValueError"),
        ("9223372036854775808", 8, "ValueError"),
        ("9223372036854775808", 10, "9223372036854775808"),
        ("9223372036854775808", 16, "43132194673522839607304"),
        ("-9223372036854775808", 2, "ValueError"),
        ("-9223372036854775808", 8, "ValueError"),
        ("-9223372036854775808", 10, "-9223372036854775808"),
        ("-9223372036854775808", 16, "-43132194673522839607304"),
        ("-9223372036854775809", 2, "ValueError"),
        ("-9223372036854775809", 8, "ValueError"),
        ("-9223372036854775809", 10, "-9223372036854775809"),
        ("-9223372036854775809", 16, "-43132194673522839607305"),
        ("18446744073709551615", 2, "ValueError"),
        ("18446744073709551615", 8, "ValueError"),
        ("18446744073709551615", 10, "18446744073709551615"),
        ("18446744073709551615", 16, "114598615266301018707477"),
        ("18446744073709551616", 2, "ValueError"),
        ("18446744073709551616", 8, "ValueError"),
        ("18446744073709551616", 10, "18446744073709551616"),
        ("18446744073709551616", 16, "114598615266301018707478"),
        ("99999999999999999999999", 2, "ValueError"),
        ("99999999999999999999999", 8, "ValueError"),
        ("99999999999999999999999", 10, "99999999999999999999999"),
        ("99999999999999999999999", 16, "2971056094284912659757898137"),
        ("ff", 2, "ValueError"),
        ("ff", 8, "ValueError"),
        ("ff", 10, "ValueError"),
        ("ff", 16, "255"),
        ("FF", 2, "ValueError"),
        ("FF", 8, "ValueError"),
        ("FF", 10, "ValueError"),
        ("FF", 16, "255"),
        ("0xff", 2, "ValueError"),
        ("0xff", 8, "ValueError"),
        ("0xff", 10, "ValueError"),
        ("0xff", 16, "255"),
        ("0XfF", 2, "ValueError"),
        ("0XfF", 8, "ValueError"),
        ("0XfF", 10, "ValueError"),
        ("0XfF", 16, "255"),
        ("0x_ff", 2, "ValueError"),
        ("0x_ff", 8, "ValueError"),
        ("0x_ff", 10, "ValueError"),
        ("0x_ff", 16, "255"),
        ("0x__ff", 2, "ValueError"),
        ("0x__ff", 8, "ValueError"),
        ("0x__ff", 10, "ValueError"),
        ("0x__ff", 16, "ValueError"),
        ("_ff", 2, "ValueError"),
        ("_ff", 8, "ValueError"),
        ("_ff", 10, "ValueError"),
        ("_ff", 16, "ValueError"),
        ("f_f", 2, "ValueError"),
        ("f_f", 8, "ValueError"),
        ("f_f", 10, "ValueError"),
        ("f_f", 16, "255"),
        ("ff_", 2, "ValueError"),
        ("ff_", 8, "ValueError"),
        ("ff_", 10, "ValueError"),
        ("ff_", 16, "ValueError"),
        ("0b101", 2, "5"),
        ("0b101", 8, "ValueError"),
        ("0b101", 10, "ValueError"),
        ("0b101", 16, "45313"),
        ("0B1_01", 2, "5"),
        ("0B1_01", 8, "ValueError"),
        ("0B1_01", 10, "ValueError"),
        ("0B1_01", 16, "45313"),
        ("0o777", 2, "ValueError"),
        ("0o777", 8, "511"),
        ("0o777", 10, "ValueError"),
        ("0o777", 16, "ValueError"),
        ("0o_7", 2, "ValueError"),
        ("0o_7", 8, "7"),
        ("0o_7", 10, "ValueError"),
        ("0o_7", 16, "ValueError"),
        ("777", 2, "ValueError"),
        ("777", 8, "511"),
        ("777", 10, "777"),
        ("777", 16, "1911"),
        ("1_000", 2, "8"),
        ("1_000", 8, "512"),
        ("1_000", 10, "1000"),
        ("1_000", 16, "4096"),
        ("1__000", 2, "ValueError"),
        ("1__000", 8, "ValueError"),
        ("1__000", 10, "ValueError"),
        ("1__000", 16, "ValueError"),
        ("_1", 2, "ValueError"),
        ("_1", 8, "ValueError"),
        ("_1", 10, "ValueError"),
        ("_1", 16, "ValueError"),
        ("1_", 2, "ValueError"),
        ("1_", 8, "ValueError"),
        ("1_", 10, "ValueError"),
        ("1_", 16, "ValueError"),
        (" 42 ", 2, "ValueError"),
        (" 42 ", 8, "34"),
        (" 42 ", 10, "42"),
        (" 42 ", 16, "66"),
        ("\u0009-7\u000A", 2, "ValueError"),
        ("\u0009-7\u000A", 8, "-7"),
        ("\u0009-7\u000A", 10, "-7"),
        ("\u0009-7\u000A", 16, "-7"),
        ("+7", 2, "ValueError"),
        ("+7", 8, "7"),
        ("+7", 10, "7"),
        ("+7", 16, "7"),
        ("- 5", 2, "ValueError"),
        ("- 5", 8, "ValueError"),
        ("- 5", 10, "ValueError"),
        ("- 5", 16, "ValueError"),
        ("+-5", 2, "ValueError"),
        ("+-5", 8, "ValueError"),
        ("+-5", 10, "ValueError"),
        ("+-5", 16, "ValueError"),
        ("", 2, "ValueError"),
        ("", 8, "ValueError"),
        ("", 10, "ValueError"),
        ("", 16, "ValueError"),
        ("   ", 2, "ValueError"),
        ("   ", 8, "ValueError"),
        ("   ", 10, "ValueError"),
        ("   ", 16, "ValueError"),
        ("+", 2, "ValueError"),
        ("+", 8, "ValueError"),
        ("+", 10, "ValueError"),
        ("+", 16, "ValueError"),
        ("-", 2, "ValueError"),
        ("-", 8, "ValueError"),
        ("-", 10, "ValueError"),
        ("-", 16, "ValueError"),
        ("12abc", 2, "ValueError"),
        ("12abc", 8, "ValueError"),
        ("12abc", 10, "ValueError"),
        ("12abc", 16, "76476"),
        ("1 2", 2, "ValueError"),
        ("1 2", 8, "ValueError"),
        ("1 2", 10, "ValueError"),
        ("1 2", 16, "ValueError"),
        ("0_7", 2, "ValueError"),
        ("0_7", 8, "7"),
        ("0_7", 10, "7"),
        ("0_7", 16, "7"),
        ("007", 2, "ValueError"),
        ("007", 8, "7"),
        ("007", 10, "7"),
        ("007", 16, "7"),
        ("\u0663", 2, "ValueError"),
        ("\u0663", 8, "3"),
        ("\u0663", 10, "3"),
        ("\u0663", 16, "3"),
        ("\u0661\u0662", 2, "ValueError"),
        ("\u0661\u0662", 8, "10"),
        ("\u0661\u0662", 10, "12"),
        ("\u0661\u0662", 16, "18"),
        ("\u00A042\u2003", 2, "ValueError"),
        ("\u00A042\u2003", 8, "34"),
        ("\u00A042\u2003", 10, "42"),
        ("\u00A042\u2003", 16, "66"),
        ("\u001C5", 2, "ValueError"),
        ("\u001C5", 8, "ValueError"),
        ("\u001C5", 10, "ValueError"),
        ("\u001C5", 16, "ValueError"),
        ("5\u001F", 2, "ValueError"),
        ("5\u001F", 8, "ValueError"),
        ("5\u001F", 10, "ValueError"),
        ("5\u001F", 16, "ValueError"),
        ("0x", 2, "ValueError"),
        ("0x", 8, "ValueError"),
        ("0x", 10, "ValueError"),
        ("0x", 16, "ValueError"),
        ("0b", 2, "ValueError"),
        ("0b", 8, "ValueError"),
        ("0b", 10, "ValueError"),
        ("0b", 16, "11"),
        ("0o", 2, "ValueError"),
        ("0o", 8, "ValueError"),
        ("0o", 10, "ValueError"),
        ("0o", 16, "ValueError"),
        ("x10", 2, "ValueError"),
        ("x10", 8, "ValueError"),
        ("x10", 10, "ValueError"),
        ("x10", 16, "ValueError"),
        ("0_x10", 2, "ValueError"),
        ("0_x10", 8, "ValueError"),
        ("0_x10", 10, "ValueError"),
        ("0_x10", 16, "ValueError"),
        ("-0x10", 2, "ValueError"),
        ("-0x10", 8, "ValueError"),
        ("-0x10", 10, "ValueError"),
        ("-0x10", 16, "-16"),
        ("+0b1", 2, "1"),
        ("+0b1", 8, "ValueError"),
        ("+0b1", 10, "ValueError"),
        ("+0b1", 16, "177"),
        ("-0b1_1", 2, "-3"),
        ("-0b1_1", 8, "ValueError"),
        ("-0b1_1", 10, "ValueError"),
        ("-0b1_1", 16, "-2833"),
        ("ffffffffffffffff", 2, "ValueError"),
        ("ffffffffffffffff", 8, "ValueError"),
        ("ffffffffffffffff", 10, "ValueError"),
        ("ffffffffffffffff", 16, "18446744073709551615"),
        ("7fffffffffffffff", 2, "ValueError"),
        ("7fffffffffffffff", 8, "ValueError"),
        ("7fffffffffffffff", 10, "ValueError"),
        ("7fffffffffffffff", 16, "9223372036854775807"),
        ("8000000000000000", 2, "ValueError"),
        ("8000000000000000", 8, "ValueError"),
        ("8000000000000000", 10, "8000000000000000"),
        ("8000000000000000", 16, "9223372036854775808"),
        ("-8000000000000000", 2, "ValueError"),
        ("-8000000000000000", 8, "ValueError"),
        ("-8000000000000000", 10, "-8000000000000000"),
        ("-8000000000000000", 16, "-9223372036854775808"),
        ("10000000000000000", 2, "65536"),
        ("10000000000000000", 8, "281474976710656"),
        ("10000000000000000", 10, "10000000000000000"),
        ("10000000000000000", 16, "18446744073709551616"),
        ("111111111111111111111111111111111111111111111111111111111111111", 2, "9223372036854775807"),
        ("111111111111111111111111111111111111111111111111111111111111111", 8, "112091102417619299354210525414422614573256347222572044873"),
        ("111111111111111111111111111111111111111111111111111111111111111", 10, "111111111111111111111111111111111111111111111111111111111111111"),
        ("111111111111111111111111111111111111111111111111111111111111111", 16, "482467038488817480931545770869532949388624936106835683497739933366304706833"),
        ("1111111111111111111111111111111111111111111111111111111111111111", 2, "18446744073709551615"),
        ("1111111111111111111111111111111111111111111111111111111111111111", 8, "896728819340954394833684203315380916586050777780576358985"),
        ("1111111111111111111111111111111111111111111111111111111111111111", 10, "1111111111111111111111111111111111111111111111111111111111111111"),
        ("1111111111111111111111111111111111111111111111111111111111111111", 16, "7719472615821079694904732333912527190217998977709370935963838933860875309329"),
        ("11111111111111111111111111111111111111111111111111111111111111111", 2, "36893488147419103231"),
        ("11111111111111111111111111111111111111111111111111111111111111111", 8, "7173830554727635158669473626523047332688406222244610871881"),
        ("11111111111111111111111111111111111111111111111111111111111111111", 10, "11111111111111111111111111111111111111111111111111111111111111111"),
        ("11111111111111111111111111111111111111111111111111111111111111111", 16, "123511561853137275118475717342600435043487983643349934975421422941774004949265"),
        ("-80", 2, "ValueError"),
        ("-80", 8, "ValueError"),
        ("-80", 10, "-80"),
        ("-80", 16, "-128"),
        ("7f", 2, "ValueError"),
        ("7f", 8, "ValueError"),
        ("7f", 10, "ValueError"),
        ("7f", 16, "127"),
        ("-7F", 2, "ValueError"),
        ("-7F", 8, "ValueError"),
        ("-7F", 10, "ValueError"),
        ("-7F", 16, "-127"),
        ("0x7f", 2, "ValueError"),
        ("0x7f", 8, "ValueError"),
        ("0x7f", 10, "ValueError"),
        ("0x7f", 16, "127"),
        ("0o17", 2, "ValueError"),
        ("0o17", 8, "15"),
        ("0o17", 10, "ValueError"),
        ("0o17", 16, "ValueError"),
        ("0b11111111", 2, "255"),
        ("0b11111111", 8, "ValueError"),
        ("0b11111111", 10, "ValueError"),
        ("0b11111111", 16, "47530971409"),
        ("1e3", 2, "ValueError"),
        ("1e3", 8, "ValueError"),
        ("1e3", 10, "ValueError"),
        ("1e3", 16, "483"),
        ("1.0", 2, "ValueError"),
        ("1.0", 8, "ValueError"),
        ("1.0", 10, "ValueError"),
        ("1.0", 16, "ValueError"),
        ("0x1g", 2, "ValueError"),
        ("0x1g", 8, "ValueError"),
        ("0x1g", 10, "ValueError"),
        ("0x1g", 16, "ValueError"),
        ("\u0085-3\u0085", 2, "ValueError"),
        ("\u0085-3\u0085", 8, "-3"),
        ("\u0085-3\u0085", 10, "-3"),
        ("\u0085-3\u0085", 16, "-3"),
    };

    // python3 (3.12): repr(float(text)), or "ValueError".
    private static readonly (string Text, string Python)[] FloatTable =
    {
        ("1_000.5", "1000.5"),
        ("1_0.0_1e1_0", "100100000000.0"),
        ("1__0.5", "ValueError"),
        ("1_.5", "ValueError"),
        ("1._5", "ValueError"),
        ("_1.5", "ValueError"),
        ("1.5_", "ValueError"),
        ("1e_5", "ValueError"),
        ("1_e5", "ValueError"),
        ("inf", "inf"),
        ("-Infinity", "-inf"),
        ("nan", "nan"),
        ("1_000", "1000.0"),
        ("in_f", "ValueError"),
        ("0x1_0", "ValueError"),
        (" 2.5 ", "2.5"),
        ("\u00A0-2.5\u2003", "-2.5"),
        ("\u001C1.5", "ValueError"),
        (".5", "0.5"),
        ("+.5", "0.5"),
        ("5.", "5.0"),
        ("1e3", "1000.0"),
        ("1E-3", "0.001"),
        ("\u0663.\u0665", "3.5"),
        ("", "ValueError"),
        ("   ", "ValueError"),
        ("1,5", "ValueError"),
        ("--1", "ValueError"),
        ("1e", "ValueError"),
        ("e1", "ValueError"),
        ("12abc", "ValueError"),
        ("1.5.2", "ValueError"),
    };

    // python3 (3.12): every BMP code unit c with int(chr(c)) defined is a run of ten Nd digits
    // starting at one of these code points (370 code points; Unicode 15.0).
    private static readonly int[] DigitRunStarts = { 0x0030, 0x0660, 0x06F0, 0x07C0, 0x0966, 0x09E6, 0x0A66, 0x0AE6, 0x0B66, 0x0BE6, 0x0C66, 0x0CE6, 0x0D66, 0x0DE6, 0x0E50, 0x0ED0, 0x0F20, 0x1040, 0x1090, 0x17E0, 0x1810, 0x1946, 0x19D0, 0x1A80, 0x1A90, 0x1B50, 0x1BB0, 0x1C40, 0x1C50, 0xA620, 0xA8D0, 0xA900, 0xA9D0, 0xA9F0, 0xAA50, 0xABF0, 0xFF10 };

    private sealed record IntType(string Name, BigInteger Min, BigInteger Max, Func<string, int, object> WithBase, Func<string, object> Base10);

    private static readonly IntType[] Types =
    {
        // int and int64 take no base argument (an arity-2 overload would make `map(int, xs)` an
        // ambiguous overload group); their base rows read the one parser directly.
        new("int", int.MinValue, int.MaxValue, (s, b) => (int)NumericText.ToSigned(s, b, int.MinValue, int.MaxValue, "int"), s => Builtins.Int(s)),
        new("int8", sbyte.MinValue, sbyte.MaxValue, (s, b) => Builtins.Int8(s, b), s => Builtins.Int8(s)),
        new("int16", short.MinValue, short.MaxValue, (s, b) => Builtins.Int16(s, b), s => Builtins.Int16(s)),
        new("int64", long.MinValue, long.MaxValue, (s, b) => NumericText.ToSigned(s, b, long.MinValue, long.MaxValue, "long"), s => Builtins.Long(s)),
        new("uint8", byte.MinValue, byte.MaxValue, (s, b) => Builtins.UInt8(s, b), s => Builtins.UInt8(s)),
        new("uint16", ushort.MinValue, ushort.MaxValue, (s, b) => Builtins.UInt16(s, b), s => Builtins.UInt16(s)),
        new("uint32", uint.MinValue, uint.MaxValue, (s, b) => Builtins.UInt32(s, b), s => Builtins.UInt32(s)),
        new("uint64", ulong.MinValue, ulong.MaxValue, (s, b) => Builtins.UInt64(s, b), s => Builtins.UInt64(s)),
    };

    private static string Expected(string python, IntType type)
    {
        if (python == "ValueError")
            return "ValueError";
        var value = BigInteger.Parse(python, CultureInfo.InvariantCulture);
        return value < type.Min || value > type.Max ? "OverflowError" : python;
    }

    private static string Observe(Func<object> parse)
    {
        try
        {
            return Convert.ToString(parse(), CultureInfo.InvariantCulture)!;
        }
        catch (ValueError)
        {
            return "ValueError";
        }
        catch (OverflowError)
        {
            return "OverflowError";
        }
    }

    private static string Show(string s) =>
        "'" + string.Concat(s.Select(c => c < 0x20 || c > 0x7E ? $"\\u{(int)c:X4}" : c.ToString())) + "'";

    [Fact]
    public void IntegerConversions_MatchPythonTwinTable()
    {
        var mismatches = new List<string>();
        foreach (var (text, @base, python) in IntTable)
        {
            foreach (var type in Types)
            {
                var expected = Expected(python, type);
                var actual = Observe(() => type.WithBase(text, @base));
                if (actual != expected)
                    mismatches.Add($"{type.Name}({Show(text)}, {@base}) = {actual}, expected {expected}");

                if (@base == 10)
                {
                    var single = Observe(() => type.Base10(text));
                    if (single != expected)
                        mismatches.Add($"{type.Name}({Show(text)}) = {single}, expected {expected}");
                }
            }
        }

        IntTable.Should().HaveCount(360);
        mismatches.Should().BeEmpty();
    }

    [Fact]
    public void IntParse_IsTheSameGrammarAsInt()
    {
        var mismatches = new List<string>();
        foreach (var (text, @base, python) in IntTable)
        {
            if (@base != 10)
                continue;
            var expected = Expected(python, Types[0]);
            var result = IntParse.Parse(text);
            var actual = result.IsOk ? result.Unwrap().ToString(CultureInfo.InvariantCulture) : "Err";
            if (actual != (expected == "OverflowError" || expected == "ValueError" ? "Err" : expected))
                mismatches.Add($"int.parse({Show(text)}) = {actual}, expected {expected}");
        }

        mismatches.Should().BeEmpty();
        IntParse.Parse("2147483648").UnwrapErr().Message.Should().Contain("out of range",
            "a well-formed literal that does not fit is not reported as an invalid literal (#2357)");
    }

    [Fact]
    public void FloatConversions_MatchPythonTwinTable()
    {
        var mismatches = new List<string>();
        foreach (var (text, python) in FloatTable)
        {
            string expected = python == "ValueError" ? "ValueError" : python;
            string viaBuiltin = Observe(() => Repr(Builtins.Float(text)));
            var parsed = DoubleParse.Parse(text);
            string viaParse = parsed.IsOk ? Repr(parsed.Unwrap()) : "ValueError";
            if (viaBuiltin != expected)
                mismatches.Add($"float({Show(text)}) = {viaBuiltin}, python {expected}");
            if (viaParse != expected)
                mismatches.Add($"float.parse({Show(text)}) = {viaParse}, python {expected}");
        }

        FloatTable.Should().HaveCount(32);
        mismatches.Should().BeEmpty();
    }

    private static string Repr(double d) => Builtins.Repr(d);

    [Fact]
    public void IntOfOneCodeUnit_MatchesPythonOnEveryCodeUnit()
    {
        var digits = new Dictionary<int, int>();
        foreach (var start in DigitRunStarts)
            for (int k = 0; k < 10; k++)
                digits[start + k] = k;

        var mismatches = new List<string>();
        for (int c = 0; c < 0x10000; c++)
        {
            var expected = digits.TryGetValue(c, out var v) ? v.ToString(CultureInfo.InvariantCulture) : "ValueError";
            var actual = Observe(() => Builtins.Int(((char)c).ToString()));
            if (actual != expected)
                mismatches.Add($"int('\\u{c:X4}') = {actual}, python {expected}");
        }

        digits.Should().HaveCount(370);
        mismatches.Should().BeEmpty();
    }
}
