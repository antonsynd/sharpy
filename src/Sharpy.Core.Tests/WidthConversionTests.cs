using Xunit;
using Sharpy;

namespace Sharpy.Core.Tests;

/// <summary>
/// Table-driven tests for per-width conversion builtins:
/// Int8, Int16, UInt8, UInt16, UInt32, UInt64, Float32.
/// </summary>
public class WidthConversionTests
{
    // ==================== Int8 (sbyte: -128 to 127) ====================

    [Theory]
    [InlineData(true, (sbyte)1)]
    [InlineData(false, (sbyte)0)]
    public void Int8_FromBool(bool input, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input));

    [Theory]
    [InlineData(0, (sbyte)0)]
    [InlineData(42, (sbyte)42)]
    [InlineData(-128, (sbyte)-128)]
    [InlineData(127, (sbyte)127)]
    public void Int8_FromInt_InRange(int input, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input));

    [Theory]
    [InlineData(128)]
    [InlineData(-129)]
    [InlineData(1000)]
    public void Int8_FromInt_OutOfRange(int input) =>
        Assert.Throws<OverflowError>(() => Builtins.Int8(input));

    [Theory]
    [InlineData(42L, (sbyte)42)]
    [InlineData(-128L, (sbyte)-128)]
    public void Int8_FromLong_InRange(long input, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input));

    [Fact]
    public void Int8_FromLong_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8(200L));

    [Theory]
    [InlineData(3.9f, (sbyte)3)]
    [InlineData(-3.9f, (sbyte)-3)]
    public void Int8_FromFloat_Truncates(float input, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input));

    [Fact]
    public void Int8_FromFloat_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int8(float.NaN));

    [Fact]
    public void Int8_FromFloat_Infinity_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8(float.PositiveInfinity));

    [Fact]
    public void Int8_FromFloat_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8(200.0f));

    [Theory]
    [InlineData(3.9, (sbyte)3)]
    [InlineData(-3.9, (sbyte)-3)]
    public void Int8_FromDouble_Truncates(double input, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input));

    [Fact]
    public void Int8_FromDouble_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int8(double.NaN));

    [Fact]
    public void Int8_FromDouble_Infinity_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8(double.PositiveInfinity));

    [Fact]
    public void Int8_FromDecimal_InRange() =>
        Assert.Equal((sbyte)3, Builtins.Int8(3.5m));

    [Fact]
    public void Int8_FromDecimal_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8(200m));

    [Theory]
    [InlineData("42", (sbyte)42)]
    [InlineData("  -128  ", (sbyte)-128)]
    [InlineData("127", (sbyte)127)]
    public void Int8_FromString_Valid(string input, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input));

    [Fact]
    public void Int8_FromString_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8("200"));

    [Fact]
    public void Int8_FromString_Invalid() =>
        Assert.Throws<ValueError>(() => Builtins.Int8("abc"));

    [Fact]
    public void Int8_FromString_Empty() =>
        Assert.Throws<ValueError>(() => Builtins.Int8(""));

    [Theory]
    [InlineData("7f", 16, (sbyte)127)]
    [InlineData("0x7f", 16, (sbyte)127)]
    [InlineData("a", 16, (sbyte)10)]
    [InlineData("1111111", 2, (sbyte)127)]
    [InlineData("0b1111111", 2, (sbyte)127)]
    [InlineData("77", 8, (sbyte)63)]
    [InlineData("0o77", 8, (sbyte)63)]
    public void Int8_FromString_WithBase(string input, int @base, sbyte expected) =>
        Assert.Equal(expected, Builtins.Int8(input, @base));

    [Fact]
    public void Int8_FromString_WithBase_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8("ff", 16));

    [Fact]
    public void Int8_FromByte_InRange() =>
        Assert.Equal((sbyte)100, Builtins.Int8((byte)100));

    [Fact]
    public void Int8_FromByte_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int8((byte)200));

    [Fact]
    public void Int8_FromSByte_Identity() =>
        Assert.Equal((sbyte)-42, Builtins.Int8((sbyte)-42));

    // ==================== Int16 (short: -32768 to 32767) ====================

    [Theory]
    [InlineData(true, (short)1)]
    [InlineData(false, (short)0)]
    public void Int16_FromBool(bool input, short expected) =>
        Assert.Equal(expected, Builtins.Int16(input));

    [Theory]
    [InlineData(0, (short)0)]
    [InlineData(32767, (short)32767)]
    [InlineData(-32768, (short)-32768)]
    public void Int16_FromInt_InRange(int input, short expected) =>
        Assert.Equal(expected, Builtins.Int16(input));

    [Theory]
    [InlineData(32768)]
    [InlineData(-32769)]
    public void Int16_FromInt_OutOfRange(int input) =>
        Assert.Throws<OverflowError>(() => Builtins.Int16(input));

    [Fact]
    public void Int16_FromFloat_Truncates() =>
        Assert.Equal((short)3, Builtins.Int16(3.9f));

    [Fact]
    public void Int16_FromFloat_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int16(float.NaN));

    [Fact]
    public void Int16_FromDouble_Truncates() =>
        Assert.Equal((short)3, Builtins.Int16(3.9));

    [Fact]
    public void Int16_FromDouble_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int16(double.NaN));

    [Fact]
    public void Int16_FromString_Valid() =>
        Assert.Equal((short)1000, Builtins.Int16("1000"));

    [Fact]
    public void Int16_FromString_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int16("40000"));

    [Fact]
    public void Int16_FromString_Invalid() =>
        Assert.Throws<ValueError>(() => Builtins.Int16("abc"));

    [Fact]
    public void Int16_FromByte_Widening() =>
        Assert.Equal((short)255, Builtins.Int16((byte)255));

    [Fact]
    public void Int16_FromSByte_Widening() =>
        Assert.Equal((short)-128, Builtins.Int16((sbyte)-128));

    [Fact]
    public void Int16_Identity() =>
        Assert.Equal((short)42, Builtins.Int16((short)42));

    [Fact]
    public void Int16_FromUShort_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.Int16((ushort)40000));

    [Fact]
    public void Int16_FromString_WithBase() =>
        Assert.Equal((short)255, Builtins.Int16("0xff", 16));

    // ==================== UInt8 (byte: 0 to 255) ====================

    [Theory]
    [InlineData(true, (byte)1)]
    [InlineData(false, (byte)0)]
    public void UInt8_FromBool(bool input, byte expected) =>
        Assert.Equal(expected, Builtins.UInt8(input));

    [Theory]
    [InlineData(0, (byte)0)]
    [InlineData(255, (byte)255)]
    public void UInt8_FromInt_InRange(int input, byte expected) =>
        Assert.Equal(expected, Builtins.UInt8(input));

    [Theory]
    [InlineData(256)]
    [InlineData(-1)]
    public void UInt8_FromInt_OutOfRange(int input) =>
        Assert.Throws<OverflowError>(() => Builtins.UInt8(input));

    [Fact]
    public void UInt8_FromFloat_Truncates() =>
        Assert.Equal((byte)3, Builtins.UInt8(3.9f));

    [Fact]
    public void UInt8_FromFloat_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt8(-1.0f));

    [Fact]
    public void UInt8_FromFloat_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.UInt8(float.NaN));

    [Fact]
    public void UInt8_FromDouble_Truncates() =>
        Assert.Equal((byte)3, Builtins.UInt8(3.9));

    [Fact]
    public void UInt8_FromDouble_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.UInt8(double.NaN));

    [Fact]
    public void UInt8_FromString_Valid() =>
        Assert.Equal((byte)42, Builtins.UInt8("42"));

    [Fact]
    public void UInt8_FromString_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt8("256"));

    [Fact]
    public void UInt8_FromString_Negative_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt8("-1"));

    [Fact]
    public void UInt8_FromString_Invalid() =>
        Assert.Throws<ValueError>(() => Builtins.UInt8("abc"));

    [Fact]
    public void UInt8_Identity() =>
        Assert.Equal((byte)200, Builtins.UInt8((byte)200));

    [Fact]
    public void UInt8_FromSByte_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt8((sbyte)-1));

    [Fact]
    public void UInt8_FromSByte_InRange() =>
        Assert.Equal((byte)100, Builtins.UInt8((sbyte)100));

    [Fact]
    public void UInt8_FromString_WithBase() =>
        Assert.Equal((byte)255, Builtins.UInt8("0xff", 16));

    // ==================== UInt16 (ushort: 0 to 65535) ====================

    [Theory]
    [InlineData(true, (ushort)1)]
    [InlineData(false, (ushort)0)]
    public void UInt16_FromBool(bool input, ushort expected) =>
        Assert.Equal(expected, Builtins.UInt16(input));

    [Theory]
    [InlineData(0, (ushort)0)]
    [InlineData(65535, (ushort)65535)]
    public void UInt16_FromInt_InRange(int input, ushort expected) =>
        Assert.Equal(expected, Builtins.UInt16(input));

    [Theory]
    [InlineData(65536)]
    [InlineData(-1)]
    public void UInt16_FromInt_OutOfRange(int input) =>
        Assert.Throws<OverflowError>(() => Builtins.UInt16(input));

    [Fact]
    public void UInt16_FromString_Valid() =>
        Assert.Equal((ushort)1000, Builtins.UInt16("1000"));

    [Fact]
    public void UInt16_FromString_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt16("70000"));

    [Fact]
    public void UInt16_FromByte_Widening() =>
        Assert.Equal((ushort)255, Builtins.UInt16((byte)255));

    [Fact]
    public void UInt16_FromSByte_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt16((sbyte)-1));

    [Fact]
    public void UInt16_FromShort_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt16((short)-1));

    [Fact]
    public void UInt16_Identity() =>
        Assert.Equal((ushort)42, Builtins.UInt16((ushort)42));

    [Fact]
    public void UInt16_FromString_WithBase() =>
        Assert.Equal((ushort)255, Builtins.UInt16("0xff", 16));

    // ==================== UInt32 (uint: 0 to 4294967295) ====================

    [Theory]
    [InlineData(true, 1u)]
    [InlineData(false, 0u)]
    public void UInt32_FromBool(bool input, uint expected) =>
        Assert.Equal(expected, Builtins.UInt32(input));

    [Fact]
    public void UInt32_FromInt_InRange() =>
        Assert.Equal(42u, Builtins.UInt32(42));

    [Fact]
    public void UInt32_FromInt_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt32(-1));

    [Fact]
    public void UInt32_FromLong_InRange() =>
        Assert.Equal(42u, Builtins.UInt32(42L));

    [Fact]
    public void UInt32_FromLong_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt32(5000000000L));

    [Fact]
    public void UInt32_FromFloat_Truncates() =>
        Assert.Equal(3u, Builtins.UInt32(3.9f));

    [Fact]
    public void UInt32_FromFloat_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt32(-1.0f));

    [Fact]
    public void UInt32_FromString_Valid() =>
        Assert.Equal(42u, Builtins.UInt32("42"));

    [Fact]
    public void UInt32_FromString_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt32("5000000000"));

    [Fact]
    public void UInt32_FromString_Negative_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt32("-1"));

    [Fact]
    public void UInt32_FromByte_Widening() =>
        Assert.Equal(255u, Builtins.UInt32((byte)255));

    [Fact]
    public void UInt32_FromUShort_Widening() =>
        Assert.Equal((uint)65535, Builtins.UInt32((ushort)65535));

    [Fact]
    public void UInt32_Identity() =>
        Assert.Equal(42u, Builtins.UInt32(42u));

    [Fact]
    public void UInt32_FromULong_OutOfRange() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt32(5000000000UL));

    [Fact]
    public void UInt32_FromString_WithBase() =>
        Assert.Equal((uint)255, Builtins.UInt32("0xff", 16));

    // ==================== UInt64 (ulong: 0 to 18446744073709551615) ====================

    [Theory]
    [InlineData(true, 1UL)]
    [InlineData(false, 0UL)]
    public void UInt64_FromBool(bool input, ulong expected) =>
        Assert.Equal(expected, Builtins.UInt64(input));

    [Fact]
    public void UInt64_FromInt_InRange() =>
        Assert.Equal(42UL, Builtins.UInt64(42));

    [Fact]
    public void UInt64_FromInt_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt64(-1));

    [Fact]
    public void UInt64_FromLong_InRange() =>
        Assert.Equal(42UL, Builtins.UInt64(42L));

    [Fact]
    public void UInt64_FromLong_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt64(-1L));

    [Fact]
    public void UInt64_FromFloat_Truncates() =>
        Assert.Equal(3UL, Builtins.UInt64(3.9f));

    [Fact]
    public void UInt64_FromFloat_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.UInt64(float.NaN));

    [Fact]
    public void UInt64_FromFloat_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt64(-1.0f));

    [Fact]
    public void UInt64_FromDouble_Truncates() =>
        Assert.Equal(3UL, Builtins.UInt64(3.9));

    [Fact]
    public void UInt64_FromDouble_NaN_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.UInt64(double.NaN));

    [Fact]
    public void UInt64_FromString_Valid() =>
        Assert.Equal(42UL, Builtins.UInt64("42"));

    [Fact]
    public void UInt64_FromString_Invalid() =>
        Assert.Throws<ValueError>(() => Builtins.UInt64("abc"));

    [Fact]
    public void UInt64_FromByte_Widening() =>
        Assert.Equal(255UL, Builtins.UInt64((byte)255));

    [Fact]
    public void UInt64_FromUShort_Widening() =>
        Assert.Equal(65535UL, Builtins.UInt64((ushort)65535));

    [Fact]
    public void UInt64_FromUInt_Widening() =>
        Assert.Equal(42UL, Builtins.UInt64(42u));

    [Fact]
    public void UInt64_Identity() =>
        Assert.Equal(42UL, Builtins.UInt64(42UL));

    [Fact]
    public void UInt64_FromSByte_Negative_ThrowsOverflowError() =>
        Assert.Throws<OverflowError>(() => Builtins.UInt64((sbyte)-1));

    [Fact]
    public void UInt64_FromString_WithBase() =>
        Assert.Equal(255UL, Builtins.UInt64("0xff", 16));

    // ==================== Float32 (float: System.Single) ====================

    [Theory]
    [InlineData(true, 1.0f)]
    [InlineData(false, 0.0f)]
    public void Float32_FromBool(bool input, float expected) =>
        Assert.Equal(expected, Builtins.Float32(input));

    [Fact]
    public void Float32_FromInt() =>
        Assert.Equal(42.0f, Builtins.Float32(42));

    [Fact]
    public void Float32_FromLong() =>
        Assert.Equal(42.0f, Builtins.Float32(42L));

    [Fact]
    public void Float32_Identity() =>
        Assert.Equal(3.14f, Builtins.Float32(3.14f));

    [Fact]
    public void Float32_FromDouble_Narrowing() =>
        Assert.Equal(3.14f, Builtins.Float32(3.14));

    [Fact]
    public void Float32_FromDouble_Overflow_Infinity()
    {
        var result = Builtins.Float32(1e40);
        Assert.True(float.IsPositiveInfinity(result));
    }

    [Fact]
    public void Float32_FromDouble_NegativeOverflow_NegativeInfinity()
    {
        var result = Builtins.Float32(-1e40);
        Assert.True(float.IsNegativeInfinity(result));
    }

    [Fact]
    public void Float32_FromDouble_NaN()
    {
        var result = Builtins.Float32(double.NaN);
        Assert.True(float.IsNaN(result));
    }

    [Fact]
    public void Float32_FromDecimal() =>
        Assert.Equal(3.14f, Builtins.Float32(3.14m));

    [Fact]
    public void Float32_FromString_Valid() =>
        Assert.Equal(3.14f, Builtins.Float32("3.14"));

    [Fact]
    public void Float32_FromString_Overflow_Infinity()
    {
        var result = Builtins.Float32("1e40");
        Assert.True(float.IsPositiveInfinity(result));
    }

    [Fact]
    public void Float32_FromString_Inf()
    {
        var result = Builtins.Float32("inf");
        Assert.True(float.IsPositiveInfinity(result));
    }

    [Fact]
    public void Float32_FromString_NegInf()
    {
        var result = Builtins.Float32("-inf");
        Assert.True(float.IsNegativeInfinity(result));
    }

    [Fact]
    public void Float32_FromString_NaN()
    {
        var result = Builtins.Float32("nan");
        Assert.True(float.IsNaN(result));
    }

    [Fact]
    public void Float32_FromString_Invalid() =>
        Assert.Throws<ValueError>(() => Builtins.Float32("abc"));

    [Fact]
    public void Float32_FromByte() =>
        Assert.Equal(255.0f, Builtins.Float32((byte)255));

    [Fact]
    public void Float32_FromSByte() =>
        Assert.Equal(-42.0f, Builtins.Float32((sbyte)-42));

    [Fact]
    public void Float32_FromShort() =>
        Assert.Equal(1000.0f, Builtins.Float32((short)1000));

    [Fact]
    public void Float32_FromUShort() =>
        Assert.Equal(65535.0f, Builtins.Float32((ushort)65535));

    [Fact]
    public void Float32_FromUInt() =>
        Assert.Equal(42.0f, Builtins.Float32(42u));

    [Fact]
    public void Float32_FromULong() =>
        Assert.Equal(42.0f, Builtins.Float32(42UL));

    // ==================== Base parsing edge cases ====================

    [Fact]
    public void ParseIntWithBase_InvalidBase_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int8("42", 3));

    [Fact]
    public void ParseIntWithBase_EmptyString_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int8("", 16));

    [Fact]
    public void ParseIntWithBase_PrefixOnly_ThrowsValueError() =>
        Assert.Throws<ValueError>(() => Builtins.Int8("0x", 16));

    [Fact]
    public void ParseIntWithBase_Binary() =>
        Assert.Equal((sbyte)10, Builtins.Int8("0b1010", 2));

    [Fact]
    public void ParseIntWithBase_Octal() =>
        Assert.Equal((sbyte)63, Builtins.Int8("0o77", 8));

    [Fact]
    public void ParseIntWithBase_NegativeWithPrefix() =>
        Assert.Equal((sbyte)-10, Builtins.Int8("-0b1010", 2));
}
