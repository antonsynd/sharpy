using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// A replacement field's attribute step on an integer enum (a CLR enum, which has no instance
/// <c>Name</c>/<c>Value</c> members) reads python's <c>name</c> through the channel the static
/// <c>.name</c> reads (<see cref="Builtins.EnumName"/>: the recorded <c>[SharpyFieldName]</c>, else the
/// field name) and <c>value</c> as the underlying integer. python3 3.12 with
/// <c>class Color(Enum): RED = 1; dark_blue = 2</c>: <c>'{0.name} {0.value}'.format(Color.dark_blue)</c>
/// is <c>dark_blue 2</c>, <c>'{0.value:&gt;3}'.format(Color.RED)</c> is <c>'  1'</c>, and
/// <c>'{0.nope}'.format(Color.RED)</c> is <c>AttributeError: 'Color' object has no attribute 'nope'</c>.
/// </summary>
public class StrFormatEnumAttributeTests
{
    public enum Color
    {
        RED = 1,
        [SharpyFieldName("dark_blue")]
        DarkBlue = 2,
    }

    public enum Wide : long
    {
        BIG = 5_000_000_000,
    }

    [Fact]
    public void NameAndValue_ReadThePythonNameAndTheUnderlyingInteger()
    {
        "{0.name} {0.value}".Format(Color.DarkBlue).Should().Be("dark_blue 2");
        "{0.name}={0.value:>3}".Format(Color.RED).Should().Be("RED=  1");
        "{0.value}".Format(Wide.BIG).Should().Be("5000000000");
    }

    [Fact]
    public void OtherAttributes_AreStillAttributeError()
    {
        FluentActions.Invoking(() => "{0.nope}".Format(Color.RED))
            .Should().Throw<AttributeError>().WithMessage("'Color' object has no attribute 'nope'");
    }
}
