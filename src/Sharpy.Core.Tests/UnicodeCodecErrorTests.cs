using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// The <see cref="UnicodeEncodeError"/> / <see cref="UnicodeDecodeError"/> types themselves (#2477): python's
/// five-argument constructor and their place under <see cref="UnicodeError"/>. What <c>str.encode</c> and
/// <c>bytes.decode</c> raise is measured against python by <see cref="UnicodeCodecPythonDifferentialTests"/>.
/// </summary>
public class UnicodeCodecErrorTests
{
    private static Bytes B(params byte[] data) => new Bytes(data);

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
