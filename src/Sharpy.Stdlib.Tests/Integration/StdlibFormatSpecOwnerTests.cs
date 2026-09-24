using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// Stdlib types whose python twin owns a <c>__format__</c> (plan-bf0244 audit R2, a #1988
/// regression): <c>ipaddress</c> addresses and interfaces (<c>_BaseAddress.__format__</c>) and
/// <c>http.HTTPStatus</c> (an <c>IntEnum</c>, <c>int.__format__</c>) implement
/// <see cref="System.IFormattable"/>, so a spec they accept is not the "no __format__" TypeError.
/// The spec is dynamic (a <c>str</c> variable), so this is the runtime route. Expected stdout is
/// python3 3.12.13 running the same program over the <c>ipaddress</c>/<c>http</c> modules.
/// </summary>
public class StdlibFormatSpecOwnerTests : StdlibIntegrationTestBase
{
    public StdlibFormatSpecOwnerTests(ITestOutputHelper output) : base(output)
    {
    }

    private const string Program = @"import ipaddress
import http
from ipaddress import IPv4Interface, IPv6Interface, IPv6Address

def show(label: str, v: object, specs: list[str]) -> None:
    for spec in specs:
        try:
            print(label + ""|"" + spec + ""|"" + repr(format(v, spec)))
        except Exception as e:
            print(label + ""|"" + spec + ""|"" + type(e).__name__ + "": "" + str(e))

def main() -> None:
    specs: list[str] = ["""", ""s"", "">10s"", ""<20s"", ""^42s"", "">10"", ""b"", ""#b"", ""_b"", ""#_b"", ""x"", ""#x"", ""X"", ""#X"", ""_x"", ""#_X"", ""n"", ""#n"", ""_n"", ""d"", ""bb"", ""o"", ""#s"", ""+s""]
    show(""v4"", ipaddress.ip_address(""1.2.3.4""), specs)
    show(""v6"", IPv6Address(""2001:db8::1""), specs)
    show(""v6z"", IPv6Address(""::""), specs)
    show(""if4"", IPv4Interface(""1.2.3.4/24""), specs)
    show(""if6"", IPv6Interface(""2001:db8::1/64""), specs)
    show(""http"", http.HTTPStatus.OK, ["""", "">6"", ""d"", ""x"", ""+""])
";

    // python3 3.12.13, same program.
    private const string Expected =
        "v4||'1.2.3.4'\n" +
        "v4|s|'1.2.3.4'\n" +
        "v4|>10s|'   1.2.3.4'\n" +
        "v4|<20s|'1.2.3.4             '\n" +
        "v4|^42s|'                 1.2.3.4                  '\n" +
        "v4|>10|TypeError: unsupported format string passed to IPv4Address.__format__\n" +
        "v4|b|'00000001000000100000001100000100'\n" +
        "v4|#b|'0b00000001000000100000001100000100'\n" +
        "v4|_b|'0000_0001_0000_0010_0000_0011_0000_0100'\n" +
        "v4|#_b|'0b0000_0001_0000_0010_0000_0011_0000_0100'\n" +
        "v4|x|'01020304'\n" +
        "v4|#x|'0x01020304'\n" +
        "v4|X|'01020304'\n" +
        "v4|#X|'0X01020304'\n" +
        "v4|_x|'0102_0304'\n" +
        "v4|#_X|'0X0102_0304'\n" +
        "v4|n|'00000001000000100000001100000100'\n" +
        "v4|#n|'0b00000001000000100000001100000100'\n" +
        "v4|_n|'0000_0001_0000_0010_0000_0011_0000_0100'\n" +
        "v4|d|TypeError: unsupported format string passed to IPv4Address.__format__\n" +
        "v4|bb|TypeError: unsupported format string passed to IPv4Address.__format__\n" +
        "v4|o|TypeError: unsupported format string passed to IPv4Address.__format__\n" +
        "v4|#s|ValueError: Alternate form (#) not allowed in string format specifier\n" +
        "v4|+s|ValueError: Sign not allowed in string format specifier\n" +
        "v6||'2001:db8::1'\n" +
        "v6|s|'2001:db8::1'\n" +
        "v6|>10s|'2001:db8::1'\n" +
        "v6|<20s|'2001:db8::1         '\n" +
        "v6|^42s|'               2001:db8::1                '\n" +
        "v6|>10|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6|b|'00100000000000010000110110111000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001'\n" +
        "v6|#b|'0b00100000000000010000110110111000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001'\n" +
        "v6|_b|'0010_0000_0000_0001_0000_1101_1011_1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001'\n" +
        "v6|#_b|'0b0010_0000_0000_0001_0000_1101_1011_1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001'\n" +
        "v6|x|'20010db8000000000000000000000001'\n" +
        "v6|#x|'0x20010db8000000000000000000000001'\n" +
        "v6|X|'20010DB8000000000000000000000001'\n" +
        "v6|#X|'0X20010DB8000000000000000000000001'\n" +
        "v6|_x|'2001_0db8_0000_0000_0000_0000_0000_0001'\n" +
        "v6|#_X|'0X2001_0DB8_0000_0000_0000_0000_0000_0001'\n" +
        "v6|n|'20010db8000000000000000000000001'\n" +
        "v6|#n|'0x20010db8000000000000000000000001'\n" +
        "v6|_n|'2001_0db8_0000_0000_0000_0000_0000_0001'\n" +
        "v6|d|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6|bb|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6|o|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6|#s|ValueError: Alternate form (#) not allowed in string format specifier\n" +
        "v6|+s|ValueError: Sign not allowed in string format specifier\n" +
        "v6z||'::'\n" +
        "v6z|s|'::'\n" +
        "v6z|>10s|'        ::'\n" +
        "v6z|<20s|'::                  '\n" +
        "v6z|^42s|'                    ::                    '\n" +
        "v6z|>10|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6z|b|'00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000'\n" +
        "v6z|#b|'0b00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000'\n" +
        "v6z|_b|'0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000'\n" +
        "v6z|#_b|'0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000'\n" +
        "v6z|x|'00000000000000000000000000000000'\n" +
        "v6z|#x|'0x00000000000000000000000000000000'\n" +
        "v6z|X|'00000000000000000000000000000000'\n" +
        "v6z|#X|'0X00000000000000000000000000000000'\n" +
        "v6z|_x|'0000_0000_0000_0000_0000_0000_0000_0000'\n" +
        "v6z|#_X|'0X0000_0000_0000_0000_0000_0000_0000_0000'\n" +
        "v6z|n|'00000000000000000000000000000000'\n" +
        "v6z|#n|'0x00000000000000000000000000000000'\n" +
        "v6z|_n|'0000_0000_0000_0000_0000_0000_0000_0000'\n" +
        "v6z|d|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6z|bb|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6z|o|TypeError: unsupported format string passed to IPv6Address.__format__\n" +
        "v6z|#s|ValueError: Alternate form (#) not allowed in string format specifier\n" +
        "v6z|+s|ValueError: Sign not allowed in string format specifier\n" +
        "if4||'1.2.3.4/24'\n" +
        "if4|s|'1.2.3.4/24'\n" +
        "if4|>10s|'1.2.3.4/24'\n" +
        "if4|<20s|'1.2.3.4/24          '\n" +
        "if4|^42s|'                1.2.3.4/24                '\n" +
        "if4|>10|TypeError: unsupported format string passed to IPv4Interface.__format__\n" +
        "if4|b|'00000001000000100000001100000100'\n" +
        "if4|#b|'0b00000001000000100000001100000100'\n" +
        "if4|_b|'0000_0001_0000_0010_0000_0011_0000_0100'\n" +
        "if4|#_b|'0b0000_0001_0000_0010_0000_0011_0000_0100'\n" +
        "if4|x|'01020304'\n" +
        "if4|#x|'0x01020304'\n" +
        "if4|X|'01020304'\n" +
        "if4|#X|'0X01020304'\n" +
        "if4|_x|'0102_0304'\n" +
        "if4|#_X|'0X0102_0304'\n" +
        "if4|n|'00000001000000100000001100000100'\n" +
        "if4|#n|'0b00000001000000100000001100000100'\n" +
        "if4|_n|'0000_0001_0000_0010_0000_0011_0000_0100'\n" +
        "if4|d|TypeError: unsupported format string passed to IPv4Interface.__format__\n" +
        "if4|bb|TypeError: unsupported format string passed to IPv4Interface.__format__\n" +
        "if4|o|TypeError: unsupported format string passed to IPv4Interface.__format__\n" +
        "if4|#s|ValueError: Alternate form (#) not allowed in string format specifier\n" +
        "if4|+s|ValueError: Sign not allowed in string format specifier\n" +
        "if6||'2001:db8::1/64'\n" +
        "if6|s|'2001:db8::1/64'\n" +
        "if6|>10s|'2001:db8::1/64'\n" +
        "if6|<20s|'2001:db8::1/64      '\n" +
        "if6|^42s|'              2001:db8::1/64              '\n" +
        "if6|>10|TypeError: unsupported format string passed to IPv6Interface.__format__\n" +
        "if6|b|'00100000000000010000110110111000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001'\n" +
        "if6|#b|'0b00100000000000010000110110111000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001'\n" +
        "if6|_b|'0010_0000_0000_0001_0000_1101_1011_1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001'\n" +
        "if6|#_b|'0b0010_0000_0000_0001_0000_1101_1011_1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001'\n" +
        "if6|x|'20010db8000000000000000000000001'\n" +
        "if6|#x|'0x20010db8000000000000000000000001'\n" +
        "if6|X|'20010DB8000000000000000000000001'\n" +
        "if6|#X|'0X20010DB8000000000000000000000001'\n" +
        "if6|_x|'2001_0db8_0000_0000_0000_0000_0000_0001'\n" +
        "if6|#_X|'0X2001_0DB8_0000_0000_0000_0000_0000_0001'\n" +
        "if6|n|'20010db8000000000000000000000001'\n" +
        "if6|#n|'0x20010db8000000000000000000000001'\n" +
        "if6|_n|'2001_0db8_0000_0000_0000_0000_0000_0001'\n" +
        "if6|d|TypeError: unsupported format string passed to IPv6Interface.__format__\n" +
        "if6|bb|TypeError: unsupported format string passed to IPv6Interface.__format__\n" +
        "if6|o|TypeError: unsupported format string passed to IPv6Interface.__format__\n" +
        "if6|#s|ValueError: Alternate form (#) not allowed in string format specifier\n" +
        "if6|+s|ValueError: Sign not allowed in string format specifier\n" +
        "http||'200'\n" +
        "http|>6|'   200'\n" +
        "http|d|'200'\n" +
        "http|x|'c8'\n" +
        "http|+|'+200'\n";

    [Fact]
    public void AddressesInterfacesAndHttpStatus_OwnTheirFormatSpec_MatchesPython()
    {
        var result = CompileAndExecute(Program);

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(Expected.TrimEnd('\n'), result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n'));
    }
}
