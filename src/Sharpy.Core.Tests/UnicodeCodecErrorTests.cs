using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>str.encode</c> / <c>bytes.decode</c> honour python's <c>errors</c> argument and raise
/// <see cref="UnicodeEncodeError"/> / <see cref="UnicodeDecodeError"/> with python's five fields and
/// message (#2477). Every expected value is python3 3.13.18's measured output for the same call.
/// </summary>
public class UnicodeCodecErrorTests
{
    private static Bytes B(params byte[] data) => new Bytes(data);

    // Attribute strings are stored as UTF-8 metadata, which cannot carry a lone surrogate, so
    // [InlineData] spells one as the escape text "\\udc80" and the test unescapes it.
    private static string Unescape(string text) => System.Text.RegularExpressions.Regex.Unescape(text);

    [Fact]
    public void Encode_strict_unencodable_raises_UnicodeEncodeError_with_python_fields()
    {
        // python3: 'café'.encode('ascii')
        var act = () => "café".Encode("ascii");
        var e = act.Should().Throw<UnicodeEncodeError>().Which;
        e.Encoding.Should().Be("ascii");
        e.Object.Should().Be("café");
        e.Start.Should().Be(3);
        e.End.Should().Be(4);
        e.Reason.Should().Be("ordinal not in range(128)");
        e.Message.Should().Be("'ascii' codec can't encode character '\\xe9' in position 3: ordinal not in range(128)");
    }

    [Fact]
    public void Encode_strict_reports_the_whole_unencodable_run()
    {
        // python3: 'aéé b'.encode('ascii') -> start 1, end 3
        var act = () => "aéé b".Encode("ascii");
        var e = act.Should().Throw<UnicodeEncodeError>().Which;
        e.Start.Should().Be(1);
        e.End.Should().Be(3);
        e.Message.Should().Be("'ascii' codec can't encode characters in position 1-2: ordinal not in range(128)");
    }

    [Theory]
    [InlineData("latin-1", "€", "'latin-1' codec can't encode character '\\u20ac' in position 0: ordinal not in range(256)")]
    [InlineData("iso-8859-1", "€", "'latin-1' codec can't encode character '\\u20ac' in position 0: ordinal not in range(256)")]
    [InlineData("utf-8", "\\udc80", "'utf-8' codec can't encode character '\\udc80' in position 0: surrogates not allowed")]
    [InlineData("UTF8", "\\udc80", "'utf-8' codec can't encode character '\\udc80' in position 0: surrogates not allowed")]
    [InlineData("utf-16", "\\udc80", "'utf-16' codec can't encode character '\\udc80' in position 0: surrogates not allowed")]
    [InlineData("utf-16-le", "\\udc80", "'utf-16-le' codec can't encode character '\\udc80' in position 0: surrogates not allowed")]
    [InlineData("utf-32", "\\udc80", "'utf-32' codec can't encode character '\\udc80' in position 0: surrogates not allowed")]
    public void Encode_strict_message_names_python_codec_and_reason(string encoding, string text, string message)
    {
        var act = () => Unescape(text).Encode(encoding);
        act.Should().Throw<UnicodeEncodeError>().WithMessage(message);
    }

    [Theory]
    [InlineData("café", "ascii", "replace", new byte[] { 0x63, 0x61, 0x66, 0x3F })]
    [InlineData("café", "ascii", "ignore", new byte[] { 0x63, 0x61, 0x66 })]
    [InlineData("café", "latin-1", "strict", new byte[] { 0x63, 0x61, 0x66, 0xE9 })]
    [InlineData("\\udc80", "utf-8", "replace", new byte[] { 0x3F })]
    [InlineData("\\udc80", "utf-8", "ignore", new byte[0])]
    [InlineData("cafe", "ascii", "bogus", new byte[] { 0x63, 0x61, 0x66, 0x65 })]
    public void Encode_non_strict_handlers_match_python(string text, string encoding, string errors, byte[] expected)
    {
        ((object)Unescape(text).Encode(encoding, errors)).Should().Be(B(expected));
    }

    [Fact]
    public void Encode_unknown_handler_raises_LookupError_when_an_error_occurs()
    {
        var act = () => "café".Encode("ascii", "bogus");
        act.Should().Throw<LookupError>().WithMessage("unknown error handler name 'bogus'");
    }

    [Fact]
    public void Decode_strict_invalid_start_byte_raises_UnicodeDecodeError_with_python_fields()
    {
        // python3: b'\xff'.decode('utf-8')
        var data = B(0xFF);
        var act = () => data.Decode("utf-8");
        var e = act.Should().Throw<UnicodeDecodeError>().Which;
        e.Encoding.Should().Be("utf-8");
        ((object)e.Object).Should().Be(data);
        e.Start.Should().Be(0);
        e.End.Should().Be(1);
        e.Reason.Should().Be("invalid start byte");
        e.Message.Should().Be("'utf-8' codec can't decode byte 0xff in position 0: invalid start byte");
    }

    [Theory]
    [InlineData(new byte[] { 0x63, 0x61, 0x66, 0xE9 }, 3, 4, "'utf-8' codec can't decode byte 0xe9 in position 3: unexpected end of data")]
    [InlineData(new byte[] { 0x61, 0xE2, 0x82, 0x62 }, 1, 3, "'utf-8' codec can't decode bytes in position 1-2: invalid continuation byte")]
    [InlineData(new byte[] { 0xF0, 0x9F, 0x98 }, 0, 3, "'utf-8' codec can't decode bytes in position 0-2: unexpected end of data")]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 }, 0, 1, "'utf-8' codec can't decode byte 0xed in position 0: invalid continuation byte")]
    [InlineData(new byte[] { 0xC0, 0x80 }, 0, 1, "'utf-8' codec can't decode byte 0xc0 in position 0: invalid start byte")]
    [InlineData(new byte[] { 0x61, 0x62, 0xE9, 0x63, 0x64, 0xFF }, 2, 3, "'utf-8' codec can't decode byte 0xe9 in position 2: invalid continuation byte")]
    public void Decode_strict_utf8_reports_python_maximal_subpart(byte[] data, int start, int end, string message)
    {
        var act = () => B(data).Decode();
        var e = act.Should().Throw<UnicodeDecodeError>().Which;
        e.Start.Should().Be(start);
        e.End.Should().Be(end);
        e.Message.Should().Be(message);
    }

    [Fact]
    public void Decode_strict_ascii_raises_per_byte()
    {
        // python3: b'\x80\x81'.decode('ascii')
        var act = () => B(0x80, 0x81).Decode("ascii");
        var e = act.Should().Throw<UnicodeDecodeError>().Which;
        e.Encoding.Should().Be("ascii");
        e.Start.Should().Be(0);
        e.End.Should().Be(1);
        e.Message.Should().Be("'ascii' codec can't decode byte 0x80 in position 0: ordinal not in range(128)");
    }

    [Theory]
    [InlineData(new byte[] { 0x63, 0x61, 0x66, 0xE9 }, "utf-8", "replace", "caf�")]
    [InlineData(new byte[] { 0x63, 0x61, 0x66, 0xE9 }, "utf-8", "ignore", "caf")]
    [InlineData(new byte[] { 0x61, 0xFF, 0xFE, 0x62 }, "utf-8", "replace", "a��b")]
    [InlineData(new byte[] { 0x61, 0xE2, 0x82, 0x62 }, "utf-8", "replace", "a�b")]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 }, "utf-8", "replace", "���")]
    [InlineData(new byte[] { 0xFF }, "latin-1", "strict", "ÿ")]
    [InlineData(new byte[] { 0xFF }, "ascii", "replace", "�")]
    [InlineData(new byte[] { 0x61 }, "utf-8", "bogus", "a")]
    public void Decode_handlers_match_python(byte[] data, string encoding, string errors, string expected)
    {
        B(data).Decode(encoding, errors).Should().Be(expected);
    }

    [Fact]
    public void Decode_unknown_handler_raises_LookupError_when_an_error_occurs()
    {
        var act = () => B(0xFF).Decode("utf-8", "bogus");
        act.Should().Throw<LookupError>().WithMessage("unknown error handler name 'bogus'");
    }

    [Fact]
    public void Decode_utf16_truncated_raises_UnicodeDecodeError_inside_the_data()
    {
        // python3: b'a'.decode('utf-16') -> 'utf-16-le' codec, start 0, end 1 (python's reason
        // 'truncated data'; .NET reports no reason, so Sharpy uses python's generic 'illegal encoding').
        var act = () => B(0x61).Decode("utf-16");
        var e = act.Should().Throw<UnicodeDecodeError>().Which;
        e.Encoding.Should().Be("utf-16-le");
        e.Start.Should().Be(0);
        e.End.Should().Be(1);
    }

    [Fact]
    public void Python_constructor_formats_str_from_the_fields()
    {
        // python3: str(UnicodeEncodeError('ascii', 'aéé', 1, 3, 'ordinal not in range(128)'))
        new UnicodeEncodeError("ascii", "aéé", 1, 3, "ordinal not in range(128)").Message
            .Should().Be("'ascii' codec can't encode characters in position 1-2: ordinal not in range(128)");
        // python3: str(UnicodeDecodeError('utf-8', b'\xff\xfe', 0, 2, 'invalid start byte'))
        new UnicodeDecodeError("utf-8", B(0xFF, 0xFE), 0, 2, "invalid start byte").Message
            .Should().Be("'utf-8' codec can't decode bytes in position 0-1: invalid start byte");
    }

    [Fact]
    public void Both_errors_are_UnicodeError_and_ValueError()
    {
        // python3: issubclass(UnicodeDecodeError, (UnicodeError, ValueError)) -> True for both
        Exception decode = new UnicodeDecodeError("utf-8", B(0xFF), 0, 1, "invalid start byte");
        Exception encode = new UnicodeEncodeError("ascii", "é", 0, 1, "ordinal not in range(128)");
        decode.Should().BeAssignableTo<UnicodeError>().And.BeAssignableTo<ValueError>();
        encode.Should().BeAssignableTo<UnicodeError>().And.BeAssignableTo<ValueError>();
    }
}
